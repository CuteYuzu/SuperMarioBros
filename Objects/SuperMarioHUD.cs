using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.SuperMarioBros.UI;

namespace osu.Game.Rulesets.SuperMarioBros.Objects
{
    /// <summary>
    /// SuperMarioHUD - 游戏界面
    /// </summary>
    public partial class SuperMarioHUD : CompositeDrawable
    {
        public SuperMarioHUD()
        {
            RelativeSizeAxes = Axes.Both;
        }

        public void SetTarget(MarioCharacter? mario, object? scoreProcessor)
        {
        }
    }
}
