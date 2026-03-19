using System.Collections.Generic;
using osu.Game.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.SuperMarioBros;

namespace osu.Game.Rulesets.SuperMarioBros.Replays
{
    /// <summary>
    /// Replay Frame - 回放帧
    /// </summary>
    public class SuperMarioReplayFrame : ReplayFrame
    {
        public float X { get; set; }
        public float Y { get; set; }
        public bool Jump { get; set; }
        public bool Dash { get; set; }
        
        public List<SuperMarioAction> Actions { get; set; } = new List<SuperMarioAction>();
        
        public SuperMarioReplayFrame()
        {
        }

        public SuperMarioReplayFrame(double time, float x, float y, bool jump = false, bool dash = false)
            : base(time)
        {
            X = x;
            Y = y;
            Jump = jump;
            Dash = dash;
            
            // 记录按键状态
            if (Jump)
                Actions.Add(SuperMarioAction.Jump);
            if (Dash)
                Actions.Add(SuperMarioAction.Dash);
        }
    }
}
