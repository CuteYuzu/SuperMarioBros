using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets;
using System;
using System.Collections.Generic;

namespace osu.Game.Rulesets.SuperMarioBros
{
    /// <summary>
    /// SuperMarioScoreProcessor - 马里奥计分系统（PP版 osu!风格）
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
        
        private readonly int[] stompScores = { 100, 200, 400, 500, 800, 1000, 2000, 4000, 5000, 8000, 10000 };
        
        // 连续踩踏计数
        public int ConsecutiveStomps { get; private set; }
        public int OneUpCount { get; private set; }
        
        // 统计数据计数器
        public int GoombasKilled { get; private set; }
        public int KoopasKilled { get; private set; }
        public int SpiniesDodged { get; private set; }
        public int SpiniesHit { get; private set; }
        
        // PP计算（四大维度）
        public double CurrentPP { get; private set; }
        public double MaxPP { get; private set; }
        
        /// <summary>
        /// 实时PP四大维度
        /// </summary>
        public double MovementPP { get; private set; }
        public double ReadingPP { get; private set; }
        public double PrecisionPP { get; private set; }
        public double AccuracyPP { get; private set; }
        
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
        /// 设置理论最高PP
        /// </summary>
        public void SetMaxPP(double maxPP)
        {
            MaxPP = maxPP;
            Console.WriteLine($"[SMB RULESET] SetMaxPP called, MaxPP={MaxPP:F1}");
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
        /// 重新计算总PP（使用1.1次方合成）
        /// </summary>
        private void RecalculatePP()
        {
            // 计算OD缩放
            double odScale = 1.0 + 0.1 * (od - 5.0);
            
            // AccuracyPP = BaseValue * Accuracy^8 * ODScale
            AccuracyPP = basePPValue * Math.Pow(CurrentAccuracy, ACCURACY_POWER) * odScale;
            
            // TotalPP = (Movement^1.1 + Reading^1.1 + Precision^1.1 + Accuracy^1.1)^(1/1.1)
            double sum = Math.Pow(MovementPP, P_NORM) + 
                         Math.Pow(ReadingPP, P_NORM) + 
                         Math.Pow(PrecisionPP, P_NORM) + 
                         Math.Pow(AccuracyPP, P_NORM);
            
            CurrentPP = Math.Pow(sum, 1.0 / P_NORM);
        }
        
        /// <summary>
        /// 添加PP（用于实时更新）
        /// </summary>
        public void AddPP(double pp)
        {
            CurrentPP += pp;
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
            IncreaseCombo();
            AddPP(GOOMBA_PP);
            OnScore?.Invoke(300);
            Console.WriteLine($"[SMB RULESET] Goomba killed, PP={CurrentPP:F1}");
        }
        
        public void OnKoopaKill()
        {
            KoopasKilled++;
            IncreaseCombo();
            AddPP(KOOPA_PP);
            OnScore?.Invoke(300);
            Console.WriteLine($"[SMB RULESET] Koopa killed, PP={CurrentPP:F1}");
        }
        
        public void OnSpinyDodged()
        {
            SpiniesDodged++;
            IncreaseCombo();
            AddPP(SPINY_PP);
            OnScore?.Invoke(300);
            Console.WriteLine($"[SMB RULESET] Spiny dodged, PP={CurrentPP:F1}");
        }
        
        public void OnSpinyHit()
        {
            SpiniesHit++;
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

        public void OnDeath()
        {
            ConsecutiveStomps = 0;
        }
        
        /// <summary>
        /// 获取PP详细信息
        /// </summary>
        public string GetPPBreakdown()
        {
            return $"PP: {CurrentPP:F1} (M:{MovementPP:F1} R:{ReadingPP:F1} P:{PrecisionPP:F1} A:{AccuracyPP:F1}) Acc:{CurrentAccuracy:P1}";
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
