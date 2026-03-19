using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osuTK.Input;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.SuperMarioBros.UI;
using System;

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
    public partial class SuperMarioInputManager : RulesetInputManager<SuperMarioAction>
    {
        private MarioCharacter? mario;
        
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
            var action = GetActionForKey(e.Key);
            if (action.HasValue && mario != null)
            {
                mario.HandlePressed(action.Value);
                return true;
            }
            return false;
        }

        protected override void OnKeyUp(osu.Framework.Input.Events.KeyUpEvent e)
        {
            var action = GetActionForKey(e.Key);
            if (action.HasValue && mario != null)
            {
                mario.HandleReleased(action.Value);
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
    }
}
