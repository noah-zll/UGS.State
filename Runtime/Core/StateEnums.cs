namespace UGS.State
{
    /// <summary>进入原因：用于日志、优先级判断与调试。</summary>
    public enum EnterReason : byte
    {
        System = 0,
        Player = 1,
        Task = 2,
        AI = 3,
        Rule = 4,
        Damage = 5,
        Timer = 6,
        Restore = 7,
    }

    /// <summary>变更类型。</summary>
    public enum StateChangeKind : byte
    {
        /// <summary>进入一个原本为空的 lane。</summary>
        Entered = 0,
        /// <summary>替换同 lane 的旧状态。</summary>
        Replaced = 1,
        /// <summary>退出 lane。</summary>
        Exited = 2,
        /// <summary>被策略/前置条件拒绝。</summary>
        Rejected = 3,
    }

    /// <summary>恢复原因。</summary>
    public enum RestoreReason : byte
    {
        Load = 0,
        Respawn = 1,
        Rebuild = 2,
        Manual = 3,
    }

    /// <summary>恢复策略：决定读档/重建实体时如何处理该状态。</summary>
    public enum RestorePolicy : byte
    {
        /// <summary>放弃该状态。</summary>
        Drop = 0,
        /// <summary>恢复为 Idle。</summary>
        ResetToIdle = 1,
        /// <summary>原样恢复并保留已进行时长。</summary>
        Resume = 2,
        /// <summary>重新进入（不保留时长）。</summary>
        Reenter = 3,
    }
}
