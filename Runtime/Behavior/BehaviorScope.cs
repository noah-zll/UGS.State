namespace UGS.State
{
    /// <summary>
    /// 行为实例的生命周期范围。
    /// <para><see cref="Shared"/>：全局单例，必须无状态，跨帧数据放 <see cref="StateContext.Data"/>。</para>
    /// <para><see cref="PerInstance"/>：每个状态实例一份，进入时创建、退出时释放，可持有实体私有的字段。</para>
    /// </summary>
    public enum BehaviorScope
    {
        Shared = 0,
        PerInstance = 1,
    }
}
