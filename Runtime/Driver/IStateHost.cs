namespace UGS.State
{
    public enum StateLogLevel : byte
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    /// <summary>
    /// 状态宿主：由数据对象实现（ChessData / SiegeWeaponData / BuildingData ...）。
    /// Core 只通过该接口访问外部世界。
    /// </summary>
    public interface IStateHost
    {
        ushort Domain { get; }

        StateRuntime Runtime { get; }

        bool HasEntity { get; }

        /// <summary>域自定义上下文（如 ChessData 本身），供能力规则使用。</summary>
        object DomainContext { get; }

        /// <summary>实体能力校验（子类裁剪、机型差异等）。所有进入路径都会调用。</summary>
        bool Supports(StateId id);

        /// <summary>执行进入副作用（清目标、退技能、退阵型……）。</summary>
        void ExecuteEnterAction(EnterActionMask action);

        void Log(StateLogLevel level, string message);
    }

    /// <summary>表现层适配：UI 进度、表情、动画、代理开关等全部由实现方翻译。</summary>
    public interface IStatePresenter
    {
        void OnStateChanged(StateChange change);
    }

    /// <summary>可选日志接口，便于宿主转发到项目日志系统。</summary>
    public interface IStateLogger
    {
        void Log(StateLogLevel level, string message);
    }

    /// <summary>空日志实现。</summary>
    public sealed class NullStateLogger : IStateLogger
    {
        public static readonly NullStateLogger Instance = new NullStateLogger();

        private NullStateLogger()
        {
        }

        public void Log(StateLogLevel level, string message)
        {
        }
    }
}
