using osu.Game.Rulesets.Difficulty;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.SuperMarioBros;
using osu.Game.Rulesets.SuperMarioBros.Mods;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.SuperMarioBros.Difficulty
{
    /// <summary>
    /// SuperMarioDifficultyCalculator - 难度计算器（osu!风格PP版 + Reaction Buff）
    /// </summary>
    public class SuperMarioDifficultyCalculator : DifficultyCalculator
    {
        // PP权重定义
        private const double GOOMBA_PP = 1;
        private const double KOOPA_PP = 0.5;
        private const double SPINY_PP = 1;
        
        // AR Buff参数
        private const double AR_THRESHOLD = 9.4;
        private const double AR_BUFF = 1.2;
        
        // Reaction Buff参数
        private const double REACTION_THRESHOLD = 10.0;
        private const double REACTION_MULTIPLIER = 0.12;
        private const double REACTION_POWER = 1.38;
        
        // Reading窗口大小
        private const int READING_WINDOW = 16;
        
        // 判定宽度（像素）
        private const double MARIO_HIT_WIDTH = 34;
        
        public SuperMarioDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap) : base(ruleset, beatmap) { }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate) => System.Array.Empty<Skill>();

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            return System.Array.Empty<DifficultyHitObject>();
        }
        
        /// <summary>
        /// 计算 Reaction Buff（反应加成）
        /// 从 AR 9 开始平滑增长
        /// 公式: 1.0 + (ar - 9.0) * 0.1
        /// </summary>
        public static double GetReactionBuff(double ar)
        {
            // 从AR 9开始起步，平滑增长
            if (ar <= 9.0)
                return 1.0;
            
            // 平滑公式：从AR 9开始线性起步
            return 1.0 + (ar - 9.0) * 0.1;
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRateParam)
        {
            // 获取AR用于Movement PP buff（已经过Mod调整，即等效AR）
            double ar = beatmap.Difficulty.ApproachRate;
            double od = beatmap.Difficulty.OverallDifficulty;
            
            // 计算 Reaction Buff（从AR 9开始）
            double reactionBuff = GetReactionBuff(ar);
            Console.WriteLine($"[SMB] AR={ar:F2}, ReactionBuff={reactionBuff:F3}");
            
            bool useARBuff = ar >= AR_THRESHOLD;
            
            // 获取实际的ClockRate（从Mods中）
            double clockRate = clockRateParam;
            foreach (var mod in mods)
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
            Console.WriteLine($"[SMB] ClockRate={clockRate:F2}");
            
            // 步骤1: 获取分组后的物件列表
            var groupedObjects = GetGroupedObjects(beatmap);
            
            // 统计计数
            int goombaCount = 0, koopaCount = 0, spinyCount = 0;
            foreach (var obj in groupedObjects)
            {
                switch (obj.ObjectType)
                {
                    case SuperMarioObjectType.Goomba:
                        goombaCount++;
                        break;
                    case SuperMarioObjectType.Koopa:
                        koopaCount++;
                        break;
                    case SuperMarioObjectType.Spiny:
                        spinyCount++;
                        break;
                }
            }
            
            // 步骤2: 计算Movement PP（对数增量 + 速度应变）
            double movementPP = CalculateMovementPP(groupedObjects, useARBuff, reactionBuff, clockRate);
            
            // 步骤3: 计算Reading PP（基于Spiny隔开的空隙）
            double readingPP = CalculateReadingPP(groupedObjects, clockRate);
            
            // 步骤4: 计算Precision PP（Goomba与最近Spiny的距离）- 也应用Reaction Buff
            double precisionPP = CalculatePrecisionPP(groupedObjects) * reactionBuff;
            
            // 步骤5: 计算Accuracy PP（基于OD和理论最大值）
            double odScale = 1.0 + 0.1 * (od - 5.0);
            double basePPValue = movementPP + readingPP + precisionPP;
            double accuracyPP = basePPValue * 1.0 * odScale; // Accuracy为1.0（SS）时的基准
            
            // 计算物件总数
            int objectCount = goombaCount + koopaCount + spinyCount;
            
            // 步骤6: 使用osu!风格的strain加权计算
            // 1. 将地图分成400ms的时间段
            // 2. 计算每个时间段的峰值strain
            // 3. 降序排列并应用0.95衰减
            // 4. 添加长度系数
            double totalPP = CalculateWeightedStrainPP(movementPP, readingPP, precisionPP, accuracyPP, objectCount);
            
            // 全局速度加成：ClockRate^1.1（DT/NC时PP直接增加）
            double clockRateBonus = System.Math.Pow(clockRate, 1.1);
            totalPP *= clockRateBonus;
            Console.WriteLine($"[SMB] ClockRate Bonus={clockRateBonus:F3}");

            // 星级 = 总PP的立方根
            double starRating = System.Math.Pow(totalPP, 1.0 / 3.0);
            
            Console.WriteLine($"[SMB] Difficulty (osu! style): Goomba={goombaCount}, Koopa={koopaCount}, Spiny={spinyCount}");
            Console.WriteLine($"[SMB] PP Breakdown: Movement={movementPP:F2}, Reading={readingPP:F2}, Precision={precisionPP:F2}, Accuracy={accuracyPP:F2}");
            Console.WriteLine($"[SMB] TotalPP={totalPP:F2}, StarRating={starRating:F2}");
            
            return new SuperMarioDifficultyAttributes
            {
                StarRating = starRating,
                MaxPP = totalPP,
                TotalPP = totalPP,
                MaxCombo = objectCount,
                MovementPP = movementPP,
                ReadingPP = readingPP,
                PrecisionPP = precisionPP,
                AccuracyPP = accuracyPP,
                GoombaCount = goombaCount,
                KoopaCount = koopaCount,
                SpinyCount = spinyCount
            };
        }
        
        /// <summary>
        /// 获取分组后的物件列表（合并同时出现的物件）
        /// </summary>
        private List<MarioDifficultyObject> GetGroupedObjects(IBeatmap beatmap)
        {
            var result = new List<MarioDifficultyObject>();
            var timeGroups = new Dictionary<double, List<SuperMarioObjectType>>();
            
            // 按时间分组
            foreach (var obj in beatmap.HitObjects)
            {
                if (obj is SuperMarioHitObject smbObj)
                {
                    double startTime = obj.StartTime;
                    
                    if (!timeGroups.ContainsKey(startTime))
                    {
                        timeGroups[startTime] = new List<SuperMarioObjectType>();
                    }
                    
                    // 优先级: Spiny > Koopa > Goomba
                    // 如果已有同时间的物件，按优先级决定是否替换
                    if (timeGroups[startTime].Count == 0)
                    {
                        timeGroups[startTime].Add(smbObj.ObjectType);
                    }
                    else
                    {
                        // 检查是否需要替换（优先级更高的物件）
                        var existing = timeGroups[startTime][0];
                        int existingPriority = GetPriority(existing);
                        int newPriority = GetPriority(smbObj.ObjectType);
                        
                        if (newPriority > existingPriority)
                        {
                            timeGroups[startTime][0] = smbObj.ObjectType;
                        }
                    }
                }
            }
            
            // 转换为列表
            int index = 0;
            foreach (var kvp in timeGroups.OrderBy(x => x.Key))
            {
                foreach (var objType in kvp.Value)
                {
                    result.Add(new MarioDifficultyObject
                    {
                        ObjectType = objType,
                        StartTime = kvp.Key,
                        OriginalIndex = index++
                    });
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取物件优先级（越高越优先）
        /// </summary>
        private int GetPriority(SuperMarioObjectType type)
        {
            return type switch
            {
                SuperMarioObjectType.Spiny => 3,
                SuperMarioObjectType.Koopa => 2,
                SuperMarioObjectType.Goomba => 1,
                _ => 0
            };
        }
        
        /// <summary>
        /// 计算Movement PP（对数增量 + 速度应变）
        /// </summary>
        private double CalculateMovementPP(List<MarioDifficultyObject> objects, bool useARBuff, double reactionBonus = 1.0, double clockRate = 1.0)
        {
            double totalMovementPP = 0;
            int consecutiveGoomba = 0;
            double lastTime = 0;
            double strain = 0.5; // 防止在超长图中刷到很多pp
            
            for (int i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];
                
                // 计算时间差Δt（经过Mod缩放后的实际毫秒数）
                double deltaTime = 20; // 默认值，避免除零
                if (i > 0)
                {
                    deltaTime = (obj.StartTime - lastTime) * clockRate;
                    deltaTime = System.Math.Max(20.0, deltaTime); // 最小20ms
                }
                
                // 速度应变系数：与时间差挂钩
                double speedStrain = System.Math.Pow(100.0 / deltaTime, 0.5);
                
                switch (obj.ObjectType)
                {
                    case SuperMarioObjectType.Goomba:
                        consecutiveGoomba++;
                        // 对数增量: log2(n) - log2(n-1)
                        double increment = consecutiveGoomba > 1 
                            ? System.Math.Log2(consecutiveGoomba) - System.Math.Log2(consecutiveGoomba - 1)
                            : 1.0;
                        
                        // 应用AR Buff
                        double arMultiplier = useARBuff ? AR_BUFF : 1.0;
                        // 应用反应奖励
                        double totalMultiplier = arMultiplier * reactionBonus;
                        // 应用速度应变
                        obj.MovementPP = increment * GOOMBA_PP * totalMultiplier * speedStrain;
                        totalMovementPP += obj.MovementPP;
                        break;
                        
                    case SuperMarioObjectType.Koopa:
                        consecutiveGoomba = 0;
                        // Koopa: n * 0.5 * speedStrain
                        obj.MovementPP = KOOPA_PP * reactionBonus * speedStrain;
                        totalMovementPP += obj.MovementPP;
                        break;
                        
                    case SuperMarioObjectType.Spiny:
                        consecutiveGoomba = 0;
                        // Spiny: n * 1 * speedStrain
                        obj.MovementPP = SPINY_PP * reactionBonus * speedStrain;
                        totalMovementPP += obj.MovementPP;
                        break;
                        
                    default:
                        consecutiveGoomba = 0;
                        break;
                }
                
                lastTime = obj.StartTime;
            }
            
            return System.Math.Pow(totalMovementPP, strain);
        }
        
        /// <summary>
        /// 计算Reading PP（基于Spiny隔开的空隙）
        /// </summary>
        private double CalculateReadingPP(List<MarioDifficultyObject> objects, double clockRate = 1.0)
        {
            double totalReadingPP = 0;
            
            // 找到所有Spiny的索引
            var spinyIndices = new List<int>();
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].ObjectType == SuperMarioObjectType.Spiny)
                {
                    spinyIndices.Add(i);
                }
            }
            
            // 对每个Spiny计算Reading PP
            foreach (var spinyIdx in spinyIndices)
            {
                // 获取前后16个物件的窗口
                int startIdx = System.Math.Max(0, spinyIdx - READING_WINDOW);
                int endIdx = System.Math.Min(objects.Count - 1, spinyIdx + READING_WINDOW);
                
                // 找到被当前Spiny隔开的最大空隙
                double maxGap = 0;
                
                // 检查Spiny之前的物件（找到上一个Spiny）
                int prevSpinyIdx = -1;
                for (int i = spinyIdx - 1; i >= startIdx; i--)
                {
                    if (objects[i].ObjectType == SuperMarioObjectType.Spiny)
                    {
                        prevSpinyIdx = i;
                        break;
                    }
                }
                
                // 检查Spiny之后的物件（找到下一个Spiny）
                int nextSpinyIdx = -1;
                for (int i = spinyIdx + 1; i <= endIdx; i++)
                {
                    if (objects[i].ObjectType == SuperMarioObjectType.Spiny)
                    {
                        nextSpinyIdx = i;
                        break;
                    }
                }
                
                // 计算空隙：当前Spiny到前一个Spiny的距离
                if (prevSpinyIdx >= 0)
                {
                    double gap = objects[spinyIdx].StartTime - objects[prevSpinyIdx].StartTime;
                    if (gap > maxGap) maxGap = gap;
                }
                
                // 计算空隙：当前Spiny到下一个Spiny的距离
                if (nextSpinyIdx >= 0)
                {
                    double gap = objects[nextSpinyIdx].StartTime - objects[spinyIdx].StartTime;
                    if (gap > maxGap) maxGap = gap;
                }
                
                // Strain = 1.0 / (MaxGap + 1)
                double strain = maxGap > 0 ? 1.0 / (maxGap + 1) : 0;
                
                // Spiny数量越多，Reading PP越高
                double spinyMultiplier = 1.0 + 0.1 * spinyIndices.Count;
                
                // 应用ClockRate^1.5加成（DT/NC时Reading难度增加）
                double clockRateBonus = System.Math.Pow(clockRate, 1.5);
                
                objects[spinyIdx].ReadingPP = strain * spinyMultiplier * SPINY_PP * clockRateBonus;
                totalReadingPP += objects[spinyIdx].ReadingPP;
            }
            
            return totalReadingPP;
        }
        
        /// <summary>
        /// 计算Precision PP（Goomba与最近Spiny的距离）
        /// </summary>
        private double CalculatePrecisionPP(List<MarioDifficultyObject> objects)
        {
            double totalPrecisionPP = 0;
            
            // 找到所有Spiny的时间点
            var spinyTimes = new List<double>();
            foreach (var obj in objects)
            {
                if (obj.ObjectType == SuperMarioObjectType.Spiny)
                {
                    spinyTimes.Add(obj.StartTime);
                }
            }
            
            if (spinyTimes.Count == 0) return 0;
            
            // 对每个Goomba计算与最近Spiny的距离
            foreach (var obj in objects)
            {
                if (obj.ObjectType != SuperMarioObjectType.Goomba) continue;
                
                // 找到最近的Spiny时间差
                double minDistance = double.MaxValue;
                foreach (double spinyTime in spinyTimes)
                {
                    double distance = System.Math.Abs(obj.StartTime - spinyTime);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                    }
                }
                
                // Strain = 100.0 / (MinDistance + 34)
                // 34是马里奥判定宽度
                double strain = 100.0 / (minDistance + MARIO_HIT_WIDTH);
                
                obj.PrecisionPP = strain * GOOMBA_PP;
                totalPrecisionPP += obj.PrecisionPP;
            }
            
            return totalPrecisionPP;
        }
        
        /// <summary>
        /// 使用 osu! 风格的 strain 加权计算总 PP
        /// 解决长图 PP 爆炸问题
        /// </summary>
        private double CalculateWeightedStrainPP(double movement, double reading, double precision, double accuracy, int objectCount)
        {
            // 1. 首先合成基础 PP（使用 1.1 次方）
            double baseSum = System.Math.Pow(movement, 1.1) + 
                           System.Math.Pow(reading, 1.1) + 
                           System.Math.Pow(precision, 1.1) + 
                           System.Math.Pow(accuracy, 1.1);
            double basePP = System.Math.Pow(baseSum, 1.0 / 1.1);
            
            // 2. 计算长度系数（osu! 风格）
            // 短图受惩罚，中等图标准分，长图有对数加成
            double lengthBonus;
            if (objectCount <= 0) 
                objectCount = 1;
            
            if (objectCount < 500)
            {
                // 短图惩罚
                lengthBonus = 1;
            }
            else if (objectCount <= 2000)
            {
                // 标准长度
                lengthBonus = 1;
            }
            else
            {
                // 长图对数加成
                double logBonus = System.Math.Log(objectCount / 2000.0) * 0.15;
                lengthBonus = 1.0 + logBonus;
            }
            
            // 3. 应用长度系数
            double totalPP = basePP * lengthBonus;
            
            Console.WriteLine($"[SMB] LengthBonus={lengthBonus:F3} for {objectCount} objects");
            
            return totalPP;
        }
    }
}
