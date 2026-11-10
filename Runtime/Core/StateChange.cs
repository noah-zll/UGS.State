namespace UGS.State
{
    /// <summary>
    /// 一次状态变更。进入/替换时同时携带 Previous 与 Current，便于表现层原子地“先卸后挂”。
    /// </summary>
    public readonly struct StateChange
    {
        public readonly StateChangeKind Kind;
        public readonly StateMachine Machine;
        public readonly ChannelId Channel;
        public readonly LaneId Lane;
        public readonly StateId From;
        public readonly StateId To;
        public readonly StateInstance Previous;
        public readonly StateInstance Current;
        public readonly EnterReason Reason;
        public readonly double Time;

        public StateChange(
            StateChangeKind kind,
            StateMachine machine,
            ChannelId channel,
            LaneId lane,
            StateId from,
            StateId to,
            StateInstance previous,
            StateInstance current,
            EnterReason reason,
            double time)
        {
            Kind = kind;
            Machine = machine;
            Channel = channel;
            Lane = lane;
            From = from;
            To = to;
            Previous = previous;
            Current = current;
            Reason = reason;
            Time = time;
        }

        public override string ToString()
        {
            return Kind + " " + Lane + " " + From + " -> " + To + " (" + Reason + ")";
        }
    }
}
