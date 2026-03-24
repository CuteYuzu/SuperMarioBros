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
using osu.Framework.Allocation;
using osuTK;
using osuTK.Graphics;
using System;
using System.Linq;

namespace osu.Game.Rulesets.SuperMarioBros
{
    /// <summary>
    /// SuperMarioPlayfield - 游戏区域
    /// </summary>
    [Cached]
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
        
        /// <summary>
        /// 获取 ScoreProcessor（供外部调用获取实时 PP）
        /// </summary>
        public SuperMarioScoreProcessor? GetScoreProcessor() => scoreProcessor;
        
        private float currentAR = 5f;
        private float playfieldWidth = 1024f;

        [BackgroundDependencyLoader(true)]
        private void load(ScoreProcessor? scoreProc)
        {
            if (scoreProc is SuperMarioScoreProcessor smbSP)
            {
                scoreProcessor = smbSP;
                Console.WriteLine("[SMB] ScoreProcessor connected via DI!");
                
                // 如果有待设置的难度属性，现在设置
                if (pendingDifficultyAttributes != null)
                {
                    scoreProcessor.SetMaxPPValues(
                        pendingDifficultyAttributes.MovementPP,
                        pendingDifficultyAttributes.ReadingPP,
                        pendingDifficultyAttributes.PrecisionPP,
                        pendingDifficultyAttributes.MaxCombo);
                    Console.WriteLine($"[SMB] Pending difficulty applied after ScoreProcessor connected! MaxCombo={pendingDifficultyAttributes.MaxCombo}");
                    pendingDifficultyAttributes = null;
                }
            }
        }

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

        /// <summary>
        /// 设置最大PP值（用于实时PP计算）
        /// </summary>
        public void SetScoreProcessorMaxPP(double movement, double reading, double precision, int totalObjects)
        {
            if (scoreProcessor != null)
            {
                scoreProcessor.SetMaxPPValues(movement, reading, precision, totalObjects);
            }
        }

        // 待设置的难度属性（当 ScoreProcessor 连接后自动设置）
        private SuperMarioDifficultyAttributes? pendingDifficultyAttributes;
        
        public void SetDifficultyAttributes(SuperMarioDifficultyAttributes attrs)
        {
            // 存储难度属性用于PP计算
            currentDifficulty = attrs;
            
            // 同时设置 ScoreProcessor 的总物件数（用于 PP 进度计算）
            if (scoreProcessor != null)
            {
                scoreProcessor.SetMaxPPValues(attrs.MovementPP, attrs.ReadingPP, attrs.PrecisionPP, attrs.MaxCombo);
                Console.WriteLine($"[SMB] Difficulty set with ScoreProcessor: SR={attrs.StarRating:F2}, MaxCombo={attrs.MaxCombo}");
            }
            else
            {
                // ScoreProcessor 还未连接，保存待设置
                pendingDifficultyAttributes = attrs;
                Console.WriteLine($"[SMB] Difficulty saved pending ScoreProcessor: SR={attrs.StarRating:F2}, MaxCombo={attrs.MaxCombo}");
            }
            
            Console.WriteLine($"[SMB] Difficulty set: SR={attrs.StarRating:F2}, Movement={attrs.MovementPP:F2}, Reading={attrs.ReadingPP:F2}, Precision={attrs.PrecisionPP:F2}, MaxCombo={attrs.MaxCombo}");
        }
        
        private SuperMarioDifficultyAttributes? currentDifficulty;

        /// <summary>
        /// 获取实时PP值
        /// </summary>
        public (double movement, double reading, double precision, double accuracy, double total) GetCurrentPP()
        {
            if (currentDifficulty == null)
                return (0, 0, 0, 0, 0);
            
            // 根据进度计算实时PP
            double progress = GetProgress();
            double accuracyFactor = scoreProcessor?.CurrentAccuracy ?? 1.0;
            
            // 当前PP = 最大PP * 进度 * 准确率
            double movement = currentDifficulty.MovementPP * progress * accuracyFactor;
            double reading = currentDifficulty.ReadingPP * progress * accuracyFactor;
            double precision = currentDifficulty.PrecisionPP * progress * accuracyFactor;
            double accuracy = currentDifficulty.AccuracyPP * accuracyFactor;
            double total = Math.Pow(
                Math.Pow(movement, 1.1) + Math.Pow(reading, 1.1) + 
                Math.Pow(precision, 1.1) + Math.Pow(accuracy, 1.1), 
                1.0 / 1.1);
            
            return (movement, reading, precision, accuracy, total);
        }

        /// <summary>
        /// 获取最大PP值
        /// </summary>
        public (double movement, double reading, double precision, double accuracy, double total) GetMaxPP()
        {
            if (currentDifficulty == null)
                return (0, 0, 0, 0, 0);
            
            // 计算最大PP（SS时）
            double maxAccuracy = currentDifficulty.AccuracyPP;
            double totalPP = Math.Pow(
                Math.Pow(currentDifficulty.MovementPP, 1.1) + 
                Math.Pow(currentDifficulty.ReadingPP, 1.1) + 
                Math.Pow(currentDifficulty.PrecisionPP, 1.1) + 
                Math.Pow(maxAccuracy, 1.1), 
                1.0 / 1.1);
            
            return (currentDifficulty.MovementPP, currentDifficulty.ReadingPP, 
                    currentDifficulty.PrecisionPP, maxAccuracy, totalPP);
        }
        
        /// <summary>
        /// 获取当前进度（0-1）- 使用物件处理进度
        /// </summary>
        private double GetProgress()
        {
            if (currentDifficulty == null || currentDifficulty.MaxCombo <= 0)
                return 0;
            
            // 使用ScoreProcessor追踪的已处理物件数
            int processed = scoreProcessor?.ProcessedObjectCount ?? 0;
            return Math.Min(1.0, (double)processed / currentDifficulty.MaxCombo);
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

            // OnStomp 只处理连续踩踏链，不增加 Combo
            mario.OnStomp += (score) => scoreProcessor?.OnStomp();
            mario.OnOneUp += () => Console.WriteLine("[SMB] 1UP!");
        }

        private float currentOD = 5f;
  
        public void SetOverallDifficulty(float od) => currentOD = od;
        
        // 时钟速率（用于调整敌人移动速度）
        private double currentClockRate = 1.0;
        
        public void SetClockRate(double rate) => currentClockRate = rate;

        /// <summary>
        /// 重置所有敌人的状态（当 seek 时调用）
        /// </summary>
        public void ResetAllEnemies()
        {
            Console.WriteLine("[SMB] Resetting all enemies...");
            lastObjectEndTime = 0;
            
            foreach (var hitObject in AllHitObjects)
            {
                if (hitObject is DrawableSuperMarioHitObject enemy)
                {
                    // 重置敌人的状态
                    enemy.ResetForSeek();
                }
            }
            
            // 重新初始化所有敌人
            foreach (var hitObject in AllHitObjects)
            {
                if (hitObject is DrawableSuperMarioHitObject enemy && !enemy.IsInitialized)
                {
                    enemy.InitializeAR(currentAR, playfieldWidth, JUDGMENT_X, SPAWN_X, currentOD, currentClockRate);
                    enemy.IsInitialized = true;
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            // 避免在Update中频繁调用InitializeAR，使用标记确保只初始化一次
            foreach (var hitObject in AllHitObjects)
            {
                if (hitObject is DrawableSuperMarioHitObject enemy && !enemy.IsInitialized)
                {
                    enemy.InitializeAR(currentAR, playfieldWidth, JUDGMENT_X, SPAWN_X, currentOD, currentClockRate);
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
                    // 使用AR计算的实际持续时间，而非SliderDuration
                    double duration = enemy.GetDuration();
                    double objEndTime = enemy.HitObject.StartTime + duration;
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
                        // 添加 seek 冷却时间检查
                        if (!enemy.IsInSeekCooldown() && CheckGoombaCollision(Mario, enemy))
                        {
                            enemy.TriggerGoombaKill();
                            // 手动更新已处理物件计数（用于 PP 进度计算）
                            scoreProcessor?.OnObjectProcessed(true);
                            Console.WriteLine($"[SMB] Goomba killed at X={enemy.X}");
                        }
                        break;

                    case SuperMarioObjectType.Koopa:
                        // Koopa判定：Mario的X坐标 > Koopa的X坐标 = 击杀
                        // 即Mario跑到了Koopa前面
                        // 添加时间检查：确保游戏开始后至少0.5秒才触发，避免开局误判
                        // 添加 seek 冷却时间检查
                        if (enemyState == EnemyState.Normal &&
                            !enemy.IsInSeekCooldown() &&
                            Mario.X > enemy.X &&
                            Time.Current > enemy.HitObject.StartTime)
                        {
                            enemy.TriggerKoopaKill();
                            // 手动更新已处理物件计数
                            scoreProcessor?.OnObjectProcessed(true);
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
                                // 标记为处理过（Miss）
                                scoreProcessor?.OnObjectProcessed(false);
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
                                        scoreProcessor?.OnObjectProcessed(false);
                                    }
                                }
                            }
                            // 撞到Mario（无敌时不受伤）
                            if (!Mario.IsInvincible && CheckAABBCollision(Mario, enemy))
                            {
                                Mario.TakeDamage();
                                enemy.TriggerResult(HitResult.Miss);
                                scoreProcessor?.ResetCombo();
                                scoreProcessor?.OnObjectProcessed(false);
                            }
                        }
                        break;

                    case SuperMarioObjectType.Spiny:
                        // 添加 seek 冷却时间检查
                        if (enemy.IsInSeekCooldown()) break;
                        
                        // Spiny判定：碰撞 = 受伤消失，躲避成功 = Perfect + Combo
                        // Auto模式下：不触发Miss，只触发Perfect
                        if (Mario.IsAutoMode)
                        {
                            // Auto模式：成功躲避（到达屏幕左侧X<0）
                            if (enemy.X < -50 && !enemy.AllJudged)
                            {
                                enemy.TriggerResult(HitResult.Perfect);
                                scoreProcessor?.OnObjectProcessed(true);
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
                            scoreProcessor?.OnObjectProcessed(false);
                            Console.WriteLine($"[SMB] Spiny hit - Mario {(Mario.IsInvincible ? "invincible" : "damaged")}");
                        }
                        // 成功躲避（到达屏幕左侧X<0且未碰撞）
                        else if (enemy.X < -50 && !enemy.AllJudged)
                        {
                            enemy.TriggerResult(HitResult.Perfect);
                            scoreProcessor?.OnObjectProcessed(true);
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
