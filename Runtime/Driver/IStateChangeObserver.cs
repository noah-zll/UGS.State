namespace UGS.State
{
    /// <summary>
    /// 可选：宿主希望感知运行时自主发生的状态变更时实现。
    /// <para>
    /// 数据层通常自己写入状态后镜像到运行时；当状态由运行时侧发起（声明式 Transition、
    /// Hold 到期、外部 <c>TryEnter</c>）时，通过本接口把结果回写给数据层，
    /// 使"数据态是事实源、运行时是执行器"的双向保持一致。
    /// </para>
    /// <para>由 <see cref="StateDriver"/> 在行为进入之前回调；实现方需自行防止重入。</para>
    /// </summary>
    public interface IStateChangeObserver
    {
        void OnStateChanged(StateChange change);
    }
}
