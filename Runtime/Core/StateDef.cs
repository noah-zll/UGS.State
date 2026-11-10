using System;
using System.Collections.Generic;

namespace UGS.State
{
    /// <summary>状态定义（不可变）。使用 <see cref="StateDefBuilder"/> 构建。</summary>
    public sealed class StateDef
    {
        public StateId Id { get; private set; }
        public LaneId Lane { get; private set; }
        /// <summary>可读名称（调试面板 / 日志 / UI 展示）。目录层不解释其内容。</summary>
        public string DisplayName { get; private set; }
        public TagSet DeclaredTags { get; private set; }
        /// <summary>声明标签 + 隐含标签展开后的结果，由 Catalog 在 Seal 时写入。</summary>
        public TagSet EffectiveTags { get; internal set; }
        public TagSet RequireAbsent { get; private set; }
        public TagSet RequirePresent { get; private set; }
        public byte Priority { get; private set; }
        public int BehaviorId { get; private set; }
        public ViewId View { get; private set; }
        public float HoldSeconds { get; private set; }
        public StateId OnArrive { get; private set; }
        public StateId AutoNext { get; private set; }
        public StateId ReturnTo { get; private set; }
        public EnterActionMask EnterActions { get; private set; }
        public RestorePolicy Restore { get; private set; }
        public IReadOnlyList<TransitionRule> Transitions { get; private set; }

        internal StateDef(StateDefBuilder builder)
        {
            Id = builder.Id;
            Lane = builder.Lane;
            DisplayName = builder.DisplayNameValue;
            DeclaredTags = builder.TagsValue;
            EffectiveTags = builder.TagsValue;
            RequireAbsent = builder.RequireAbsentValue;
            RequirePresent = builder.RequirePresentValue;
            Priority = builder.PriorityValue;
            BehaviorId = builder.BehaviorIdValue;
            View = builder.ViewValue;
            HoldSeconds = builder.HoldSecondsValue;
            OnArrive = builder.OnArriveValue;
            AutoNext = builder.AutoNextValue;
            ReturnTo = builder.ReturnToValue;
            EnterActions = builder.EnterActionsValue;
            Restore = builder.RestoreValue;
            Transitions = builder.TransitionsValue != null && builder.TransitionsValue.Count > 0
                ? builder.TransitionsValue.ToArray()
                : Array.Empty<TransitionRule>();
        }

        public override string ToString()
        {
            return Id + "@" + Lane;
        }
    }

    /// <summary>状态定义的流式构建器。</summary>
    public sealed class StateDefBuilder
    {
        internal StateId Id;
        internal LaneId Lane;
        internal string DisplayNameValue;
        internal TagSet TagsValue;
        internal TagSet RequireAbsentValue;
        internal TagSet RequirePresentValue;
        internal byte PriorityValue;
        internal int BehaviorIdValue;
        internal ViewId ViewValue;
        internal float HoldSecondsValue;
        internal StateId OnArriveValue;
        internal StateId AutoNextValue;
        internal StateId ReturnToValue;
        internal EnterActionMask EnterActionsValue;
        internal RestorePolicy RestoreValue = RestorePolicy.Resume;
        internal List<TransitionRule> TransitionsValue;

        private StateDefBuilder(StateId id, LaneId lane)
        {
            Id = id;
            Lane = lane;
        }

        public static StateDefBuilder Create(StateId id, LaneId lane)
        {
            return new StateDefBuilder(id, lane);
        }

        public StateDefBuilder Tags(TagSet tags)
        {
            TagsValue = tags;
            return this;
        }

        /// <summary>可读名称：仅用于展示，不参与任何判定。</summary>
        public StateDefBuilder DisplayName(string name)
        {
            DisplayNameValue = name;
            return this;
        }

        public StateDefBuilder RequireAbsent(TagSet tags)
        {
            RequireAbsentValue = tags;
            return this;
        }

        public StateDefBuilder RequirePresent(TagSet tags)
        {
            RequirePresentValue = tags;
            return this;
        }

        public StateDefBuilder Priority(byte priority)
        {
            PriorityValue = priority;
            return this;
        }

        public StateDefBuilder Behavior(int behaviorId)
        {
            BehaviorIdValue = behaviorId;
            return this;
        }

        public StateDefBuilder View(ViewId view)
        {
            ViewValue = view;
            return this;
        }

        public StateDefBuilder Hold(float seconds)
        {
            HoldSecondsValue = seconds;
            return this;
        }

        public StateDefBuilder OnArrive(StateId id)
        {
            OnArriveValue = id;
            return this;
        }

        public StateDefBuilder AutoNext(StateId id)
        {
            AutoNextValue = id;
            return this;
        }

        public StateDefBuilder ReturnTo(StateId id)
        {
            ReturnToValue = id;
            return this;
        }

        public StateDefBuilder EnterActions(EnterActionMask actions)
        {
            EnterActionsValue = actions;
            return this;
        }

        public StateDefBuilder Restore(RestorePolicy policy)
        {
            RestoreValue = policy;
            return this;
        }

        public StateDefBuilder Transition(TransitionTrigger trigger, StateId to, Func<StateTransitionContext, bool> condition = null)
        {
            if (TransitionsValue == null)
            {
                TransitionsValue = new List<TransitionRule>();
            }

            TransitionsValue.Add(new TransitionRule(trigger, to, condition));
            return this;
        }

        public StateDef Build()
        {
            return new StateDef(this);
        }
    }
}
