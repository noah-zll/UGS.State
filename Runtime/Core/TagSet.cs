using System;

namespace UGS.State
{
    /// <summary>
    /// 128 位标签集合。使用值类型 + 项目层常量而非 enum，保证使用方可自由扩展。
    /// Core 不定义任何业务标签。
    /// </summary>
    public readonly struct TagSet : IEquatable<TagSet>
    {
        public static readonly TagSet None = default;

        private readonly ulong _lo;
        private readonly ulong _hi;

        public TagSet(ulong lo, ulong hi)
        {
            _lo = lo;
            _hi = hi;
        }

        public bool IsEmpty
        {
            get { return _lo == 0UL && _hi == 0UL; }
        }

        /// <summary>是否包含 other 的全部位。</summary>
        public bool HasAll(TagSet other)
        {
            return (_lo & other._lo) == other._lo && (_hi & other._hi) == other._hi;
        }

        /// <summary>是否包含 other 的任意一位。</summary>
        public bool HasAny(TagSet other)
        {
            return (_lo & other._lo) != 0UL || (_hi & other._hi) != 0UL;
        }

        /// <summary>按位号构造（0~127）。</summary>
        public static TagSet FromBit(int bit)
        {
            if (bit < 0 || bit > 127)
            {
                throw new ArgumentOutOfRangeException(nameof(bit), "Tag bit must be in [0,127].");
            }

            return bit < 64
                ? new TagSet(1UL << bit, 0UL)
                : new TagSet(0UL, 1UL << (bit - 64));
        }

        public static TagSet operator |(TagSet left, TagSet right)
        {
            return new TagSet(left._lo | right._lo, left._hi | right._hi);
        }

        public static TagSet operator &(TagSet left, TagSet right)
        {
            return new TagSet(left._lo & right._lo, left._hi & right._hi);
        }

        public static TagSet operator ~(TagSet value)
        {
            return new TagSet(~value._lo, ~value._hi);
        }

        public bool Equals(TagSet other)
        {
            return _lo == other._lo && _hi == other._hi;
        }

        public override bool Equals(object obj)
        {
            return obj is TagSet other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_lo.GetHashCode() * 397) ^ _hi.GetHashCode();
            }
        }

        public override string ToString()
        {
            return "0x" + _hi.ToString("X16") + _lo.ToString("X16");
        }

        public static bool operator ==(TagSet left, TagSet right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TagSet left, TagSet right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 标签分段约定（非强制）：0~31 预留给框架，各域从 32 开始每 32 位划分一段，
    /// 由项目层自行定义常量，Core 不提供任何业务标签。
    /// </summary>
}
