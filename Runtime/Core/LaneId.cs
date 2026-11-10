using System;

namespace UGS.State
{
    /// <summary>
    /// lane 标识。核心预置 4 条，域可注册更多（如 Condition / Construction）。
    /// </summary>
    public readonly struct LaneId : IEquatable<LaneId>
    {
        public readonly byte Value;

        public LaneId(byte value)
        {
            Value = value;
        }

        public bool Equals(LaneId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is LaneId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "Lane" + Value;
        }

        public static bool operator ==(LaneId left, LaneId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LaneId left, LaneId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>lane 的静态描述。</summary>
    public readonly struct LaneDef
    {
        public readonly LaneId Id;
        public readonly string Name;
        /// <summary>是否为单值 lane（同 lane 只允许一个状态）。</summary>
        public readonly bool SingleValue;

        public LaneDef(LaneId id, string name, bool singleValue = true)
        {
            Id = id;
            Name = name;
            SingleValue = singleValue;
        }
    }

    /// <summary>核心 lane 词表。</summary>
    public static class CoreLanes
    {
        /// <summary>生存/不可控：Dead / Coma / Collapse / Surrender ……</summary>
        public static readonly LaneId Life = new LaneId(0);
        /// <summary>主动行为：Idle / Sleep / Work / Moving ……</summary>
        public static readonly LaneId Activity = new LaneId(1);
        /// <summary>短时反应：BeAttack / Hurt / Tumble ……</summary>
        public static readonly LaneId Reaction = new LaneId(2);
        /// <summary>意图：Go2Work / Go2Eat / Collect ……</summary>
        public static readonly LaneId Intent = new LaneId(3);
    }
}
