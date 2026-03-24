using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.SuperMarioBros
{
    /// <summary>
    /// SuperMarioScoreProcessor - 马里奥计分系统（PP版 osu!风格 + 第四十二轮改进）
    /// </summary>
    public partial class SuperMarioScoreProcessor : ScoreProcessor
    {
        // PP权重定义
        private const double GOOMBA_PP = 1.0;
        private const double KOOPA_PP = 0.5;
        private const double SPINY_PP = 1.0;
        
        // PP计算参数
        private const double P_NORM = 1.1;
        private const double ACCURACY_POWER = 8.0;
        
        // ===== 第四十二轮新增：Miss惩罚参数 =====
        private const double MISS_BASE = 0.96;
        private const double MISS_POWER = 1.2;
        private const double LENGTH_DIVISOR_POWER = 0.25;
        
        private readonly int[] stompScores = { 100, 200, 400, 500, 800, 1000, 2000, 4000, 5000, 8000, 10000 };
        
        // 连续踩踏计数
        public int ConsecutiveStomps { get; private set; }
        public int OneUpCount { get; private set; }
        
        // 统计数据计数器
        public int GoombasKilled { get; private set; }
        public int KoopasKilled { get; private set; }
        public int SpiniesDodged { get; private set; }
        public int SpiniesHit { get; private set; }
        
        // Miss计数（用于动态惩罚）
        public int MissCount { get; private set; }
        
        // ===== 第四十四轮新增：Timing物件统计 =====
        // 用于Accuracy PP计算：每个Goomba和Spiny作为独立timing判定物件
        public int TimingObjectCount { get; private set; }
        public int TimingScore { get; private set; }  // 6分满分(300), 2分(100), 1分(50), 0分(Miss)
        
        // ===== 第四十五轮新增：已处理物件追踪 =====
        // 用于实时PP计算
        public int ProcessedObjectCount { get; private set; }
        public int TotalObjectCount { get; private set; }
        
        // ===== 第四十五轮新增：窗口追踪 =====
        // Movement和Reading按窗口计算
        public int CompletedWindowCount { get; private set; }
        public int TotalWindowCount { get; private set; }
        
        // PP计算（四大维度）
        public double CurrentPP { get; private set; }
        public double MaxPP { get; private set; }
        
        /// <summary>
        /// 实时PP四大维度（实际游玩表现）
        /// </summary>
        public double MovementPP { get; private set; }
        public double ReadingPP { get; private set; }
        public double PrecisionPP { get; private set; }
        public double AccuracyPP { get; private set; }
        
        /// <summary>
        /// 理论最大PP（用于计算实时PP的比例）
        /// </summary>
        private double maxMovementPP = 0;
        private double maxReadingPP = 0;
        private double maxPrecisionPP = 0;
        
        /// <summary>
        /// OD值（用于Accuracy PP计算）
        /// </summary>
        private double od = 5.0;
        
        /// <summary>
        /// 当前准确率（0.0 - 1.0）
        /// </summary>
        public double CurrentAccuracy { get; private set; } = 1.0;
        
        /// <summary>
        /// 基础PP值（Movement + Reading + Precision的总和）
        /// </summary>
        private double basePPValue = 0;
        
        /// <summary>
        /// 总物件数量（用于Miss惩罚计算）
        /// </summary>
        private int totalObjects = 0;
        
        /// <summary>
        /// Intensity Bonus（从DifficultyAttributes获取）
        /// </summary>
        private double intensityBonus = 1.0;
        
        public event Action<int>? OnScore;
        public event Action? OnOneUp;

        public SuperMarioScoreProcessor(Ruleset ruleset) : base(ruleset)
        {
            ConsecutiveStomps = 0;
            OneUpCount = 0;
            GoombasKilled = 0;
            KoopasKilled = 0;
            SpiniesDodged = 0;
            SpiniesHit = 0;
            CurrentPP = 0;
            MaxPP = 0;
            MovementPP = 0;
            ReadingPP = 0;
            PrecisionPP = 0;
            AccuracyPP = 0;
        }
        
        /// <summary>
        /// 设置OD值
        /// </summary>
        public void SetOD(double odValue)
        {
            od = odValue;
            RecalculatePP();
        }
        
        /// <summary>
        /// 设置理论最高PP（第四十二轮改进：增加Intensity Bonus和TotalObjects参数）
        /// </summary>
        public void SetMaxPP(double maxPP, double intensity = 1.0, int totalObj = 0)
        {
            MaxPP = maxPP;
            intensityBonus = intensity;
            totalObjects = totalObj > 0 ? totalObj : (int)maxPP; // 估算
            Console.WriteLine($"[SMB RULESET] SetMaxPP called, MaxPP={MaxPP:F1}, Intensity={intensityBonus:F3}, TotalObjects={totalObjects}");
        }
        
        /// <summary>
        /// 设置基础PP值（来自DifficultyCalculator）
        /// </summary>
        public void SetBasePPValues(double movement, double reading, double precision)
        {
            MovementPP = movement;
            ReadingPP = reading;
            PrecisionPP = precision;
            basePPValue = movement + reading + precision;
            RecalculatePP();
            Console.WriteLine($"[SMB] BasePP set: Movement={MovementPP:F2}, Reading={ReadingPP:F2}, Precision={PrecisionPP:F2}");
        }
        
        /// <summary>
        /// 更新准确率
        /// </summary>
        public void SetAccuracy(double accuracy)
        {
            CurrentAccuracy = Math.Clamp(accuracy, 0.0, 1.0);
            RecalculatePP();
        }
        
        /// <summary>
        /// 添加PP（用于实时更新）
        /// </summary>
        public void AddPP(double pp)
        {
            CurrentPP += pp;
        }
        
        /// <summary>
        /// 重新计算总PP（使用1.1次方合成 + Miss惩罚应用到各维度）
        /// </summary>
        private void RecalculatePP()
        {
            // 使用 osu!std Accuracy PP 公式：1.52163^OD * accuracy^24 * 2.83
            const double ACCURACY_OD_BASE = 1.52163;
            const double ACCURACY_POWER = 24.0;
            const double ACCURACY_BASE = 2.83;
            
            // 计算 Accuracy PP（使用实时准确率）
            double odScale = Math.Pow(ACCURACY_OD_BASE, od);
            double currentAccuracyPP = odScale * Math.Pow(CurrentAccuracy, ACCURACY_POWER) * ACCURACY_BASE;
            
            // 计算 Length Bonus（基于当前已处理物件数）
            double currentLengthBonus = CalculateLengthBonus(ProcessedObjectCount);
            
            // 计算 Miss 惩罚
            double missPenalty = CalculateMissPenalty(MissCount, totalObjects);
            
            // 将 Miss 惩罚应用到各维度（不包括 Accuracy）
            double effectiveMovement = MovementPP * currentLengthBonus * intensityBonus * missPenalty;
            double effectiveReading = ReadingPP * currentLengthBonus * intensityBonus * missPenalty;
            double effectivePrecision = PrecisionPP * currentLengthBonus * intensityBonus * missPenalty;
            // Accuracy 不应用 Miss 惩罚，只和准确率相关
            
            // 合成总 PP
            double sum = Math.Pow(effectiveMovement, P_NORM) + 
                         Math.Pow(effectiveReading, P_NORM) + 
                         Math.Pow(effectivePrecision, P_NORM) + 
                         Math.Pow(currentAccuracyPP, P_NORM);
            
            CurrentPP = Math.Pow(sum, 1.0 / P_NORM);
            
            Console.WriteLine($"[SMB ScoreProcessor] PP: Acc={CurrentAccuracy:P1}, Miss={MissCount}, LB={currentLengthBonus:F3}, IB={intensityBonus:F3}, MissPen={missPenalty:F3}, Total={CurrentPP:F2}");
        }
        
        /// <summary>
        /// 计算长度加成
        /// </summary>
        private double CalculateLengthBonus(int objectCount)
        {
            if (objectCount < 500)
                return 0.95 + 0.4 * Math.Min(1.0, objectCount / 1500.0);
            else if (objectCount <= 1500)
                return 0.95 + 0.4 * Math.Min(1.0, objectCount / 1500.0);
            else
                return Math.Min(1.5, 1.35 + 0.1 * Math.Log10(objectCount / 1500.0));
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
            
            Console.WriteLine($"[SMB] MissPenalty: {missCount} misses / {totalHits} hits = {penalty:F4}");
            
            return penalty;
        }
        
        /// <summary>
        /// 设置理论最大PP（从DifficultyAttributes获取）
        /// </summary>
        public void SetMaxPPValues(double movement, double reading, double precision, int totalObjects, int totalWindows = 0)
        {
            maxMovementPP = movement;
            maxReadingPP = reading;
            maxPrecisionPP = precision;
            TotalObjectCount = totalObjects;
            TotalWindowCount = totalWindows > 0 ? totalWindows : totalObjects; // 简化：如果没有提供窗口数，用物件数
            
            // 初始实时PP等于最大PP
            MovementPP = movement;
            ReadingPP = reading;
            PrecisionPP = precision;
        }
        
        /// <summary>
        /// 追踪窗口完成（用于Movement和Reading实时PP计算）
        /// 每400ms调用一次
        /// </summary>
        public void OnWindowCompleted()
        {
            CompletedWindowCount++;
            UpdateRealTimePP();
        }
        
        /// <summary>
        /// 追踪物件处理（用于Precision实时PP计算）
        /// </summary>
        public void OnObjectProcessed(bool hit)
        {
            ProcessedObjectCount++;
            // 每次物件处理后更新实时PP
            UpdateRealTimePP();
        }
        
        /// <summary>
        /// 更新实时PP（手动调用时更新所有维度）
        /// </summary>
        public void UpdateRealTimePP()
        {
            RecalculateRealTimePP();
        }
        
        /// <summary>
        /// 重新计算实时PP（基于实际游玩进度和准确率）
        /// 核心逻辑：
        /// - Movement和Reading：基于窗口进度
        /// - Precision：基于物件进度
        /// </summary>
        private void RecalculateRealTimePP()
        {
            if (TotalObjectCount <= 0 && TotalWindowCount <= 0) return;
            
            // 准确率因子
            double accuracyFactor = CurrentAccuracy;
            
            // ===== Movement：基于窗口进度 =====
            double windowProgress = TotalWindowCount > 0 ? (double)CompletedWindowCount / TotalWindowCount : 0;
            if (windowProgress > 0)
            {
                // 使用平方根使变化更平滑
                double smoothWindowProgress = Math.Sqrt(windowProgress);
                MovementPP = maxMovementPP * smoothWindowProgress * accuracyFactor;
            }
            
            // ===== Reading：基于窗口进度（与Movement类似）=====
            if (windowProgress > 0)
            {
                double smoothWindowProgress = Math.Sqrt(windowProgress);
                ReadingPP = maxReadingPP * smoothWindowProgress * accuracyFactor;
            }
            
            // ===== Precision：基于物件进度 =====
            double objectProgress = TotalObjectCount > 0 ? (double)ProcessedObjectCount / TotalObjectCount : 0;
            if (objectProgress > 0)
            {
                double smoothObjectProgress = Math.Sqrt(objectProgress);
                PrecisionPP = maxPrecisionPP * smoothObjectProgress * accuracyFactor;
            }
        }
        
        /// <summary>
        /// 增加Combo
        /// </summary>
        public void IncreaseCombo()
        {
            Combo.Value++;
            Console.WriteLine($"[SMB] Combo increased to {Combo.Value}");
        }
        
        /// <summary>
        /// 断开Combo
        /// </summary>
        public void BreakCombo()
        {
            Combo.Value = 0;
            Console.WriteLine("[SMB] Combo broken");
        }

        public void OnStomp()
        {
            ConsecutiveStomps++;
            
            int scoreIndex = Math.Min(ConsecutiveStomps - 1, stompScores.Length - 1);
            int points = stompScores[scoreIndex];
            
            if (points == 10000)
            {
                OneUpCount++;
                OnOneUp?.Invoke();
            }
            
            OnScore?.Invoke(points);
        }
        
        public void OnGoombaKill()
        {
            GoombasKilled++;
            // ===== 第四十四轮新增：Timing统计 =====
            // Goomba成功踩踏得300分(6分)
            TimingObjectCount++;
            TimingScore += 6;
            // ===== 第四十五轮新增：物件处理追踪 =====
            OnObjectProcessed(true); // 成功处理
            IncreaseCombo();
            AddPP(GOOMBA_PP);
            OnScore?.Invoke(300);
            Console.WriteLine($"[SMB RULESET] Goomba killed, PP={CurrentPP:F1}");
        }
        
        public void OnKoopaKill()
        {
            KoopasKilled++;
            // ===== 第四十五轮新增：物件处理追踪 =====
            OnObjectProcessed(true); // 成功处理
            IncreaseCombo();
            AddPP(KOOPA_PP);
            OnScore?.Invoke(300);
            Console.WriteLine($"[SMB RULESET] Koopa killed, PP={CurrentPP:F1}");
        }
        
        public void OnSpinyDodged()
        {
            SpiniesDodged++;
            // ===== 第四十四轮新增：Timing统计 =====
            // Spiny成功躲避得300分(6分)
            TimingObjectCount++;
            TimingScore += 6;
            // ===== 第四十五轮新增：物件处理追踪 =====
            OnObjectProcessed(true); // 成功处理
            IncreaseCombo();
            AddPP(SPINY_PP);
            OnScore?.Invoke(300);
            Console.WriteLine($"[SMB RULESET] Spiny dodged, PP={CurrentPP:F1}");
        }
        
        public void OnSpinyHit()
        {
            SpiniesHit++;
            // ===== 第四十四轮新增：Timing统计 =====
            // Spiny碰撞得0分(Miss)
            TimingObjectCount++;
            TimingScore += 0;
            // ===== 第四十五轮新增：物件处理追踪 =====
            OnObjectProcessed(false); // 处理失败
            MissCount++; // Spiny碰撞算作Miss
            BreakCombo();
            // Spiny碰撞不给PP
        }

        public void OnShellKick()
        {
            ConsecutiveStomps++;
            int scoreIndex = Math.Min(ConsecutiveStomps - 1, stompScores.Length - 1);
            int points = stompScores[scoreIndex] * 2;
            OnScore?.Invoke(points);
        }

        public void ResetCombo()
        {
            ConsecutiveStomps = 0;
            BreakCombo();
        }
        
        /// <summary>
        /// 完全重置 ScoreProcessor（seek 时调用）
        /// </summary>
        public void Reset()
        {
            ConsecutiveStomps = 0;
            OneUpCount = 0;
            GoombasKilled = 0;
            KoopasKilled = 0;
            SpiniesDodged = 0;
            SpiniesHit = 0;
            MissCount = 0;
            TimingObjectCount = 0;
            TimingScore = 0;
            ProcessedObjectCount = 0;
            BreakCombo();
            
            Console.WriteLine("[SMB] ScoreProcessor reset for seek");
        }

        public void OnDeath()
        {
            ConsecutiveStomps = 0;
            MissCount++; // 死亡算作Miss
            BreakCombo();
        }
        
        /// <summary>
        /// 获取PP详细信息（实时PP，显示实际数值而非百分比）
        /// </summary>
        public string GetPPBreakdown()
        {
            double progress = TotalWindowCount > 0 ? (double)CompletedWindowCount / TotalWindowCount * 100 : 0;
            return $"PP: {CurrentPP:F1} ({progress:F0}% done) [M:{MovementPP:F1}/{maxMovementPP:F1} R:{ReadingPP:F1}/{maxReadingPP:F1} P:{PrecisionPP:F1}/{maxPrecisionPP:F1} A:{AccuracyPP:F1}] Acc:{CurrentAccuracy:P1}";
        }
        
        /// <summary>
        /// 获取自定义统计结果
        /// </summary>
        public IEnumerable<HitEventStatistics> GetStatistics()
        {
            var stats = new List<HitEventStatistics>();
            
            if (GoombasKilled > 0)
                stats.Add(new HitEventStatistics(HitResultType.Perfect, GoombasKilled, "Goombas Killed"));
            
            if (KoopasKilled > 0)
                stats.Add(new HitEventStatistics(HitResultType.Perfect, KoopasKilled, "Koopas Killed"));
            
            if (SpiniesDodged > 0)
                stats.Add(new HitEventStatistics(HitResultType.Perfect, SpiniesDodged, "Spinies Dodged"));
            
            return stats;
        }
    }
    
    /// <summary>
    /// 简单的统计数据结构
    /// </summary>
    public class HitEventStatistics
    {
        public HitResultType Result { get; }
        public int Count { get; }
        public string Name { get; }
        
        public HitEventStatistics(HitResultType result, int count, string name)
        {
            Result = result;
            Count = count;
            Name = name;
        }
    }
    
    public enum HitResultType
    {
        Miss,
        Mehh,
        Ok,
        Good,
        Great,
        Perfect
    }
}
