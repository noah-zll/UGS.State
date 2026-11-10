using System;
using System.Collections.Generic;
using System.Text;

namespace UGS.State
{
    /// <summary>
    /// 状态目录：注册状态定义、lane、表现、行为、能力规则与隐含标签。
    /// 一个域一个实例；创建 <see cref="StateRuntime"/> 时会自动 Seal 并校验。
    /// </summary>
    public sealed class StateCatalog
    {
        private readonly Dictionary<StateId, StateDef> _states = new Dictionary<StateId, StateDef>();
        private readonly Dictionary<int, StateViewDef> _views = new Dictionary<int, StateViewDef>();
        private readonly Dictionary<int, Func<StateBehavior>> _behaviorFactories = new Dictionary<int, Func<StateBehavior>>();
        private readonly Dictionary<int, StateBehavior> _behaviorInstances = new Dictionary<int, StateBehavior>();
        private readonly Dictionary<int, BehaviorScope> _behaviorScopes = new Dictionary<int, BehaviorScope>();
        private readonly Dictionary<AbilityId, AbilityRule> _abilityRules = new Dictionary<AbilityId, AbilityRule>();
        private readonly Dictionary<string, StateId> _idAliases = new Dictionary<string, StateId>(StringComparer.Ordinal);
        private readonly Dictionary<string, TagSet> _tagAliases = new Dictionary<string, TagSet>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<TagSet, TagSet>> _expansions = new List<KeyValuePair<TagSet, TagSet>>();
        private readonly List<LaneDef> _lanes = new List<LaneDef>();
        private readonly Dictionary<LaneId, int> _laneIndex = new Dictionary<LaneId, int>();
        private bool _sealed;

        public ushort Domain { get; private set; }
        public string Name { get; private set; }
        public StateId AliveStateId { get; private set; }
        public StateId IdleStateId { get; private set; }
        public bool IsSealed { get { return _sealed; } }
        public IReadOnlyList<LaneDef> Lanes { get { return _lanes; } }
        public IStateTransitionPolicy TransitionPolicy { get; private set; }

        public StateCatalog(ushort domain, string name)
        {
            Domain = domain;
            Name = string.IsNullOrEmpty(name) ? "catalog" : name;

            RegisterLane(new LaneDef(CoreLanes.Life, "life"));
            RegisterLane(new LaneDef(CoreLanes.Activity, "activity"));
            RegisterLane(new LaneDef(CoreLanes.Reaction, "reaction"));
            RegisterLane(new LaneDef(CoreLanes.Intent, "intent"));

            TransitionPolicy = new DefaultStateTransitionPolicy(this);
        }

        #region 注册

        public StateCatalog RegisterLane(LaneDef lane)
        {
            ThrowIfSealed();
            if (_laneIndex.ContainsKey(lane.Id))
            {
                throw new StateException("Lane " + lane.Id + " already registered in catalog " + Name + ".");
            }

            _laneIndex[lane.Id] = _lanes.Count;
            _lanes.Add(lane);
            return this;
        }

        public StateCatalog Register(StateDef def)
        {
            ThrowIfSealed();
            if (def == null)
            {
                throw new ArgumentNullException(nameof(def));
            }

            if (def.Id.IsNone)
            {
                throw new StateException("State id cannot be None (catalog " + Name + ").");
            }

            if (_states.ContainsKey(def.Id))
            {
                throw new StateException("State " + def.Id + " already registered in catalog " + Name + ".");
            }

            _states.Add(def.Id, def);
            return this;
        }

        public StateCatalog RegisterView(StateViewDef view)
        {
            ThrowIfSealed();
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (view.Id.IsNone)
            {
                throw new StateException("View id cannot be None (catalog " + Name + ").");
            }

            if (_views.ContainsKey(view.Id.Value))
            {
                throw new StateException("View " + view.Id + " already registered in catalog " + Name + ".");
            }

            _views.Add(view.Id.Value, view);
            return this;
        }

        /// <summary>
        /// 注册行为工厂。<paramref name="scope"/> 决定实例生命周期：
        /// <see cref="BehaviorScope.Shared"/>（默认）由目录持有单例；
        /// <see cref="BehaviorScope.PerInstance"/> 由 <see cref="StateDriver"/> 在每次进入时新建、退出时释放。
        /// </summary>
        public StateCatalog RegisterBehavior(int behaviorId, Func<StateBehavior> factory,
            BehaviorScope scope = BehaviorScope.Shared)
        {
            ThrowIfSealed();
            if (behaviorId == 0)
            {
                throw new StateException("Behavior id 0 is reserved for 'no behavior' (catalog " + Name + ").");
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (_behaviorFactories.ContainsKey(behaviorId))
            {
                throw new StateException("Behavior " + behaviorId + " already registered in catalog " + Name + ".");
            }

            _behaviorFactories.Add(behaviorId, factory);
            _behaviorScopes.Add(behaviorId, scope);
            return this;
        }

        public StateCatalog RegisterAbility(AbilityId ability, AbilityRule rule)
        {
            ThrowIfSealed();
            if (ability.IsNone)
            {
                throw new StateException("Ability id cannot be None (catalog " + Name + ").");
            }

            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            _abilityRules[ability] = rule;
            return this;
        }

        public StateCatalog RegisterIdAlias(string name, StateId id)
        {
            ThrowIfSealed();
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Alias name is empty.", nameof(name));
            }

            _idAliases[name] = id;
            return this;
        }

        public StateCatalog RegisterTagAlias(string name, TagSet tag)
        {
            ThrowIfSealed();
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Alias name is empty.", nameof(name));
            }

            _tagAliases[name] = tag;
            return this;
        }

        /// <summary>声明隐含标签：当状态带有 declared 时自动补上 implied。</summary>
        public StateCatalog ExpandTag(TagSet declared, TagSet implied)
        {
            ThrowIfSealed();
            if (declared.IsEmpty)
            {
                throw new StateException("Expansion trigger tag cannot be empty (catalog " + Name + ").");
            }

            _expansions.Add(new KeyValuePair<TagSet, TagSet>(declared, implied));
            return this;
        }

        public StateCatalog SetAliveState(StateId id)
        {
            ThrowIfSealed();
            AliveStateId = id;
            return this;
        }

        public StateCatalog SetIdleState(StateId id)
        {
            ThrowIfSealed();
            IdleStateId = id;
            return this;
        }

        public StateCatalog SetTransitionPolicy(IStateTransitionPolicy policy)
        {
            ThrowIfSealed();
            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            TransitionPolicy = policy;
            return this;
        }

        #endregion

        #region 查询

        public int LaneCount
        {
            get { return _lanes.Count; }
        }

        public LaneDef GetLane(int index)
        {
            return _lanes[index];
        }

        public int GetLaneIndex(LaneId lane)
        {
            int index;
            return _laneIndex.TryGetValue(lane, out index) ? index : -1;
        }

        public bool TryGetState(StateId id, out StateDef def)
        {
            return _states.TryGetValue(id, out def);
        }

        public StateDef GetState(StateId id)
        {
            StateDef def;
            if (!_states.TryGetValue(id, out def))
            {
                throw new StateException("State " + id + " not found in catalog " + Name + ".");
            }

            return def;
        }

        public StateViewDef GetView(ViewId id)
        {
            if (id.IsNone)
            {
                return null;
            }

            StateViewDef view;
            return _views.TryGetValue(id.Value, out view) ? view : null;
        }

        /// <summary>取共享行为单例；未注册或注册为逐实例时返回 null。</summary>
        public StateBehavior GetBehavior(int behaviorId)
        {
            if (behaviorId == 0 || GetBehaviorScope(behaviorId) != BehaviorScope.Shared)
            {
                return null;
            }

            StateBehavior behavior;
            if (_behaviorInstances.TryGetValue(behaviorId, out behavior))
            {
                return behavior;
            }

            Func<StateBehavior> factory;
            if (!_behaviorFactories.TryGetValue(behaviorId, out factory))
            {
                return null;
            }

            behavior = factory();
            _behaviorInstances[behaviorId] = behavior;
            return behavior;
        }

        /// <summary>行为实例的生命周期范围；未注册时返回 <see cref="BehaviorScope.Shared"/>。</summary>
        public BehaviorScope GetBehaviorScope(int behaviorId)
        {
            BehaviorScope scope;
            return _behaviorScopes.TryGetValue(behaviorId, out scope) ? scope : BehaviorScope.Shared;
        }

        /// <summary>新建一个行为实例（不缓存）；未注册返回 null。</summary>
        public StateBehavior CreateBehavior(int behaviorId)
        {
            if (behaviorId == 0)
            {
                return null;
            }

            Func<StateBehavior> factory;
            return _behaviorFactories.TryGetValue(behaviorId, out factory) ? factory() : null;
        }

        public bool HasBehavior(int behaviorId)
        {
            return behaviorId == 0 || _behaviorFactories.ContainsKey(behaviorId);
        }

        public bool TryResolveId(string alias, out StateId id)
        {
            return _idAliases.TryGetValue(alias, out id);
        }

        public bool TryResolveTag(string alias, out TagSet tag)
        {
            return _tagAliases.TryGetValue(alias, out tag);
        }

        /// <summary>展开隐含标签（最多迭代 8 轮，防止循环配置）。</summary>
        public TagSet Expand(TagSet tags)
        {
            TagSet result = tags;
            for (int pass = 0; pass < 8; pass++)
            {
                bool changed = false;
                for (int i = 0; i < _expansions.Count; i++)
                {
                    KeyValuePair<TagSet, TagSet> expansion = _expansions[i];
                    if (result.HasAll(expansion.Key) && !result.HasAll(expansion.Value))
                    {
                        result = result | expansion.Value;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    break;
                }
            }

            return result;
        }

        public bool EvaluateAbility(AbilityId ability, IStateHost host, StateMachine machine)
        {
            if (ability.IsNone)
            {
                return true;
            }

            AbilityRule rule;
            if (!_abilityRules.TryGetValue(ability, out rule))
            {
                return true;
            }

            return rule(new AbilityContext(
                host,
                machine,
                machine != null ? machine.Tags : TagSet.None,
                host != null ? host.DomainContext : null));
        }

        /// <summary>
        /// 用任意标签集合评估能力。供“无运行时、只有状态定义”的冷数据查询使用。
        /// </summary>
        public bool EvaluateAbility(AbilityId ability, IStateHost host, TagSet tags)
        {
            if (ability.IsNone)
            {
                return true;
            }

            AbilityRule rule;
            if (!_abilityRules.TryGetValue(ability, out rule))
            {
                return true;
            }

            return rule(new AbilityContext(
                host,
                null,
                tags,
                host != null ? host.DomainContext : null));
        }

        #endregion

        #region 校验 / 封存

        /// <summary>校验并冻结目录。重复调用安全。</summary>
        public void Seal()
        {
            if (_sealed)
            {
                return;
            }

            List<string> errors = new List<string>();

            // 先展开有效标签（校验错误也保留展开结果，便于排查）。
            foreach (StateDef def in _states.Values)
            {
                def.EffectiveTags = Expand(def.DeclaredTags);
            }

            foreach (StateDef def in _states.Values)
            {
                if (GetLaneIndex(def.Lane) < 0)
                {
                    errors.Add("State " + def.Id + " uses unregistered lane " + def.Lane + ".");
                }

                if (def.Lane == CoreLanes.Intent && def.OnArrive.IsNone)
                {
                    errors.Add("Intent state " + def.Id + " must define OnArrive.");
                }

                if (def.Lane == CoreLanes.Reaction &&
                    def.HoldSeconds <= 0f &&
                    def.AutoNext.IsNone &&
                    def.ReturnTo.IsNone)
                {
                    errors.Add("Reaction state " + def.Id + " must define Hold > 0 or AutoNext/ReturnTo.");
                }

                if (def.HoldSeconds < 0f)
                {
                    errors.Add("State " + def.Id + " has negative HoldSeconds.");
                }

                if (def.BehaviorId != 0 && !_behaviorFactories.ContainsKey(def.BehaviorId))
                {
                    errors.Add("State " + def.Id + " references unregistered behavior " + def.BehaviorId + ".");
                }

                if (!def.View.IsNone && !_views.ContainsKey(def.View.Value))
                {
                    errors.Add("State " + def.Id + " references unregistered view " + def.View + ".");
                }

                if (def.Transitions != null)
                {
                    for (int i = 0; i < def.Transitions.Count; i++)
                    {
                        TransitionRule rule = def.Transitions[i];
                        if (rule == null)
                        {
                            errors.Add("State " + def.Id + " has a null transition rule.");
                            continue;
                        }

                        if (!rule.To.IsNone && !_states.ContainsKey(rule.To))
                        {
                            errors.Add("State " + def.Id + " transitions to unknown state " + rule.To + ".");
                        }
                    }
                }
            }

            if (!AliveStateId.IsNone && !_states.ContainsKey(AliveStateId))
            {
                errors.Add("AliveStateId " + AliveStateId + " is not registered.");
            }

            if (!IdleStateId.IsNone && !_states.ContainsKey(IdleStateId))
            {
                errors.Add("IdleStateId " + IdleStateId + " is not registered.");
            }

            if (errors.Count > 0)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("StateCatalog '").Append(Name).Append("' validation failed (")
                    .Append(errors.Count).Append(" error(s)):\n");
                for (int i = 0; i < errors.Count; i++)
                {
                    builder.Append("  - ").Append(errors[i]).Append('\n');
                }

                throw new StateException(builder.ToString());
            }

            _sealed = true;
        }

        /// <summary>同 <see cref="Seal"/>，用于测试/启动期显式校验。</summary>
        public void Validate()
        {
            Seal();
        }

        private void ThrowIfSealed()
        {
            if (_sealed)
            {
                throw new StateException("StateCatalog '" + Name + "' is sealed; register everything before creating StateRuntime.");
            }
        }

        #endregion
    }
}
