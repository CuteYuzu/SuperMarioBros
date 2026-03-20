using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Input.Bindings;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using osu.Game.Rulesets.SuperMarioBros;
using osu.Game.Rulesets.UI;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using Colour4 = osu.Framework.Graphics.Colour4;

namespace osu.Game.Rulesets.SuperMarioBros.UI
{
    /// <summary>
    /// SuperMarioBros 按键显示 - 独立实现的按键框
    /// 显示 Z/X/←/→ 四个按键、按下状态和按键次数
    /// </summary>
    public partial class SuperMarioKeyCounterDisplay : Container
    {
        /// <summary>
        /// 按键框是否始终可见
        /// </summary>
        public bool AlwaysVisible { get; set; } = true;

        private readonly Dictionary<SuperMarioAction, SuperMarioKeyVisual> keyVisuals = new();
        
        private readonly FillFlowContainer<SuperMarioKeyVisual> keyFlow;

        public SuperMarioKeyCounterDisplay()
        {
            AutoSizeAxes = Axes.Both;
            
            Add(keyFlow = new FillFlowContainer<SuperMarioKeyVisual>
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Spacing = new Vector2(5, 2),
                Direction = FillDirection.Vertical,
                AutoSizeAxes = Axes.Both,
            });

            // 创建四个按键的视觉显示
            CreateKeyVisuals();
        }

        private void CreateKeyVisuals()
        {
            // 定义四个按键
            var keyConfigs = new[]
            {
                (SuperMarioAction.Jump, "Z"),
                (SuperMarioAction.Dash, "X"),
                (SuperMarioAction.MoveLeft, "←"),
                (SuperMarioAction.MoveRight, "→"),
            };

            foreach (var (action, label) in keyConfigs)
            {
                var visual = new SuperMarioKeyVisual
                {
                    KeyLabel = label,
                };
                keyVisuals[action] = visual;
                keyFlow.Add(visual);
            }
        }

        /// <summary>
        /// 更新按键状态和计数
        /// </summary>
        public void UpdateKeyState(SuperMarioAction action, bool isPressed)
        {
            if (keyVisuals.TryGetValue(action, out var visual))
            {
                visual.IsPressed = isPressed;
                if (isPressed)
                {
                    visual.IncrementCount();
                }
            }
        }
    }

    /// <summary>
    /// 单个按键的视觉显示
    /// </summary>
    public partial class SuperMarioKeyVisual : Container
    {
        private readonly Box background;
        private readonly OsuSpriteText keyLabel;
        private readonly OsuSpriteText countLabel;
        
        private static readonly Colour4 pressed_color = Colour4.FromHex("#ffde00");
        private static readonly Colour4 unpressed_color = Colour4.FromHex("#444444");
        private static readonly Colour4 text_color = Colour4.White;
        private static readonly Colour4 count_color = Colour4.FromHex("#888888");
        
        private int pressCount;
        
        public string KeyLabel
        {
            get => keyLabel.Text.ToString();
            set => keyLabel.Text = value;
        }

        private bool isPressed;
        
        public bool IsPressed
        {
            get => isPressed;
            set
            {
                if (isPressed == value) return;
                isPressed = value;
                
                background.Colour = value ? pressed_color : unpressed_color;
            }
        }

        public void IncrementCount()
        {
            pressCount++;
            countLabel.Text = pressCount.ToString();
        }

        public SuperMarioKeyVisual()
        {
            Size = new Vector2(50, 40);
            CornerRadius = 5;
            
            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = unpressed_color,
                },
                keyLabel = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Y = -6,
                    Font = OsuFont.Numeric.With(size: 18),
                    Colour = text_color,
                    Text = "?",
                },
                countLabel = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Y = 8,
                    Font = OsuFont.Numeric.With(size: 12),
                    Colour = count_color,
                    Text = "0",
                },
            };
        }
    }
}
