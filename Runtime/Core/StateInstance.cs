namespace UGS.State
{
    /// <summary>
    /// 一次状态进入的实例数据。行为本身推荐无状态单例，跨帧/跨实体数据放在 <see cref="Data"/>。
    /// </summary>
    public sealed class StateInstance
    {
        public StateId Id { get; private set; }
        public LaneId Lane { get; private set; }
        public StateArgs Args { get; internal set; }
        public float Elapsed { get; internal set; }
        /// <summary>行为自用的黑板数据，由行为在 OnEnter/OnExit 中维护。</summary>
        public object Data { get; set; }

        internal StateInstance(StateId id, LaneId lane, StateArgs args)
        {
            Id = id;
            Lane = lane;
            Args = args;
            Elapsed = 0f;
        }

        public override string ToString()
        {
            return Id + "@" + Lane + " t=" + Elapsed;
        }
    }
}
