# Super Mario Bros Ruleset for osu!lazer

<p align="center">
  <img src="https://img.shields.io/badge/status-development-orange?style=flat-square">
  <img src="https://img.shields.io/badge/platform-osu!lazer-blue?style=flat-square">
  <img src="https://img.shields.io/badge/language-C%23-brightgreen?style=flat-square">
</p>

## 📺 演示视频

[![Demo Video](https://img.shields.io/badge/B%E7%AB%99-%E8%A7%86%E9%A2%91%E9%93%BB%E5%8F%82)]([https://www.bilibili.com/video/BVxxx](https://www.bilibili.com/video/BV1YGwkzwEah/))

> 玩法 Demo 示例，使用的是 I can't wait 这张图

---

## 🎮 玩法介绍

类似 **2D 横板强制卷轴** 玩法！

### 操作方式

| 按键 | 动作 |
|------|------|
| ← → | 左右移动 |
| Z | 跳跃 |
| X | 加速/冲刺 |

### 物件系统

| 物件 | 颜色 | 效果 |
|------|------|------|
| **Goomba** (棕色方块) | 棕色 | 碰撞 +1 Combo，漏掉会掉血，但不会 Miss |
| **Koopa** (绿色方块) | 绿色 | 碰撞或经过上方后 +1 Combo |
| **Spiny** (蓝色三角形/尖刺) | 蓝色 | 碰到会 **Miss**，滚出屏幕后 +1 Combo |

### 参数说明

| 参数 | 作用 |
|------|------|
| **Circle Size** | 暂无实际作用 |
| **HP** | 控制掉血速度（同 osu!std） |
| **OD** | 控制 Spiny（蓝色尖刺）的实际判定像素范围 |
| **AR** | 控制强制卷轴速度 |

> **AR 公式**：AR 0 = 9s，每 +0.1 AR，时间 -0.07s，最高 AR 11 = 1.3s

---

## ✨ 已实现功能

### ✅ PP 计算系统 (Beta)
- Movement PP（移动难度）
- Reading PP（阅读谱面难度）
- Precision PP（精确度难度）
- Accuracy PP（准度难度）
- **Reaction Buff**：AR > 10 时获得额外加成
- **速度应变**：时间间隔越短，PP 越高
- **长度系数**：防止长图 PP 爆炸

### ✅ Mod 支持
| Mod | 效果 |
|-----|------|
| **Easy** | 降低难度（AR/OD/HP × 0.5）|
| **No Fail** | 不会死亡 |
| **Half Time** | 慢速（0.75x） |
| **Day Core** | 变暗效果 |
| **Hard Rock** | 增加难度（AR/OD/HP × 1.4）|
| **Double Time** | 加速（1.5x）|
| **Night Core** | 加速 + 音频升调 |
| **Auto** | 自动游玩，Spiny 不会导致 Miss |

---

## 🛠️ 技术细节

### PP 计算公式

```
总 PP = (Movement^1.1 + Reading^1.1 + Precision^1.1 + Accuracy^1.1)^(1/1.1) × ClockRate^1.1
```

### AR ↔ ms 转换
```
AR → ms: 1000 × max(1.3, 9.0 - (ar × 0.7))
```

---

## 📦 下载

请访问 [Releases](https://github.com/CuteYuzu/SuperMarioBros/releases) 页面下载最新的 DLL 文件。

### 安装方法

1. 编译项目或下载 Release 中的 DLL
2. 将 DLL 放入 osu!lazer 的 `rulesets` 文件夹
3. 启动 osu!lazer 即可使用

---

## 🤝 贡献者

- **开发者**：CuteYuzu
- **AI 助手**：OpenClaw (MiniMax 模型)
- **特别感谢**：Gemini

---

## 📋 开发日志

- **2026-03-17**：项目启动，生成基础代码
- **2026-03-18**：物理判定系统重构完成
- **2026-03-19**：PP 计算系统升级为 osu! 风格，新增 Mod 支持和 Auto 模式

---

## ⚠️ 注意事项

本项目仍处于**开发初期**，可能存在 bug，欢迎提交 Issue！

---

*感谢体验！🎮*
