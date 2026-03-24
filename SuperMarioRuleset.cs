using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osuTK.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.IO.Stores;
using osu.Framework.Graphics;
using System.Collections.Generic;
using System;
using System.Linq;
using osu.Game.Rulesets.SuperMarioBros.Mods;
using osu.Game.Rulesets.SuperMarioBros.Replays;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.SuperMarioBros
{
    /// <summary>
    /// SuperMarioRuleset - 马里奥规则集
    /// </summary>
    public class SuperMarioRuleset : Ruleset
    {
        static SuperMarioRuleset()
        {
            Console.WriteLine("[SMB FATAL] Static constructor - Assembly is being touched!");
        }
        
        public override string ShortName => "smb";
        public override string Description => "Super Mario Bros";
        public override string RulesetAPIVersionSupported => "2022.822.0";

        private IResourceStore<byte[]>? resourceStore;

        public SuperMarioRuleset() 
        {
            Console.WriteLine("[SMB FATAL] SuperMarioRuleset Constructor was CALLED.");
        }

        /// <summary>
        /// 创建资源存储
        /// </summary>
        public override IResourceStore<byte[]> CreateResourceStore()
        {
            Console.WriteLine("[SMB FATAL] CreateResourceStore was CALLED.");
            try
            {
                var assembly = typeof(SuperMarioRuleset).Assembly;
                resourceStore = new DllResourceStore(assembly);
                Console.WriteLine("[SMB] ResourceStore created");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMB RULESET] ResourceStore error: {ex.Message}");
            }
            return resourceStore ?? new ResourceStore<byte[]>();
        }

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) 
        {
            Console.WriteLine("[SMB FATAL] CreateBeatmapConverter was CALLED.");
            return new SuperMarioBeatmapConverter(beatmap, this);
        }
        
        public override IBeatmapProcessor CreateBeatmapProcessor(IBeatmap beatmap) => null;
        
        public override ScoreProcessor CreateScoreProcessor() 
        {
            Console.WriteLine("[SMB FATAL] CreateScoreProcessor was CALLED.");
            return new SuperMarioScoreProcessor(this);
        }
        
        public override HealthProcessor CreateHealthProcessor(double drainStartTime) => new DrainingHealthProcessor(drainStartTime);
        
        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null) 
        {
            Console.WriteLine("[SMB FATAL] CreateDrawableRulesetWith was CALLED. If you see this, the ruleset is being activated.");
            try
            {
                var drawableRuleset = new DrawableSuperMarioRuleset(this, beatmap, mods);
                Console.WriteLine("[SMB FATAL] DrawableSuperMarioRuleset instance created successfully.");
                return drawableRuleset;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMB FATAL] CRASH in CreateDrawableRulesetWith: {ex}");
                throw;
            }
        }
        
        public override IEnumerable<Mod> GetModsFor(ModType type)
        {
            switch (type)
            {
                case ModType.DifficultyReduction:
                    return new Mod[]
                    {
                        new SuperMarioModEasy(),
                        new SuperMarioModNoFail(),
                        new SuperMarioModHalfTime(),
                    };
                    
                case ModType.DifficultyIncrease:
                    return new Mod[]
                    {
                        new SuperMarioModHardRock(),
                        new SuperMarioModDoubleTime(),
                        new SuperMarioModNightCore(),
                    };
                    
                case ModType.Fun:
                    return new Mod[]
                    {
                        new SuperMarioModDayCore(),
                    };
                    
                case ModType.Automation:
                    return new Mod[]
                    {
                        new SuperMarioModAutoplay(),
                    };
                    
                default:
                    return System.Array.Empty<Mod>();
            }
        }
        
        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) 
        {
            Console.WriteLine("[SMB FATAL] CreateDifficultyCalculator was CALLED.");
            return new Difficulty.SuperMarioDifficultyCalculator(RulesetInfo, beatmap);
        }
        
        public override PerformanceCalculator CreatePerformanceCalculator() => new SuperMarioPerformanceCalculator();
        
        /// <summary>
        /// 创建可转换的回放帧（用于导入/导出回放）
        /// </summary>
        public override IConvertibleReplayFrame CreateConvertibleReplayFrame() => new SuperMarioReplayFrame();
        
        public override StatisticItem[] CreateStatisticsForScore(ScoreInfo score, IBeatmap playableBeatmap)
        {
            Console.WriteLine($"[SMB] CreateStatisticsForScore called!");
            
            return new[]
            {
                new StatisticItem("Performance Breakdown", () => new PerformanceBreakdownChart(score, playableBeatmap)
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                }),
            };
        }

        public override IEnumerable<HitResult> GetValidHitResults()
        {
            return new[]
            {
                HitResult.Perfect,
                HitResult.Great,
                HitResult.Ok,
                HitResult.Miss
            };
        }
        
        public override IEnumerable<KeyBinding> GetDefaultKeyBindings(int variant = 0) => new[]
        {
            new KeyBinding(InputKey.Z, SuperMarioAction.Jump),
            new KeyBinding(InputKey.X, SuperMarioAction.Dash),
            new KeyBinding(InputKey.Left, SuperMarioAction.MoveLeft),
            new KeyBinding(InputKey.Right, SuperMarioAction.MoveRight),
        };

        public override IEnumerable<RulesetBeatmapAttribute> GetBeatmapAttributesForDisplay(IBeatmapInfo beatmapInfo, IReadOnlyCollection<Mod> mods)
        {
            var originalDifficulty = beatmapInfo.Difficulty;
            var effectiveDifficulty = GetAdjustedDisplayDifficulty(beatmapInfo, mods);
            
            // 计算AR对应的出怪时间
            double ar = effectiveDifficulty.ApproachRate;
            double preemptTime = Math.Max(1.3, 9 - 0.7 * ar);
            
            // 计算OD对应的Spiny像素判定
            double od = effectiveDifficulty.OverallDifficulty;
            double spinySize = 1 + 2.9 * od;  // OD 0 = 1px, OD 10 = 30px

            // AR: Approach Rate - 控制小怪出现时间（AR越大，小怪出现得越快）
            yield return new RulesetBeatmapAttribute("Approach Rate", @"AR", originalDifficulty.ApproachRate, effectiveDifficulty.ApproachRate, 10)
            {
                Description = "控制小怪在屏幕上的出现时间。AR越大，小怪出现得越快。",
                AdditionalMetrics = new[]
                {
                    new RulesetBeatmapAttribute.AdditionalMetric("小怪出现时间", $"{preemptTime:F2} 秒")
                }
            };

            // OD: Overall Difficulty - 控制Spiny的像素间距（OD越大，Spiny间距越小，难度越高）
            yield return new RulesetBeatmapAttribute("Accuracy", @"OD", originalDifficulty.OverallDifficulty, effectiveDifficulty.OverallDifficulty, 10)
            {
                Description = "控制Spiny的判定框大小。OD越大，Spiny的判定框越大，难度越高。",
                AdditionalMetrics = new[]
                {
                    new RulesetBeatmapAttribute.AdditionalMetric("Spiny判定框大小", $"{spinySize:F1} x {spinySize:F1} 像素")
                }
            };

            // HP: Health Drain - 仍然是掉血速度
            yield return new RulesetBeatmapAttribute("HP Drain", @"HP", originalDifficulty.DrainRate, effectiveDifficulty.DrainRate, 10)
            {
                Description = "控制生命值衰减速度。HP越大，掉血越快。"
            };
        }
    }
}
