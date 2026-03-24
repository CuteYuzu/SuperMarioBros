using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using Colour4 = osu.Framework.Graphics.Colour4;

namespace osu.Game.Rulesets.SuperMarioBros.UI
{
    /// <summary>
    /// SuperMarioPPDisplay - 实时显示 PP 数值
    /// 显示在游玩界面左侧中间
    /// </summary>
    public partial class SuperMarioPPDisplay : Container
    {
        private readonly SpriteText movementText;
        private readonly SpriteText readingText;
        private readonly SpriteText precisionText;
        private readonly SpriteText accuracyText;
        private readonly SpriteText totalText;

        public SuperMarioPPDisplay()
        {
            AutoSizeAxes = Axes.Both;
            
            // 确保默认可见
            AlwaysPresent = true;
            
            Add(new FillFlowContainer
            {
                Direction = FillDirection.Vertical,
                AutoSizeAxes = Axes.Both,
                Spacing = new Vector2(0, 3),
                Children = new Drawable[]
                {
                    movementText = new OsuSpriteText
                    {
                        Font = OsuFont.Numeric.With(size: 14),
                        Colour = Colour4.FromHex("#FF6B6B"), // 红色
                        Text = "Loading...",
                    },
                    readingText = new OsuSpriteText
                    {
                        Font = OsuFont.Numeric.With(size: 14),
                        Colour = Colour4.FromHex("#4ECDC4"), // 青色
                        Text = "Loading...",
                    },
                    precisionText = new OsuSpriteText
                    {
                        Font = OsuFont.Numeric.With(size: 14),
                        Colour = Colour4.FromHex("#FFE66D"), // 黄色
                        Text = "Loading...",
                    },
                    accuracyText = new OsuSpriteText
                    {
                        Font = OsuFont.Numeric.With(size: 14),
                        Colour = Colour4.FromHex("#95E1D3"), // 绿色
                        Text = "Loading...",
                    },
                    totalText = new OsuSpriteText
                    {
                        Font = OsuFont.Numeric.With(size: 16),
                        Colour = Colour4.White,
                        Text = "Loading...",
                    },
                },
            });
        }

        public void UpdatePP(double movement, double reading, double precision, double accuracy, double total)
        {
            // 初始时显示最大值（理论PP）
            movementText.Text = $"Movement: {movement:F2} / {movement:F2}pp";
            readingText.Text = $"Reading: {reading:F2} / {reading:F2}pp";
            precisionText.Text = $"Precision: {precision:F2} / {precision:F2}pp";
            accuracyText.Text = $"Accuracy: {accuracy:F2} / {accuracy:F2}pp";
            totalText.Text = $"Total: {total:F2} / {total:F2}pp";
        }

        public void UpdatePPWithCurrent(double movement, double reading, double precision, double accuracy, double total,
            double maxMovement, double maxReading, double maxPrecision, double maxAccuracy, double maxTotal)
        {
            movementText.Text = $"Movement: {movement:F2} / {maxMovement:F2}pp";
            readingText.Text = $"Reading: {reading:F2} / {maxReading:F2}pp";
            precisionText.Text = $"Precision: {precision:F2} / {maxPrecision:F2}pp";
            accuracyText.Text = $"Accuracy: {accuracy:F2} / {maxAccuracy:F2}pp";
            totalText.Text = $"Total: {total:F2} / {maxTotal:F2}pp";
        }
    }
}
