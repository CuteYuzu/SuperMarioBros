using System;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.SuperMarioBros.Mods
{
    /// <summary>
    /// AR/OD 与毫秒的互相转换
    /// </summary>
    public static class SMBDifficultyConverter
    {
        /// <summary>
        /// 将 AR 转换为毫秒（出怪时间）
        /// </summary>
        public static double ARToMs(double ar)
        {
            return 1000 * Math.Max(1.3, 9.0 - (ar * 0.7));
        }
        
        /// <summary>
        /// 将毫秒转换回 AR
        /// </summary>
        public static double MsToAR(double ms)
        {
            return (9.0 - (ms / 1000.0)) / 0.7;
        }
        
        /// <summary>
        /// 将 OD 转换为毫秒（判定窗）
        /// </summary>
        public static double ODToMs(double od)
        {
            // OD 范围 0-10，判定窗范围约 80ms - 20ms
            return 80 - (od * 6);
        }
        
        /// <summary>
        /// 将毫秒转换回 OD
        /// </summary>
        public static double MsToOD(double ms)
        {
            return (80 - ms) / 6;
        }
    }
    
    /// <summary>
    /// Easy Mod - 降低难度
    /// </summary>
    public class SuperMarioModEasy : ModEasy, IApplicableToDifficulty
    {
        public override LocalisableString Description => "更大的判定区，更简单的马里奥！";
        public override double ScoreMultiplier => 0.5;
        
        public override void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            base.ApplyToDifficulty(difficulty);
            
            // Easy: OD 也降低
            difficulty.OverallDifficulty *= ADJUST_RATIO;
        }
    }

    /// <summary>
    /// No Fail Mod - 不会死亡
    /// </summary>
    public class SuperMarioModNoFail : ModNoFail
    {
        public override LocalisableString Description => "你不会失败！";
        public override double ScoreMultiplier => 0.5;
    }

    /// <summary>
    /// Half Time Mod - 慢速
    /// </summary>
    public class SuperMarioModHalfTime : ModHalfTime, IApplicableToDifficulty
    {
        public override LocalisableString Description => "慢一点~";
        public override double ScoreMultiplier => 0.5;
        
        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            float speed = (float)SpeedChange.Value;
            
            // AR: ms / speed
            double arMs = SMBDifficultyConverter.ARToMs(difficulty.ApproachRate);
            double newArMs = arMs / speed;
            difficulty.ApproachRate = (float)Math.Clamp(SMBDifficultyConverter.MsToAR(newArMs), 0, 11);
            
            // OD: ms / speed
            double odMs = SMBDifficultyConverter.ODToMs(difficulty.OverallDifficulty);
            double newOdMs = odMs / speed;
            difficulty.OverallDifficulty = (float)Math.Clamp(SMBDifficultyConverter.MsToOD(newOdMs), 0, 10);
        }
    }

    /// <summary>
    /// DayCore Mod - 变暗
    /// </summary>
    public class SuperMarioModDayCore : ModDaycore
    {
        public override LocalisableString Description => "白天变黑夜...";
        public override double ScoreMultiplier => 0.3;
    }

    /// <summary>
    /// Hard Rock Mod - 增加难度
    /// </summary>
    public class SuperMarioModHardRock : ModHardRock, IApplicableToDifficulty
    {
        public override LocalisableString Description => "一切都变难了！";
        public override double ScoreMultiplier => 1.4;
        
        public override void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            base.ApplyToDifficulty(difficulty);
            
            // HardRock: AR 和 OD 都增加（上限10）
            difficulty.ApproachRate = Math.Min(10, difficulty.ApproachRate * ADJUST_RATIO);
            difficulty.OverallDifficulty = Math.Min(10, difficulty.OverallDifficulty * ADJUST_RATIO);
        }
    }

    /// <summary>
    /// Double Time Mod - 加速
    /// </summary>
    public class SuperMarioModDoubleTime : ModDoubleTime, IApplicableToDifficulty
    {
        // 反应奖励阈值
        public const float REACTION_BONUS_MIN_AR = 10.1f;
        public const float REACTION_BONUS_MAX_AR = 11.11f;
        public const double REACTION_BONUS_MULTIPLIER = 1.2;
        
        public override LocalisableString Description => "加速！";
        public override double ScoreMultiplier => 1.5;
        
        /// <summary>
        /// 获取反应奖励乘数（用于 PP 计算）
        /// </summary>
        public double GetReactionBonus(float originalAR)
        {
            if (originalAR >= REACTION_BONUS_MIN_AR && originalAR <= REACTION_BONUS_MAX_AR)
            {
                float t = (originalAR - REACTION_BONUS_MIN_AR) / (REACTION_BONUS_MAX_AR - REACTION_BONUS_MIN_AR);
                return 1.0 + REACTION_BONUS_MULTIPLIER * t;
            }
            return 1.0;
        }
        
        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            float speed = (float)SpeedChange.Value;
            
            // AR: ms / speed
            double arMs = SMBDifficultyConverter.ARToMs(difficulty.ApproachRate);
            double newArMs = arMs / speed;
            difficulty.ApproachRate = (float)Math.Clamp(SMBDifficultyConverter.MsToAR(newArMs), 0, 11);
            
            // OD: ms / speed
            double odMs = SMBDifficultyConverter.ODToMs(difficulty.OverallDifficulty);
            double newOdMs = odMs / speed;
            difficulty.OverallDifficulty = (float)Math.Clamp(SMBDifficultyConverter.MsToOD(newOdMs), 0, 10);
        }
    }

    /// <summary>
    /// NightCore Mod - 加速 + 变暗（继承 DT）
    /// </summary>
    public class SuperMarioModNightCore : ModNightcore, IApplicableToDifficulty
    {
        // 反应奖励阈值（和 DT 一样）
        public const float REACTION_BONUS_MIN_AR = 10.1f;
        public const float REACTION_BONUS_MAX_AR = 11.11f;
        public const double REACTION_BONUS_MULTIPLIER = 1.2;
        
        public override LocalisableString Description => "加速 + 变暗！";
        public override double ScoreMultiplier => 1.5;
        
        /// <summary>
        /// 获取反应奖励乘数
        /// </summary>
        public double GetReactionBonus(float originalAR)
        {
            if (originalAR >= REACTION_BONUS_MIN_AR && originalAR <= REACTION_BONUS_MAX_AR)
            {
                float t = (originalAR - REACTION_BONUS_MIN_AR) / (REACTION_BONUS_MAX_AR - REACTION_BONUS_MIN_AR);
                return 1.0 + REACTION_BONUS_MULTIPLIER * t;
            }
            return 1.0;
        }
        
        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            float speed = (float)SpeedChange.Value;
            
            // 和 DT 一样的速度计算
            double arMs = SMBDifficultyConverter.ARToMs(difficulty.ApproachRate);
            double newArMs = arMs / speed;
            difficulty.ApproachRate = (float)Math.Clamp(SMBDifficultyConverter.MsToAR(newArMs), 0, 11);
            
            double odMs = SMBDifficultyConverter.ODToMs(difficulty.OverallDifficulty);
            double newOdMs = odMs / speed;
            difficulty.OverallDifficulty = (float)Math.Clamp(SMBDifficultyConverter.MsToOD(newOdMs), 0, 10);
        }
    }
}
