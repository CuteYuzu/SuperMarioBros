using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.SuperMarioBros.UI;
using osu.Game.Rulesets.SuperMarioBros.Objects;
using osu.Game.Rulesets.SuperMarioBros.Difficulty;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;
using System;
using System.Linq;

namespace osu.Game.Rulesets.SuperMarioBros
{
    /// <summary>
    /// SuperMarioPlayfield - 游戏区域
    /// </summary>
    public partial class SuperMarioPlayfield : Playfield
    {
        // BottomLeft坐标系：0,0是左下角地面
        public const float JUDGMENT_X = 100f;
        public const float GROUND_Y = 0f;  // BottomLeft坐标系下0就是地面
        public const float SPAWN_X = 1400f;

        public MarioCharacter? Mario { get; private set; }

        private Container? mainContainer;
        private Box? groundBox;

        private SuperMarioScoreProcessor? scoreProcessor;
        private float currentAR = 5f;
        private float playfieldWidth = 1024f;

        public SuperMarioPlayfield()
        {
            RelativeSizeAxes = Axes.Both;
            Size = Vector2.One;  // 强制固定尺寸

            AddInternal(mainContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft
            });

            mainContainer.Add(groundBox = new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 50f,
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Colour = new Color4(0, 200, 0, 48) // 透明地面，实际碰撞由MarioCharacter处理
            });
        }

        public void SetScoreProcessor(SuperMarioScoreProcessor processor)
        {
            scoreProcessor = processor;
        }

        public void SetDifficultyAttributes(SuperMarioDifficultyAttributes attrs)
        {
            // 难度属性已设置
            Console.WriteLine($"[SMB] Difficulty set: MaxPP={attrs.MaxPP}, SR={attrs.StarRating:F2}");
        }

        public void SetApproachRate(float ar) => currentAR = ar;
        public void SetPlayfieldSize(float width, float height) => playfieldWidth = width;

        public void SetMario(MarioCharacter mario)
        {
            Mario = mario;
            // 添加到mainContainer而不是HitObjectContainer，确保Mario在怪物层之上
            mainContainer?.Add(mario);

            // 注意：Depth在MarioCharacter构造函数中设置，这里不再修改
            // 以免触发布局刷新导致递归

            // 地面设置
            mario.GroundY = GROUND_Y;
            mario.LeftBound = 0;
            mario.RightBound = playfieldWidth - 40;

            // 传递OD值用于计算无敌时长
            mario.SetOverallDifficulty(currentOD);

            mario.OnStomp += (score) => scoreProcessor?.OnStomp();
            mario.OnOneUp += () => Console.WriteLine("[SMB] 1UP!");
        }

        private float currentOD = 5f;

        public void SetOverallDifficulty(float od) => currentOD = od;

        protected override void Update()
        {
            base.Update();

            // 避免在Update中频繁调用InitializeAR，使用标记确保只初始化一次
            foreach (var hitObject in AllHitObjects)
            {
                if (hitObject is DrawableSuperMarioHitObject enemy && !enemy.IsInitialized)
                {
                    enemy.InitializeAR(currentAR, playfieldWidth, JUDGMENT_X, SPAWN_X, currentOD);
                    enemy.IsInitialized = true;
                }
            }

            CheckCollisions();
            
            // 自动结束检测：当所有物件均已判定且时间超过最后一个物件的EndTime
            CheckForAutoComplete();
        }

        private double lastObjectEndTime = 0;

        private void CheckForAutoComplete()
        {
            // 记录最后一个物件的时间
            foreach (var hitObject in AllHitObjects)
            {
                if (hitObject is DrawableSuperMarioHitObject enemy)
                {
                    double objEndTime = enemy.HitObject.StartTime + enemy.HitObject.SliderDuration;
                    if (objEndTime > lastObjectEndTime)
                        lastObjectEndTime = objEndTime;
                }
            }

            // 如果所有物件都已判定，且当前时间超过最后一个物件结束时间+1秒
            if (AllHitObjects.All(h => h.AllJudged) && AllHitObjects.Count() > 0)
            {
                if (Time.Current > lastObjectEndTime + 1000)
                {
                    Console.WriteLine($"[SMB] All objects judged at {Time.Current}, triggering completion...");

                    // 强制设置HealthProcessor为最大值触发完成
                    // 通过ScoreProcessor触发结算
                    if (scoreProcessor != null)
                    {
                        // 标记为已完成
                        scoreProcessor.OnDeath();
                    }
                }
            }
        }

        private void CheckCollisions()
        {
            if (Mario == null || Mario.IsDead) return;

            foreach (var hitObject in AllHitObjects.ToList())
            {
                // 关键：必须检查AllJudged、IsAlive和LifetimeEnd，防止重复触发判定
                if (hitObject is not DrawableSuperMarioHitObject enemy ||
                    enemy.AllJudged ||
                    !enemy.IsAlive ||
                    enemy.LifetimeEnd <= Time.Current)
                    continue;

                var objType = enemy.HitObject.ObjectType;
                var enemyState = enemy.State;

                // 根据怪物类型使用不同的判定逻辑
                // 注意：无论Mario是否无敌，都可以正常击杀怪物
                switch (objType)
                {
                    case SuperMarioObjectType.Goomba:
                        // Goomba判定：Mario中心点进入Goomba碰撞箱 = 击杀
                        // 移除击杀后的反弹，保持Mario原有运动轨迹
                        if (CheckGoombaCollision(Mario, enemy))
                        {
                            enemy.TriggerGoombaKill();
                            scoreProcessor?.OnGoombaKill();
                            Console.WriteLine($"[SMB] Goomba killed at X={enemy.X}");
                        }
                        break;

                    case SuperMarioObjectType.Koopa:
                        // Koopa判定：Mario的X坐标 > Koopa的X坐标 = 击杀
                        // 即Mario跑到了Koopa前面
                        if (enemyState == EnemyState.Normal && Mario.X > enemy.X)
                        {
                            enemy.TriggerKoopaKill();
                            scoreProcessor?.OnKoopaKill();
                            Console.WriteLine($"[SMB] Koopa passed at Mario.X={Mario.X}, enemy.X={enemy.X}");
                        }
                        // 龟壳处理（Koopa被踩后变成龟壳）
                        else if (enemyState == EnemyState.Shell)
                        {
                            // 站立时被踢龟壳
                            if (Mario.IsOnGround)
                            {
                                enemy.KickShell();
                                scoreProcessor?.OnShellKick();
                            }
                            // 侧面碰撞受伤（无敌时不受伤）
                            else if (!Mario.IsInvincible)
                            {
                                Mario.TakeDamage();
                                enemy.TriggerResult(HitResult.Miss);
                                scoreProcessor?.ResetCombo();
                            }
                        }
                        else if (enemyState == EnemyState.MovingShell)
                        {
                            // 移动龟壳杀敌
                            foreach (var other in AllHitObjects.ToList())
                            {
                                if (other is DrawableSuperMarioHitObject e && e != enemy && !e.AllJudged)
                                {
                                    if (e.X > enemy.X && e.X < enemy.X + 60)
                                    {
                                        scoreProcessor?.OnShellKick();
                                        e.TriggerResult(HitResult.Miss);
                                        e.Expire();
                                    }
                                }
                            }
                            // 撞到Mario（无敌时不受伤）
                            if (!Mario.IsInvincible && CheckAABBCollision(Mario, enemy))
                            {
                                Mario.TakeDamage();
                                enemy.TriggerResult(HitResult.Miss);
                                scoreProcessor?.ResetCombo();
                            }
                        }
                        break;

                    case SuperMarioObjectType.Spiny:
                        // Spiny判定：碰撞 = 受伤消失，躲避成功 = Perfect + Combo
                        // Auto模式下：不触发Miss，只触发Perfect
                        if (Mario.IsAutoMode)
                        {
                            // Auto模式：成功躲避（到达屏幕左侧X<0）
                            if (enemy.X < -50 && !enemy.AllJudged)
                            {
                                enemy.TriggerResult(HitResult.Perfect);
                                scoreProcessor?.OnSpinyDodged();
                                Console.WriteLine("[SMB] Auto: Spiny dodged!");
                            }
                        }
                        else if (CheckSpinyCollision(Mario, enemy))
                        {
                            // 碰到Spiny：无敌时不扣血，但物件依然消失
                            if (!Mario.IsInvincible)
                            {
                                Mario.TakeDamage();
                                scoreProcessor?.ResetCombo();
                            }
                            enemy.Expire();
                            enemy.TriggerResult(HitResult.Miss);
                            scoreProcessor?.OnSpinyHit();
                            Console.WriteLine($"[SMB] Spiny hit - Mario {(Mario.IsInvincible ? "invincible" : "damaged")}");
                        }
                        // 成功躲避（到达屏幕左侧X<0且未碰撞）
                        else if (enemy.X < -50 && !enemy.AllJudged)
                        {
                            enemy.TriggerResult(HitResult.Perfect);
                            scoreProcessor?.OnSpinyDodged();
                            Console.WriteLine("[SMB] Spiny dodged!");
                        }
                        break;

                    default:
                        // 其他物件使用默认AABB碰撞
                        if (!Mario.IsInvincible && CheckAABBCollision(Mario, enemy))
                        {
                            Mario.TakeDamage();
                            enemy.TriggerResult(HitResult.Miss);
                            scoreProcessor?.ResetCombo();
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Goomba碰撞检测：Mario中心点是否在Goomba碰撞箱内
        /// </summary>
        private bool CheckGoombaCollision(MarioCharacter mario, DrawableSuperMarioHitObject enemy)
        {
            // Mario中心点
            float marioCenterX = mario.X;
            float marioCenterY = mario.Y + mario.DrawHeight / 2;

            // Goomba碰撞箱（60x60）
            float enemyLeft = enemy.X - 30;
            float enemyRight = enemy.X + 30;
            float enemyTop = enemy.Y + 30;
            float enemyBottom = enemy.Y - 30;

            // 检查Mario中心是否在Goomba碰撞箱内
            return marioCenterX > enemyLeft && marioCenterX < enemyRight &&
                   marioCenterY > enemyBottom && marioCenterY < enemyTop;
        }

        /// <summary>
        /// Spiny碰撞检测：使用OD动态判定箱
        /// </summary>
        private bool CheckSpinyCollision(MarioCharacter mario, DrawableSuperMarioHitObject enemy)
        {
            // OD动态判定：OD 0 = 1px, OD 10 = 30px (半长)
            float od = enemy.CurrentOD;
            float hitboxSize = 1f + (od / 10f) * 29f;

            // Spiny判定区域：底部中心
            float spinyCenterX = enemy.X;
            float spinyBottomY = enemy.Y - enemy.DrawHeight / 2;

            // Mario中心点
            float marioCenterX = mario.X;
            float marioBottomY = mario.Y;

            // 检查Mario中心是否在Spiny的判定区域内
            float dx = Math.Abs(marioCenterX - spinyCenterX);
            float dy = marioBottomY - spinyBottomY;

            // 判定区域为hitboxSize x hitboxSize的矩形
            return dx < hitboxSize && dy > -hitboxSize && dy < hitboxSize;
        }

        private bool CheckAABBCollision(MarioCharacter mario, DrawableSuperMarioHitObject enemy)
        {
            // 统一使用60x60尺寸进行碰撞检测
            float marioLeft = mario.X - 30;
            float marioRight = mario.X + 30;
            float marioTop = mario.Y + 60;
            float marioBottom = mario.Y;

            float enemyLeft = enemy.X - 30;
            float enemyRight = enemy.X + 30;
            float enemyTop = enemy.Y + 30;
            float enemyBottom = enemy.Y - 30;

            return marioLeft < enemyRight && marioRight > enemyLeft &&
                   marioBottom < enemyTop && marioTop > enemyBottom;
        }
    }
}
