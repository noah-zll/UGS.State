using UGS.State;

namespace UGS.State.Tests
{
    /// <summary>项目层词汇表（测试内自建，证明 Core 不依赖任何业务常量）。</summary>
    internal static class TestTags
    {
        public static readonly TagSet Incapacitated = TagSet.FromBit(0);
        public static readonly TagSet CannotFight = TagSet.FromBit(1);
        public static readonly TagSet CannotControl = TagSet.FromBit(2);
        public static readonly TagSet CannotSleep = TagSet.FromBit(3);
        public static readonly TagSet Workable = TagSet.FromBit(4);
        public static readonly TagSet CannotTakeTask = TagSet.FromBit(5);
        public static readonly TagSet BlocksAI = TagSet.FromBit(6);
    }

    internal static class TestAbilities
    {
        public static readonly AbilityId LiveAI = new AbilityId(1);
        public static readonly AbilityId Fight = new AbilityId(2);
        public static readonly AbilityId Control = new AbilityId(3);
        public static readonly AbilityId TakeTask = new AbilityId(4);
        public static readonly AbilityId Sleep = new AbilityId(5);
        public static readonly AbilityId Work = new AbilityId(6);
    }

    internal static class TestEnterActions
    {
        public static readonly EnterActionMask CancelTask = new EnterActionMask(1u << 0);
    }

    internal static class TestCatalog
    {
        public static readonly StateId Alive = new StateId(1, 1);
        public static readonly StateId Idle = new StateId(1, 2);
        public static readonly StateId Work = new StateId(1, 3);
        public static readonly StateId Dead = new StateId(1, 4);
        public static readonly StateId Move = new StateId(1, 5);
        public static readonly StateId GoWork = new StateId(1, 6);
        public static readonly StateId Hurt = new StateId(1, 7);
        public static readonly StateId Cancel = new StateId(1, 8);

        public static StateCatalog Create()
        {
            StateCatalog catalog = new StateCatalog(1, "test");
            catalog.SetAliveState(Alive);
            catalog.SetIdleState(Idle);

            catalog.ExpandTag(
                TestTags.Incapacitated,
                TestTags.CannotFight | TestTags.CannotControl | TestTags.CannotSleep |
                TestTags.CannotTakeTask | TestTags.BlocksAI);

            catalog.RegisterAbility(TestAbilities.Fight,
                (AbilityContext context) => !context.Tags.HasAny(TestTags.CannotFight));
            catalog.RegisterAbility(TestAbilities.Control,
                (AbilityContext context) => !context.Tags.HasAny(TestTags.CannotControl));
            catalog.RegisterAbility(TestAbilities.LiveAI,
                (AbilityContext context) => !context.Tags.HasAny(TestTags.Incapacitated));
            catalog.RegisterAbility(TestAbilities.TakeTask,
                (AbilityContext context) => !context.Tags.HasAny(TestTags.CannotTakeTask));
            catalog.RegisterAbility(TestAbilities.Sleep,
                (AbilityContext context) => !context.Tags.HasAny(TestTags.CannotSleep));
            catalog.RegisterAbility(TestAbilities.Work,
                (AbilityContext context) => context.Tags.HasAny(TestTags.Workable));

            catalog.Register(StateDefBuilder
                .Create(Alive, CoreLanes.Life)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(Idle, CoreLanes.Activity)
                .Tags(TestTags.Workable)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(Work, CoreLanes.Activity)
                .Priority(5)
                .Tags(TestTags.BlocksAI)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(Dead, CoreLanes.Life)
                .Tags(TestTags.Incapacitated)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(Move, CoreLanes.Activity)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(GoWork, CoreLanes.Intent)
                .OnArrive(Work)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(Hurt, CoreLanes.Reaction)
                .Hold(0.1f)
                .ReturnTo(Idle)
                .Build());

            catalog.Register(StateDefBuilder
                .Create(Cancel, CoreLanes.Activity)
                .EnterActions(TestEnterActions.CancelTask)
                .Build());

            return catalog;
        }
    }
}
