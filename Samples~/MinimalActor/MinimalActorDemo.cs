using System;
using UGS.State;

namespace UGS.State.Samples
{
    /// <summary>
    /// 最小示例：一个“工人”实体。可直接在控制台/编辑器里调用 Run() 观察输出。
    /// 这里包含示例自己的词汇表（Tag / Ability / EnterAction），证明这些属于项目层而非框架。
    /// </summary>
    public static class MinimalActorDemo
    {
        private const ushort Domain = 100;

        private static readonly StateId Alive = new StateId(Domain, 1);
        private static readonly StateId Idle = new StateId(Domain, 2);
        private static readonly StateId Walk = new StateId(Domain, 3);
        private static readonly StateId Work = new StateId(Domain, 4);
        private static readonly StateId GoWork = new StateId(Domain, 5);
        private static readonly StateId Hurt = new StateId(Domain, 6);
        private static readonly StateId Dead = new StateId(Domain, 7);

        // ---- 项目层词汇（不属于 UGS.State 包）----
        private static class DemoTags
        {
            public static readonly TagSet Incapacitated = TagSet.FromBit(0);
            public static readonly TagSet CannotFight = TagSet.FromBit(1);
            public static readonly TagSet CannotControl = TagSet.FromBit(2);
            public static readonly TagSet CannotSleep = TagSet.FromBit(3);
            public static readonly TagSet Workable = TagSet.FromBit(4);
            public static readonly TagSet CannotTakeTask = TagSet.FromBit(5);
            public static readonly TagSet BlocksAI = TagSet.FromBit(6);
            public static readonly TagSet IsMoving = TagSet.FromBit(7);
            public static readonly TagSet CanBePlundered = TagSet.FromBit(8);
        }

        private static class DemoAbilities
        {
            public static readonly AbilityId LiveAI = new AbilityId(1);
            public static readonly AbilityId Fight = new AbilityId(2);
            public static readonly AbilityId Control = new AbilityId(3);
            public static readonly AbilityId TakeTask = new AbilityId(4);
            public static readonly AbilityId Sleep = new AbilityId(5);
            public static readonly AbilityId Work = new AbilityId(6);
        }

        private static class DemoEnterActions
        {
            public static readonly EnterActionMask CancelTask = new EnterActionMask(1u << 0);
            public static readonly EnterActionMask ClearTarget = new EnterActionMask(1u << 1);
        }

        private sealed class LogBehavior : StateBehavior
        {
            private readonly string _name;

            public LogBehavior(string name)
            {
                _name = name;
            }

            public override void OnEnter(StateContext context)
            {
                Console.WriteLine("[Enter] " + _name + " args=" + context.Args);
            }

            public override void OnTick(StateContext context, float deltaTime)
            {
                Console.WriteLine("[Tick ] " + _name + " elapsed=" + context.Elapsed.ToString("F2"));
            }

            public override void OnExit(StateContext context)
            {
                Console.WriteLine("[Exit ] " + _name);
            }
        }

        private sealed class ActorHost : IStateHost, IStateQueryable
        {
            public ushort Domain { get { return MinimalActorDemo.Domain; } }
            public StateRuntime Runtime { get; private set; }
            public bool HasEntity { get { return true; } }
            public object DomainContext { get { return this; } }

            public ActorHost(StateCatalog catalog)
            {
                Runtime = new StateRuntime(catalog, this);
            }

            public bool Supports(StateId id)
            {
                return true;
            }

            public void ExecuteEnterAction(EnterActionMask action)
            {
                Console.WriteLine("[Action] " + action);
            }

            public void Log(StateLogLevel level, string message)
            {
                Console.WriteLine("[" + level + "] " + message);
            }

            public bool Can(AbilityId ability)
            {
                return Runtime.Default.Can(ability);
            }

            public bool Has(TagSet tag)
            {
                return Runtime.Default.Has(tag);
            }

            public StateId Primary
            {
                get
                {
                    StateId life = Runtime.Default.Get(CoreLanes.Life);
                    return life.IsNone ? Runtime.Default.Get(CoreLanes.Activity) : life;
                }
            }
        }

        private sealed class ActorPresenter : IStatePresenter
        {
            public void OnStateChanged(StateChange change)
            {
                Console.WriteLine("[View ] " + change);
            }
        }

        public static void Run()
        {
            StateCatalog catalog = BuildCatalog();
            ActorHost host = new ActorHost(catalog);

            using (StateDriver driver = new StateDriver(host, new ActorPresenter()))
            {
                StateMachine machine = host.Runtime.Default;

                machine.TryEnter(Alive);
                machine.TryEnter(Idle);
                machine.TryEnter(GoWork, StateArgs.Timed(2f), EnterReason.Task);
                machine.TryEnter(Walk, StateArgs.Move(3f, 4f), EnterReason.Task);

                driver.Tick(0.5f);
                machine.TryCompleteIntent(EnterReason.Task);   // 进入 Work
                driver.Tick(1.0f);

                machine.TryEnter(Hurt, default(StateArgs), EnterReason.Damage); // Reaction 叠加，不打断 Work
                driver.Tick(0.5f);                                              // Hurt Hold 0.3s 后自动退出

                StateSnapshot snapshot = host.Runtime.Capture();
                Console.WriteLine("[Snap ] activity=" + host.Runtime.Default.Get(CoreLanes.Activity));

                machine.TryEnter(Dead, default(StateArgs), EnterReason.Damage);
                Console.WriteLine("[Query] canFight=" + host.Can(DemoAbilities.Fight) +
                                  " canLiveAI=" + host.Can(DemoAbilities.LiveAI));

                host.Runtime.Restore(snapshot, RestoreReason.Load);
                Console.WriteLine("[Load ] activity=" + host.Runtime.Default.Get(CoreLanes.Activity));
            }
        }

        private static StateCatalog BuildCatalog()
        {
            StateCatalog catalog = new StateCatalog(Domain, "MinimalActor");
            catalog.SetAliveState(Alive);
            catalog.SetIdleState(Idle);

            // 项目层规则：失去意识会同时阻断战斗/控制/睡眠/任务/AI。
            catalog.ExpandTag(
                DemoTags.Incapacitated,
                DemoTags.CannotFight | DemoTags.CannotControl | DemoTags.CannotSleep |
                DemoTags.CannotTakeTask | DemoTags.BlocksAI);

            catalog.RegisterAbility(DemoAbilities.Fight,
                (AbilityContext context) => !context.Tags.HasAny(DemoTags.CannotFight));
            catalog.RegisterAbility(DemoAbilities.Control,
                (AbilityContext context) => !context.Tags.HasAny(DemoTags.CannotControl));
            catalog.RegisterAbility(DemoAbilities.LiveAI,
                (AbilityContext context) => !context.Tags.HasAny(DemoTags.Incapacitated));
            catalog.RegisterAbility(DemoAbilities.TakeTask,
                (AbilityContext context) => !context.Tags.HasAny(DemoTags.CannotTakeTask));
            catalog.RegisterAbility(DemoAbilities.Sleep,
                (AbilityContext context) => !context.Tags.HasAny(DemoTags.CannotSleep));
            catalog.RegisterAbility(DemoAbilities.Work,
                (AbilityContext context) => context.Tags.HasAny(DemoTags.Workable));

            catalog.RegisterView(new StateViewDef(new ViewId(1), "工作中", ProgressMode.Elapsed));

            catalog.RegisterBehavior(1, () => new LogBehavior("walk"));
            catalog.RegisterBehavior(2, () => new LogBehavior("work"));
            catalog.RegisterBehavior(3, () => new LogBehavior("hurt"));

            catalog.Register(StateDefBuilder
                .Create(Alive, CoreLanes.Life)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(Dead, CoreLanes.Life)
                .Tags(DemoTags.Incapacitated | DemoTags.CanBePlundered)
                .EnterActions(DemoEnterActions.CancelTask | DemoEnterActions.ClearTarget)
                .Restore(RestorePolicy.Drop)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(Idle, CoreLanes.Activity)
                .Tags(DemoTags.Workable)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(Walk, CoreLanes.Activity)
                .Tags(DemoTags.IsMoving)
                .Behavior(1)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(Work, CoreLanes.Activity)
                .Tags(DemoTags.BlocksAI | DemoTags.CannotSleep)
                .Behavior(2)
                .View(new ViewId(1))
                .Hold(3f)
                .AutoNext(Idle)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(GoWork, CoreLanes.Intent)
                .OnArrive(Work)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(Hurt, CoreLanes.Reaction)
                .Tags(DemoTags.CannotFight)
                .Behavior(3)
                .Hold(0.3f)
                .ReturnTo(Work)
                .Build());

            return catalog;
        }
    }
}
