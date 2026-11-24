using System;

namespace UGS.State
{
    /// <summary>
    /// 行为插件基类。
    /// <para>默认推荐实现为无状态单例（<see cref="BehaviorScope.Shared"/>），跨帧数据放 <see cref="StateContext.Data"/>。</para>
    /// <para>需要持有实体私有字段时注册为 <see cref="BehaviorScope.PerInstance"/>：框架会为每次进入新建实例，
    /// 并在退出时调用 <see cref="Dispose"/>。</para>
    /// </summary>
    public abstract class StateBehavior : IDisposable
    {
        public virtual bool CanEnter(StateContext context)
        {
            return true;
        }

        public virtual void OnEnter(StateContext context)
        {
        }

        public virtual void OnTick(StateContext context, float deltaTime)
        {
        }

        public virtual void OnExit(StateContext context)
        {
        }

        /// <summary>释放逐实例行为持有的资源。共享单例不会被调用。</summary>
        public virtual void Dispose()
        {
        }
    }

    /// <summary>行为上下文：通过宿主接口访问外部世界，不直接依赖具体实体类型。</summary>
    public readonly struct StateContext
    {
        public readonly IStateHost Host;
        public readonly StateMachine Machine;
        public readonly StateInstance Instance;
        /// <summary>true 表示本次 OnExit 来自驱动销毁（实体隐藏 / 卸载），而不是真实的状态切换。</summary>
        public readonly bool IsDisposing;

        public StateContext(IStateHost host, StateMachine machine, StateInstance instance, bool isDisposing = false)
        {
            Host = host;
            Machine = machine;
            Instance = instance;
            IsDisposing = isDisposing;
        }

        public StateId Id
        {
            get { return Instance != null ? Instance.Id : StateId.None; }
        }

        public StateArgs Args
        {
            get { return Instance != null ? Instance.Args : StateArgs.Empty; }
        }

        public float Elapsed
        {
            get { return Instance != null ? Instance.Elapsed : 0f; }
        }

        /// <summary>行为自用数据，等价于 Instance.Data。</summary>
        public object Data
        {
            get { return Instance != null ? Instance.Data : null; }
            set
            {
                if (Instance != null)
                {
                    Instance.Data = value;
                }
            }
        }
    }
}
