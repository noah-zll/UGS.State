# MinimalActor

纯 C# 示例：不依赖 MonoBehaviour，演示“新增一个状态 = 一条定义（+ 可选一个行为）”。

把本目录复制到工程 `Assets` 下即可运行（需先安装 `com.ugs.state`）。

```csharp
MinimalActorDemo.Run();
```

示例覆盖：

- 自定义域 id（`StateId`）
- lane：Activity / Reaction / Intent
- Tag 声明与隐含标签展开（`Incapacitated`）
- 行为生命周期（`OnEnter / OnTick / OnExit`）
- 快照捕获与恢复
