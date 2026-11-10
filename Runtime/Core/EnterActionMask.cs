using System;

namespace UGS.State
{
    /// <summary>
    /// 进入状态时需要宿主执行的一次性动作位集合。
    /// Core 不定义任何具体动作；域自行声明常量，例如：
    /// <c>public static readonly EnterActionMask CancelTask = new EnterActionMask(1u &lt;&lt; 0);</c>
    /// </summary>
    public readonly struct EnterActionMask : IEquatable<EnterActionMask>
    {
        public static readonly EnterActionMask None = default;

        public readonly uint Bits;

        public EnterActionMask(uint bits)
        {
            Bits = bits;
        }

        public bool IsEmpty
        {
            get { return Bits == 0u; }
        }

        public bool HasAll(EnterActionMask other)
        {
            return (Bits & other.Bits) == other.Bits;
        }

        public bool HasAny(EnterActionMask other)
        {
            return (Bits & other.Bits) != 0u;
        }

        public static EnterActionMask operator |(EnterActionMask left, EnterActionMask right)
        {
            return new EnterActionMask(left.Bits | right.Bits);
        }

        public static EnterActionMask operator &(EnterActionMask left, EnterActionMask right)
        {
            return new EnterActionMask(left.Bits & right.Bits);
        }

        public static EnterActionMask operator ~(EnterActionMask value)
        {
            return new EnterActionMask(~value.Bits);
        }

        public bool Equals(EnterActionMask other)
        {
            return Bits == other.Bits;
        }

        public override bool Equals(object obj)
        {
            return obj is EnterActionMask other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)Bits;
        }

        public override string ToString()
        {
            return "EnterActions(0x" + Bits.ToString("X8") + ")";
        }

        public static bool operator ==(EnterActionMask left, EnterActionMask right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EnterActionMask left, EnterActionMask right)
        {
            return !left.Equals(right);
        }
    }
}
