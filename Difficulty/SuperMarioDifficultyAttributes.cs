using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.SuperMarioBros.Difficulty
{
    /// <summary>
    /// SuperMarioDifficultyAttributes - 难度属性（osu!风格PP版）
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
    }
    
    /// <summary>
    /// 难度计算用的物件结构
    /// </summary>
    public class MarioDifficultyObject
    {
        public SuperMarioObjectType ObjectType { get; set; }
        public double StartTime { get; set; }
        public int OriginalIndex { get; set; }
        
        // PP贡献
        public double MovementPP { get; set; }
        public double ReadingPP { get; set; }
        public double PrecisionPP { get; set; }
    }
}
