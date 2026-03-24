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
    /// SuperMarioDifficultyCalculator - 难度计算器（第四十七轮改进：基于Spiny间距的Reading PP）
    /// </summary>
    public class SuperMarioDifficultyCalculator : DifficultyCalculator
    {
        // 当前谱面信息（用于日志输出）
        public static string CurrentMapInfo { get; set; } = "Unknown";
        
        // 文件锁（防止多线程写入冲突）
        private static readonly object debugFileLock = new object();
        
        /// <summary>
        /// 从 beatmap 获取谱面信息
        /// </summary>
        private static string GetMapInfoFromBeatmap(IBeatmap beatmap)
        {
            try
            {
                var meta = beatmap.BeatmapInfo?.Metadata;
                string artist = meta?.Artist ?? "Unknown";
                string title = meta?.Title ?? "Unknown";
                string diff = beatmap.BeatmapInfo?.DifficultyName ?? "Unknown";
                return $"{artist} - {title}[{diff}]";
            }
            catch
            {
                return "Unknown";
            }
        }
        
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
        
        // ===== 第四十二轮新增：分段峰值加权参数 =====
        // Section时间窗口（5000ms）
        private const double SECTION_DURATION = 5000.0;
        
        // 滑动步长（400ms）
        private const double SECTION_STEP = 400.0;
        
        // Precision 物件窗口大小
        private const int PRECISION_WINDOW = 32;
        
        // 峰值衰减系数（用于 Movement 和 Reading 的 Section 级加权）
        private const double STRAIN_DECAY = 0.8;
        
        // Precision 衰减系数（更陡峭，用于压制长图虚高）
        private const double PRECISION_DECAY = 0.9963;
        
        // 标准物件数量（用于长度系数）
        private const double STANDARD_OBJECT_COUNT = 1500.0;
        
        // DT密度阈值（Objects per Second）
        private const double DENSITY_THRESHOLD = 3.0;
        private const double DENSITY_SCALE = 5.0;
        private const double DENSITY_BONUS_MAX = 0.15;
        
        // Miss惩罚参数
        private const double MISS_BASE = 0.96;
        private const double MISS_POWER = 1.2;
        private const double LENGTH_DIVISOR_POWER = 0.25;
        
        // ===== 第四十三轮新增：最小完美跳数参数 =====
        // Spiny跳跃滞空时间（毫秒）- 一次跳跃能覆盖的Spiny时间范围
        private const double JUMP_DURATION_MS = 500.0;
        
        // 滑动步长（毫秒）
        private const double STEP_MS = 400.0;
        
        // Goomba密度基准（每秒）
        private const double GOOMBA_DENSITY基准 = 3.0;
        
        // Spiny因子系数
        private const double SPINY_FACTOR_BASE = 0.1;
        
        // ===== 第四十六轮改进：Reading PP 分布模型 =====
        // 马里奥水平速度（像素/毫秒）- 典型值约 0.17 px/ms
        private const double MARIO_VELOCITY = 0.17;
        
        // 跳跃滞空时间（毫秒）- 用于计算不确定范围
        private const double JUMP_AIR_TIME = 500.0;
        
        // 小怪生成范围（像素）
        private const double SPAWN_RANGE = 1400.0;
        
        // 基础判定宽度（像素）
        private const double BASE_HIT_WIDTH = 34.0;
        
        // 读图不确定性系数（越高代表读图越难）
        private const double READING_UNCERTAINTY_FACTOR = 2.5;
        
        // Reading PP 放大系数（将概率值转换为 PP）
        private const double READING_PP_SCALE = 100.0;
        
        public SuperMarioDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap) : base(ruleset, beatmap) { }
        
        /// <summary>
        /// 从IBeatmap创建计算器（用于DrawableRuleset）
        /// </summary>
        public SuperMarioDifficultyCalculator(IRulesetInfo ruleset, IBeatmap beatmap) : base(ruleset, null)
        {
            // 创建一个最小化的WorkingBeatmap包装
            this.Beatmap = beatmap;
        }
        
        // 重写Beatmap属性以支持IBeatmap构造
        public new IBeatmap Beatmap { get; }
        
        /// <summary>
        /// 从IBeatmap计算难度（用于DrawableRuleset）
        /// </summary>
        public SuperMarioDifficultyAttributes CalculateFromBeatmap(IBeatmap beatmap, Mod[]? mods = null)
        {
            // 获取谱面信息
            if (CurrentMapInfo == "Unknown")
                CurrentMapInfo = GetMapInfoFromBeatmap(beatmap);
            
            lock (debugFileLock)
            {
                System.IO.File.AppendAllText("C:\\temp\\smb_debug.txt", "DifficultyCalculator.CalculateFromBeatmap: Starting...\n");
            }
            var result = Calculate(mods ?? Array.Empty<Mod>());
            lock (debugFileLock)
            {
                System.IO.File.AppendAllText("C:\\temp\\smb_debug.txt", "DifficultyCalculator.CalculateFromBeatmap: Done!\n");
            }
            return (SuperMarioDifficultyAttributes)result;
        }

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
        
        /// <summary>
        /// 计算等效AR（用于DT/HT模式下的PP计算）
        /// DT使游戏变快，等效于AR提高
        /// 公式: effectiveAR = originalAR + log2(clockRate) * 3
        /// 例如: DT(1.5x) -> +2.2, HT(0.75x) -> -1.4
        /// </summary>
        public static double GetEffectiveAR(double originalAR, double clockRate)
        {
            // 使用log2来平滑计算
            double arDelta = Math.Log2(clockRate) * 3.0;
            return originalAR + arDelta;
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRateParam)
        {
            // 获取原始AR/OD
            double originalAR = beatmap.Difficulty.ApproachRate;
            double od = beatmap.Difficulty.OverallDifficulty;
            
            // 计算等效AR（用于PP计算）
            double ar = GetEffectiveAR(originalAR, clockRateParam);
            
            // 计算 Reaction Buff（从AR 9开始）
            double reactionBuff = GetReactionBuff(ar);
            Console.WriteLine($"[SMB] OriginalAR={originalAR:F2}, EffectiveAR={ar:F2}, ClockRate={clockRateParam:F2}, ReactionBuff={reactionBuff:F3}");
            
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
            
            // 步骤1: 获取分组后的物件列表（时间根据clockRate缩放）
            var groupedObjects = GetGroupedObjects(beatmap, clockRate);
            
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
            
            // 计算物件总数和时长
            int objectCount = goombaCount + koopaCount + spinyCount;
            double durationMs = beatmap.HitObjects.LastOrDefault()?.StartTime ?? 0;
            double durationSeconds = durationMs / 1000.0;
            
            // ===== 第四十二轮新增：计算物件密度（用于DT溢价） =====
            double objectDensity = durationSeconds > 0 ? objectCount / durationSeconds : 0;
            Console.WriteLine($"[SMB] ObjectDensity={objectDensity:F2} obj/s, Duration={durationSeconds:F1}s");
            
            // 步骤2&3: 使用第四十三轮改进的"最小完美跳数"算法
            // 直接在CalculateSectionPeaks中完成所有计算
            var (movementPP, readingPP, _) = CalculateSectionPeaks(groupedObjects, ar, od, clockRate);
            
            // 步骤4: 计算Precision PP（Goomba与最近Spiny的距离）
            // 使用GitHub上的实现：Goomba与最近Spiny的距离决定Precision Strain
            double precisionPP = CalculatePrecisionPP(groupedObjects) * reactionBuff;
            
            // 步骤5: 计算Accuracy PP（基于OD和理论最大值）
            // 使用 osu!std Accuracy PP 公式: 1.52163^OD * accuracy^24 * 2.83
            // 这个值在后面会被 accuracyValue 覆盖
            
            // ===== 第四十二轮新增：DT密度溢价 =====
            // 当ClockRate > 1.0且物件密度高时，提供额外加成
            double intensityBonus = 1.0;
            if (clockRate > 1.0 && objectDensity > DENSITY_THRESHOLD)
            {
                double densityExcess = (objectDensity - DENSITY_THRESHOLD) / DENSITY_SCALE;
                intensityBonus = 1.0 + DENSITY_BONUS_MAX * System.Math.Min(1.0, densityExcess);
                Console.WriteLine($"[SMB] IntensityBonus={intensityBonus:F3} (density={objectDensity:F2})");
            }
            
            // 计算长度加成
            double lengthBonus = CalculateLengthBonus(objectCount);
            
            // 计算速度加成
            double clockRateBonus = Math.Pow(clockRate, 1.1);
            
            // 将全局系数应用到各维度（SS时）
            // 这样 DifficultyAttributes 中存储的就是最终的维度值
            movementPP *= lengthBonus * intensityBonus * clockRateBonus;
            readingPP *= lengthBonus * intensityBonus * clockRateBonus;
            precisionPP *= lengthBonus * intensityBonus * clockRateBonus;
            
            // Accuracy PP 也要乘上系数（使用 osu!std 公式）
            const double ACCURACY_OD_BASE = 1.52163;
            const double ACCURACY_POWER = 24.0;
            const double ACCURACY_BASE = 2.83;
            
            // Accuracy PP 使用 osu!std 公式: 2.83 * 1.52163^OD * Accuracy^24 * min(1.15, (CIRCLE_COUNT/1000)^0.3)
            // CIRCLE_COUNT = Goomba + Spiny（有判定点的物件）
            int circleCount = goombaCount + spinyCount;
            double accuracyLengthBonus = Math.Min(1.15, Math.Pow(circleCount / 1000.0, 0.3));
            
            double accuracyPP = ACCURACY_BASE * Math.Pow(ACCURACY_OD_BASE, od) * Math.Pow(1.0, ACCURACY_POWER) * accuracyLengthBonus;
            
            // ===== 第四十四轮修复：DifficultyCalculator只算基础难度 =====
            // StarRating只包含Movement + Reading + Precision（不含AccuracyPP）
            // Accuracy PP 已在前面计算
            
            // 避免DifficultyCalculator和PerformanceCalculator的AccuracyPP公式不一致
            double basePP = CalculateBasePP(movementPP, readingPP, precisionPP);
            
            // 星级 = 基础PP的立方根（不含AccuracyPP和bonus）
            double starRating = System.Math.Pow(basePP, 1.0 / 3.0);
            
            // 输出到文件（注意：现在各维度已经包含 lengthBonus * intensityBonus * clockRateBonus）
            string debugInfo = $"[{CurrentMapInfo}] [DifficultyCalculator] movementPP={movementPP:F2}, readingPP={readingPP:F2}, precisionPP={precisionPP:F2}, accuracyPP={accuracyPP:F2}, basePP={basePP:F2}, starRating={starRating:F2}, LB={lengthBonus:F3}, IB={intensityBonus:F3}, CRB={clockRateBonus:F3}\n";
            lock (debugFileLock)
            {
                System.IO.File.AppendAllText("C:\\temp\\smb_debug.txt", debugInfo);
            }
            
            Console.WriteLine($"[SMB] StarRating (without Accuracy/Bonus): {starRating:F2}");
            
            // 存储用于实时计算的峰值数据
            StorePeakData(groupedObjects);
            
            Console.WriteLine($"[SMB] Difficulty (osu! style): Goomba={goombaCount}, Koopa={koopaCount}, Spiny={spinyCount}");
            Console.WriteLine($"[SMB] PP Breakdown (Section-Weighted): Movement={movementPP:F2}, Reading={readingPP:F2}, Precision={precisionPP:F2}, Accuracy={accuracyPP:F2}");
            Console.WriteLine($"[SMB] BasePP={basePP:F2}, StarRating={starRating:F2}");
            
            // MaxPP 包含所有 PP 成分
            return new SuperMarioDifficultyAttributes
            {
                StarRating = starRating,
                MaxPP = basePP + accuracyPP,
                TotalPP = basePP + accuracyPP,
                MaxCombo = objectCount,
                MovementPP = movementPP,
                ReadingPP = readingPP,
                PrecisionPP = precisionPP,
                AccuracyPP = accuracyPP,
                GoombaCount = goombaCount,
                KoopaCount = koopaCount,
                SpinyCount = spinyCount,
                ObjectDensity = objectDensity,
                IntensityBonus = intensityBonus,
                LengthBonus = lengthBonus,
                ClockRateBonus = clockRateBonus,
                // ===== 第四十四轮新增：Timing物件统计 =====
                TimingObjectCount = goombaCount + spinyCount,
                OverallDifficulty = od
            };
        }
        
        /// <summary>
        /// 获取分组后的物件列表（合并同时出现的物件）
        /// </summary>
        public List<MarioDifficultyObject> GetGroupedObjects(IBeatmap beatmap, double clockRate = 1.0)
        {
            var result = new List<MarioDifficultyObject>();
            var timeGroups = new Dictionary<double, List<SuperMarioObjectType>>();
            
            // 按时间分组（DT下时间间隔变短，所以除以clockRate）
            foreach (var obj in beatmap.HitObjects)
            {
                if (obj is SuperMarioHitObject smbObj)
                {
                    // DT: 时间变短 = 原始时间 / clockRate
                    double startTime = obj.StartTime / clockRate;
                    
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
        /// 计算各物件的原始Strain值（用于后续峰值提取）
        /// </summary>
        private void CalculateRawStrains(List<MarioDifficultyObject> objects, bool useARBuff, double reactionBonus = 1.0, double clockRate = 1.0)
        {
            int consecutiveGoomba = 0;
            double lastTime = 0;
            
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
                        obj.RawMovementStrain = increment * GOOMBA_PP * totalMultiplier * speedStrain;
                        break;
                        
                    case SuperMarioObjectType.Koopa:
                        consecutiveGoomba = 0;
                        // Koopa: n * 0.5 * speedStrain
                        obj.RawMovementStrain = KOOPA_PP * reactionBonus * speedStrain;
                        break;
                        
                    case SuperMarioObjectType.Spiny:
                        consecutiveGoomba = 0;
                        // Spiny: n * 1 * speedStrain
                        obj.RawMovementStrain = SPINY_PP * reactionBonus * speedStrain;
                        break;
                        
                    default:
                        consecutiveGoomba = 0;
                        break;
                }
                
                lastTime = obj.StartTime;
            }
        }
        
        /// <summary>
        /// ===== 第四十三轮核心改进：基于"最小完美跳数"的Movement PP算法 =====
        /// 使用AR决定的滑动窗口，计算每窗口的最小完美跳数及跳跃难度
        /// </summary>
        public (double movementPP, double readingPP, double precisionPP) CalculateSectionPeaks(List<MarioDifficultyObject> objects, double ar, double od, double clockRate)
        {
            if (objects.Count == 0)
                return (0, 0, 0);
            
            // 计算AR决定的窗口长度（毫秒）
            double windowLengthMs = System.Math.Max(1.3, 9 - 0.7 * ar) * 1000;
            Console.WriteLine($"[SMB] AR={ar:F1}, WindowLength={windowLengthMs:F0}ms");
            
            // 先计算Reading PP的原始值（需要 AR 和 OD）
            CalculateRawReadingStrains(objects, ar, od, clockRate);
            
            // 使用新的"最小完美跳数"算法计算Movement PP
            double movementPP = CalculateMovementPPWithJumpCount(objects, ar, windowLengthMs);
            
            // Reading PP使用原有的峰值加权
            var readingPeaks = new List<double>();
            double currentSectionEnd = SECTION_DURATION;
            double currentReadingMax = 0;
            
            foreach (var obj in objects)
            {
                while (obj.StartTime > currentSectionEnd)
                {
                    readingPeaks.Add(currentReadingMax);
                    currentReadingMax = 0;
                    currentSectionEnd += SECTION_DURATION;
                }
                if (obj.RawReadingStrain > currentReadingMax)
                    currentReadingMax = obj.RawReadingStrain;
            }
            readingPeaks.Add(currentReadingMax);
            
            double weightedReading = CalculateWeightedSum(readingPeaks);
            double readingPP = System.Math.Pow(weightedReading, 0.5);
            
            // ===== 新增：Spiny 个数和平均距离加成 =====
            // 1. Spiny 个数加成：Spiny >= 5 时才有加成，线性关系
            int spinyCount = objects.Count(o => o.ObjectType == SuperMarioObjectType.Spiny);
            double spinyCountBonus = 1.0;
            if (spinyCount >= 5)
            {
                // 每增加 1 个 Spiny，加成 +0.02
                spinyCountBonus = 1.0 + 0.02 * (spinyCount - 5);
            }
            
            // 2. Spiny 平均距离加成：400ms 以下才有加成
            double avgGapBonus = 1.0;
            var spinyTimes = objects.Where(o => o.ObjectType == SuperMarioObjectType.Spiny).Select(o => o.StartTime).OrderBy(t => t).ToList();
            if (spinyTimes.Count > 1)
            {
                double totalGap = 0;
                int gapCount = 0;
                for (int i = 1; i < spinyTimes.Count; i++)
                {
                    double gap = spinyTimes[i] - spinyTimes[i - 1];
                    if (gap > 0 && gap < 5000) // 合理范围内的 gap
                    {
                        totalGap += gap;
                        gapCount++;
                    }
                }
                if (gapCount > 0)
                {
                    double avgGap = totalGap / gapCount; // 平均间隔（毫秒）
                    // 平均间隔 < 400ms 时才有加成
                    if (avgGap < 400)
                    {
                        // avgGap = 0 → 1.1, avgGap = 400 → 1.0
                        avgGapBonus = 1.0 + 0.1 * (400 - avgGap) / 400;
                    }
                }
            }
            
            // 应用加成
            readingPP *= spinyCountBonus * avgGapBonus;
            
            Console.WriteLine($"[SMB] Movement (JumpCount): {movementPP:F2}, Reading (Peak): {readingPP:F2}, SpinyCountBonus={spinyCountBonus:F3}, AvgGapBonus={avgGapBonus:F3}");
            
            return (movementPP, readingPP, 0);
        }
        
        /// <summary>
        /// ===== 第四十三轮改进版：基于Spiny分段的Movement PP计算 =====
        /// 核心算法：
        /// 1. 识别Spiny连续段（时间差<=500ms的Spiny为同一段）
        /// 2. 每段对应一次跳跃
        /// 3. 跳跃难度 = Spiny因子 * 时间应变 * Goomba密度因子
        /// 4. 所有跳跃难度降序加权
        /// </summary>
        private double CalculateMovementPPWithJumpCount(List<MarioDifficultyObject> objects, double ar, double windowLengthMs)
        {
            if (objects.Count == 0)
                return 0;
            
            // 1. 提取所有Spiny和Goomba
            var spinies = objects.Where(o => o.ObjectType == SuperMarioObjectType.Spiny)
                                .OrderBy(o => o.StartTime)
                                .Select(o => o.StartTime)
                                .ToList();
            
            var goombas = objects.Where(o => o.ObjectType == SuperMarioObjectType.Goomba)
                                .OrderBy(o => o.StartTime)
                                .Select(o => o.StartTime)
                                .ToList();
            
            // 如果没有Spiny，返回0（Movement PP主要由其他维度体现）
            if (spinies.Count == 0)
            {
                Console.WriteLine($"[SMB] No Spiny found, MovementPP = 0");
                return 0;
            }
            
            // 2. 识别Spiny连续段
            var segments = new List<SpinySegment>();
            for (int i = 0; i < spinies.Count; i++)
            {
                double spinyTime = spinies[i];
                if (i == 0 || spinyTime - spinies[i - 1] > JUMP_DURATION_MS)
                {
                    // 新段开始
                    segments.Add(new SpinySegment 
                    { 
                        StartTime = spinyTime, 
                        EndTime = spinyTime, 
                        Count = 1 
                    });
                }
                else
                {
                    // 加入当前段
                    var seg = segments[segments.Count - 1];
                    seg.EndTime = spinyTime;
                    seg.Count++;
                }
            }
            
            Console.WriteLine($"[SMB] Spiny segments: {segments.Count}");
            
            // 3. 计算每个跳跃的贡献
            var jumpContributions = new List<double>();
            
            for (int idx = 0; idx < segments.Count; idx++)
            {
                var seg = segments[idx];
                
                // Spiny因子：段内Spiny数量越多，难度越高
                double spinyFactor = 1.0 + SPINY_FACTOR_BASE * (seg.Count - 1);
                
                // 跳跃时间中心
                double jumpTime = (seg.StartTime + seg.EndTime) / 2.0;
                
                // 时间应变：跳跃间隔
                double delta;
                if (idx == 0)
                {
                    // 第一次跳跃：使用jumpTime作为delta
                    delta = jumpTime;
                }
                else
                {
                    // 后续跳跃：与前一次跳跃的间隔
                    var prevSeg = segments[idx - 1];
                    double prevJumpTime = (prevSeg.StartTime + prevSeg.EndTime) / 2.0;
                    delta = jumpTime - prevJumpTime;
                }
                delta = System.Math.Max(20.0, delta); // 防止除零
                
                // ===== 第四十三轮修复：调整时间应变因子 =====
                // 改为平方根形式，并设置上限
                double timeStrain = 1.0 + System.Math.Sqrt(1000.0 / delta);
                timeStrain = System.Math.Min(timeStrain, 5.0); // 上限5.0
                
                // ===== 第四十三轮修复：调整Goomba密度因子 =====
                // 改为对数增长形式，设置上限，防止GGG...KS类型的图PP虚高
                double goombaFactor = 1.0;
                if (idx > 0)
                {
                    double gapStart = segments[idx - 1].EndTime;
                    double gapEnd = seg.StartTime;
                    double gapLength = gapEnd - gapStart;
                    
                    if (gapLength > 0)
                    {
                        // 统计间隙内的Goomba数量
                        int goombaInGap = goombas.Count(t => t >= gapStart && t <= gapEnd);
                        if (goombaInGap > 0)
                        {
                            double density = goombaInGap / (gapLength / 1000.0);
                            // 对数增长：density=10时，log(11)≈2.4，系数0.3→约1.72
                            goombaFactor = 1.0 + 0.3 * System.Math.Log(1.0 + density);
                            // 上限2.0，防止过高
                            goombaFactor = System.Math.Min(goombaFactor, 2.0);
                        }
                    }
                }
                
                // 跳跃总贡献
                double jumpContribution = spinyFactor * timeStrain * goombaFactor;
                jumpContributions.Add(jumpContribution);
                
                Console.WriteLine($"[SMB] Jump {idx + 1}: Spiny={seg.Count}, delta={delta:F0}ms, factor={spinyFactor:F2}*{timeStrain:F2}*{goombaFactor:F2}={jumpContribution:F2}");
            }
            
            // 4. 加权峰值求和
            var sorted = jumpContributions.OrderByDescending(c => c).ToList();
            double weightedSum = 0;
            double weight = 1.0;
            for (int i = 0; i < sorted.Count; i++)
            {
                weightedSum += sorted[i] * weight;
                weight *= STRAIN_DECAY;
            }
            
            // 5. 压缩并应用AR加成
            double movementPP = System.Math.Pow(weightedSum, 1.0 / 1.1);
            
            // AR > 9 时额外加成
            if (ar > 9.0)
            {
                movementPP *= 1.0 + 0.1 * (ar - 9.0);
            }
            
            Console.WriteLine($"[SMB] Movement (SpinySegment): {segments.Count} jumps, weightedSum={weightedSum:F2}, AR_bonus={ar:F1}, final={movementPP:F2}");
            
            return movementPP;
        }
        
        /// <summary>
        /// Spiny连续段结构
        /// </summary>
        private class SpinySegment
        {
            public double StartTime { get; set; }
            public double EndTime { get; set; }
            public int Count { get; set; }
        }
        
        /// <summary>
        /// 计算加权求和（降序排列 + 0.95^i衰减）
        /// </summary>
        private double CalculateWeightedSum(List<double> peaks)
        {
            if (peaks.Count == 0)
                return 0;
                
            // 降序排列
            var sortedPeaks = peaks.OrderByDescending(p => p).ToList();
            
            // 应用衰减求和
            double sum = 0;
            for (int i = 0; i < sortedPeaks.Count; i++)
            {
                sum += sortedPeaks[i] * System.Math.Pow(STRAIN_DECAY, i);
            }
            
            return sum;
        }
        
        /// <summary>
        /// ===== 第四十七轮改进：基于 Spiny 间距离的 Reading PP 计算 =====
        /// 考虑 K S K S 交替模式，计算跳跃能否越过 Spiny
        /// </summary>
        private void CalculateRawReadingStrains(List<MarioDifficultyObject> objects, double ar, double od, double clockRate = 1.0)
        {
            // 收集所有 Spiny 的时间和索引
            var spinyTimes = new List<double>();
            var spinyIndices = new List<int>();
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].ObjectType == SuperMarioObjectType.Spiny)
                {
                    spinyTimes.Add(objects[i].StartTime);
                    spinyIndices.Add(i);
                }
            }
            
            if (spinyTimes.Count == 0) return;
            
            // 计算小怪生成速度（基于 AR）
            double spawnTime = System.Math.Max(1.3, 9 - 0.7 * ar); // 秒
            double spawnVelocity = SPAWN_RANGE / (spawnTime * 1000); // 像素/毫秒
            
            // 计算 Spiny 判定宽度（基础宽度 + 速度影响）
            double spinyHitWindow = BASE_HIT_WIDTH + spawnVelocity * 0.2 * 1000; // 像素
            
            // OD 决定的额外判定宽度
            double odBonus = 1.0 + 2.9 * od; // OD 0 = 1px, OD 10 = 30px
            
            Console.WriteLine($"[SMB Reading] AR={ar:F1}, spawnVel={spawnVelocity:F4}px/ms, spinyHW={spinyHitWindow:F1}px, ODBonus={odBonus:F1}px");
            
            // 对每个 Spiny 计算 Reading PP
            for (int i = 0; i < spinyIndices.Count; i++)
            {
                double currentTime = spinyTimes[i];
                int currentIdx = spinyIndices[i];
                
                // 1. 找出当前 Spiny 后面 1400px 范围内的所有 Spiny
                // 使用时间差 × spawnVelocity 来估算像素距离
                double maxTimeInRange = SPAWN_RANGE / spawnVelocity; // 毫秒
                
                var gaps = new List<double>();
                
                for (int j = i + 1; j < spinyTimes.Count; j++)
                {
                    double timeDiff = spinyTimes[j] - currentTime; // 后续 Spiny
                    
                    // 只考虑 1400px 范围内的
                    if (timeDiff > maxTimeInRange) break;
                    
                    // 计算像素距离
                    double gapDistance = timeDiff * spawnVelocity;
                    
                    // 减去两个 Spiny 的判定宽度 + OD 加成
                    // 两个 Spiny 本身的宽度需要跳过
                    double effectiveGap = gapDistance - (spinyHitWindow + odBonus) - (spinyHitWindow + odBonus);
                    
                    // 减去跳跃滞空期间移动的距离（玩家在空中的水平位移）
                    double airTravelDistance = JUMP_AIR_TIME * MARIO_VELOCITY;
                    effectiveGap -= airTravelDistance;
                    
                    // 有效 Gap 不能为负
                    if (effectiveGap < 0) effectiveGap = 0;
                    
                    gaps.Add(effectiveGap);
                }
                
                // 2. 如果没有后续 Spiny，使用默认时间差
                if (gaps.Count == 0)
                {
                    // 假设下一个 Spiny 在标准间隔处
                    double defaultGap = 1000 * spawnVelocity; // 默认 1 秒
                    gaps.Add(defaultGap);
                }
                
                // 3. 计算成功命中概率
                // 使用非线性/反比例关系：Gap 越小，难度越高
                double totalFailureProb = 0;
                double sigma = MARIO_VELOCITY * JUMP_AIR_TIME * READING_UNCERTAINTY_FACTOR;
                
                foreach (double gap in gaps)
                {
                    // ===== 第四十八轮改进：使用非线性反比例关系 =====
                    // 当 gap = 0 时，failProb = 1（完全无法跳过）
                    // 当 gap 很大时，failProb → 0
                    // 使用更激进的公式：failProb = 1 - gap / (gap + sigma * 3)
                    // 这样当 gap < sigma 时 failProb > 0.5
                    
                    double failProb;
                    if (gap <= 0)
                    {
                        // Gap <= 0 说明完全不可能跳过
                        failProb = 1.0;
                    }
                    else
                    {
                        // 更激进的非线性公式
                        // 当 gap = 0 → failProb = 1
                        // 当 gap = sigma * 3 → failProb = 0.5
                        // 当 gap → ∞ → failProb → 0
                        failProb = 1.0 - gap / (gap + sigma * 3.0);
                    }
                    
                    // 取最大失败概率
                    if (failProb > totalFailureProb)
                        totalFailureProb = failProb;
                }
                
                // 4. 成功概率 = 1 - 失败概率
                double successProbability = 1.0 - totalFailureProb;
                
                // 5. Reading 难度 = 1 - 成功概率
                double strain = 1.0 - successProbability;
                
                // 6. 应用 AR Buff（AR >= 9.4 时）
                if (ar >= AR_THRESHOLD)
                {
                    strain *= AR_BUFF;
                }
                
                // 7. 应用 ClockRate 加成
                double clockRateBonus = System.Math.Pow(clockRate, 1.2);
                
                // 8. 应用放大系数（将概率值转换为合理的 PP 范围）
                objects[currentIdx].RawReadingStrain = strain * SPINY_PP * clockRateBonus * READING_PP_SCALE;
                
                Console.WriteLine($"[SMB Reading] Spiny[{i}] at {currentTime:F0}: gaps={gaps.Count}, maxGap={gaps.Max():F0}px, failProb={totalFailureProb:F3}, strain={strain:F3}, scaled={objects[currentIdx].RawReadingStrain:F3}");
            }
        }
        
        /// <summary>
        /// 正态分布累积分布函数（CDF）
        /// 使用误差函数近似：CDF(x) = 0.5 × (1 + erf(x / √2))
        /// </summary>
        private double NormalCDF(double x)
        {
            // 使用 Abramowitz & Stegun 的近似公式
            const double a1 = 0.254829592;
            const double a2 = -0.284496736;
            const double a3 = 1.421413741;
            const double a4 = -1.453152027;
            const double a5 = 1.061405429;
            const double p = 0.3275911;
            
            double sign = 1;
            if (x < 0)
                sign = -1;
            
            double xAbs = System.Math.Abs(x) / System.Math.Sqrt(2);
            
            double t = 1.0 / (1.0 + p * xAbs);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * System.Math.Exp(-xAbs * xAbs);
            
            return 0.5 * (1.0 + sign * y);
        }
        
        /// <summary>
        /// 存储峰值数据（用于实时PP计算）
        /// </summary>
        private void StorePeakData(List<MarioDifficultyObject> objects)
        {
            // 这个方法用于存储Section峰值数据，以便在实时计算时使用
            // 当前实现将峰值数据存储在对象本身中
            // 实际使用时，需要在ScoreProcessor中按相同逻辑计算
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
        /// v1.1版本 + 按Precision PP降序排序 + 0.98衰减加权
        /// </summary>
        public double CalculatePrecisionPP(List<MarioDifficultyObject> objects)
        {
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
            
            // 收集所有Goomba的Precision PP（不衰减）
            var goombaPPs = new List<double>();
            
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
                double strain = 100.0 / (minDistance + MARIO_HIT_WIDTH);
                
                obj.PrecisionPP = strain * GOOMBA_PP;
                goombaPPs.Add(obj.PrecisionPP);
            }
            
            if (goombaPPs.Count == 0) return 0;
            
            // 按Precision PP降序排序
            var sortedPPs = goombaPPs.OrderByDescending(p => p).ToList();
            
            // 使用0.98^i衰减加权（最高为1.0）
            double totalPrecisionPP = 0;
            for (int i = 0; i < sortedPPs.Count; i++)
            {
                totalPrecisionPP += sortedPPs[i] * System.Math.Pow(PRECISION_DECAY, i);
            }
            
            // 输出日志（安全处理少于3个Goomba的情况）
            string topInfo = goombaPPs.Count >= 3 
                ? $"Top3=[{sortedPPs[0]:F2}, {sortedPPs[1]:F2}, {sortedPPs[2]:F2}]"
                : $"Top{goombaPPs.Count}=[{string.Join(", ", sortedPPs.Select(p => p.ToString("F2")))}]";
            Console.WriteLine($"[SMB Precision] Goombas={goombaPPs.Count}, {topInfo}, Total={totalPrecisionPP:F2}");
            
            return totalPrecisionPP;
        }
        
        /// <summary>
        /// ===== 第四十二轮改进：使用 osu! 风格的 strain 加权计算总 PP =====
        /// 解决长图 PP 爆炸问题，加入Intensity Bonus
        /// </summary>
        private double CalculateWeightedStrainPP(double movement, double reading, double precision, double accuracy, int objectCount, double intensityBonus = 1.0)
        {
            // 1. 首先合成基础 PP（使用 1.1 次方）
            double baseSum = System.Math.Pow(movement, 1.1) + 
                           System.Math.Pow(reading, 1.1) + 
                           System.Math.Pow(precision, 1.1) + 
                           System.Math.Pow(accuracy, 1.1);
            double basePP = System.Math.Pow(baseSum, 1.0 / 1.1);
            
            // 2. 计算长度系数（osu! 风格 - 改进版）
            // 短图受惩罚，中等图标准分，长图有对数加成
            double lengthBonus;
            if (objectCount <= 0) 
                objectCount = 1;
            
            if (objectCount < 500)
            {
                // 短图惩罚
                lengthBonus = 0.95 + 0.4 * System.Math.Min(1.0, objectCount / STANDARD_OBJECT_COUNT);
            }
            else if (objectCount <= (int)STANDARD_OBJECT_COUNT)
            {
                // 标准长度
                lengthBonus = 0.95 + 0.4 * System.Math.Min(1.0, objectCount / STANDARD_OBJECT_COUNT);
            }
            else
            {
                // 长图对数加成（使用改进公式）
                double logBonus = 0.1 * System.Math.Log10(objectCount / STANDARD_OBJECT_COUNT);
                lengthBonus = 1.35 + logBonus;
                lengthBonus = System.Math.Min(1.5, lengthBonus); // 封顶1.5
            }
            
            // 3. 应用长度系数和Intensity Bonus
            double totalPP = basePP * lengthBonus * intensityBonus;
            
            Console.WriteLine($"[SMB] LengthBonus={lengthBonus:F3} for {objectCount} objects");
            Console.WriteLine($"[SMB] Final PP = BasePP({basePP:F2}) * Length({lengthBonus:F3}) * Intensity({intensityBonus:F3}) = {totalPP:F2}");
            
            return totalPP;
        }
        
        /// <summary>
        /// <summary>
        /// ===== 第四十四轮新增：计算基础PP（不含Accuracy和bonus）=====
        /// 用于StarRating计算
        /// </summary>
        private double CalculateBasePP(double movement, double reading, double precision)
        {
            // TotalPP = (Movement^1.1 + Reading^1.1 + Precision^1.1)^(1/1.1)
            double sum = System.Math.Pow(movement, 1.1) + 
                         System.Math.Pow(reading, 1.1) + 
                         System.Math.Pow(precision, 1.1);
            
            return System.Math.Pow(sum, 1.0 / 1.1);
        }
        
        /// ===== 第四十二轮新增：计算动态Miss惩罚 =====
        /// 使用非线性公式，长图容错率高但怕断连，短图惩罚重
        /// </summary>
        public static double CalculateMissPenalty(double missCount, double totalHits)
        {
            if (missCount <= 0)
                return 1.0;
            
            // 公式: 0.96^(miss^1.2 / (totalHits/1000)^0.25)
            double missFactor = System.Math.Pow(missCount, MISS_POWER);
            double lengthFactor = System.Math.Pow(totalHits / 1000.0, LENGTH_DIVISOR_POWER);
            
            double penalty = System.Math.Pow(MISS_BASE, missFactor / lengthFactor);
            
            Console.WriteLine($"[SMB] MissPenalty: {missCount} misses / {totalHits} hits = {penalty:F4}");
            
            return penalty;
        }
        
        /// <summary>
        /// 计算长度加成
        /// </summary>
        private double CalculateLengthBonus(int objectCount)
        {
            if (objectCount < 500)
                return 0.95 + 0.4 * System.Math.Min(1.0, objectCount / 1500.0);
            else if (objectCount <= 1500)
                return 0.95 + 0.4 * System.Math.Min(1.0, objectCount / 1500.0);
            else
                return System.Math.Min(1.5, 1.35 + 0.1 * System.Math.Log10(objectCount / 1500.0));
        }
    }
}
