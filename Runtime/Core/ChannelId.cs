using System;

namespace UGS.State
{
    /// <summary>
    /// 状态机通道。一个宿主可持有多个通道（行为 / 交互 / 动画），默认使用 <see cref="Default"/>。
    /// </summary>
    public readonly struct ChannelId : IEquatable<ChannelId>
    {
        public static readonly ChannelId Default = new ChannelId(0);

        public readonly byte Value;

        public ChannelId(byte value)
        {
            Value = value;
        }

        public bool Equals(ChannelId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ChannelId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "Channel" + Value;
        }

        public static bool operator ==(ChannelId left, ChannelId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChannelId left, ChannelId right)
        {
            return !left.Equals(right);
        }
    }
}
