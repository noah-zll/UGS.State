using NUnit.Framework;
using UGS.State;

namespace UGS.State.Tests
{
    public sealed class ExtensionAndDriverTests
    {
        private sealed class CountingBehavior : StateBehavior
        {
            public int Entered;
            public int Ticked;
            public int Exited;

            public override void OnEnter(StateContext context)
            {
                Entered++;
            }

            public override void OnTick(StateContext context, float deltaTime)
            {
                Ticked++;
            }

            public override void OnExit(StateContext context)
            {
                Exited++;
            }
        }

        private sealed class DisposableBehavior : StateBehavior
        {
            public static int Created;
            public static int Disposed;

            public override void OnEnter(StateContext context)
            {
            }

            public override void Dispose()
            {
                Disposed++;
            }
        }

        [Test]
        public void PerInstanceBehavior_NewInstancePerEntry_DisposedOnExit()
        {
            StateId idle = new StateId(11, 1);
            StateId working = new StateId(11, 2);

            StateCatalog catalog = new StateCatalog(11, "per-instance");
            catalog.Register(StateDefBuilder.Create(idle, CoreLanes.Activity).Build());
            catalog.Register(StateDefBuilder.Create(working, CoreLanes.Activity).Behavior(7).Build());
            DisposableBehavior.Created = 0;
            DisposableBehavior.Disposed = 0;
            catalog.RegisterBehavior(7, () =>
            {
                DisposableBehavior.Created++;
                return new DisposableBehavior();
            }, BehaviorScope.PerInstance);

            TestHost host = new TestHost(catalog, 11);
            using (StateDriver driver = new StateDriver(host, null))
            {
                StateMachine machine = host.Runtime.Default;

                Assert.IsTrue(machine.TryEnter(working, default(StateArgs), EnterReason.AI));
                Assert.AreEqual(1, DisposableBehavior.Created);

                Assert.IsTrue(machine.TryEnter(idle, default(StateArgs), EnterReason.AI));
                Assert.AreEqual(1, DisposableBehavior.Disposed);

                Assert.IsTrue(machine.TryEnter(working, default(StateArgs), EnterReason.AI));
                Assert.AreEqual(2, DisposableBehavior.Created);
            }

            // 退出状态各释放一次，Dispose 时再释放最后一个。
            Assert.AreEqual(2, DisposableBehavior.Disposed);
        }

        [Test]
        public void Driver_NotifiesObserverOfRuntimeTransitions()
        {
            TestHost host = new TestHost(TestCatalog.Create());

            using (StateDriver driver = new StateDriver(host, null))
            {
                // 数据层自己写入再镜像时，runtime 的 TryEnter 同样会触发变更回调。
                Assert.IsTrue(host.Runtime.Default.TryEnter(TestCatalog.Idle));
                Assert.AreEqual(1, host.Observed.Count);
                Assert.AreEqual(TestCatalog.Idle, host.Observed[0].Current.Id);

                // Hold 到期的自动转换也要回调给宿主。
                driver.Tick(0.05f);
                Assert.IsTrue(host.Runtime.Default.TryEnter(TestCatalog.Hurt));
                int before = host.Observed.Count;

                driver.Tick(0.1f);
                Assert.Greater(host.Observed.Count, before);
                Assert.IsTrue(host.Runtime.Default.Get(CoreLanes.Reaction).IsNone);
            }
        }

        [Test]
        public void CustomDomain_WorksWithoutCoreChanges()
        {
            LaneId heatLane = new LaneId(20);
            TagSet onFire = TagSet.FromBit(70);
            AbilityId overheat = new AbilityId(2001);

            StateId idle = new StateId(9, 1);
            StateId aiming = new StateId(9, 2);

            StateCatalog catalog = new StateCatalog(9, "turret");
            catalog.RegisterLane(new LaneDef(heatLane, "heat"));

            CountingBehavior behavior = new CountingBehavior();
            catalog.Register(StateDefBuilder.Create(idle, CoreLanes.Activity).Build());
            catalog.Register(StateDefBuilder
                .Create(aiming, CoreLanes.Activity)
                .Tags(onFire)
                .Behavior(1)
                .Build());
            catalog.RegisterBehavior(1, () => behavior);
            catalog.RegisterAbility(overheat, (AbilityContext context) => context.Tags.HasAny(onFire));

            TestHost host = new TestHost(catalog, 9);
            TestPresenter presenter = new TestPresenter();

            using (StateDriver driver = new StateDriver(host, presenter))
            {
                StateMachine machine = host.Runtime.Default;

                Assert.IsTrue(machine.TryEnter(aiming, default(StateArgs), EnterReason.AI));
                Assert.IsTrue(machine.Has(onFire));
                Assert.IsTrue(machine.Can(overheat));

                driver.Tick(0.1f);

                Assert.AreEqual(1, behavior.Entered);
                Assert.AreEqual(1, behavior.Ticked);

                Assert.IsTrue(machine.TryEnter(idle, default(StateArgs), EnterReason.AI));
                Assert.AreEqual(1, behavior.Exited);
                Assert.Greater(presenter.Changes.Count, 0);
            }
        }

        [Test]
        public void HoldExpiry_ExitsReaction()
        {
            TestHost host = new TestHost(TestCatalog.Create());
            TestPresenter presenter = new TestPresenter();

            using (StateDriver driver = new StateDriver(host, presenter))
            {
                StateMachine machine = host.Runtime.Default;
                Assert.IsTrue(machine.TryEnter(TestCatalog.Hurt));

                driver.Tick(0.05f);
                Assert.AreEqual(TestCatalog.Hurt, machine.Get(CoreLanes.Reaction));

                driver.Tick(0.1f);
                Assert.IsTrue(machine.Get(CoreLanes.Reaction).IsNone);
            }
        }

        [Test]
        public void Snapshot_RoundTripsLanes()
        {
            StateCatalog catalog = TestCatalog.Create();
            TestHost first = new TestHost(catalog);
            first.Runtime.Default.TryEnter(TestCatalog.Idle);
            first.Runtime.Default.TryEnter(TestCatalog.Hurt);

            StateSnapshot snapshot = first.Runtime.Capture();

            TestHost second = new TestHost(catalog);
            second.Runtime.Restore(snapshot, RestoreReason.Load);

            Assert.AreEqual(TestCatalog.Idle, second.Runtime.Default.Get(CoreLanes.Activity));
            Assert.AreEqual(TestCatalog.Hurt, second.Runtime.Default.Get(CoreLanes.Reaction));
        }

        [Test]
        public void Migrator_AdvancesVersion()
        {
            StateSnapshot snapshot = new StateSnapshot { Version = 1, Channels = new ChannelSnapshot[0] };
            StateMigrator migrator = new StateMigrator()
                .Register(1, (StateSnapshot value) =>
                {
                    value.Channels = new[] { new ChannelSnapshot { Channel = 42 } };
                });

            StateSnapshot migrated = migrator.Migrate(snapshot, 2);

            Assert.AreEqual(2, migrated.Version);
            Assert.AreEqual(42, migrated.Channels[0].Channel);
        }
    }
}
