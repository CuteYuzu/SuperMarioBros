using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osuTK.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.IO.Stores;
using System.Collections.Generic;
using System;
using System.Linq;
using osu.Game.Rulesets.SuperMarioBros.Mods;

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
    }
}
