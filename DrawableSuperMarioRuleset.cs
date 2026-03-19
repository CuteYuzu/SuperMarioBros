using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.SuperMarioBros.UI;
using osu.Game.Rulesets.SuperMarioBros.Objects;
using osu.Game.Rulesets.SuperMarioBros.Difficulty;
using osu.Game.Rulesets.SuperMarioBros.Mods;
using osu.Game.Beatmaps;
using System.Collections.Generic;
using System;
using osu.Framework.Allocation;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.SuperMarioBros
{
    /// <summary>
    /// DrawableSuperMarioRuleset - 可绘制规则集（osu!风格PP版）
    /// </summary>
    public partial class DrawableSuperMarioRuleset : DrawableRuleset<SuperMarioHitObject>
    {
        public MarioCharacter? Mario { get; private set; }
        private SuperMarioInputManager? inputManager;
        private SuperMarioTextureStore? textureStore;
        private SuperMarioPlayfield? playfield;
        
        // 难度属性（四大维度PP）
        private double movementPP = 0;
        private double readingPP = 0;
        private double precisionPP = 0;

        public DrawableSuperMarioRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods)
            : base(ruleset, ConvertBeatmap(beatmap), mods)
        {
            Console.WriteLine("[SMB FATAL] DrawableSuperMarioRuleset Constructor was CALLED.");
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Console.WriteLine("[SMB FATAL] DrawableSuperMarioRuleset load() was CALLED.");
            
            try
            {
                Mario = new MarioCharacter();
                Console.WriteLine("[SMB] Mario created");
                
                textureStore = new SuperMarioTextureStore();
                DrawableSuperMarioHitObject.SetTextureStore(textureStore);
                Console.WriteLine("[SMB] TextureStore ready");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMB] Load error: {ex.Message}");
            }
        }

        private static IBeatmap ConvertBeatmap(IBeatmap beatmap)
        {
            if (beatmap.HitObjects.Count == 0)
                return beatmap;

            var firstObj = beatmap.HitObjects[0];
            if (firstObj is SuperMarioHitObject)
                return beatmap;

            var converted = new Beatmap { BeatmapInfo = beatmap.BeatmapInfo };

            foreach (var obj in beatmap.HitObjects)
            {
                var smbObj = new SuperMarioHitObject { StartTime = obj.StartTime };
                string typeName = obj.GetType().Name;

                if (typeName.Contains("Circle"))
                    smbObj.ObjectType = SuperMarioObjectType.Goomba;
                else if (typeName.Contains("Slider"))
                    smbObj.ObjectType = SuperMarioObjectType.Koopa;
                else if (typeName.Contains("Spinner"))
                    smbObj.ObjectType = SuperMarioObjectType.Goomba;
                else
                    smbObj.ObjectType = SuperMarioObjectType.Goomba;

                smbObj.JudgementType = JudgementType.Stomp;
                converted.HitObjects.Add(smbObj);
            }

            return converted;
        }

        protected override Playfield CreatePlayfield() 
        {
            playfield = new SuperMarioPlayfield();
            return playfield;
        }

        public override DrawableHitObject<SuperMarioHitObject> CreateDrawableRepresentation(SuperMarioHitObject h) 
            => new DrawableSuperMarioHitObject(h);

        protected override RulesetInputManager<SuperMarioAction> CreateInputManager() 
        {
            inputManager = new SuperMarioInputManager(Ruleset.RulesetInfo);
            return inputManager;
        }

        protected override void LoadComplete()
        {
            Console.WriteLine("[SMB CRITICAL] LoadComplete START - This must appear!");
            base.LoadComplete();
            Console.WriteLine("[SMB CRITICAL] LoadComplete AFTER base - This must appear!");
            
            try
            {
                // 检测是否有Auto Mod
                bool isAutoMode = false;
                if (Mods != null)
                {
                    foreach (var mod in Mods)
                    {
                        if (mod is SuperMarioModAutoplay)
                        {
                            isAutoMode = true;
                            Console.WriteLine("[SMB] Auto Mode detected!");
                            break;
                        }
                    }
                }
                
                if (inputManager != null && Mario != null)
                    inputManager.SetMario(Mario);
                
                if (playfield != null && Mario != null)
                {
                    // 设置Auto Mode
                    Mario.IsAutoMode = isAutoMode;
                    playfield.SetMario(Mario);
                    playfield.SetApproachRate(Beatmap.Difficulty.ApproachRate);
                    playfield.SetOverallDifficulty(Beatmap.Difficulty.OverallDifficulty);
                    
                    // 在坐标系就绪后初始化Mario位置 - 使用正确的地面Y坐标
                    Mario.InitializePosition(SuperMarioPlayfield.JUDGMENT_X, SuperMarioPlayfield.GROUND_Y);
                    
                    // 手动计算难度（使用新的osu!风格PP算法）
                    double ar = Beatmap.Difficulty.ApproachRate;
                    double od = Beatmap.Difficulty.OverallDifficulty;
                    
                    // 计算四大维度PP
                    int goombaCount = 0, koopaCount = 0, spinyCount = 0;
                    foreach (var obj in Beatmap.HitObjects)
                    {
                        if (obj is SuperMarioHitObject smbObj)
                        {
                            switch (smbObj.ObjectType)
                            {
                                case SuperMarioObjectType.Goomba: goombaCount++; break;
                                case SuperMarioObjectType.Koopa: koopaCount++; break;
                                case SuperMarioObjectType.Spiny: spinyCount++; break;
                            }
                        }
                        else
                        {
                            goombaCount++;
                        }
                    }
                    
                    // 简化版Movement PP：对数增量
                    movementPP = CalculateMovementPP(goombaCount, koopaCount, spinyCount, ar);
                    
                    // 简化版Reading PP
                    readingPP = CalculateReadingPP(goombaCount, spinyCount);
                    
                    // 简化版Precision PP
                    precisionPP = CalculatePrecisionPP(goombaCount, spinyCount);
                    
                    // OD缩放
                    double odScale = 1.0 + 0.1 * (od - 5.0);
                    double baseValue = movementPP + readingPP + precisionPP;
                    double accuracyPP = baseValue * odScale; // SS时的Accuracy PP
                    
                    // 1.1次方合成总PP
                    double totalPP = CalculateTotalPP(movementPP, readingPP, precisionPP, accuracyPP);
                    
                    double sr = Math.Pow(totalPP, 1.0 / 3.0);
                    
                    var diffAttrs = new SuperMarioDifficultyAttributes
                    {
                        StarRating = sr,
                        MaxPP = totalPP,
                        TotalPP = totalPP,
                        MovementPP = movementPP,
                        ReadingPP = readingPP,
                        PrecisionPP = precisionPP,
                        AccuracyPP = accuracyPP,
                        GoombaCount = goombaCount,
                        KoopaCount = koopaCount,
                        SpinyCount = spinyCount
                    };
                    playfield.SetDifficultyAttributes(diffAttrs);
                    
                    Console.WriteLine($"[SMB] PP Breakdown: Movement={movementPP:F2}, Reading={readingPP:F2}, Precision={precisionPP:F2}, Accuracy={accuracyPP:F2}");
                    Console.WriteLine($"[SMB] TotalPP={totalPP:F2}, SR={sr:F2}");
                }
                
                Console.WriteLine("[SMB] Loading Success!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMB] LoadComplete error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 计算Movement PP（对数增量）
        /// </summary>
        private double CalculateMovementPP(int goombaCount, int koopaCount, int spinyCount, double ar)
        {
            bool useARBuff = ar >= 9.4;
            double arMultiplier = useARBuff ? 1.2 : 1.0;
            
            // 连续Goomba的对数增量: log2(n) - log2(n-1)
            double goombaPP = 0;
            for (int i = 1; i <= goombaCount; i++)
            {
                double increment = i > 1 ? Math.Log2(i) - Math.Log2(i - 1) : 1.0;
                goombaPP += increment * arMultiplier;
            }
            
            // Koopa: n * 0.5
            double koopaPP = koopaCount * 0.5;
            
            // Spiny: n * 1
            double spinyPP = spinyCount * 1.0;
            
            return goombaPP + koopaPP + spinyPP;
        }
        
        /// <summary>
        /// 计算Reading PP（基于Spiny）
        /// </summary>
        private double CalculateReadingPP(int goombaCount, int spinyCount)
        {
            if (spinyCount == 0) return 0;
            
            // Spiny数量越多，Reading PP越高
            double spinyMultiplier = 1.0 + 0.1 * spinyCount;
            
            return spinyCount * 1.0 * spinyMultiplier * 0.5;
        }
        
        /// <summary>
        /// 计算Precision PP（Goomba与Spiny的距离）
        /// </summary>
        private double CalculatePrecisionPP(int goombaCount, int spinyCount)
        {
            if (goombaCount == 0 || spinyCount == 0) return 0;
            
            // 假设Goomba和Spiny均匀分布，平均距离约为总物件数的一半
            double avgDistance = Math.Max(1, (goombaCount + spinyCount) / 2.0);
            double strain = 100.0 / (avgDistance + 34);
            
            return goombaCount * strain * 0.3;
        }
        
        /// <summary>
        /// 使用1.1次方合成总PP
        /// </summary>
        private double CalculateTotalPP(double movement, double reading, double precision, double accuracy)
        {
            double sum = Math.Pow(movement, 1.1) + 
                         Math.Pow(reading, 1.1) + 
                         Math.Pow(precision, 1.1) + 
                         Math.Pow(accuracy, 1.1);
            
            return Math.Pow(sum, 1.0 / 1.1);
        }
    }
}
