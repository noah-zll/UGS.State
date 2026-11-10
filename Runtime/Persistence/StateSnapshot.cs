using System;
using System.Collections.Generic;

namespace UGS.State
{
    /// <summary>单个通道的快照。</summary>
    [Serializable]
    public sealed class ChannelSnapshot
    {
        public int Channel;
        public int[] LaneStates;
        public float[] LaneElapsed;
        public int TargetId;
        public float Value;
    }

    /// <summary>
    /// 状态快照。包的职责是“结构 + 迁移注册”；具体 JSON 序列化由游戏侧完成。
    /// </summary>
    [Serializable]
    public sealed class StateSnapshot
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public ChannelSnapshot[] Channels;
    }

    /// <summary>宿主实现的快照存取（可附带域自定义字段）。</summary>
    public interface IStateSnapshotStore
    {
        StateSnapshot Capture();

        void Restore(StateSnapshot snapshot, RestoreReason reason);
    }

    /// <summary>版本迁移：按 fromVersion 注册升级步骤。</summary>
    public sealed class StateMigrator
    {
        private readonly SortedDictionary<int, Action<StateSnapshot>> _steps =
            new SortedDictionary<int, Action<StateSnapshot>>();

        public StateMigrator Register(int fromVersion, Action<StateSnapshot> step)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            _steps[fromVersion] = step;
            return this;
        }

        public StateSnapshot Migrate(StateSnapshot snapshot, int targetVersion = StateSnapshot.CurrentVersion)
        {
            if (snapshot == null)
            {
                return null;
            }

            int guard = 0;
            while (snapshot.Version < targetVersion)
            {
                Action<StateSnapshot> step;
                if (!_steps.TryGetValue(snapshot.Version, out step))
                {
                    break;
                }

                step(snapshot);
                snapshot.Version++;

                guard++;
                if (guard > 64)
                {
                    throw new StateException("StateMigrator exceeded 64 steps; check for cyclic version registration.");
                }
            }

            return snapshot;
        }
    }
}
