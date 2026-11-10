# Changelog

## Unreleased

- `StateDef.DisplayName` / `StateDefBuilder.DisplayName(string)`：状态可读名称（仅用于展示，不参与判定）
- `StateCatalog.EvaluateAbility(ability, host, TagSet)`：用任意标签集合评估能力，供无运行时的冷数据查询
- `BehaviorScope`（`Shared` / `PerInstance`）：`RegisterBehavior(..., scope)` 可声明逐实例行为；`StateDriver` 在进入时新建、退出时调用 `StateBehavior.Dispose()` 释放
- `StateBehavior` 实现 `IDisposable`；`StateCatalog.CreateBehavior(int)` 提供不缓存的实例创建
- `IStateChangeObserver`：宿主可选实现，运行时自主发生的状态变更（声明式 Transition / Hold 到期 / 外部 TryEnter）回调给数据层，`StateDriver` 在行为进入前触发

## 0.1.0

首个版本：与玩法无关的状态框架核心。

- `StateId` / `LaneId` / `ChannelId` / `ViewId` / `TagSet` / `AbilityId` / `EnterActionMask` / `ViewFlagMask` 值类型
- Core 不含业务词汇：Tag / Ability / 进入动作 / 表现标志均由项目层声明并注册
- `StateDefBuilder` + `StateCatalog`（注册、隐含标签展开、启动期校验、数据驱动 DTO 预留）
- `StateMachine` / `StateRuntime`（lane 互斥、优先级、能力查询、变更事件、变更流水、重入队列）
- `StateBehavior` / `StateContext` / `CompositeBehavior`
- `IStateHost` / `IStatePresenter` / `StateDriver`（Tick 活跃行为、驱动表现层）
- `IStateQueryable`（`Can` / `Has` / `Primary`），跨实体查询只用能力与标签
- `StateSnapshot` / `IStateSnapshotStore` / `StateMigrator`
- EditMode 测试与最小示例
