namespace UGS.State
{
    /// <summary>
    /// 跨实体统一查询接口：AI、战斗目标筛选、交互、UI 都只用它，不关心对方是哪种实体。
    /// 能力与标签的具体词表由项目层定义并注册。
    /// </summary>
    public interface IStateQueryable
    {
        bool Can(AbilityId ability);

        /// <summary>是否包含全部给定标签。</summary>
        bool Has(TagSet tag);

        /// <summary>用于展示/日志的主状态：Life 非空取 Life，否则取 Activity。</summary>
        StateId Primary { get; }
    }
}
