using System;
using System.Linq;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.SuperMarioBros.Difficulty;
using osu.Game.Rulesets.SuperMarioBros.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.SuperMarioBros
{
    /// <summary>
    /// SuperMarioPerformanceCalculator - PP计算器（osu!风格PP版 + 时空速率补正）
    /// </summary>
    public class SuperMarioPerformanceCalculator : PerformanceCalculator
    {
        // PP计算参数
        private const double P_NORM = 1.1;
        private const double ACCURACY_POWER = 8.0;
        
        public SuperMarioPerformanceCalculator()
            : base(new SuperMarioRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var smbAttributes = (SuperMarioDifficultyAttributes)attributes;
            
            Console.WriteLine($"[SMB PerfCalc] StarRating={smbAttributes.StarRating}, MaxCombo={smbAttributes.MaxCombo}");
            
            // 获取四大维度PP（来自DifficultyCalculator，已包含速度应变和Reaction Buff）
            double movementPP = smbAttributes.MovementPP;
            double readingPP = smbAttributes.ReadingPP;
            double precisionPP = smbAttributes.PrecisionPP;
            double accuracyBasePP = smbAttributes.AccuracyPP;
            
            // 获取OD用于Accuracy PP计算
            double od = 5.0;
            double basePPValue = movementPP + readingPP + precisionPP;
            if (accuracyBasePP > 0 && basePPValue > 0)
            {
                double odScale = accuracyBasePP / basePPValue;
                od = (odScale - 1.0) / 0.1 + 5.0;
            }
            
            // 获取当前准确率（0.0 - 1.0）
            double accuracy = score.Accuracy;
            Console.WriteLine($"[SMB PerfCalc] Accuracy={accuracy:P1}");
            
            // 计算OD缩放
            double odMultiplier = 1.0 + 0.1 * (od - 5.0);
            
            // 计算Accuracy PP: BaseValue * Accuracy^8 * ODScale
            double accuracyPP = basePPValue * Math.Pow(accuracy, ACCURACY_POWER) * odMultiplier;
            
            // 使用1.1次方合成总PP
            double totalPP = CalculateTotalPP(movementPP, readingPP, precisionPP, accuracyPP);
            
            // 获取ClockRate（从ScoreInfo的Mods中）
            double clockRate = 1.0;
            if (score.Mods != null)
            {
                foreach (var mod in score.Mods)
                {
                    if (mod is SuperMarioModDoubleTime dt)
                    {
                        clockRate = dt.SpeedChange.Value;
                        break;
                    }
                    if (mod is SuperMarioModNightCore nc)
                    {
                        clockRate = nc.SpeedChange.Value;
                        break;
                    }
                    if (mod is SuperMarioModHalfTime ht)
                    {
                        clockRate = ht.SpeedChange.Value;
                        break;
                    }
                }
            }
            Console.WriteLine($"[SMB PerfCalc] ClockRate={clockRate:F2}");
            
            // 全局速度加成：ClockRate^1.1（DT/NC时PP直接增加）
            double clockRateBonus = Math.Pow(clockRate, 1.1);
            totalPP *= clockRateBonus;
            Console.WriteLine($"[SMB PerfCalc] ClockRate Bonus={clockRateBonus:F3}");
            
            // 计算物件总数
            int objectCount = smbAttributes.GoombaCount + smbAttributes.KoopaCount + smbAttributes.SpinyCount;
            
            // 应用长度系数（osu!风格，无短图惩罚）
            double lengthBonus = CalculateLengthBonus(objectCount);
            totalPP *= lengthBonus;
            Console.WriteLine($"[SMB PerfCalc] LengthBonus={lengthBonus:F3} for {objectCount} objects");
            
            // 根据Miss调整PP（使用更低的衰减系数）
            int missCount = score.Statistics.ContainsKey(HitResult.Miss) ? score.Statistics[HitResult.Miss] : 0;
            
            if (missCount > 0)
            {
                // 更低的Miss衰减系数（替代短图惩罚）
                double missPenalty = Math.Pow(0.985, missCount);
                totalPP *= missPenalty;
                Console.WriteLine($"[SMB PerfCalc] Miss Penalty={missPenalty:F3} for {missCount} misses");
            }
            
            Console.WriteLine($"[SMB PerfCalc] PP Breakdown: Movement={movementPP:F2}, Reading={readingPP:F2}, Precision={precisionPP:F2}, Accuracy={accuracyPP:F2}");
            Console.WriteLine($"[SMB PerfCalc] Final Total = {totalPP:F2} (OD={od:F1}, Acc={accuracy:P1}, Miss={missCount})");

            return new PerformanceAttributes { Total = totalPP };
        }
        
        /// <summary>
        /// 计算长度系数（osu!风格，无短图惩罚）
        /// </summary>
        private double CalculateLengthBonus(int objectCount)
        {
            if (objectCount <= 0) 
                return 1.0;
            
            // 无短图惩罚 - 标准长度和对数加成
            if (objectCount <= 2000)
            {
                // 标准长度：1.0
                return 1.0;
            }
            else
            {
                // 长图对数加成
                double logBonus = Math.Log(objectCount / 2000.0) * 0.15;
                return 1.0 + logBonus;
            }
        }
        
        /// <summary>
        /// 使用1.1次方合成总PP（osu!风格Minkowski Sum）
        /// </summary>
        private double CalculateTotalPP(double movement, double reading, double precision, double accuracy)
        {
            // TotalPP = (Movement^1.1 + Reading^1.1 + Precision^1.1 + Accuracy^1.1)^(1/1.1)
            double sum = Math.Pow(movement, P_NORM) + 
                         Math.Pow(reading, P_NORM) + 
                         Math.Pow(precision, P_NORM) + 
                         Math.Pow(accuracy, P_NORM);
            
            return Math.Pow(sum, 1.0 / P_NORM);
        }
    }
}
