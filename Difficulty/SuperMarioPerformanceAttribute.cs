using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.SuperMarioBros.Difficulty
{
    /// <summary>
    /// SuperMario Performance Attributes - 用于在osu!lazer中显示PP Breakdown
    /// </summary>
    public class SuperMarioPerformanceAttribute : PerformanceAttributes
    {
        [JsonProperty("movement")]
        public double Movement { get; set; }
        
        [JsonProperty("reading")]
        public double Reading { get; set; }
        
        [JsonProperty("precision")]
        public double Precision { get; set; }
        
        [JsonProperty("accuracy")]
        public double Accuracy { get; set; }
        
        [JsonProperty("achieved_pp")]
        public double AchievedPP { get; set; }
        
        [JsonProperty("maximum")]
        public double Maximum { get; set; }
        
        public override IEnumerable<PerformanceDisplayAttribute> GetAttributesForDisplay()
        {
            foreach (var attribute in base.GetAttributesForDisplay())
            {
                yield return attribute;
            }

            yield return new PerformanceDisplayAttribute(nameof(Movement), "Movement", Movement);
            yield return new PerformanceDisplayAttribute(nameof(Reading), "Reading", Reading);
            yield return new PerformanceDisplayAttribute(nameof(Precision), "Precision", Precision);
            yield return new PerformanceDisplayAttribute(nameof(Accuracy), "Accuracy", Accuracy);
            yield return new PerformanceDisplayAttribute(nameof(AchievedPP), "Achieved PP", AchievedPP);
            yield return new PerformanceDisplayAttribute(nameof(Maximum), "Maximum", Maximum);
        }
    }
}
