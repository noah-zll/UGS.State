using System;

namespace UGS.State
{
    /// <summary>
    /// 进入状态时携带的参数。字段含义由域约定：
    /// A/B 通常为坐标或两个标量，Duration 为时长，TargetId 为目标实体，Extra 为复杂负载。
    /// </summary>
    public readonly struct StateArgs
    {
        public static readonly StateArgs Empty = default;

        public readonly float A;
        public readonly float B;
        public readonly int TargetId;
        public readonly int Count;
        public readonly float Duration;
        public readonly object Extra;

        public StateArgs(
            float a = 0f,
            float b = 0f,
            int targetId = 0,
            int count = 0,
            float duration = 0f,
            object extra = null)
        {
            A = a;
            B = b;
            TargetId = targetId;
            Count = count;
            Duration = duration;
            Extra = extra;
        }

        /// <summary>移动到 (x, y)。</summary>
        public static StateArgs Move(float x, float y, int targetId = 0)
        {
            return new StateArgs(x, y, targetId);
        }

        /// <summary>带时长的行为。</summary>
        public static StateArgs Timed(float duration, string text = null)
        {
            return new StateArgs(0f, 0f, 0, 0, duration, text);
        }

        /// <summary>只带目标 id。</summary>
        public static StateArgs Target(int targetId)
        {
            return new StateArgs(0f, 0f, targetId);
        }

        /// <summary>自定义负载。</summary>
        public static StateArgs Custom(object extra)
        {
            return new StateArgs(0f, 0f, 0, 0, 0f, extra);
        }

        public override string ToString()
        {
            return "Args(" + A + "," + B + ",target=" + TargetId + ",count=" + Count + ",dur=" + Duration + ")";
        }
    }
}
