using System;

namespace UGS.State
{
    /// <summary>
    /// 跨域唯一的状态 id：(Domain, Local)。
    /// 域内仍可用 enum 定义编号，通过 partial 提供隐式转换。
    /// </summary>
    public readonly struct StateId : IEquatable<StateId>
    {
        public static readonly StateId None = default;

        public readonly ushort Domain;
        public readonly ushort Local;

        public StateId(ushort domain, ushort local)
        {
            Domain = domain;
            Local = local;
        }

        public bool IsNone
        {
            get { return Domain == 0 && Local == 0; }
        }

        /// <summary>打包为 int，便于存档与日志。</summary>
        public int Packed
        {
            get { return (Domain << 16) | Local; }
        }

        public static StateId Unpack(int packed)
        {
            return new StateId((ushort)(packed >> 16), (ushort)(packed & 0xFFFF));
        }

        public bool Equals(StateId other)
        {
            return Domain == other.Domain && Local == other.Local;
        }

        public override bool Equals(object obj)
        {
            return obj is StateId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Packed;
        }

        public override string ToString()
        {
            return IsNone ? "None" : Domain + ":" + Local;
        }

        public static bool operator ==(StateId left, StateId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StateId left, StateId right)
        {
            return !left.Equals(right);
        }
    }
}
