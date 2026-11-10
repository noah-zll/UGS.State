using NUnit.Framework;
using UGS.State;

namespace UGS.State.Tests
{
    public sealed class StateCatalogTests
    {
        private static StateId Id(ushort local)
        {
            return new StateId(2, local);
        }

        [Test]
        public void DuplicateState_Throws()
        {
            StateCatalog catalog = new StateCatalog(2, "dup");
            catalog.Register(StateDefBuilder.Create(Id(1), CoreLanes.Activity).Build());

            Assert.Throws<StateException>(() =>
                catalog.Register(StateDefBuilder.Create(Id(1), CoreLanes.Activity).Build()));
        }

        [Test]
        public void IntentWithoutOnArrive_ThrowsOnSeal()
        {
            StateCatalog catalog = new StateCatalog(2, "intent");
            catalog.Register(StateDefBuilder.Create(Id(1), CoreLanes.Intent).Build());

            Assert.Throws<StateException>(() => catalog.Seal());
        }

        [Test]
        public void ReactionWithoutExit_ThrowsOnSeal()
        {
            StateCatalog catalog = new StateCatalog(2, "reaction");
            catalog.Register(StateDefBuilder.Create(Id(1), CoreLanes.Reaction).Build());

            Assert.Throws<StateException>(() => catalog.Seal());
        }

        [Test]
        public void UnknownBehavior_ThrowsOnSeal()
        {
            StateCatalog catalog = new StateCatalog(2, "behavior");
            catalog.Register(StateDefBuilder.Create(Id(1), CoreLanes.Activity).Behavior(99).Build());

            Assert.Throws<StateException>(() => catalog.Seal());
        }

        [Test]
        public void UnknownView_ThrowsOnSeal()
        {
            StateCatalog catalog = new StateCatalog(2, "view");
            catalog.Register(StateDefBuilder.Create(Id(1), CoreLanes.Activity).View(new ViewId(9)).Build());

            Assert.Throws<StateException>(() => catalog.Seal());
        }

        [Test]
        public void UnknownTransitionTarget_ThrowsOnSeal()
        {
            StateCatalog catalog = new StateCatalog(2, "transition");
            catalog.Register(StateDefBuilder
                .Create(Id(1), CoreLanes.Activity)
                .Transition(TransitionTrigger.OnTick, Id(999))
                .Build());

            Assert.Throws<StateException>(() => catalog.Seal());
        }

        [Test]
        public void AliveStateMustExist()
        {
            StateCatalog catalog = new StateCatalog(2, "alive");
            catalog.SetAliveState(Id(999));
            catalog.Register(StateDefBuilder.Create(Id(1), CoreLanes.Activity).Build());

            Assert.Throws<StateException>(() => catalog.Seal());
        }

        [Test]
        public void RegisterAfterSeal_Throws()
        {
            StateCatalog catalog = new StateCatalog(2, "sealed");
            catalog.Register(StateDefBuilder.Create(Id(1), CoreLanes.Activity).Build());
            catalog.Seal();

            Assert.Throws<StateException>(() =>
                catalog.Register(StateDefBuilder.Create(Id(2), CoreLanes.Activity).Build()));
        }

        [Test]
        public void Expansion_AppliesImpliedTags()
        {
            StateCatalog catalog = new StateCatalog(2, "expand");
            TagSet declared = TagSet.FromBit(40);
            TagSet implied = TagSet.FromBit(41);
            catalog.ExpandTag(declared, implied);
            catalog.Register(StateDefBuilder.Create(Id(1), CoreLanes.Activity).Tags(declared).Build());
            catalog.Seal();

            TagSet effective;
            StateDef def;
            catalog.TryGetState(Id(1), out def);
            effective = def.EffectiveTags;

            Assert.IsTrue(effective.HasAll(declared));
            Assert.IsTrue(effective.HasAll(implied));
        }
    }
}
