using System.Collections.Generic;
using osu.Framework.Input.StateChanges;
using osu.Framework.Utils;
using osu.Game.Replays;
using osu.Game.Rulesets.Replays;
using osuTK;

namespace osu.Game.Rulesets.SuperMarioBros.Replays
{
    /// <summary>
    /// SuperMarioFramedReplayInputHandler - 处理回放输入
    /// </summary>
    public class SuperMarioFramedReplayInputHandler : FramedReplayInputHandler<SuperMarioReplayFrame>
    {
        public SuperMarioFramedReplayInputHandler(Replay replay)
            : base(replay)
        {
        }

        protected override bool IsImportant(SuperMarioReplayFrame frame)
        {
            // 任何按键变化都是重要的
            return frame.Jump || frame.Dash || frame.Actions.Count > 0;
        }

        protected override void CollectReplayInputs(List<IInput> inputs)
        {
            // 插值计算当前位置
            float x = Interpolation.ValueAt(CurrentTime, StartFrame?.X ?? 200f, EndFrame?.X ?? 200f, StartFrame?.Time ?? 0, EndFrame?.Time ?? 0);
            float y = Interpolation.ValueAt(CurrentTime, StartFrame?.Y ?? 468f, EndFrame?.Y ?? 468f, StartFrame?.Time ?? 0, EndFrame?.Time ?? 0);

            // 获取当前帧的按键状态
            var actions = CurrentFrame?.Actions ?? new List<SuperMarioAction>();

            // 添加按键状态输入（Jump, Dash 等）
            inputs.Add(new ReplayState<SuperMarioAction>
            {
                PressedActions = actions
            });
        }
    }
}
