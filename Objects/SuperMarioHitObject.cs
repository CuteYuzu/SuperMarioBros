using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.SuperMarioBros
{
    /// <summary>
    /// SuperMarioHitObject - 基础物件类
    /// </summary>
    public class SuperMarioHitObject : HitObject
    {
        public SuperMarioObjectType ObjectType { get; set; }
        public JudgementType JudgementType { get; set; }
        public bool IsSlider { get; set; }
        public double SliderDuration { get; set; }
        public double SliderEndTime { get; set; }
        
        // 动态速度继承：将滑条的SliderVelocity传递给龟壳
        public float SliderVelocity { get; set; } = 1.0f;
    }

    public enum SuperMarioObjectType
    {
        Goomba, Koopa, Spiny, PiranhaPlant, Blooper, CheepCheep, BulletBill,
        BuzzyBeetle, HammerBro, Lakitu, Bowser, KoopaShell, QuestionBlock,
        BrickBlock, Pipe, Vine, Coin, Mushroom, FireFlower, Star, Flag, Boss
    }

    public enum JudgementType
    {
        Stomp, Fireball, Collect, ShellKick, HeadBonk, Pass
    }
}
