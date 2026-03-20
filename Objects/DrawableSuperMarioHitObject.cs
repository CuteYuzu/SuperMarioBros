using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;
using System;
using osu.Framework.Allocation;

namespace osu.Game.Rulesets.SuperMarioBros.Objects
{
    /// <summary>
    /// 敌人状态
    /// </summary>
    public enum EnemyState { Normal, Squashed, Shell, MovingShell }

    /// <summary>
    /// DrawableSuperMarioHitObject - AR公式校准版
    /// </summary>
    public partial class DrawableSuperMarioHitObject : DrawableHitObject<SuperMarioHitObject>
    {
        private Drawable fallbackBox;
        private Box borderBox;
        private static SuperMarioTextureStore? textureStore;
        
        // 状态
        private EnemyState state = EnemyState.Normal;
        private bool isKicked;
        private float shellVelocityX;
        
        // 物理参数
        private double duration = 3.0; // AR公式: Math.Max(1.3, 8.0 - AR*0.6)
        private float playfieldWidth = 1024f;
        private float judgmentX = 100f;
        private float spawnX = 1200f;
        private float despawnX = -100f;
        
        // 动态速度继承：滑条速度
        private float sliderVelocity = 1.0f;
        private const float BaseShellSpeed = 60f;
        
        // 当前OD值（用于Spiny判定箱）
        private float currentOD = 5f;
        public float CurrentOD => currentOD;
        
        // 初始化标记，防止Update中重复初始化
        public bool IsInitialized { get; set; }
        
        public bool IsKicked => isKicked;
        public new EnemyState State => state;
        
        /// <summary>
        /// 获取物件的实际持续时间（毫秒）
        /// </summary>
        public double GetDuration()
        {
            return duration;
        }
        
        public DrawableSuperMarioHitObject(SuperMarioHitObject hitObject)
            : base(hitObject)
        {
            Console.WriteLine($"[SMB] Creating enemy at {hitObject.StartTime}, type={hitObject.ObjectType}");
            
            // 彻底禁用相对属性
            RelativeSizeAxes = Axes.None;
            RelativePositionAxes = Axes.None;
            Size = new Vector2(60, 60);
            
            // 统一使用BottomLeft坐标系
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            AlwaysPresent = true;
            
            AddInternal(borderBox = new Box
            {
                Size = new Vector2(64, 64),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Colour = new Color4(255, 255, 255, 0),
                Depth = 1
            });
            
            // 创建默认的方块作为基础图形
            fallbackBox = new Box
            {
                Size = new Vector2(60, 60),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Colour = new Color4(255, 255, 0, 255),
                AlwaysPresent = true
            };
            
            AddInternal(fallbackBox);
            
            SetAppearance();
        }

        public static void SetTextureStore(SuperMarioTextureStore store) => textureStore = store;

        private void SetAppearance()
        {
            try
            {
                var objType = HitObject?.ObjectType ?? SuperMarioObjectType.Goomba;
                
                // 移除旧的fallbackBox
                RemoveInternal(fallbackBox, false);
                
                // 为Spiny创建三角形，其他敌人保持方块
                if (objType == SuperMarioObjectType.Spiny)
                {
                    // 创建三角形 - 使用完整的命名空间
                    fallbackBox = new osu.Framework.Graphics.Shapes.Triangle
                    {
                        Size = new Vector2(32, 32),
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = new Color4(0, 255, 255, 255), // 刺球浅蓝色
                        AlwaysPresent = true
                    };
                }
                else
                {
                    // 其他敌人保持方块
                    fallbackBox = new Box
                    {
                        Size = new Vector2(32, 32),
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = (state, objType) switch
                        {
                            (EnemyState.Shell, _) => new Color4(0, 255, 0, 255),
                            (EnemyState.MovingShell, _) => new Color4(0, 255, 0, 255),
                            (_, SuperMarioObjectType.Goomba) => new Color4(139, 69, 19, 255),
                            (_, SuperMarioObjectType.Koopa) => new Color4(0, 180, 0, 255),
                            (_, SuperMarioObjectType.Coin) => new Color4(255, 215, 0, 255),
                            _ => new Color4(128, 128, 128, 255)
                        },
                        AlwaysPresent = true
                    };
                }
                
                // 设置大小
                Size = (state, objType) switch
                {
                    (EnemyState.Shell, _) => new Vector2(32, 16),
                    (EnemyState.MovingShell, _) => new Vector2(32, 16),
                    (_, SuperMarioObjectType.Coin) => new Vector2(16, 24),
                    _ => new Vector2(32, 32)
                };
                
                fallbackBox.Size = Size;
                borderBox.Size = new Vector2(Size.X + 4, Size.Y + 4);
                
                // 添加新的图形
                AddInternal(fallbackBox);
            }
            catch { }
        }

        private double baseDuration; // 基础持续时间（不受DT/HT影响）
        private double clockRate = 1.0; // 时间流速因子
        
        public void InitializeAR(float ar, float width, float jX, float sX, float od = 5f, double rate = 1.0)
        {
            try
            {
                playfieldWidth = width;
                judgmentX = jX;
                spawnX = sX;
                currentOD = od;
                clockRate = rate;
                
                // 获取SliderVelocity
                if (HitObject != null && HitObject.SliderVelocity > 0)
                {
                    sliderVelocity = HitObject.SliderVelocity;
                }
                
                // 基础AR公式: Math.Max(1.3, 9.0 - AR*0.7)
                baseDuration = 1000 * Math.Max(1.3, 9.0 - (ar * 0.7));
                
                // 持续时间保持原始（不受clockRate影响）
                // 敌人移动速度的调整在Update()中单独处理
                duration = baseDuration;
                
                // 生命周期需要考虑clockRate
                // DT下敌人移动更慢，需要更长时间才能到达despawn点
                double adjustedDuration = duration * clockRate;
                LifetimeStart = HitObject.StartTime - adjustedDuration;
                LifetimeEnd = HitObject.StartTime + adjustedDuration + 1.0;
                
                // 设置Y坐标为地面高度（BottomLeft坐标系下Y=0是地面）
                Y = 0;
                
                Console.WriteLine($"[SMB] Enemy initialized at X={spawnX}, Y={Y}, AR={ar}, OD={od}, Rate={clockRate}, BaseDuration={baseDuration}ms, ActualDuration={duration}ms");
            }
            catch { }
        }

        /// <summary>
        /// 直接触发结果，允许Playfield在任何时刻通过物理碰撞直接结束该物件
        /// </summary>
        public void TriggerResult(HitResult result)
        {
            if (Judged) return;
            // 新版API：直接传入HitResult
            ApplyResult(result);
            
            // 立即触发消失/踩扁动画
            if (result == HitResult.Great)
            {
                PlayStompAnimation();
            }
        }
        
        /// <summary>
        /// 踩扁动画：将Scale.Y缩减至0.3f，停止X轴移动，延迟300ms后Expire
        /// </summary>
        public void PlayStompAnimation()
        {
            if (state == EnemyState.Squashed) return; // 已经踩扁
            
            state = EnemyState.Squashed;
            
            // 压扁效果
            Scale = new Vector2(1f, 1f);
            
            // 停止X轴移动
            if (borderBox != null) borderBox.Alpha = 0.5f;
            if (fallbackBox != null) fallbackBox.Alpha = 0.5f;
            
            // 延迟消失
            this.Delay(300).FadeOut(100).Then().Expire();
        }

        public void OnStomped()
        {
            if (HitObject.ObjectType == SuperMarioObjectType.Koopa && state == EnemyState.Normal)
            {
                state = EnemyState.Shell;
                Scale = new Vector2(1f, 1f);
                SetAppearance();
                return;
            }
            
            // 被踩扁 - 调用动画方法
            PlayStompAnimation();
        }

        public void KickShell()
        {
            if (isKicked) return;
            
            state = EnemyState.MovingShell;
            isKicked = true;
            // 物理速度公式: VelocityX = BaseSpeed * SliderMultiplier * 2
            shellVelocityX = BaseShellSpeed * sliderVelocity * 2f;
            Console.WriteLine($"[SMB] Shell kicked! velocity={shellVelocityX}, sliderVelocity={sliderVelocity}");
            SetAppearance();
        }

        public void MarkHit()
        {
            if (!Judged)
                ApplyResult(HitResult.Perfect);
        }
        
        /// <summary>
        /// Goomba被踩死：变金色，判定Great，增加Combo
        /// </summary>
        public void TriggerGoombaKill()
        {
            if (Judged) return;
            
            state = EnemyState.Squashed;
            
            // 变金色
            if (fallbackBox != null)
                fallbackBox.Colour = new Color4(255, 215, 0, 255); // Gold
            
            // 压扁效果
            Scale = new Vector2(1f, 1f);
            
            // 新版API：直接传入HitResult
            ApplyResult(HitResult.Perfect);
            
            // 延迟消失
            this.Delay(300).FadeOut(100).Then().Expire();
        }
        
        /// <summary>
        /// Koopa被踩死：变浅绿色，判定Perfect（300分）
        /// </summary>
        public void TriggerKoopaKill()
        {
            if (Judged) return;
            
            state = EnemyState.Squashed;
            
            // 变浅绿色
            if (fallbackBox != null)
                fallbackBox.Colour = new Color4(144, 238, 144, 255); // LightGreen
            
            // 压扁效果
            Scale = new Vector2(1f, 1f);
            
            // 新版API：直接传入HitResult
            ApplyResult(HitResult.Great);
            
            // 延迟消失
            this.Delay(300).FadeOut(100).Then().Expire();
        }

        protected override void Update()
        {
            if (!IsLoaded) return;
            
            try
            {
                base.Update();
                
                // 移动龟壳
                if (state == EnemyState.MovingShell)
                {
                    float dt = (float)Math.Max(0, Time.Elapsed) / 1000f;
                    float newX = X + shellVelocityX * dt;
                    if (float.IsFinite(newX)) X = newX;
                    
                    if (X > playfieldWidth * 2)
                        Expire();
                    return;
                }
                
                // 龟壳/被踩扁不移动
                if (state == EnemyState.Shell || state == EnemyState.Squashed)
                    return;
                
                // 正常敌人移动
                if (duration <= 0) return;
                
                // 进度: 0=spawnX, 1=judgmentX
                // 使用真实经过时间来计算移动（不受clockRate影响）
                // 这样敌人在DT下会以原始速度移动
                double elapsedRealTime = (Time.Current - HitObject.StartTime) / clockRate;
                float progress = (float)(elapsedRealTime / duration);
                float clampedProgress = Math.Clamp(progress, -0.5f, 1.5f);
                
                // 从spawnX移动到despawnX（原始速度）
                float totalDistance = spawnX - despawnX;
                X = spawnX - (clampedProgress * totalDistance);
                
                if (!float.IsFinite(X)) X = spawnX;
            }
            catch { }
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            // 强制空间判定：禁用法定时机判定
            // 无论timeOffset是多少，都允许正常判定
            if (Judged) return;
            
            // 允许在任何时刻进行判定（时间驱动不再是限制）
            if (userTriggered)
            {
                ApplyResult(HitResult.Perfect);
                return;
            }
            
            // 只有在物件真正离开屏幕时才判定Miss
            if (X < despawnX - 100)
            {
                // 如果是Goomba离开屏幕，则返回一个OK
                if (HitObject.ObjectType == SuperMarioObjectType.Goomba)
                {
                    ApplyResult(HitResult.Ok);
                }
                else
                {
                    ApplyResult(HitResult.Miss);
                }
                
            }
        }
    }
}