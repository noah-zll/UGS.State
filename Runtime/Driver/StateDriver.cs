using System;
using System.Collections.Generic;
using System.Threading;

namespace UGS.State
{
    /// <summary>
    /// 实体侧驱动：订阅状态变更、维护行为生命周期、推进运行时与行为 Tick、通知表现层。
    /// 一个实体一个实例（通常由实体模块持有）。
    /// </summary>
    public sealed class StateDriver : IDisposable
    {
        private sealed class ActiveEntry
        {
            public StateMachine Machine;
            public StateInstance Instance;
            public StateBehavior Behavior;
            /// <summary>逐实例行为由 Driver 创建，退出时需要释放。</summary>
            public bool OwnsBehavior;
            public bool Active;
        }

        private readonly IStateHost _host;
        private readonly IStatePresenter _presenter;
        private readonly IStateChangeObserver _observer;
        private readonly Dictionary<StateInstance, ActiveEntry> _entries =
            new Dictionary<StateInstance, ActiveEntry>();
        private readonly List<ActiveEntry> _ordered = new List<ActiveEntry>();
        private readonly List<ActiveEntry> _tickBuffer = new List<ActiveEntry>();
        private readonly List<StateMachine> _attached = new List<StateMachine>();
        /// <summary>驱动所在线程（行为会操作 Unity 对象，只能在主线程执行生命周期）。</summary>
        private readonly int _mainThreadId;
        private bool _needsReconcile;
        private bool _disposed;

        public StateDriver(IStateHost host, IStatePresenter presenter)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            _host = host;
            _presenter = presenter;
            _observer = host as IStateChangeObserver;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _host.Runtime.MachineCreated += Attach;
            Attach(_host.Runtime.Default);
        }

        public IStatePresenter Presenter
        {
            get { return _presenter; }
        }

        public void Attach(StateMachine machine)
        {
            if (_disposed || machine == null || _attached.Contains(machine))
            {
                return;
            }

            _attached.Add(machine);
            machine.Changed += OnChanged;
        }

        public void Detach(StateMachine machine)
        {
            if (machine == null || !_attached.Remove(machine))
            {
                return;
            }

            machine.Changed -= OnChanged;
        }

        /// <summary>推进时间、处理自动转换，并 Tick 所有活跃行为。</summary>
        public void Tick(float deltaTime)
        {
            if (_disposed)
            {
                return;
            }

            // 后台线程产生过状态变更：先按运行时当前状态重建行为，保证行为与数据一致。
            if (_needsReconcile)
            {
                ReconcileBehaviors();
            }

            _host.Runtime.Tick(deltaTime);

            // 反应 lane 是叠加层：它活跃时暂停其它 lane 的行为推进，
            // 避免受击 / 格挡 / 倒地期间，战斗、移动等行为继续运行。
            bool reactionActive = !_host.Runtime.Default.Get(CoreLanes.Reaction).IsNone;

            // 拷贝一份，避免 OnTick 内发生转换修改 _ordered。
            _tickBuffer.Clear();
            for (int i = 0; i < _ordered.Count; i++)
            {
                _tickBuffer.Add(_ordered[i]);
            }

            for (int i = 0; i < _tickBuffer.Count; i++)
            {
                ActiveEntry entry = _tickBuffer[i];
                if (!entry.Active || entry.Instance == null || entry.Behavior == null)
                {
                    continue;
                }

                if (reactionActive && entry.Instance.Lane != CoreLanes.Reaction)
                {
                    continue;
                }

                try
                {
                    entry.Behavior.OnTick(new StateContext(_host, entry.Machine, entry.Instance), deltaTime);
                }
                catch (Exception exception)
                {
                    _host.Log(StateLogLevel.Error,
                        "Behavior OnTick failed for " + entry.Instance.Id + ": " + exception);
                }
            }
        }

        /// <summary>
        /// 获取当前活跃的指定类型行为实例。
        /// <para>供外部事件（动画帧、受击等）向逐实例行为请求内部阶段切换，避免行为把入口暴露成静态状态。</para>
        /// </summary>
        public T TryGetActiveBehavior<T>() where T : StateBehavior
        {
            if (_disposed)
            {
                return null;
            }

            for (int i = 0; i < _ordered.Count; i++)
            {
                ActiveEntry entry = _ordered[i];
                if (entry.Active && entry.Behavior is T behavior)
                {
                    return behavior;
                }
            }

            return null;
        }

        private void OnChanged(StateChange change)
        {
            if (_disposed)
            {
                return;
            }

            // 行为会操作 Unity 对象，不能跨线程执行。
            // 后台线程（存档加载 / 数据模拟）只改数据，行为留到下一次主线程 Tick 前按运行时状态重建。
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                _needsReconcile = true;
                return;
            }

            if (change.Previous != null)
            {
                ExitBehavior(change.Previous);
            }

            // 先让数据层跟上，行为 OnEnter 才能读到一致的事实源。
            if (_observer != null)
            {
                try
                {
                    _observer.OnStateChanged(change);
                }
                catch (Exception exception)
                {
                    _host.Log(StateLogLevel.Error, "Observer failed: " + exception);
                }
            }

            if (change.Current != null)
            {
                EnterBehavior(change.Machine, change.Current);
            }

            if (_presenter != null)
            {
                try
                {
                    _presenter.OnStateChanged(change);
                }
                catch (Exception exception)
                {
                    _host.Log(StateLogLevel.Error, "Presenter failed: " + exception);
                }
            }
        }

        private void EnterBehavior(StateMachine machine, StateInstance instance)
        {
            if (instance == null || _entries.ContainsKey(instance))
            {
                return;
            }

            StateDef def;
            if (!_host.Runtime.Catalog.TryGetState(instance.Id, out def))
            {
                return;
            }

            StateCatalog catalog = _host.Runtime.Catalog;
            bool ownsBehavior = catalog.GetBehaviorScope(def.BehaviorId) == BehaviorScope.PerInstance;
            StateBehavior behavior = ownsBehavior
                ? catalog.CreateBehavior(def.BehaviorId)
                : catalog.GetBehavior(def.BehaviorId);
            if (behavior == null)
            {
                return;
            }

            ActiveEntry entry = new ActiveEntry
            {
                Machine = machine,
                Instance = instance,
                Behavior = behavior,
                OwnsBehavior = ownsBehavior,
                Active = true,
            };

            _entries[instance] = entry;
            _ordered.Add(entry);

            try
            {
                behavior.OnEnter(new StateContext(_host, machine, instance));
            }
            catch (Exception exception)
            {
                _host.Log(StateLogLevel.Error,
                    "Behavior OnEnter failed for " + instance.Id + ": " + exception);
            }
        }

        private void ExitBehavior(StateInstance instance)
        {
            ActiveEntry entry;
            if (instance == null || !_entries.TryGetValue(instance, out entry))
            {
                return;
            }

            _entries.Remove(instance);
            _ordered.Remove(entry);
            entry.Active = false;

            try
            {
                entry.Behavior.OnExit(new StateContext(_host, entry.Machine, instance));
            }
            catch (Exception exception)
            {
                _host.Log(StateLogLevel.Error,
                    "Behavior OnExit failed for " + instance.Id + ": " + exception);
            }

            ReleaseBehavior(entry);
        }

        /// <summary>
        /// 按运行时当前各 lane 的状态重建行为：先退出全部旧行为，再依次进入当前状态的行为。
        /// <para>用于后台线程改过状态后，把行为对齐到最新数据。</para>
        /// </summary>
        private void ReconcileBehaviors()
        {
            _needsReconcile = false;

            for (int i = _ordered.Count - 1; i >= 0; i--)
            {
                ActiveEntry entry = _ordered[i];
                entry.Active = false;
                _entries.Remove(entry.Instance);

                try
                {
                    entry.Behavior.OnExit(new StateContext(_host, entry.Machine, entry.Instance));
                }
                catch (Exception exception)
                {
                    _host.Log(StateLogLevel.Error,
                        "Behavior OnExit failed for " + entry.Instance.Id + ": " + exception);
                }

                ReleaseBehavior(entry);
            }

            _ordered.Clear();

            StateCatalog catalog = _host.Runtime.Catalog;
            IReadOnlyList<StateMachine> machines = _host.Runtime.Machines;
            for (int m = 0; m < machines.Count; m++)
            {
                StateMachine machine = machines[m];
                for (int i = 0; i < catalog.LaneCount; i++)
                {
                    StateInstance instance = machine.Active(catalog.GetLane(i).Id);
                    if (instance != null)
                    {
                        EnterBehavior(machine, instance);
                    }
                }
            }
        }

        /// <summary>释放逐实例行为（共享单例不做处理）。</summary>
        private void ReleaseBehavior(ActiveEntry entry)
        {
            if (!entry.OwnsBehavior || entry.Behavior == null)
            {
                return;
            }

            StateBehavior behavior = entry.Behavior;
            entry.Behavior = null;
            entry.OwnsBehavior = false;

            try
            {
                behavior.Dispose();
            }
            catch (Exception exception)
            {
                _host.Log(StateLogLevel.Warning,
                    "Behavior Dispose failed for " + entry.Instance.Id + ": " + exception);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _host.Runtime.MachineCreated -= Attach;

            for (int i = 0; i < _attached.Count; i++)
            {
                _attached[i].Changed -= OnChanged;
            }

            _attached.Clear();

            for (int i = _ordered.Count - 1; i >= 0; i--)
            {
                ActiveEntry entry = _ordered[i];
                entry.Active = false;
                try
                {
                    entry.Behavior.OnExit(new StateContext(_host, entry.Machine, entry.Instance));
                }
                catch (Exception exception)
                {
                    _host.Log(StateLogLevel.Warning,
                        "Behavior OnExit during dispose failed for " + entry.Instance.Id + ": " + exception);
                }

                ReleaseBehavior(entry);
            }

            _entries.Clear();
            _ordered.Clear();
            _tickBuffer.Clear();
        }
    }
}
