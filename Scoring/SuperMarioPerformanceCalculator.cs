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
    /// SuperMarioPerformanceCalculator - PP计算器（第四十四轮改进：osu!std Accuracy PP公式）
    /// </summary>
    public class SuperMarioPerformanceCalculator : PerformanceCalculator
    {
        // PP计算参数
        private const double P_NORM = 1.1;
        
        // ===== osu!std Accuracy PP参数 =====
        private const double ACCURACY_OD_BASE = 1.52163;
        private const double ACCURACY_POWER = 24.0;
        private const double ACCURACY_BASE = 2.83;
        
        // ===== 第四十二轮新增：Miss惩罚参数 =====
        private const double MISS_BASE = 0.96;
        private const double MISS_POWER = 1.2;
        private const double LENGTH_DIVISOR_POWER = 0.25;
        
        // 标准物件数量（用于长度系数）
        private const double STANDARD_OBJECT_COUNT = 1500.0;
        
        public SuperMarioPerformanceCalculator()
            : base(new SuperMarioRuleset())
        {
        }

        protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
        {
            var smbAttributes = (SuperMarioDifficultyAttributes)attributes;
            
            Console.WriteLine($"[SMB PerfCalc] StarRating={smbAttributes.StarRating}, MaxCombo={smbAttributes.MaxCombo}");
            
            // 获取四大维度PP（来自DifficultyCalculator，已包含Section峰值加权）
            double movementPP = smbAttributes.MovementPP;
            double readingPP = smbAttributes.ReadingPP;
            double precisionPP = smbAttributes.PrecisionPP;
            
            // ===== 第四十四轮改进（简化版）：直接使用osu!原生accuracy =====
            // osu!的accuracy已经包含了所有物件（Goomba+Koopa+Spiny）
            // Koopa通过X坐标自动判定，不需要特殊处理
            double od = smbAttributes.OverallDifficulty > 0 ? smbAttributes.OverallDifficulty : 5.0;
            int totalObjects = smbAttributes.TimingObjectCount;
            
            // 直接使用osu!计算的accuracy（0.0-1.0）
            double accuracy = score.Accuracy;
            Console.WriteLine($"[SMB PerfCalc] Accuracy={accuracy:P1}, TotalObjects={totalObjects}");
            
            // osu!std Accuracy PP公式: 1.52163^OD * accuracy^24 * 2.83
            // 注意：accuracy低于90%时，此值会趋近于0
            double accuracyValue = Math.Pow(ACCURACY_OD_BASE, od) * Math.Pow(accuracy, ACCURACY_POWER) * ACCURACY_BASE;
            
            // 长图加成（基于总物件数）
            double accuracyLengthBonus = Math.Min(1.15, Math.Pow(totalObjects / 1000.0, 0.3));
            accuracyValue *= accuracyLengthBonus;
            
            Console.WriteLine($"[SMB PerfCalc] AccuracyPP (osu!std): {accuracyValue:F2} = {ACCURACY_OD_BASE:F5}^{od:F1} * {accuracy:P1}^{ACCURACY_POWER:F0} * {ACCURACY_BASE:F2} * {accuracyLengthBonus:F3}");
            
            // 合成总PP（含Accuracy）
            double totalPP = CalculateTotalPP(movementPP, readingPP, precisionPP, accuracyValue);
            
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
            
            // 计算物件总数（用于长度系数）
            int objectCount = smbAttributes.GoombaCount + smbAttributes.KoopaCount + smbAttributes.SpinyCount;
            
            // ===== 第四十二轮改进：应用新的长度系数 =====
            // 使用总物件数作为长度系数计算的基础
            double lengthBonus = CalculateLengthBonus(objectCount);
            
            // ===== 第四十二轮改进：获取Intensity Bonus（DT密度溢价）=====
            // 如果DifficultyAttributes中有IntensityBonus，使用它；否则计算
            double intensityBonus = smbAttributes.IntensityBonus > 0 ? smbAttributes.IntensityBonus : 1.0;
            
            // 如果有DT mod但IntensityBonus未计算，需要重新计算
            if (clockRate > 1.0 && intensityBonus == 1.0)
            {
                // 估算物件密度（基于总时长）
                double durationSeconds = smbAttributes.MaxCombo > 0 ? (smbAttributes.MaxCombo * 1000.0 / (clockRate * 2.0)) : 60.0; // 简化估算
                double density = objectCount / Math.Max(1, durationSeconds);
                
                // 计算Intensity Bonus
                const double DENSITY_THRESHOLD = 3.0;
                const double DENSITY_SCALE = 5.0;
                const double DENSITY_BONUS_MAX = 0.15;
                
                if (density > DENSITY_THRESHOLD)
                {
                    double densityExcess = (density - DENSITY_THRESHOLD) / DENSITY_SCALE;
                    intensityBonus = 1.0 + DENSITY_BONUS_MAX * Math.Min(1.0, densityExcess);
                }
            }
            
            totalPP *= lengthBonus * intensityBonus;
            Console.WriteLine($"[SMB PerfCalc] LengthBonus={lengthBonus:F3}, IntensityBonus={intensityBonus:F3} for {objectCount} objects");
            
            // ===== 第四十二轮改进：动态Miss惩罚 =====
            // 使用新公式：0.96^(miss^1.2 / (totalHits/1000)^0.25)
            int missCount = score.Statistics.ContainsKey(HitResult.Miss) ? score.Statistics[HitResult.Miss] : 0;
            
            if (missCount > 0)
            {
                double missPenalty = CalculateMissPenalty(missCount, objectCount);
                totalPP *= missPenalty;
                Console.WriteLine($"[SMB PerfCalc] Miss Penalty={missPenalty:F4} for {missCount} misses");
            }
            
            Console.WriteLine($"[SMB PerfCalc] PP Breakdown: Movement={movementPP:F2}, Reading={readingPP:F2}, Precision={precisionPP:F2}, Accuracy={accuracyValue:F2}");
            Console.WriteLine($"[SMB PerfCalc] Final Total = {totalPP:F2} (OD={od:F1}, Acc={accuracy:P1}, Miss={missCount})");

            // ===== 第四十四轮新增：返回详细PP Breakdown =====
            // 获取MaxPP（理论最高PP，从DifficultyAttributes）
            double maxPP = smbAttributes.MaxPP;
            
            Console.WriteLine($"[SMB PerfCalc] Returning SuperMarioPerformanceAttribute:");
            Console.WriteLine($"[SMB]   Movement={movementPP:F2}, Reading={readingPP:F2}, Precision={precisionPP:F2}, Accuracy={accuracyValue:F2}");
            Console.WriteLine($"[SMB]   Total={totalPP:F2}, MaxPP={maxPP:F2}");
            
            var result = new SuperMarioPerformanceAttribute
            {
                Movement = movementPP,
                Reading = readingPP,
                Precision = precisionPP,
                Accuracy = accuracyValue,
                AchievedPP = totalPP,
                Maximum = maxPP > 0 ? maxPP : totalPP,
                Total = totalPP
            };
            
            Console.WriteLine($"[SMB PerfCalc] Created SuperMarioPerformanceAttribute successfully!");
            
            return result;
        }
        
        /// <summary>
        /// ===== 第四十二轮改进：计算长度系数 =====
        /// 参考osu!风格：短图惩罚，标准长度正常，长图对数加成
        /// </summary>
        private double CalculateLengthBonus(int objectCount)
        {
            if (objectCount <= 0) 
                return 1.0;
            
            if (objectCount < 500)
            {
                // 短图惩罚
                return 0.95 + 0.4 * Math.Min(1.0, objectCount / STANDARD_OBJECT_COUNT);
            }
            else if (objectCount <= (int)STANDARD_OBJECT_COUNT)
            {
                // 标准长度
                return 0.95 + 0.4 * Math.Min(1.0, objectCount / STANDARD_OBJECT_COUNT);
            }
            else
            {
                // 长图对数加成（封顶1.5）
                double logBonus = 0.1 * Math.Log10(objectCount / STANDARD_OBJECT_COUNT);
                double lengthBonus = 1.35 + logBonus;
                return Math.Min(1.5, lengthBonus);
            }
        }
        
        /// <summary>
        /// ===== 第四十二轮新增：计算动态Miss惩罚 =====
        /// 使用非线性公式，长图容错率高但怕断连，短图惩罚重
        /// </summary>
        private double CalculateMissPenalty(double missCount, double totalHits)
        {
            if (missCount <= 0)
                return 1.0;
            
            if (totalHits <= 0)
                totalHits = 1;
            
            // 公式: 0.96^(miss^1.2 / (totalHits/1000)^0.25)
            double missFactor = Math.Pow(missCount, MISS_POWER);
            double lengthFactor = Math.Pow(totalHits / 1000.0, LENGTH_DIVISOR_POWER);
            
            double penalty = Math.Pow(MISS_BASE, missFactor / lengthFactor);
            
            return penalty;
        }
        
        /// <summary>
        /// 使用1.1次方合成总PP（osu!风格Minkowski Sum）- 含Accuracy
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
        
        /// <summary>
        /// ===== 第四十四轮新增：不含Accuracy的PP合成（用于StarRating计算）=====
        /// </summary>
        private double CalculateTotalPP(double movement, double reading, double precision)
        {
            // TotalPP = (Movement^1.1 + Reading^1.1 + Precision^1.1)^(1/1.1)
            double sum = Math.Pow(movement, P_NORM) + 
                         Math.Pow(reading, P_NORM) + 
                         Math.Pow(precision, P_NORM);
            
            return Math.Pow(sum, 1.0 / P_NORM);
        }
    }
}
