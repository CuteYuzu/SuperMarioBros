using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.SuperMarioBros;
using osu.Game.Rulesets.SuperMarioBros.Beatmaps;
using osu.Game.Rulesets.SuperMarioBros.Objects;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Rulesets;
using osu.Framework.Extensions.IEnumerableExtensions;

namespace osu.Game.Rulesets.SuperMarioBros
{
    /// <summary>
    /// SuperMarioBeatmapConverter - 谱面转换器
    /// </summary>
    public class SuperMarioBeatmapConverter : BeatmapConverter<SuperMarioHitObject>
    {
        public SuperMarioBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
            : base(beatmap, ruleset)
        {
        }

        public override bool CanConvert() => Beatmap.HitObjects.Count > 0;

        protected override Beatmap<SuperMarioHitObject> CreateBeatmap() => new SuperMarioBeatmap();

        protected override IEnumerable<SuperMarioHitObject> ConvertHitObject(HitObject obj, IBeatmap beatmap, CancellationToken cancellationToken)
        {
            // 确定物件类型
            SuperMarioObjectType objectType = SuperMarioObjectType.Goomba;
            JudgementType judgementType = JudgementType.Stomp;
            
            string typeName = obj.GetType().Name;
            float sliderVelocity = 1.0f;
            
            if (obj is IHasDuration)
            {
                // Slider = Koopa
                if (typeName.Contains("Slider"))
                {
                    objectType = SuperMarioObjectType.Koopa;
                    
                    // 获取SliderVelocity（难度参数）
                    if (beatmap.Difficulty.SliderMultiplier > 0)
                    {
                        sliderVelocity = (float)beatmap.Difficulty.SliderMultiplier;
                    }
                }
                // Spinner = Goomba
                else if (typeName.Contains("Spinner"))
                {
                    objectType = SuperMarioObjectType.Goomba;
                }
            }
            else if (typeName.Contains("Circle"))
            {
                objectType = SuperMarioObjectType.Goomba;
            }
            
            // 创建主物件（Koopa）
            var smbObj = new SuperMarioHitObject
            {
                StartTime = obj.StartTime,
                ObjectType = objectType,
                JudgementType = judgementType,
                SliderVelocity = sliderVelocity
            };
            
            // 如果是有时长物件(Slider)，在EndTime生成Spiny
            if (obj is IHasDuration sliderObj && typeName.Contains("Slider"))
            {
                smbObj.IsSlider = true;
                smbObj.SliderDuration = sliderObj.Duration;
                smbObj.SliderEndTime = sliderObj.EndTime;
                
                Console.WriteLine($"[SMB] Slider: Koopa at {obj.StartTime}, velocity={sliderVelocity}, will add Spiny at {sliderObj.EndTime}");
                
                // 必须在EndTime处yield return一个新的Spiny物件
                yield return new SuperMarioHitObject
                {
                    StartTime = sliderObj.EndTime,
                    ObjectType = SuperMarioObjectType.Spiny,
                    JudgementType = JudgementType.Stomp,
                    SliderVelocity = sliderVelocity
                };
            }
            
            yield return smbObj;
        }
    }
}
