using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.SuperMarioBros.Replays
{
    /// <summary>
    /// SuperMarioReplayRecorder - 录制玩家输入
    /// </summary>
    public partial class SuperMarioReplayRecorder : ReplayRecorder<SuperMarioAction>
    {
        public SuperMarioReplayRecorder(Score score)
            : base(score)
        {
        }

        protected override ReplayFrame HandleFrame(Vector2 mousePosition, List<SuperMarioAction> actions, ReplayFrame previousFrame)
        {
            // 获取当前时间
            double currentTime = Time.Current;
            
            // 如果时间为0或负数，使用上一帧的时间
            if (currentTime <= 0 && previousFrame != null)
                currentTime = previousFrame.Time;
            
            // 从 mousePosition 中提取 X, Y 坐标
            float x = mousePosition.X;
            float y = mousePosition.Y;

            // 检测 Jump 和 Dash 状态
            bool jump = actions.Contains(SuperMarioAction.Jump);
            bool dash = actions.Contains(SuperMarioAction.Dash);

            return new SuperMarioReplayFrame(currentTime, x, y, jump, dash);
        }
    }
}
