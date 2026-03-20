using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.SuperMarioBros.Objects;
using osuTK;

namespace osu.Game.Rulesets.SuperMarioBros.Replays
{
    /// <summary>
    /// Auto Generator - 参考Tau实现
    /// </summary>
    public class SuperMarioAutoGenerator : AutoGenerator
    {
        #region Constants
        
        // Mario位置常量
        private const float JUDGMENT_X = 200f;      // 判定线X
        private const float GROUND_Y = 468f;        // 地面Y
        private const float SPAWN_X = 1400f;        // 生成X
        private const float ENEMY_SPEED = 60f;      // 敌人移动速度 px/s
        private const float MARIO_SPEED = 400f;     // Mario移动速度 px/s
        private const float JUMP_DETECT_DISTANCE = 200f; // 起跳检测距离
        private const float JUMP_HEIGHT = 150f;      // 跳跃高度
        
        #endregion
        
        #region Fields
        
        protected Replay Replay;
        protected List<ReplayFrame> Frames => Replay.Frames;
        
        private readonly IReadOnlyList<IApplicableToRate> timeAffectingMods;
        private int buttonIndex;
        
        #endregion

        public SuperMarioAutoGenerator(IBeatmap beatmap, IReadOnlyList<Mod> mods) 
            : base(beatmap)
        {
            Replay = new Replay();
            timeAffectingMods = mods.OfType<IApplicableToRate>().ToList();
        }

        public override Replay Generate()
        {
            // 获取敌人列表
            var enemies = GetOrderedEnemies();
            
            if (enemies.Count == 0)
                return Replay;
                
            // 初始帧
            AddFrameToReplay(new SuperMarioReplayFrame(-100000, JUDGMENT_X, GROUND_Y, false, false));
            
            // 生成回放
            buttonIndex = 0;
            
            foreach (var enemy in enemies)
            {
                AddEnemyReplay(enemy);
            }
            
            // 添加结束帧
            var lastFrame = (SuperMarioReplayFrame)Frames.LastOrDefault();
            float endX = lastFrame?.X ?? JUDGMENT_X;
            float endY = lastFrame?.Y ?? GROUND_Y;
            AddFrameToReplay(new SuperMarioReplayFrame(enemies.Last().StartTime + 5000, endX, endY, false, false));
            
            return Replay;
        }
        
        private List<SuperMarioHitObject> GetOrderedEnemies()
        {
            var enemies = new List<SuperMarioHitObject>();
            foreach (var obj in Beatmap.HitObjects)
            {
                if (obj is SuperMarioHitObject smbObj)
                {
                    enemies.Add(smbObj);
                }
            }
            return enemies.OrderBy(e => e.StartTime).ToList();
        }
        
        private void AddEnemyReplay(SuperMarioHitObject enemy)
        {
            var lastFrame = (SuperMarioReplayFrame)Frames.LastOrDefault();
            float startX = lastFrame?.X ?? JUDGMENT_X;
            float startY = lastFrame?.Y ?? GROUND_Y;
            double lastTime = lastFrame?.Time ?? -100000;
            
            // 计算敌人到达时间
            double enemyTime = enemy.StartTime;
            
            // 等待时间
            double waitTime = enemyTime - 2000; // 提前2秒准备
            if (waitTime > lastTime)
            {
                // 添加等待帧（保持在当前位置）
                AddFrameToReplay(new SuperMarioReplayFrame(waitTime, startX, startY, false, false));
                lastTime = waitTime;
            }
            
            // 生成移动帧（从当前位置移动到敌人位置）
            double timeDiff = ApplyModsToTimeDelta(lastTime, enemyTime);
            
            if (timeDiff > 0)
            {
                // 计算移动目标X
                // 敌人到达判定线的时间 = enemyTime
                // 敌人位置 = SPAWN_X - (enemyTime - enemy.SpawnTime) * speed
                // 假设敌人Spawn在 enemyTime + 20000
                double enemySpawnTime = enemyTime + 20000;
                float enemyX = (float)(SPAWN_X - (enemyTime + 20000 - enemySpawnTime) / 1000.0 * ENEMY_SPEED);
                
                // 目标位置：敌人到达判定线时Mario也应该在判定线
                float targetX = JUDGMENT_X;
                float targetY = GROUND_Y;
                
                // 确定是否需要起跳
                bool shouldJump = ShouldJump(enemy);
                
                // 使用插值生成中间帧
                GenerateMovementFrames(lastTime, enemyTime, startX, startY, targetX, targetY, shouldJump);
                
                // 添加按键帧
                if (shouldJump)
                {
                    AddJumpFrame(enemyTime, targetX, targetY);
                }
            }
        }
        
        private bool ShouldJump(SuperMarioHitObject enemy)
        {
            // Spiny需要起跳躲避
            if (enemy.ObjectType == SuperMarioObjectType.Spiny)
                return true;
                
            // Goomba需要起跳踩踏
            if (enemy.ObjectType == SuperMarioObjectType.Goomba)
                return true;
                
            // Koopa不需要起跳
            return false;
        }
        
        private void GenerateMovementFrames(double startTime, double endTime, float startX, float startY, float targetX, float targetY, bool shouldJump)
        {
            // 生成中间帧
            for (double t = startTime + GetFrameDelay(startTime); t < endTime; t += GetFrameDelay(t))
            {
                float progress = (float)((t - startTime) / (endTime - startTime));
                
                // 位置插值 (简化版)
                float x = startX + (targetX - startX) * progress;
                float y = startY;
                
                // 如果需要跳跃，应用跳跃弧线
                if (shouldJump && progress > 0.3f && progress < 0.7f)
                {
                    y = startY + (targetY - startY - JUMP_HEIGHT) * (float)Math.Sin(progress * Math.PI);
                }
                
                AddFrameToReplay(new SuperMarioReplayFrame(t, x, y, false, false));
            }
        }
        
        private void AddJumpFrame(double time, float x, float y)
        {
            // 起跳帧
            buttonIndex++;
            bool isRight = buttonIndex % 2 == 0;
            
            AddFrameToReplay(new SuperMarioReplayFrame(time, x, y, true, false));
            
            // 起跳后一段时间释放按键
            double keyUpTime = time + 300;
            AddFrameToReplay(new SuperMarioReplayFrame(keyUpTime, x, y, false, false));
        }
        
        #region Utilities (from Tau)
        
        private double ApplyModsToTimeDelta(double startTime, double endTime)
        {
            double delta = endTime - startTime;
            return timeAffectingMods.Aggregate(delta, (current, mod) => current / mod.ApplyToRate(startTime));
        }
        
        private double ApplyModsToRate(double time, double rate)
            => timeAffectingMods.Aggregate(rate, (current, mod) => mod.ApplyToRate(time, current));
        
        private double GetFrameDelay(double time)
            => ApplyModsToRate(time, 1000.0 / 60);
        
        private int FindInsertionIndex(ReplayFrame frame)
        {
            int index = Frames.BinarySearch(frame, new ReplayFrameComparer());
            
            if (index < 0)
                index = ~index;
            else
            {
                while (index < Frames.Count && frame.Time == Frames[index].Time)
                    ++index;
            }
            
            return index;
        }
        
        private void AddFrameToReplay(ReplayFrame frame) 
            => Frames.Insert(FindInsertionIndex(frame), frame);
        
        private class ReplayFrameComparer : IComparer<ReplayFrame>
        {
            public int Compare(ReplayFrame f1, ReplayFrame f2)
            {
                if (f1 == null) throw new ArgumentNullException(nameof(f1));
                if (f2 == null) throw new ArgumentNullException(nameof(f2));
                return f1.Time.CompareTo(f2.Time);
            }
        }
        
        #endregion
    }
}
