using System;

namespace UGS.State
{
    /// <summary>
    /// 能力标识（“能不能做某事”）。Core 不定义任何能力；由项目层声明常量（如 Fight = new AbilityId(2)），
    /// 再通过 <see cref="StateCatalog.RegisterAbility"/> 注册规则。
    /// </summary>
    public readonly struct AbilityId : IEquatable<AbilityId>
    {
        public static readonly AbilityId None = default;

        public readonly int Value;

        public AbilityId(int value)
        {
            Value = value;
        }

        public bool IsNone
        {
            get { return Value == 0; }
        }

        public bool Equals(AbilityId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is AbilityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "Ability" + Value;
        }

        public static bool operator ==(AbilityId left, AbilityId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AbilityId left, AbilityId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>能力规则上下文：Core 只提供通用信息，域通过 <see cref="Domain"/> 携带自己的数据。</summary>
    public readonly struct AbilityContext
    {
        public readonly IStateHost Host;
        public readonly StateMachine Machine;
        public readonly TagSet Tags;
        public readonly object Domain;

        public AbilityContext(IStateHost host, StateMachine machine, TagSet tags, object domain)
        {
            Host = host;
            Machine = machine;
            Tags = tags;
            Domain = domain;
        }
    }

    /// <summary>能力规则：由域注册，Core 提供默认实现。</summary>
    public delegate bool AbilityRule(AbilityContext context);
}
