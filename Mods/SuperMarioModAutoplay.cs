using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.SuperMarioBros.Replays;
using System.Collections.Generic;
using osu.Framework.Localisation;

namespace osu.Game.Rulesets.SuperMarioBros.Mods
{
    /// <summary>
    /// Auto Mod - 自动完成游戏
    /// </summary>
    public class SuperMarioModAutoplay : ModAutoplay
    {
        public override string Name => "Auto";
        public override string Acronym => "AT";
        public override ModType Type => ModType.Automation;
        public override double ScoreMultiplier => 1;
        public override LocalisableString Description => "观看自动完成游戏的过程~";
        
        public override ModReplayData CreateReplayData(IBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            return new ModReplayData(new SuperMarioAutoGenerator(beatmap, mods).Generate(), new ModCreatedUser { Username = "Auto" });
        }
    }
}
