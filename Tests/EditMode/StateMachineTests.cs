using NUnit.Framework;
using UGS.State;

namespace UGS.State.Tests
{
    public sealed class StateMachineTests
    {
        [Test]
        public void SameLane_ReplacesPrevious()
        {
            TestHost host = new TestHost(TestCatalog.Create());
            StateMachine machine = host.Runtime.Default;

            Assert.IsTrue(machine.TryEnter(TestCatalog.Idle));
            Assert.IsTrue(machine.TryEnter(TestCatalog.Work));

            Assert.IsTrue(machine.Is(TestCatalog.Work));
            Assert.IsFalse(machine.Is(TestCatalog.Idle));
        }

        [Test]
        public void SameState_IsNoOp()
        {
            TestHost host = new TestHost(TestCatalog.Create());
            StateMachine machine = host.Runtime.Default;
            TestPresenter presenter = new TestPresenter();
            machine.Changed += presenter.OnStateChanged;

            Assert.IsTrue(machine.TryEnter(TestCatalog.Idle));
            Assert.IsFalse(machine.TryEnter(TestCatalog.Idle));
            Assert.AreEqual(1, presenter.Changes.Count);
        }

        [Test]
        public void LowerPriority_IsRejected()
        {
            TestHost host = new TestHost(TestCatalog.Create());
            StateMachine machine = host.Runtime.Default;
            TestPresenter presenter = new TestPresenter();
            machine.Changed += presenter.OnStateChanged;

            Assert.IsTrue(machine.TryEnter(TestCatalog.Work));
            Assert.IsFalse(machine.TryEnter(TestCatalog.Idle));

            Assert.IsTrue(machine.Is(TestCatalog.Work));
            Assert.AreEqual(StateChangeKind.Rejected, presenter.Changes[presenter.Changes.Count - 1].Kind);
        }

        [Test]
        public void Force_BypassesPriority()
        {
            TestHost host = new TestHost(TestCatalog.Create());
            StateMachine machine = host.Runtime.Default;

            Assert.IsTrue(machine.TryEnter(TestCatalog.Work));
            Assert.IsTrue(machine.TryEnter(TestCatalog.Idle, default(StateArgs), EnterReason.Player, true));
            Assert.IsTrue(machine.Is(TestCatalog.Idle));
        }

        [Test]
        public void Life_BlocksActivity_UntilAlive()
        {
            TestHost host = new TestHost(TestCatalog.Create());
            StateMachine machine = host.Runtime.Default;

            Assert.IsTrue(machine.TryEnter(TestCatalog.Idle));
            Assert.IsTrue(machine.TryEnter(TestCatalog.Dead));
            Assert.IsTrue(machine.Get(CoreLanes.Activity).IsNone);
            Assert.IsFalse(machine.TryEnter(TestCatalog.Idle));
            Assert.IsTrue(machine.Is(TestCatalog.Dead));

            Assert.IsTrue(machine.TryEnter(TestCatalog.Alive));
            Assert.IsTrue(machine.TryEnter(TestCatalog.Idle));
            Assert.IsTrue(machine.Is(TestCatalog.Idle));
        }

        [Test]
        public void ParallelLanes_Coexist()
        {
            TestHost host = new TestHost(TestCatalog.Create());
            StateMachine machine = host.Runtime.Default;

            Assert.IsTrue(machine.TryEnter(TestCatalog.Idle));
            Assert.IsTrue(machine.TryEnter(TestCatalog.Hurt));

            Assert.AreEqual(TestCatalog.Idle, machine.Get(CoreLanes.Activity));
            Assert.AreEqual(TestCatalog.Hurt, machine.Get(CoreLanes.Reaction));
        }

        [Test]
        public void Tags_AreExpanded()
        {
            TestHost host = new TestHost(TestCatalog.Create());
            StateMachine machine = host.Runtime.Default;

            Assert.IsTrue(machine.TryEnter(TestCatalog.Dead));

            Assert.IsTrue(machine.Has(TestTags.CannotFight));
            Assert.IsTrue(machine.Has(TestTags.CannotControl));
            Assert.IsTrue(machine.Has(TestTags.BlocksAI));
            Assert.IsFalse(machine.Has(TestTags.Workable));
        }

        [Test]
        public void EnterActions_AreExecuted()
        {
            TestHost host = new TestHost(TestCatalog.Create());

            Assert.IsTrue(host.Runtime.Default.TryEnter(TestCatalog.Cancel));
            Assert.AreEqual(TestEnterActions.CancelTask, host.LastAction);
        }

        [Test]
        public void EnterActions_AreSkippedOnRestore()
        {
            TestHost host = new TestHost(TestCatalog.Create());

            Assert.IsTrue(host.Runtime.Default.TryEnter(
                TestCatalog.Cancel, default(StateArgs), EnterReason.Restore, true));
            Assert.AreEqual(EnterActionMask.None, host.LastAction);
        }

        [Test]
        public void UpdateArgs_RefreshesActiveState()
        {
            TestHost host = new TestHost(TestCatalog.Create());
            StateMachine machine = host.Runtime.Default;

            Assert.IsTrue(machine.TryEnter(TestCatalog.Move, StateArgs.Move(1f, 2f)));
            Assert.IsTrue(machine.UpdateArgs(CoreLanes.Activity, StateArgs.Move(3f, 4f)));

            Assert.AreEqual(3f, machine.Active(CoreLanes.Activity).Args.A);
            Assert.AreEqual(4f, machine.Active(CoreLanes.Activity).Args.B);
        }

        [Test]
        public void CompleteIntent_EntersOnArrive()
        {
            TestHost host = new TestHost(TestCatalog.Create());
            StateMachine machine = host.Runtime.Default;

            Assert.IsTrue(machine.TryEnter(TestCatalog.Move));
            Assert.IsTrue(machine.TryEnter(TestCatalog.GoWork, StateArgs.Timed(3f), EnterReason.Task));
            Assert.AreEqual(TestCatalog.GoWork, machine.Get(CoreLanes.Intent));

            Assert.IsTrue(machine.TryCompleteIntent(EnterReason.Task));
            Assert.AreEqual(TestCatalog.Work, machine.Get(CoreLanes.Activity));
            Assert.IsTrue(machine.Get(CoreLanes.Intent).IsNone);
        }

        [Test]
        public void ReentrantEnter_IsQueued()
        {
            TestHost host = new TestHost(TestCatalog.Create());
            StateMachine machine = host.Runtime.Default;

            machine.Changed += change =>
            {
                if (change.Kind == StateChangeKind.Entered && change.To == TestCatalog.Idle)
                {
                    machine.TryEnter(TestCatalog.Work, default(StateArgs), EnterReason.Rule);
                }
            };

            Assert.IsTrue(machine.TryEnter(TestCatalog.Idle));
            Assert.IsTrue(machine.Is(TestCatalog.Work));
        }

        [Test]
        public void CanAndHas_UseAbilityRules()
        {
            TestHost host = new TestHost(TestCatalog.Create());
            StateMachine machine = host.Runtime.Default;

            Assert.IsTrue(machine.TryEnter(TestCatalog.Idle));
            Assert.IsTrue(machine.Can(TestAbilities.Work));
            Assert.IsTrue(machine.Can(TestAbilities.Fight));

            Assert.IsTrue(machine.TryEnter(TestCatalog.Dead));
            Assert.IsFalse(machine.Can(TestAbilities.Fight));
            Assert.IsFalse(machine.Can(TestAbilities.LiveAI));
        }
    }
}
