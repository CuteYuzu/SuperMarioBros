using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.SuperMarioBros.Objects;

namespace osu.Game.Rulesets.SuperMarioBros.Beatmaps
{
    /// <summary>
    /// SuperMarioBeatmap - 自定义谱面类，用于显示物件统计信息
    /// </summary>
    public class SuperMarioBeatmap : Beatmap<SuperMarioHitObject>
    {
        public override IEnumerable<BeatmapStatistic> GetStatistics()
        {
            int goombas = HitObjects.Count(h => h.ObjectType == SuperMarioObjectType.Goomba);
            int koopas = HitObjects.Count(h => h.ObjectType == SuperMarioObjectType.Koopa);
            int spinies = HitObjects.Count(h => h.ObjectType == SuperMarioObjectType.Spiny);
            int total = Math.Max(1, goombas + koopas + spinies);

            return new[]
            {
                new BeatmapStatistic
                {
                    Name = "Goombas",
                    Content = goombas.ToString(),
                    CreateIcon = () => new BeatmapStatisticIcon(BeatmapStatisticsIconType.Circles),
                    BarDisplayLength = goombas / (float)total,
                },
                new BeatmapStatistic
                {
                    Name = "Koopas",
                    Content = koopas.ToString(),
                    CreateIcon = () => new BeatmapStatisticIcon(BeatmapStatisticsIconType.Sliders),
                    BarDisplayLength = koopas / (float)total,
                },
                new BeatmapStatistic
                {
                    Name = "Spinies",
                    Content = spinies.ToString(),
                    CreateIcon = () => new BeatmapStatisticIcon(BeatmapStatisticsIconType.Spinners),
                    BarDisplayLength = Math.Min(spinies / 10f, 1),
                }
            };
        }
    }
}
