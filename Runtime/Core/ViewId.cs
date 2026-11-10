using System;

namespace UGS.State
{
    /// <summary>表现定义 id。0 表示无表现描述。</summary>
    public readonly struct ViewId : IEquatable<ViewId>
    {
        public static readonly ViewId None = default;

        public readonly int Value;

        public ViewId(int value)
        {
            Value = value;
        }

        public bool IsNone
        {
            get { return Value == 0; }
        }

        public bool Equals(ViewId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ViewId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return IsNone ? "ViewNone" : "View" + Value;
        }

        public static bool operator ==(ViewId left, ViewId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ViewId left, ViewId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>进度来源。</summary>
    public enum ProgressMode : byte
    {
        None = 0,
        Elapsed = 1,
        Remaining = 2,
        Value = 3,
    }

    /// <summary>
    /// 表现标志位集合。Core 不定义具体标志，由项目层声明常量（如 HideWeapon = new ViewFlagMask(1 &lt;&lt; 3)）。
    /// </summary>
    public readonly struct ViewFlagMask : IEquatable<ViewFlagMask>
    {
        public static readonly ViewFlagMask None = default;

        public readonly ushort Bits;

        public ViewFlagMask(ushort bits)
        {
            Bits = bits;
        }

        public bool IsEmpty
        {
            get { return Bits == 0; }
        }

        public bool HasAll(ViewFlagMask other)
        {
            return (Bits & other.Bits) == other.Bits;
        }

        public bool HasAny(ViewFlagMask other)
        {
            return (Bits & other.Bits) != 0;
        }

        public static ViewFlagMask operator |(ViewFlagMask left, ViewFlagMask right)
        {
            return new ViewFlagMask((ushort)(left.Bits | right.Bits));
        }

        public static ViewFlagMask operator &(ViewFlagMask left, ViewFlagMask right)
        {
            return new ViewFlagMask((ushort)(left.Bits & right.Bits));
        }

        public bool Equals(ViewFlagMask other)
        {
            return Bits == other.Bits;
        }

        public override bool Equals(object obj)
        {
            return obj is ViewFlagMask other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Bits;
        }

        public override string ToString()
        {
            return "ViewFlags(0x" + Bits.ToString("X4") + ")";
        }

        public static bool operator ==(ViewFlagMask left, ViewFlagMask right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ViewFlagMask left, ViewFlagMask right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 表现描述：Core 只描述“要什么”，Presenter 决定“怎么显示”。
    /// Title / AnimKey / IconKey 都是 key，不是资源路径。
    /// </summary>
    public sealed class StateViewDef
    {
        public readonly ViewId Id;
        public readonly string Title;
        public readonly ProgressMode Progress;
        public readonly string TextFormat;
        public readonly string AnimKey;
        public readonly int IconKey;
        public readonly ViewFlagMask Flags;

        public StateViewDef(
            ViewId id,
            string title,
            ProgressMode progress = ProgressMode.None,
            string textFormat = null,
            string animKey = null,
            int iconKey = 0,
            ViewFlagMask flags = default(ViewFlagMask))
        {
            Id = id;
            Title = title;
            Progress = progress;
            TextFormat = textFormat;
            AnimKey = animKey;
            IconKey = iconKey;
            Flags = flags;
        }
    }
}
