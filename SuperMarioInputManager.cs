using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osuTK.Input;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.SuperMarioBros.UI;
using osu.Game.Rulesets.Replays;
using osu.Game.Input.Handlers;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.ClicksPerSecond;
using osu.Game.Scoring;
using System;
using System.Linq;

namespace osu.Game.Rulesets.SuperMarioBros
{
    /// <summary>
    /// SuperMarioAction - 马里奥动作
    /// </summary>
    public enum SuperMarioAction
    {
        MoveLeft,
        MoveRight,
        Jump,
        Dash,
    }

    /// <summary>
    /// SuperMarioInputManager - 输入管理器
    /// </summary>
    [Cached]
    public partial class SuperMarioInputManager : RulesetInputManager<SuperMarioAction>, IHasReplayHandler, IHasRecordingHandler
    {
        private MarioCharacter? mario;
        
        /// <summary>
        /// Replay 录制器
        /// </summary>
        public ReplayRecorder? Recorder { get; set; }
        
        /// <summary>
        /// 按键状态变化回调 - Action<SuperMarioAction, bool> (动作, 是否按下)
        /// </summary>
        public Action<SuperMarioAction, bool>? OnKeyStateChanged;
        
        public SuperMarioInputManager(RulesetInfo ruleset)
            : base(ruleset, 0, SimultaneousBindingMode.Unique)
        {
        }

        public void SetMario(MarioCharacter character)
        {
            mario = character;
        }

        protected override bool OnKeyDown(osu.Framework.Input.Events.KeyDownEvent e)
        {
            // 忽略按键重复（按住不放时的自动重复）
            if (e.Repeat) return false;
            
            var action = GetActionForKey(e.Key);
            if (action.HasValue)
            {
                if (mario != null)
                    mario.HandlePressed(action.Value);
                
                // 触发按键状态变化回调
                OnKeyStateChanged?.Invoke(action.Value, true);
                return true;
            }
            return false;
        }

        protected override void OnKeyUp(osu.Framework.Input.Events.KeyUpEvent e)
        {
            var action = GetActionForKey(e.Key);
            if (action.HasValue)
            {
                if (mario != null)
                    mario.HandleReleased(action.Value);
                
                // 触发按键状态变化回调
                OnKeyStateChanged?.Invoke(action.Value, false);
            }
        }

        private SuperMarioAction? GetActionForKey(Key key)
        {
            return key switch
            {
                Key.Left => SuperMarioAction.MoveLeft,
                Key.Right => SuperMarioAction.MoveRight,
                Key.Z => SuperMarioAction.Jump,
                Key.X => SuperMarioAction.Dash,
                _ => null
            };
        }

        #region ICanAttachHUDPieces 实现

        /// <summary>
        /// 附加 InputCountController 到输入管理器
        /// </summary>
        public void Attach(InputCountController inputCountController)
        {
            // 从默认按键绑定创建按键计数器触发器
            var triggers = KeyBindingContainer.DefaultKeyBindings
                .Select(b => b.GetAction<SuperMarioAction>())
                .Distinct()
                .Select(action => new SuperMarioKeyCounterActionTrigger(action))
                .ToArray();

            // 添加到 KeyBindingContainer 和 InputCountController
            KeyBindingContainer.AddRange(triggers);
            inputCountController.AddRange(triggers);
        }

        /// <summary>
        /// 附加 ClicksPerSecondController 到输入管理器
        /// </summary>
        public void Attach(ClicksPerSecondController controller)
        {
            KeyBindingContainer.Add(new SuperMarioActionListener(controller));
        }

        #endregion
    }

    /// <summary>
    /// SuperMario 按键计数器动作触发器
    /// </summary>
    public partial class SuperMarioKeyCounterActionTrigger : KeyCounterActionTrigger<SuperMarioAction>
    {
        public SuperMarioKeyCounterActionTrigger(SuperMarioAction action)
            : base(action)
        {
        }
    }

    /// <summary>
    /// SuperMario 动作监听器 - 用于统计每秒点击次数
    /// </summary>
    public partial class SuperMarioActionListener : Component, IKeyBindingHandler<SuperMarioAction>
    {
        private readonly ClicksPerSecondController controller;

        public SuperMarioActionListener(ClicksPerSecondController controller)
        {
            this.controller = controller;
        }

        public bool OnPressed(KeyBindingPressEvent<SuperMarioAction> e)
        {
            controller.AddInputTimestamp();
            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<SuperMarioAction> e)
        {
        }
    }
}
