using System;

namespace UGS.State
{
    /// <summary>转换触发时机。</summary>
    public enum TransitionTrigger : byte
    {
        /// <summary>Intent 到达目标点。</summary>
        OnArrive = 0,
        /// <summary>HoldSeconds 到期。</summary>
        OnTimeout = 1,
        /// <summary>每次 Tick 评估。</summary>
        OnTick = 2,
        /// <summary>外部信号（域主动调用）。</summary>
        OnSignal = 3,
    }

    /// <summary>转换条件上下文。</summary>
    public readonly struct StateTransitionContext
    {
        public readonly StateMachine Machine;
        public readonly StateInstance Instance;
        public readonly StateDef Def;
        public readonly TransitionTrigger Trigger;

        public StateTransitionContext(
            StateMachine machine,
            StateInstance instance,
            StateDef def,
            TransitionTrigger trigger)
        {
            Machine = machine;
            Instance = instance;
            Def = def;
            Trigger = trigger;
        }
    }

    /// <summary>一条条件转换规则。</summary>
    public sealed class TransitionRule
    {
        public readonly TransitionTrigger Trigger;
        public readonly StateId To;
        public readonly Func<StateTransitionContext, bool> Condition;

        public TransitionRule(
            TransitionTrigger trigger,
            StateId to,
            Func<StateTransitionContext, bool> condition = null)
        {
            Trigger = trigger;
            To = to;
            Condition = condition;
        }
    }

    /// <summary>转换请求。</summary>
    public readonly struct TransitionRequest
    {
        public readonly StateMachine Machine;
        public readonly StateId From;
        public readonly StateId To;
        public readonly LaneId Lane;
        public readonly EnterReason Reason;
        public readonly bool Force;

        public TransitionRequest(
            StateMachine machine,
            StateId from,
            StateId to,
            LaneId lane,
            EnterReason reason,
            bool force)
        {
            Machine = machine;
            From = from;
            To = to;
            Lane = lane;
            Reason = reason;
            Force = force;
        }
    }

    /// <summary>转换策略：域可替换以追加“阵型中禁止移动”等规则。</summary>
    public interface IStateTransitionPolicy
    {
        bool CanEnter(TransitionRequest request);
    }

    /// <summary>
    /// 默认策略：
    /// 1. 目标必须存在；
    /// 2. 同状态重复进入视为 no-op（force 除外）；
    /// 3. Life 非初始/存活时拒绝非 Life lane；
    /// 4. 低优先级不能抢占高优先级（force 除外）。
    /// </summary>
    public sealed class DefaultStateTransitionPolicy : IStateTransitionPolicy
    {
        private readonly StateCatalog _catalog;

        public DefaultStateTransitionPolicy(StateCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            _catalog = catalog;
        }

        public bool CanEnter(TransitionRequest request)
        {
            StateDef target;
            if (!_catalog.TryGetState(request.To, out target))
            {
                return false;
            }

            StateId current = request.Machine.Get(target.Lane);
            if (current == request.To && !request.Force)
            {
                return false;
            }

            if (target.Lane != CoreLanes.Life)
            {
                StateId life = request.Machine.Get(CoreLanes.Life);
                StateId alive = _catalog.AliveStateId;
                if (!life.IsNone && !alive.IsNone && life != alive)
                {
                    return false;
                }
            }

            if (!current.IsNone && !request.Force)
            {
                StateDef currentDef;
                if (_catalog.TryGetState(current, out currentDef) && currentDef.Priority > target.Priority)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
