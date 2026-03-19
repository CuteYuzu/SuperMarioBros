using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.SuperMarioBros.Objects;
using osuTK;

namespace osu.Game.Rulesets.SuperMarioBros.Replays
{
    /// <summary>
    /// Auto Generator - 自动生成回放数据
    /// </summary>
    public class SuperMarioAutoGenerator : AutoGenerator
    {
        // 常量定义
        private const float MARIO_START_X = 100f;
        private const float GROUND_Y = 0f;
        private const float SPAWN_X = 1400f;
        private const float JUMP_DETECT_DISTANCE = 150f; // 距离敌人多少像素时起跳
        
        public SuperMarioAutoGenerator(IBeatmap beatmap, IReadOnlyList<Mod> mods) 
            : base(beatmap)
        {
            Replay = new Replay();
        }

        protected Replay Replay;

        public override Replay Generate()
        {
            // 添加初始帧
            Replay.Frames.Add(new SuperMarioReplayFrame(-100000, MARIO_START_X, GROUND_Y));
            
            // 获取所有敌人物件
            var enemies = new List<ReplayEnemy>();
            foreach (var obj in Beatmap.HitObjects)
            {
                if (obj is SuperMarioHitObject smbObj)
                {
                    float x = SPAWN_X - (float)(obj.StartTime * 60); // 假设速度为60px/s
                    enemies.Add(new ReplayEnemy
                    {
                        Time = obj.StartTime,
                        X = x,
                        ObjectType = smbObj.ObjectType
                    });
                }
            }
            
            // 按时间排序
            enemies.Sort((a, b) => a.Time.CompareTo(b.Time));
            
            // 生成回放帧
            double lastTime = -100000;
            float currentX = MARIO_START_X;
            float currentY = GROUND_Y;
            bool isJumping = false;
            
            foreach (var enemy in enemies)
            {
                // 生成从上一个时间点到敌人出现时间点的帧
                double frameTime = enemy.Time - 2000; // 提前2秒开始预测
                
                if (frameTime > lastTime)
                {
                    // 决定是否起跳
                    bool shouldJump = false;
                    float distanceToEnemy = enemy.X - currentX;
                    
                    // 如果是 Spiny，且距离足够近，需要起跳
                    if (enemy.ObjectType == SuperMarioObjectType.Spiny && distanceToEnemy < JUMP_DETECT_DISTANCE && distanceToEnemy > 0)
                    {
                        shouldJump = true;
                    }
                    
                    // 添加帧
                    Replay.Frames.Add(new SuperMarioReplayFrame(
                        frameTime,
                        currentX,
                        currentY,
                        shouldJump,
                        false
                    ));
                    
                    lastTime = frameTime;
                    
                    // 如果起跳了，更新状态
                    if (shouldJump)
                    {
                        isJumping = true;
                    }
                }
            }
            
            return Replay;
        }
        
        private class ReplayEnemy
        {
            public double Time { get; set; }
            public float X { get; set; }
            public SuperMarioObjectType ObjectType { get; set; }
        }
    }
}
