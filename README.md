# Super Mario Bros Ruleset for osu!lazer

## ⚠️ 当前状态：开发中 - 需要手动修复

### 项目位置
```
D:\.openclaw\workspace\SuperMarioBros\
```

### 已创建的文件

| 文件 | 说明 |
|------|------|
| `osu.Game.Rulesets.SuperMarioBros.csproj` | 项目配置 |
| `SuperMarioRuleset.cs` | 规则集主类（需修复）|
| `Objects/SuperMarioHitObject.cs` | 物件类（完成）|
| `SuperMarioBeatmapConverter.cs` | 谱面转换器（需修复）|
| `SuperMarioScoreProcessor.cs` | HP/计分系统（完成）|
| `SuperMarioDifficultyCalculator.cs` | 难度计算（需修复）|
| `DrawableSuperMarioRuleset.cs` | 渲染层（需修复）|

### 编译错误（29个）

**核心问题**：osu! Game API 2024.517.0 与文档描述差异巨大

#### 关键问题：
1. `BeatmapConverter` - 命名空间 `osu.Game.Beatmaps.Converters` 不存在
2. `DrawableRuleset` - 需要实现 20+ 个抽象成员
3. `DifficultyCalculator` - 方法签名不匹配
4. `IWorkingBeatmap` - 类型不存在

---

## 🔧 修复建议

### 方法1：使用旧版 API（推荐）

尝试使用更稳定的旧版 osu!Game，例如：

```xml
<PackageReference Include="ppy.osu.Game" Version="2022.805.1" />
```

### 方法2：参考官方源码

查看 osu!lazer 源码中的实际 Ruleset 实现：
- https://github.com/ppy/osu/tree/master/osu.Game/Rulesets

### 方法3：Visual Studio 智能提示

1. 用 Visual Studio 打开项目
2. 让它显示错误列表
3. 按照提示实现缺失的抽象成员

---

## 🎮 玩法逻辑（已完成设计）

### osu! → Mario 映射

| osu! 物件 | Mario 元素 | 动作 |
|-----------|-----------|------|
| Hit Circle | Goomba/Koopa | 跳跃踩踏 |
| Hit Circle (高位置) | 问号块 | 头撞 |
| Slider | 藤蔓/管道 | 攀爬 |
| Spinner | Boss 战 | 射击 |

### HP 系统

- 初始：超级马里奥（大）
- 第一次 Miss：变小
- 小马里奥 Miss：Game Over
- 30 连击：掉落蘑菇

---

## 📝 需要的抽象成员（DrawableRuleset）

根据编译错误，需要实现以下成员：

```csharp
public class DrawableSuperMarioRuleset : DrawableRuleset
{
    // 属性
    public override Playfield Playfield { get; }
    public override GameField Camera { get; }
    public override CursorContainer Cursor { get; }
    public override Container Overlays { get; }
    public override IFrameStableClock FrameStableClock { get; }
    public override bool FrameStablePlayback { get; set; }
    public override bool AllowBackwardsSeeks { get; set; }
    public override IAdjustableClock Audio { get; }
    public override double GameplayStartTime { get; }
    public override IEnumerable<DrawableHitObject> Objects { get; }
    public override IReadOnlyList<Mod> Mods { get; }

    // 方法
    public override void CancelResume() { }
    public override void RequestResume(Action continueAction) { }
    public override void SetReplayScore(Score score) { }
    public override void SetRecordTarget(Score score) { }

    // 事件
    public override event Action<JudgementResult> NewResult;
    public override event Action<JudgementResult> RevertResult;
}
```

---

## 🚀 下一步

1. **尝试旧版 API** 或
2. **手动在 VS 中实现所有抽象成员**

---

*创建时间：2026-03-17*
