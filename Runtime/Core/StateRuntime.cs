using System;
using System.Collections.Generic;

namespace UGS.State
{
    /// <summary>
    /// 宿主的状态运行时：持有一个或多个状态机通道，负责时间推进与快照。
    /// 每个数据对象一份；默认只使用 <see cref="Default"/> 通道。
    /// </summary>
    public sealed class StateRuntime
    {
        private readonly Dictionary<ChannelId, StateMachine> _machinesByChannel =
            new Dictionary<ChannelId, StateMachine>();

        private readonly List<StateMachine> _machines = new List<StateMachine>();

        public StateCatalog Catalog { get; private set; }
        public IStateHost Host { get; private set; }
        public StateMachine Default { get; private set; }

        /// <summary>运行时累计时间（秒），仅由 Tick 推进。</summary>
        public double Now { get; private set; }

        /// <summary>新通道创建时触发，便于 Driver 自动挂载。</summary>
        public event Action<StateMachine> MachineCreated;

        public StateRuntime(StateCatalog catalog, IStateHost host)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            Catalog = catalog;
            Host = host;
            Catalog.Seal();

            Default = new StateMachine(this, Catalog, ChannelId.Default);
            _machinesByChannel[Default.Channel] = Default;
            _machines.Add(Default);
        }

        public StateMachine Get(ChannelId channel)
        {
            StateMachine machine;
            if (_machinesByChannel.TryGetValue(channel, out machine))
            {
                return machine;
            }

            machine = new StateMachine(this, Catalog, channel);
            _machinesByChannel[channel] = machine;
            _machines.Add(machine);

            Action<StateMachine> handler = MachineCreated;
            if (handler != null)
            {
                handler(machine);
            }

            return machine;
        }

        public IReadOnlyList<StateMachine> Machines
        {
            get { return _machines; }
        }

        /// <summary>推进所有通道：累计时间、处理 Hold 到期、条件转换。</summary>
        public void Tick(float deltaTime)
        {
            Now += deltaTime;
            int count = _machines.Count;
            for (int i = 0; i < count; i++)
            {
                _machines[i].Tick(deltaTime);
            }
        }

        #region 快照

        public StateSnapshot Capture()
        {
            StateSnapshot snapshot = new StateSnapshot();
            ChannelSnapshot[] channels = new ChannelSnapshot[_machines.Count];

            for (int m = 0; m < _machines.Count; m++)
            {
                StateMachine machine = _machines[m];
                int laneCount = Catalog.LaneCount;
                int[] laneStates = new int[laneCount];
                float[] laneElapsed = new float[laneCount];
                int targetId = 0;
                float value = 0f;
                bool payloadTaken = false;

                for (int i = 0; i < laneCount; i++)
                {
                    LaneDef lane = Catalog.GetLane(i);
                    StateInstance instance = machine.Active(lane.Id);
                    if (instance == null)
                    {
                        continue;
                    }

                    laneStates[i] = instance.Id.Packed;
                    laneElapsed[i] = instance.Elapsed;

                    if (!payloadTaken)
                    {
                        targetId = instance.Args.TargetId;
                        value = instance.Args.A;
                        payloadTaken = true;
                    }
                }

                channels[m] = new ChannelSnapshot
                {
                    Channel = machine.Channel.Value,
                    LaneStates = laneStates,
                    LaneElapsed = laneElapsed,
                    TargetId = targetId,
                    Value = value,
                };
            }

            snapshot.Channels = channels;
            return snapshot;
        }

        public void Restore(StateSnapshot snapshot, RestoreReason reason)
        {
            if (snapshot == null || snapshot.Channels == null)
            {
                return;
            }

            for (int m = 0; m < snapshot.Channels.Length; m++)
            {
                ChannelSnapshot channelSnapshot = snapshot.Channels[m];
                if (channelSnapshot == null)
                {
                    continue;
                }

                StateMachine machine = Get(new ChannelId((byte)channelSnapshot.Channel));
                machine.Clear(EnterReason.Restore);

                if (channelSnapshot.LaneStates == null)
                {
                    continue;
                }

                int laneCount = Catalog.LaneCount;
                for (int i = 0; i < channelSnapshot.LaneStates.Length && i < laneCount; i++)
                {
                    StateId id = StateId.Unpack(channelSnapshot.LaneStates[i]);
                    if (id.IsNone)
                    {
                        continue;
                    }

                    StateDef def;
                    if (!Catalog.TryGetState(id, out def))
                    {
                        continue;
                    }

                    if (def.Restore == RestorePolicy.Drop)
                    {
                        continue;
                    }

                    if (def.Restore == RestorePolicy.ResetToIdle)
                    {
                        if (Catalog.IdleStateId.IsNone || !Catalog.TryGetState(Catalog.IdleStateId, out def))
                        {
                            continue;
                        }

                        id = Catalog.IdleStateId;
                    }

                    float elapsed = channelSnapshot.LaneElapsed != null && i < channelSnapshot.LaneElapsed.Length
                        ? channelSnapshot.LaneElapsed[i]
                        : 0f;

                    if (def.Restore == RestorePolicy.Reenter)
                    {
                        elapsed = 0f;
                    }

                    StateArgs args = new StateArgs(0f, 0f, channelSnapshot.TargetId, 0, channelSnapshot.Value, null);
                    machine.RestoreState(id, args, elapsed);
                }
            }
        }

        public void ClearAll(EnterReason reason = EnterReason.System)
        {
            for (int i = 0; i < _machines.Count; i++)
            {
                _machines[i].Clear(reason);
            }
        }

        #endregion
    }
}
