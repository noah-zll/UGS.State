using System.Collections.Generic;
using UGS.State;

namespace UGS.State.Tests
{
    internal sealed class TestHost : IStateHost, IStateQueryable, IStateChangeObserver
    {
        public ushort Domain { get; private set; }
        public StateRuntime Runtime { get; private set; }
        public bool HasEntity { get { return true; } }
        public object DomainContext { get { return this; } }
        public EnterActionMask LastAction { get; private set; }
        public readonly List<string> Logs = new List<string>();
        public readonly List<StateChange> Observed = new List<StateChange>();

        public TestHost(StateCatalog catalog, ushort domain = 1)
        {
            Domain = domain;
            Runtime = new StateRuntime(catalog, this);
        }

        public bool Supports(StateId id)
        {
            return true;
        }

        public void ExecuteEnterAction(EnterActionMask action)
        {
            LastAction = action;
        }

        public void Log(StateLogLevel level, string message)
        {
            Logs.Add(level + ":" + message);
        }

        public void OnStateChanged(StateChange change)
        {
            Observed.Add(change);
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

    internal sealed class TestPresenter : IStatePresenter
    {
        public readonly List<StateChange> Changes = new List<StateChange>();

        public void OnStateChanged(StateChange change)
        {
            Changes.Add(change);
        }
    }
}
