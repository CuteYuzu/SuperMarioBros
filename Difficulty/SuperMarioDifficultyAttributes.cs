using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.SuperMarioBros.Difficulty
{
    /// <summary>
    /// SuperMarioDifficultyAttributes - 难度属性（osu!风格PP版 + 第四十二轮改进）
    /// </summary>
    public class SuperMarioDifficultyAttributes : DifficultyAttributes
    {
        /// <summary>
        /// 理论最高PP（所有物件都成功处理）
        /// </summary>
        public double MaxPP { get; set; }
        
        /// <summary>
        /// 总PP（未开方）
        /// </summary>
        public double TotalPP { get; set; }
        
        /// <summary>
        /// Movement PP - 物件移动难度
        /// </summary>
        public double MovementPP { get; set; }
        
        /// <summary>
        /// Reading PP - 阅读谱面难度
        /// </summary>
        public double ReadingPP { get; set; }
        
        /// <summary>
        /// Precision PP - 精确度难度（Goomba与Spiny的距离）
        /// </summary>
        public double PrecisionPP { get; set; }
        
        /// <summary>
        /// Accuracy PP - 准度难度
        /// </summary>
        public double AccuracyPP { get; set; }
        
        /// <summary>
        /// 物件数量统计
        /// </summary>
        public int GoombaCount { get; set; }
        public int KoopaCount { get; set; }
        public int SpinyCount { get; set; }
        
        // ===== 第四十二轮新增：密度与强度属性 =====
        /// <summary>
        /// 物件密度（Objects per Second）
        /// </summary>
        public double ObjectDensity { get; set; }
        
        /// <summary>
        /// DT强度加成（基于密度的溢价）
        /// </summary>
        public double IntensityBonus { get; set; }
        
        /// <summary>
        /// 长度加成（基于物件总数）
        /// </summary>
        public double LengthBonus { get; set; }
        
        /// <summary>
        /// 速度加成（基于 ClockRate）
        /// </summary>
        public double ClockRateBonus { get; set; }
        
        // ===== 第四十四轮新增：Timing物件统计 =====
        /// <summary>
        /// Timing判定物件总数（Goomba + Spiny）
        /// </summary>
        public int TimingObjectCount { get; set; }
        
        /// <summary>
        /// Overall Difficulty（OD值）
        /// </summary>
        public double OverallDifficulty { get; set; }
    }
    
    /// <summary>
    /// 难度计算用的物件结构（第四十二轮改进）
    /// </summary>
    public class MarioDifficultyObject
    {
        public SuperMarioObjectType ObjectType { get; set; }
        public double StartTime { get; set; }
        public int OriginalIndex { get; set; }
        
        // PP贡献（已弃用，使用RawStrain）
        public double MovementPP { get; set; }
        public double ReadingPP { get; set; }
        public double PrecisionPP { get; set; }
        
        // ===== 第四十二轮新增：原始Strain值（用于Section峰值提取） =====
        public double RawMovementStrain { get; set; }
        public double RawReadingStrain { get; set; }
        
        // Section索引（用于实时计算）
        public int SectionIndex { get; set; }
    }
}
