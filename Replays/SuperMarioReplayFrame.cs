using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Rulesets.SuperMarioBros;

namespace osu.Game.Rulesets.SuperMarioBros.Replays
{
    /// <summary>
    /// Replay Frame - 回放帧
    /// </summary>
    public class SuperMarioReplayFrame : ReplayFrame, IConvertibleReplayFrame
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

        /// <summary>
        /// 从旧版回放帧转换（用于导入 .osr 文件）
        /// </summary>
        public void FromLegacy(LegacyReplayFrame currentFrame, IBeatmap beatmap, ReplayFrame? lastFrame = null)
        {
            X = currentFrame.Position.X;
            Y = currentFrame.Position.Y;
            
            // 转换按钮状态
            Actions.Clear();
            
            // 映射旧版按钮到 SuperMarioAction
            // 注意：这是简化版本，根据需要调整
            if (currentFrame.MouseLeft || currentFrame.MouseRight)
            {
                // 假设左键/右键 = Jump
                Actions.Add(SuperMarioAction.Jump);
            }
        }

        /// <summary>
        /// 转换为旧版回放帧（用于导出 .osr 文件）
        /// </summary>
        public LegacyReplayFrame ToLegacy(IBeatmap beatmap)
        {
            ReplayButtonState state = ReplayButtonState.None;
            
            if (Actions.Contains(SuperMarioAction.Jump))
                state |= ReplayButtonState.Left1;
            if (Actions.Contains(SuperMarioAction.Dash))
                state |= ReplayButtonState.Right1;
            
            return new LegacyReplayFrame(Time, X, Y, state);
        }

        /// <summary>
        /// 检查两个帧是否等价
        /// </summary>
        public override bool IsEquivalentTo(ReplayFrame other)
        {
            if (!(other is SuperMarioReplayFrame frame))
                return false;
            
            return Time == frame.Time 
                && X == frame.X 
                && Y == frame.Y 
                && Jump == frame.Jump 
                && Dash == frame.Dash
                && Actions.SequenceEqual(frame.Actions);
        }
    }
}
