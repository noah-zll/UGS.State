using System;
using System.Collections.Generic;

namespace UGS.State
{
    /// <summary>
    /// 一个通道内的状态机：维护各 lane 的当前状态、标签掩码、能力查询与变更事件。
    /// 只处理数据；行为生命周期与表现由 <see cref="StateDriver"/> 订阅 <see cref="Changed"/> 后处理。
    /// </summary>
    public sealed class StateMachine
    {
        private struct PendingAction
        {
            public bool Exit;
            public LaneId Lane;
            public StateId Id;
            public StateArgs Args;
            public EnterReason Reason;
            public bool Force;
        }

        private readonly StateCatalog _catalog;
        private readonly StateRuntime _runtime;
        private readonly StateInstance[] _slots;
        private readonly Queue<PendingAction> _pending = new Queue<PendingAction>();
        private readonly StateChangeLog _history = new StateChangeLog(32);
        private TagSet _tags;
        private bool _processing;

        internal StateMachine(StateRuntime runtime, StateCatalog catalog, ChannelId channel)
        {
            _runtime = runtime;
            _catalog = catalog;
            Channel = channel;
            _slots = new StateInstance[catalog.LaneCount];
            _tags = TagSet.None;
        }

        public ChannelId Channel { get; private set; }

        public TagSet Tags
        {
            get { return _tags; }
        }

        /// <summary>最近的变更流水（环形缓冲）。</summary>
        public StateChangeLog History
        {
            get { return _history; }
        }

        /// <summary>一次转换（进入/替换/退出/被拒）触发一次。</summary>
        public event Action<StateChange> Changed;

        #region 查询

        public StateId Get(LaneId lane)
        {
            int index = _catalog.GetLaneIndex(lane);
            if (index < 0 || _slots[index] == null)
            {
                return StateId.None;
            }

            return _slots[index].Id;
        }

        public StateInstance Active(LaneId lane)
        {
            int index = _catalog.GetLaneIndex(lane);
            return index < 0 ? null : _slots[index];
        }

        public bool Is(StateId id)
        {
            return Get(GetLaneOf(id)) == id && id != StateId.None;
        }

        /// <summary>是否包含全部给定标签。</summary>
        public bool Has(TagSet tags)
        {
            return _tags.HasAll(tags);
        }

        public bool Can(AbilityId ability)
        {
            return _catalog.EvaluateAbility(ability, _runtime.Host, this);
        }

        private LaneId GetLaneOf(StateId id)
        {
            StateDef def;
            return _catalog.TryGetState(id, out def) ? def.Lane : CoreLanes.Activity;
        }

        #endregion

        #region 进入 / 退出

        public bool TryEnter(
            StateId id,
            StateArgs args = default(StateArgs),
            EnterReason reason = EnterReason.System,
            bool force = false)
        {
            if (_processing)
            {
                Enqueue(false, CoreLanes.Activity, id, args, reason, force);
                return true;
            }

            bool result = ExecuteEnter(id, args, reason, force);
            DrainPending();
            return result;
        }

        /// <summary>重入安全：总在本次转换处理完后执行。</summary>
        public bool TryPostEnter(
            StateId id,
            StateArgs args = default(StateArgs),
            EnterReason reason = EnterReason.System,
            bool force = false)
        {
            if (_processing)
            {
                Enqueue(false, CoreLanes.Activity, id, args, reason, force);
                return true;
            }

            return TryEnter(id, args, reason, force);
        }

        public bool TryExit(LaneId lane)
        {
            return TryExit(lane, EnterReason.System);
        }

        public bool TryExit(LaneId lane, EnterReason reason)
        {
            if (_processing)
            {
                Enqueue(true, lane, StateId.None, default(StateArgs), reason, false);
                return true;
            }

            bool result = ExecuteExit(lane, reason);
            DrainPending();
            return result;
        }

        /// <summary>在不触发转换的前提下刷新当前状态参数（例如移动目标点）。</summary>
        public bool UpdateArgs(LaneId lane, StateArgs args)
        {
            int index = _catalog.GetLaneIndex(lane);
            if (index < 0 || _slots[index] == null)
            {
                return false;
            }

            _slots[index].Args = args;
            return true;
        }

        /// <summary>Intent 到达：退出 Intent 并进入其 OnArrive。</summary>
        public bool TryCompleteIntent(EnterReason reason = EnterReason.System)
        {
            StateId intentId = Get(CoreLanes.Intent);
            if (intentId.IsNone)
            {
                return false;
            }

            StateDef intentDef;
            if (!_catalog.TryGetState(intentId, out intentDef) || intentDef.OnArrive.IsNone)
            {
                return false;
            }

            StateInstance intent = Active(CoreLanes.Intent);
            StateArgs args = intent != null ? intent.Args : default(StateArgs);

            TryExit(CoreLanes.Intent, reason);
            return TryEnter(intentDef.OnArrive, args, reason);
        }

        #endregion

        #region 内部执行

        private bool ExecuteEnter(StateId id, StateArgs args, EnterReason reason, bool force)
        {
            _processing = true;
            try
            {
                return EnterCore(id, args, reason, force);
            }
            finally
            {
                _processing = false;
            }
        }

        private bool EnterCore(StateId id, StateArgs args, EnterReason reason, bool force)
        {
            StateDef def;
            if (!_catalog.TryGetState(id, out def))
            {
                _runtime.Host.Log(StateLogLevel.Error, "StateMachine " + Channel + ": unknown state " + id + ".");
                return false;
            }

            int index = _catalog.GetLaneIndex(def.Lane);
            if (index < 0)
            {
                _runtime.Host.Log(StateLogLevel.Error, "StateMachine " + Channel + ": unregistered lane " + def.Lane + ".");
                return false;
            }

            if (!_runtime.Host.Supports(id))
            {
                RaiseRejected(index, id, def.Lane, reason);
                return false;
            }

            StateInstance current = _slots[index];
            if (current != null && current.Id == id && !force)
            {
                return false;
            }

            if (!force)
            {
                TransitionRequest request = new TransitionRequest(
                    this,
                    current != null ? current.Id : StateId.None,
                    id,
                    def.Lane,
                    reason,
                    false);

                if (!_catalog.TransitionPolicy.CanEnter(request))
                {
                    RaiseRejected(index, id, def.Lane, reason);
                    return false;
                }
            }

            TagSet otherTags = RecomputeWithout(index);
            if (!def.RequireAbsent.IsEmpty && otherTags.HasAny(def.RequireAbsent))
            {
                RaiseRejected(index, id, def.Lane, reason);
                return false;
            }

            if (!def.RequirePresent.IsEmpty && !otherTags.HasAll(def.RequirePresent))
            {
                RaiseRejected(index, id, def.Lane, reason);
                return false;
            }

            StateInstance previous = current;
            _slots[index] = null;

            StateInstance instance = new StateInstance(id, def.Lane, args);
            _slots[index] = instance;
            RecomputeTags();

            // Life 进入非存活状态时，自动退出其它 lane，避免“死亡但仍在工作/受击”的残留。
            if (def.Lane == CoreLanes.Life &&
                !_catalog.AliveStateId.IsNone &&
                id != _catalog.AliveStateId)
            {
                SuspendOtherLanes(def.Lane, reason);
            }

            // 读档/镜像恢复不重放进入清理动作，避免重复执行 ReturnTask/QuitFormation 等副作用。
            if (!def.EnterActions.IsEmpty && reason != EnterReason.Restore)
            {
                try
                {
                    _runtime.Host.ExecuteEnterAction(def.EnterActions);
                }
                catch (Exception exception)
                {
                    _runtime.Host.Log(StateLogLevel.Error, "EnterActions failed for " + id + ": " + exception);
                }
            }

            StateChangeKind kind = previous != null ? StateChangeKind.Replaced : StateChangeKind.Entered;
            Raise(new StateChange(
                kind,
                this,
                Channel,
                def.Lane,
                previous != null ? previous.Id : StateId.None,
                id,
                previous,
                instance,
                reason,
                _runtime.Now));

            return true;
        }

        private bool ExecuteExit(LaneId lane, EnterReason reason)
        {
            _processing = true;
            try
            {
                return ExitCore(lane, reason);
            }
            finally
            {
                _processing = false;
            }
        }

        private bool ExitCore(LaneId lane, EnterReason reason)
        {
            int index = _catalog.GetLaneIndex(lane);
            if (index < 0)
            {
                return false;
            }

            StateInstance current = _slots[index];
            if (current == null)
            {
                return false;
            }

            _slots[index] = null;
            RecomputeTags();

            Raise(new StateChange(
                StateChangeKind.Exited,
                this,
                Channel,
                lane,
                current.Id,
                StateId.None,
                current,
                null,
                reason,
                _runtime.Now));

            return true;
        }

        private void Enqueue(bool exit, LaneId lane, StateId id, StateArgs args, EnterReason reason, bool force)
        {
            _pending.Enqueue(new PendingAction
            {
                Exit = exit,
                Lane = lane,
                Id = id,
                Args = args,
                Reason = reason,
                Force = force,
            });
        }

        private void DrainPending()
        {
            while (_pending.Count > 0)
            {
                PendingAction action = _pending.Dequeue();
                if (action.Exit)
                {
                    ExecuteExit(action.Lane, action.Reason);
                }
                else
                {
                    ExecuteEnter(action.Id, action.Args, action.Reason, action.Force);
                }
            }
        }

        private void SuspendOtherLanes(LaneId keepLane, EnterReason reason)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null || _slots[i].Lane == keepLane)
                {
                    continue;
                }

                ExitCore(_slots[i].Lane, reason);
            }
        }

        private void RaiseRejected(int index, StateId id, LaneId lane, EnterReason reason)
        {
            StateInstance current = index >= 0 && index < _slots.Length ? _slots[index] : null;
            Raise(new StateChange(
                StateChangeKind.Rejected,
                this,
                Channel,
                lane,
                current != null ? current.Id : StateId.None,
                id,
                current,
                null,
                reason,
                _runtime.Now));
        }

        private void Raise(StateChange change)
        {
            _history.Add(change);
            Action<StateChange> handler = Changed;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(change);
            }
            catch (Exception exception)
            {
                _runtime.Host.Log(StateLogLevel.Error, "State change handler failed: " + exception);
            }
        }

        #endregion

        #region 标签 / Tick / 恢复

        private void RecomputeTags()
        {
            TagSet tags = TagSet.None;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    continue;
                }

                StateDef def;
                if (_catalog.TryGetState(_slots[i].Id, out def))
                {
                    tags = tags | def.EffectiveTags;
                }
            }

            _tags = tags;
        }

        private TagSet RecomputeWithout(int skipIndex)
        {
            TagSet tags = TagSet.None;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i == skipIndex || _slots[i] == null)
                {
                    continue;
                }

                StateDef def;
                if (_catalog.TryGetState(_slots[i].Id, out def))
                {
                    tags = tags | def.EffectiveTags;
                }
            }

            return tags;
        }

        internal void Tick(float deltaTime)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                StateInstance instance = _slots[i];
                if (instance == null)
                {
                    continue;
                }

                instance.Elapsed += deltaTime;

                StateDef def;
                if (!_catalog.TryGetState(instance.Id, out def))
                {
                    continue;
                }

                if (def.HoldSeconds > 0f && instance.Elapsed >= def.HoldSeconds)
                {
                    if (TryTransitions(def, instance, TransitionTrigger.OnTimeout))
                    {
                        continue;
                    }

                    StateId next = !def.ReturnTo.IsNone ? def.ReturnTo : def.AutoNext;
                    ApplyNext(def.Lane, next, instance.Args, EnterReason.Timer);
                    continue;
                }

                if (def.Transitions != null && def.Transitions.Count > 0)
                {
                    TryTransitions(def, instance, TransitionTrigger.OnTick);
                }
            }
        }

        private bool TryTransitions(StateDef def, StateInstance instance, TransitionTrigger trigger)
        {
            if (def.Transitions == null)
            {
                return false;
            }

            for (int i = 0; i < def.Transitions.Count; i++)
            {
                TransitionRule rule = def.Transitions[i];
                if (rule == null || rule.Trigger != trigger)
                {
                    continue;
                }

                StateTransitionContext context = new StateTransitionContext(this, instance, def, trigger);
                if (rule.Condition != null && !rule.Condition(context))
                {
                    continue;
                }

                if (ApplyNext(def.Lane, rule.To, instance.Args, EnterReason.Timer))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 执行“下一个状态”：同 lane 直接替换；跨 lane 先退出当前 lane，避免 Reaction 残留。
        /// </summary>
        private bool ApplyNext(LaneId currentLane, StateId next, StateArgs args, EnterReason reason)
        {
            if (next.IsNone)
            {
                return TryExit(currentLane, reason);
            }

            StateDef nextDef;
            if (_catalog.TryGetState(next, out nextDef) && nextDef.Lane == currentLane)
            {
                return TryEnter(next, args, reason);
            }

            TryExit(currentLane, reason);
            return TryEnter(next, args, reason);
        }

        internal void Clear(EnterReason reason)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    continue;
                }

                TryExit(_slots[i].Lane, reason);
            }
        }

        internal void RestoreState(StateId id, StateArgs args, float elapsed)
        {
            if (!TryEnter(id, args, EnterReason.Restore, true))
            {
                return;
            }

            StateDef def;
            if (!_catalog.TryGetState(id, out def))
            {
                return;
            }

            int index = _catalog.GetLaneIndex(def.Lane);
            if (index >= 0 && _slots[index] != null)
            {
                _slots[index].Elapsed = elapsed;
            }
        }

        #endregion
    }
}
