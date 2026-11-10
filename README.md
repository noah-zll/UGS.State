# UGS.State

一个与玩法无关的实体状态框架：数据驱动的状态定义、正交 lane、Tag/Ability 查询、可插拔行为与表现层。

- 纯 C#（`noEngineReferences: true`），不依赖 UnityEngine / GameFramework / 第三方库
- **Core 不含任何业务词汇**：Tag、Ability、进入动作、表现标志都由项目层声明并注册
- 扩展方式：域包注册状态定义、标签、能力、行为、表现；**Core 不需要改动**
- 复用对象：棋子、骷髅兵、巨型棋子、攻城器械、建筑、阵法……

## 安装

在工程的 `Packages/manifest.json` 中加入：

```json
"com.ugs.state": "file:D:/Projects/UGF/UGS.State"
```

## 职责边界

| 层 | 内容 | 例子 |
| --- | --- | --- |
| UGS.State（本包） | 机制：`StateId` / `LaneId` / `TagSet` / `AbilityId` / `EnterActionMask` / `ViewFlagMask` / `StateDef` / `StateCatalog` / `StateMachine` / `StateRuntime` / `StateBehavior` / `StateDriver` / 快照 | 不含 `Fight`、`CannotSleep`、`CancelTask`、`Chess` 等业务常量 |
| 项目层（游戏工程） | 词汇与规则：Tag/Ability/EnterAction/ViewFlag 常量、能力规则、隐含标签展开、状态表、行为实现、表现适配 | `GameTags.CannotFight`、`GameAbilities.Fight`、`GameEnterActions.CancelTask`、`ChessStateCatalog` |

建议把项目层词汇集中到 `Assets/scripts/StateSystem/GameStateVocabulary.cs`，所有实体域共用，保证跨实体查询一致。

## 五分钟接入

```csharp
// 1) 项目层词汇（只声明一次，所有实体域共用）
public static class GameTags
{
    public static readonly TagSet Incapacitated = TagSet.FromBit(0);
    public static readonly TagSet CannotFight   = TagSet.FromBit(1);
    public static readonly TagSet Workable      = TagSet.FromBit(4);
    public static readonly TagSet BlocksAI      = TagSet.FromBit(6);
    // …… Chess 域从 32 开始，Building 从 64 开始
}

public static class GameAbilities
{
    public static readonly AbilityId LiveAI = new AbilityId(1);
    public static readonly AbilityId Fight  = new AbilityId(2);
    public static readonly AbilityId Work   = new AbilityId(6);
}

public static class GameEnterActions
{
    public static readonly EnterActionMask CancelTask = new EnterActionMask(1u << 0);
    public static readonly EnterActionMask ClearTarget = new EnterActionMask(1u << 1);
}

// 2) 域内 id（数值只追加）
public enum ChessStateId : ushort
{
    Alive = 100, Dead = 110,
    Idle = 200, Work = 230,
    Hurt = 400,
    Go2Work = 500,
}

// 3) 状态表（域包）
public static class ChessStateCatalog
{
    public const ushort Domain = 1;
    public static readonly StateId Alive = new StateId(Domain, (ushort)ChessStateId.Alive);
    public static readonly StateId Dead  = new StateId(Domain, (ushort)ChessStateId.Dead);

    public static StateCatalog Build()
    {
        var catalog = new StateCatalog(Domain, "Chess");
        catalog.SetAliveState(Alive);
        catalog.SetIdleState(new StateId(Domain, (ushort)ChessStateId.Idle));

        // 项目层规则
        catalog.ExpandTag(GameTags.Incapacitated,
            GameTags.CannotFight | GameTags.BlocksAI);
        catalog.RegisterAbility(GameAbilities.Fight,
            (AbilityContext c) => !c.Tags.HasAny(GameTags.CannotFight));
        catalog.RegisterAbility(GameAbilities.Work,
            (AbilityContext c) => c.Tags.HasAny(GameTags.Workable));

        catalog.Register(StateDefBuilder.Create(Alive, CoreLanes.Life).Build());
        catalog.Register(StateDefBuilder.Create(Dead, CoreLanes.Life)
            .Tags(GameTags.Incapacitated)
            .EnterActions(GameEnterActions.CancelTask | GameEnterActions.ClearTarget)
            .Build());
        catalog.Register(StateDefBuilder
            .Create(new StateId(Domain, (ushort)ChessStateId.Idle), CoreLanes.Activity)
            .Tags(GameTags.Workable)
            .Build());
        catalog.Register(StateDefBuilder
            .Create(new StateId(Domain, (ushort)ChessStateId.Work), CoreLanes.Activity)
            .Tags(GameTags.BlocksAI)
            .Build());
        // ……其余状态
        return catalog;
    }
}

// 4) 宿主（数据对象实现）
public partial class ChessData : IStateHost, IStateQueryable
{
    public ushort Domain => ChessStateCatalog.Domain;
    public StateRuntime Runtime { get; private set; }
    public bool HasEntity => HasEntity;
    public object DomainContext => this;
    public bool Supports(StateId id) => true;
    public void ExecuteEnterAction(EnterActionMask action) { /* ReturnTask / QuitFormation ... */ }
    public void Log(StateLogLevel level, string message) { /* 转发项目日志 */ }

    public void InitState()
    {
        Runtime = new StateRuntime(ChessStateCatalog.Build(), this);
    }

    public bool Can(AbilityId ability) => Runtime.Default.Can(ability);
    public bool Has(TagSet tag) => Runtime.Default.Has(tag);
    public StateId Primary => Runtime.Default.Get(CoreLanes.Life).IsNone
        ? Runtime.Default.Get(CoreLanes.Activity)
        : Runtime.Default.Get(CoreLanes.Life);
}

// 5) 进入 / 查询 / 驱动
chessData.Runtime.Default.TryEnter(
    new StateId(1, (ushort)ChessStateId.Work),
    StateArgs.Timed(3f),
    EnterReason.Task);

bool canFight = chessData.Can(GameAbilities.Fight);
bool blocked  = chessData.Has(GameTags.BlocksAI);

var driver = new StateDriver(chessData, new ChessStatePresenter(chessEntity));
driver.Tick(Time.deltaTime);
```

## 扩展点

| 新增 | 做法 | 是否改包 |
| --- | --- | --- |
| 状态 | 域内 id + `catalog.Register(...)` | 否 |
| 标签 | 项目层 `TagSet.FromBit(域起始位 + n)` | 否 |
| 能力 | 项目层 `AbilityId` + `catalog.RegisterAbility(...)` | 否 |
| 进入动作 / 表现标志 | 项目层 `EnterActionMask` / `ViewFlagMask` 常量 | 否 |
| 行为 | 继承 `StateBehavior` + `catalog.RegisterBehavior(...)` | 否 |
| 表现 | `StateViewDef` + `IStatePresenter` 映射 | 否 |
| lane / 通道 | `catalog.RegisterLane(...)` / `runtime.Get(channel)` | 否 |
| 转换条件 | `StateDefBuilder.Transition(...)` / `IStateTransitionPolicy` | 否 |

## 设计约束

- `Runtime/UGS.State.Runtime.asmdef` 的 `references` 必须为空；
- 包内不出现 `UnityEngine` / 项目类型 / 第三方序列化库；
- 包内不出现任何业务词汇（Tag / Ability / 动作 / 表现标志），只保留值类型与注册机制；
- 行为只通过 `StateContext` / `IStateHost` 访问外部世界；
- 所有“会增长”的枚举式概念（Tag/Ability/Lane/Channel/View）都用值类型 + 注册，不使用 C# enum。

## 语义约定

- 配置了 `SetAliveState` 后，进入非存活的 Life 状态（Dead/Coma/Collapse…）会自动退出 Activity / Reaction / Intent，避免“死亡但仍在工作/受击”的残留。
- 同 lane 低优先级状态不能覆盖高优先级状态；`force: true` 可强行进入（读档、剧情、系统级重置）。
- 受击等 Reaction 与 Activity 并行存在；`Hold` 到期后若 `ReturnTo`/`AutoNext` 指向其它 lane，会先退出 Reaction 再进入目标，不会残留。
- `TryEnter` 在事件回调中调用会自动排队（`TryPostEnter` 同义），避免重入破坏状态。
- `StateDef.Restore` 控制读档策略：`Drop` / `ResetToIdle` / `Resume`（保留时长）/ `Reenter`（重置时长）。

## 实现说明（与设计文档的差异）

- `StateDef` 使用不可变类 + `StateDefBuilder` 构建（文档中为结构体示意），避免长参数构造。
- `IStateCapability` 合并为 `IStateHost.Supports(StateId)`，保证所有进入路径都会校验实体能力。
- `IStateTransitionPolicy` 使用按值传参的 `TransitionRequest`（便于 Unity 2021 的 C# 9 编译）。
- 业务词汇（原 `CoreTags` / `CoreAbilities` / `StateEnterAction` / `StateViewFlags`）已全部移出包，改由项目层声明。

## 测试

`Tests/EditMode` 为纯 C# NUnit 测试，覆盖：

- lane 互斥 / 跨 lane 并存 / 优先级 / Life 拦截 Activity 并自动清理其他 lane
- 目录校验（Intent 缺 OnArrive、Reaction 无回收、缺行为/表现、悬空转换）
- 非 Chess 的扩展样例（自定义 lane / tag / ability / behavior）
- 快照捕获与恢复、版本迁移
- 驱动生命周期（OnEnter/OnTick/OnExit、Hold 到期自动退出）

## 文档

完整设计见 `GameDev/Doc/UGS_State_状态框架包设计方案.md` 与 `GameDev/Doc/棋子状态系统重构设计方案.md`。
