using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;
using System;
using osu.Framework.Allocation;

namespace osu.Game.Rulesets.SuperMarioBros.UI
{
    /// <summary>
    /// MarioCharacter - 稳定版物理引擎
    /// </summary>
    public partial class MarioCharacter : CompositeDrawable
    {
        // 物理常量
        private const float Gravity = 2700f;
        private const float Acceleration = 5000f;
        private const float Friction = 1200f;
        private const float MaxWalkSpeed = 400f;
        private const float MaxDashSpeed = 600f;
        private const float JumpVelocity = -800f;
        private const float StompBounceVelocity = -500f;

        public float VelocityX { get; private set; }
        public float VelocityY { get; private set; }
        
        public float GroundY { get; set; } = 0f;
        public float LeftBound { get; set; } = 0f;
        public float RightBound { get; set; } = 1024f;
        
        private bool isOnGround;
        private bool isMovingLeft;
        private bool isMovingRight;
        private bool isHoldingJump;
        private bool isDashing;
        
        // 跳跃冷却：必须松开跳跃键后才能再次起跳
        private bool canJump = true;
        // 提前起跳范围（像素）
        private const float EarlyJumpThreshold = 4f;

        public bool IsInvincible { get; private set; }
        private bool isDead;
        public bool IsDead => isDead || health <= 0;
        private int health = 2;
        public int Health => health;
        private double invincibleTimeRemaining;
        
        // Auto模式：无敌
        public bool IsAutoMode { get; set; }
        
        // OD动态无敌时长：3.0s - (OD * 0.2s)，范围1s-3s
        private double invincibleDuration = 2000;
        
        public int StompChain { get; private set; }
        
        // 设置OD值，计算无敌时长
        public void SetOverallDifficulty(float od)
        {
            // OD 0 = 3.0s, OD 10 = 1.0s (线性变化)
            invincibleDuration = (3.0 - (od * 0.2)) * 1000;
            Console.WriteLine($"[SMB] OD={od}, invincibleDuration={invincibleDuration}ms");
        }

        public bool IsOnGround => isOnGround;
        public bool IsFalling => VelocityY > 0 && !isOnGround;

        private MarioActionState currentState = MarioActionState.Idle;
        public MarioActionState State => currentState;

        public event Action<int>? OnStomp;
        public event Action? OnOneUp;

        // 双层渲染：底层 Box + 顶层 Sprite
        private Container? container;
        private Box? fallbackBox;
        private Box? borderBox;

        public MarioCharacter()
        {
            Console.WriteLine("[SMB] MarioCharacter Created");
            
            // 彻底禁用相对属性
            RelativeSizeAxes = Axes.None;
            RelativePositionAxes = Axes.None;
            Size = new Vector2(34, 34);  // 缩小到34x34
            AlwaysPresent = true;
            
            // 设置Depth确保渲染在最上层（只设置一次）
            Depth = -1;
            
            // 使用BottomLeft坐标系（0,0是左下角地面）
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            
            // 内部容器 - 也禁用相对属性
            AddInternal(container = new Container
            {
                Size = new Vector2(34, 34),
                RelativeSizeAxes = Axes.None,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AlwaysPresent = true
            });
            
            // 底层白色边框
            container.Add(borderBox = new Box
            {
                Size = new Vector2(34, 34),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Colour = new Color4(255, 255, 255, 255)
            });
            
            // 橙色主体
            container.Add(fallbackBox = new Box
            {
                Size = new Vector2(34, 34),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Colour = new Color4(255, 165, 0, 255) // 橙色
            });
        }
        
        /// <summary>
        /// 在LoadComplete后调用，设置初始位置
        /// </summary>
        public void InitializePosition(float x, float groundY)
        {
            // BottomLeft坐标系：0,0是左下角地面
            // Position就是相对于左下角的坐标
            X = x;
            Y = groundY;
            GroundY = groundY;
            
            // 开局无敌保护
            IsInvincible = true;
            invincibleTimeRemaining = 2000;
            Console.WriteLine($"[SMB] Mario initialized at X={x}, Y={groundY}, grace period: 2s invincibility");
        }

        public void HandlePressed(SuperMarioAction action)
        {
            Console.WriteLine($"[SMB] HandlePressed: {action}");
            
            // 无敌期间也可以移动和跳跃，只是不会再受伤
            if (currentState == MarioActionState.Dead) return;
            
            switch (action)
            {
                case SuperMarioAction.MoveLeft: isMovingLeft = true; break;
                case SuperMarioAction.MoveRight: isMovingRight = true; break;
                case SuperMarioAction.Jump: BeginJump(); break;
                case SuperMarioAction.Dash: isDashing = true; break;
            }
        }

        public void HandleReleased(SuperMarioAction action)
        {
            switch (action)
            {
                case SuperMarioAction.MoveLeft: isMovingLeft = false; break;
                case SuperMarioAction.MoveRight: isMovingRight = false; break;
                case SuperMarioAction.Jump: EndJump(); break;
                case SuperMarioAction.Dash: isDashing = false; break;
            }
        }

        private void BeginJump()
        {
            if (currentState == MarioActionState.Dead) return;
            
            // 检查是否可以跳跃（必须先松开跳跃键）
            if (!canJump) return;
            
            // 优先使用完全着地的状态
            if (isOnGround)
            {
                VelocityY = JumpVelocity;
                isHoldingJump = true;
                isOnGround = false;
                currentState = MarioActionState.Jumping;
                canJump = false;  // 开始跳跃后必须松开键
                return;
            }
            
            // 提前起跳：距离地面4像素内也可以跳
            float distanceToGround = GroundY - Y;
            if (distanceToGround <= EarlyJumpThreshold && VelocityY >= 0)
            {
                VelocityY = JumpVelocity;
                isHoldingJump = true;
                isOnGround = false;
                currentState = MarioActionState.Jumping;
                canJump = false;  // 开始跳跃后必须松开键
            }
        }

        private void EndJump()
        {
            isHoldingJump = false;
            if (VelocityY < 0) VelocityY *= 0.5f;
            
            // 松开跳跃键后，允许再次起跳
            canJump = true;
        }

        public void ApplyStompBounce(bool isJumpHeld = false)
        {
            if (currentState == MarioActionState.Dead) return;
            
            // 按住跳跃键时反弹更高
            float bounceVelocity = isJumpHeld ? StompBounceVelocity * 1.5f : StompBounceVelocity;
            VelocityY = bounceVelocity;
            isOnGround = false;
            currentState = MarioActionState.Jumping;
            
            StompChain++;
            int score = StompChain <= 0 ? 100 : (StompChain > 10 ? 10000 : new[] { 100, 200, 400, 500, 800, 1000, 2000, 4000, 5000, 8000 }[Math.Min(StompChain - 1, 9)]);
            
            if (StompChain == 11)
            {
                StompChain = 0;
                OnOneUp?.Invoke();
            }
            
            OnStomp?.Invoke(score);
        }

        public void TakeDamage()
        {
            // Auto模式或无敌期间不受伤
            if (IsAutoMode || IsInvincible || isDead) return;
            
            // health--;
            // OD无敌系统：根据OD计算无敌时长
            IsInvincible = true;
            invincibleTimeRemaining = invincibleDuration;
            // 注意：不重置VelocityX，保持受伤前的运动惯性
            Console.WriteLine($"[SMB] TakeDamage! Invincible for {invincibleDuration}ms");
        }

        public void Die()
        {
            if (isDead) return;
            health = 0;
            isDead = true;
        }

        /// <summary>
        /// 简化版 Update - 避免任何可能触发布局的属性修改
        /// </summary>
        protected override void Update()
        {
            if (!IsLoaded) return;
            
            try
            {
                base.Update();
                
                float dt = (float)Math.Max(0, Time.Elapsed) / 1000f;
                if (dt <= 0 || dt > 0.1f) dt = 0.016f; // 限制dt范围
                
                // 无敌闪烁 - 只修改Alpha
                if (IsInvincible)
                {
                    invincibleTimeRemaining -= Time.Elapsed;
                    if (invincibleTimeRemaining <= 0)
                    {
                        IsInvincible = false;
                        Alpha = 1f;
                    }
                    else
                    {
                        Alpha = (invincibleTimeRemaining % 200 < 100) ? 0.5f : 1f;
                    }
                }
                
                if (currentState == MarioActionState.Dead)
                {
                    // 死亡时只更新Y
                    VelocityY += Gravity * dt;
                    Y += VelocityY * dt;
                    return;
                }

                // 水平移动 - 只修改X
                float currentMaxSpeed = isDashing ? MaxDashSpeed : MaxWalkSpeed;

                if (isMovingLeft)
                    VelocityX -= Acceleration * dt;
                else if (isMovingRight)
                    VelocityX += Acceleration * dt;
                else
                {
                    if (VelocityX > 0)
                    {
                        VelocityX -= Friction * dt;
                        if (VelocityX < 0) VelocityX = 0;
                    }
                    else if (VelocityX < 0)
                    {
                        VelocityX += Friction * dt;
                        if (VelocityX > 0) VelocityX = 0;
                    }
                }

                VelocityX = Math.Clamp(VelocityX, -currentMaxSpeed, currentMaxSpeed);
                
                // 只修改X，不直接修改Position
                X += VelocityX * dt;

                // 重力 - BottomLeft坐标系下，重力让Y增加（向下）
                float currentGravity = (isHoldingJump && VelocityY < 0) ? Gravity * 0.5f : Gravity;
                VelocityY += currentGravity * dt;
                
                // 只修改Y
                Y += VelocityY * dt;

                // 落地检测 - BottomLeft下Y >= GroundY意味着到了地面
                if (Y >= GroundY)
                {
                    Y = GroundY;
                    VelocityY = 0;
                    isOnGround = true;
                    currentState = Math.Abs(VelocityX) > 10 ? MarioActionState.Running : MarioActionState.Idle;
                    StompChain = 0;
                }
                else
                {
                    currentState = VelocityY < 0 ? MarioActionState.Jumping : MarioActionState.Falling;
                }

                // 边界限制
                if (X < LeftBound) X = LeftBound;
                if (X > RightBound) X = RightBound;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMB] Mario Update error: {ex.Message}");
            }
        }
    }

    public enum MarioActionState { Idle, Running, Jumping, Falling, Dead }
}
