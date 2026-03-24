using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.SuperMarioBros.UI;
using osu.Game.Rulesets.SuperMarioBros.Objects;
using osu.Game.Rulesets.SuperMarioBros.Difficulty;
using osu.Game.Rulesets.SuperMarioBros.Mods;
using osu.Game.Rulesets.SuperMarioBros.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Replays;
using osu.Game.Input.Handlers;
using osu.Game.Beatmaps;
using osu.Game.Scoring;
using System.Collections.Generic;
using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Game.Rulesets.Difficulty;
using osu.Framework.Graphics;
using osu.Game.Screens.Play.HUD;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.SuperMarioBros
{
    /// <summary>
    /// DrawableSuperMarioRuleset - 可绘制规则集（osu!风格PP版）
    /// </summary>
    public partial class DrawableSuperMarioRuleset : DrawableRuleset<SuperMarioHitObject>
    {
        public MarioCharacter? Mario { get; private set; }
        private SuperMarioInputManager? inputManager;
        private SuperMarioTextureStore? textureStore;
        private SuperMarioPlayfield? playfield;

        // 难度属性（四大维度PP）
        private double movementPP = 0;
        private double readingPP = 0;
        private double precisionPP = 0;
        private double accuracyPP = 0;  // 用于 Update 中显示

        // 最大PP（理论PP）
        private double maxMovementPP = 0;
        private double maxReadingPP = 0;
        private double maxPrecisionPP = 0;
        private double maxAccuracyPP = 0;
        private double maxTotalPP = 0;

        // 时钟速率（DT=1.5, HT=0.75, 正常=1.0）
        private double clockRate = 1.0;

        // 按键显示
        private SuperMarioKeyCounterDisplay? keyCounterDisplay;

        // PP 显示（已移除，使用日志输出）
        // private SuperMarioPPDisplay? ppDisplay;
        
        // 当前谱面信息
        private string currentMapInfo = "Unknown - Unknown[Unknown]";

        public DrawableSuperMarioRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods)
            : base(ruleset, ConvertBeatmap(beatmap, mods), mods)
        {
            // 设置谱面信息供 Calculator 使用
            var meta = beatmap.BeatmapInfo?.Metadata;
            string artist = meta?.Artist ?? "Unknown";
            string title = meta?.Title ?? "Unknown";
            string diff = beatmap.BeatmapInfo?.DifficultyName ?? "Unknown";
            string mapInfo = $"{artist} - {title}[{diff}]";
            
            SuperMarioDifficultyCalculator.CurrentMapInfo = mapInfo;
            SuperMarioPerformanceCalculator.CurrentMapInfo = mapInfo;
            
            Console.WriteLine($"[SMB] Constructor - {mapInfo}");
            Console.WriteLine($"[SMB] Objects count: {beatmap.HitObjects.Count}");

            // 计算clockRate
            if (mods != null)
            {
                foreach (var mod in mods)
                {
                    if (mod is IApplicableToRate rateMod)
                    {
                        clockRate = rateMod.ApplyToRate(0, 1.0);
                        Console.WriteLine($"[SMB] ClockRate detected: {clockRate}");
                        break;
                    }
                }
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Console.WriteLine("[SMB FATAL] DrawableSuperMarioRuleset load() was CALLED.");

            try
            {
                Mario = new MarioCharacter();
                Console.WriteLine("[SMB] Mario created");

                textureStore = new SuperMarioTextureStore();
                DrawableSuperMarioHitObject.SetTextureStore(textureStore);
                Console.WriteLine("[SMB] TextureStore ready");

                // 创建按键显示（显示在屏幕右上角中间位置）
                keyCounterDisplay = new SuperMarioKeyCounterDisplay
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Right = 20, Top = 100 },
                    AlwaysVisible = true,  // 始终显示
                };

                // 添加到Overlays容器
                Overlays.Add(keyCounterDisplay);
                Console.WriteLine("[SMB] KeyCounterDisplay created");

                // PP 显示已移除，使用日志输出

                // 尝试通过依赖注入获取ScoreProcessor
                // ScoreProcessor应该在Player中被缓存，我们可以通过playfield间接访问
                if (playfield != null)
                {
                    Console.WriteLine("[SMB] Playfield is ready, waiting for ScoreProcessor...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMB] Load error: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前谱面信息（Artist - Title[Difficulty]）
        /// </summary>
        private string GetMapInfo()
        {
            try
            {
                var meta = Beatmap.BeatmapInfo?.Metadata;
                string artist = meta?.Artist ?? "Unknown";
                string title = meta?.Title ?? "Unknown";
                string diff = Beatmap.BeatmapInfo?.DifficultyName ?? "Unknown";
                currentMapInfo = $"{artist} - {title}[{diff}]";
                return currentMapInfo;
            }
            catch
            {
                return currentMapInfo;
            }
        }

        private static IBeatmap ConvertBeatmap(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
        {
            if (beatmap.HitObjects.Count == 0)
                return beatmap;

            var firstObj = beatmap.HitObjects[0];
            if (firstObj is SuperMarioHitObject)
                return beatmap;

            var converted = new Beatmap { BeatmapInfo = beatmap.BeatmapInfo };

            // 保持原始StartTime，不缩放
            // 敌人移动速度的调整在Update()中单独处理

            foreach (var obj in beatmap.HitObjects)
            {
                // 保持原始StartTime
                var smbObj = new SuperMarioHitObject { StartTime = obj.StartTime };
                string typeName = obj.GetType().Name;

                if (typeName.Contains("Circle"))
                    smbObj.ObjectType = SuperMarioObjectType.Goomba;
                else if (typeName.Contains("Slider"))
                    smbObj.ObjectType = SuperMarioObjectType.Koopa;
                else if (typeName.Contains("Spinner"))
                    smbObj.ObjectType = SuperMarioObjectType.Goomba;
                else
                    smbObj.ObjectType = SuperMarioObjectType.Goomba;

                smbObj.JudgementType = JudgementType.Stomp;
                converted.HitObjects.Add(smbObj);
            }

            return converted;
        }

        protected override Playfield CreatePlayfield()
        {
            playfield = new SuperMarioPlayfield();
            return playfield;
        }

        public override DrawableHitObject<SuperMarioHitObject> CreateDrawableRepresentation(SuperMarioHitObject h)
            => new DrawableSuperMarioHitObject(h);

        protected override RulesetInputManager<SuperMarioAction> CreateInputManager()
        {
            inputManager = new SuperMarioInputManager(Ruleset.RulesetInfo);
            return inputManager;
        }

        /// <summary>
        /// 创建 Replay 输入处理器（用于 Auto 和回放）
        /// </summary>
        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay)
        {
            return new SuperMarioFramedReplayInputHandler(replay);
        }
        
        /// <summary>
        /// 创建 Replay 录制器（用于录制玩家操作）
        /// TODO: 暂时禁用，等修复 inputManager 问题后再启用
        /// </summary>
        protected override ReplayRecorder CreateReplayRecorder(Score score)
        {
            return null; // 暂时禁用
            // return new SuperMarioReplayRecorder(score);
        }

        protected override void LoadComplete()
        {
            Console.WriteLine("[SMB CRITICAL] LoadComplete START - This must appear!");
            base.LoadComplete();
            Console.WriteLine("[SMB CRITICAL] LoadComplete AFTER base - This must appear!");

            try
            {
                // 检测是否有Auto Mod
                bool isAutoMode = false;
                if (Mods != null)
                {
                    foreach (var mod in Mods)
                    {
                        if (mod is SuperMarioModAutoplay)
                        {
                            isAutoMode = true;
                            Console.WriteLine("[SMB] Auto Mode detected!");
                            break;
                        }
                    }
                }

                if (inputManager != null && Mario != null)
                {
                    inputManager.SetMario(Mario);

                    // 注册按键状态变化回调，更新按键显示
                    inputManager.OnKeyStateChanged = (action, isPressed) =>
                    {
                        if (keyCounterDisplay != null)
                            keyCounterDisplay.UpdateKeyState(action, isPressed);
                    };
                }

                if (playfield != null && Mario != null)
                {
                    // 设置Auto Mode
                    Mario.IsAutoMode = isAutoMode;
                    playfield.SetMario(Mario);
                    playfield.SetApproachRate(Beatmap.Difficulty.ApproachRate);
                    playfield.SetOverallDifficulty(Beatmap.Difficulty.OverallDifficulty);
                    playfield.SetClockRate(clockRate);

                    // 在坐标系就绪后初始化Mario位置 - 使用正确的地面Y坐标
                    Mario.InitializePosition(SuperMarioPlayfield.JUDGMENT_X, SuperMarioPlayfield.GROUND_Y);

                    // 手动计算难度（使用新的osu!风格PP算法 + 第四十二轮改进）
                    double ar = Beatmap.Difficulty.ApproachRate;
                    double od = Beatmap.Difficulty.OverallDifficulty;

                    // 计算四大维度PP
                    int goombaCount = 0, koopaCount = 0, spinyCount = 0;
                    foreach (var obj in Beatmap.HitObjects)
                    {
                        if (obj is SuperMarioHitObject smbObj)
                        {
                            switch (smbObj.ObjectType)
                            {
                                case SuperMarioObjectType.Goomba: goombaCount++; break;
                                case SuperMarioObjectType.Koopa: koopaCount++; break;
                                case SuperMarioObjectType.Spiny: spinyCount++; break;
                            }
                        }
                        else
                        {
                            goombaCount++;
                        }
                    }

                    int objectCount = goombaCount + koopaCount + spinyCount;

                    double totalPP = 0;
                    double lengthBonus = 1.0;

                    // 使用异步方式调用 DifficultyCalculator（10秒超时）
                    try
                    {
                        // 设置谱面信息供 DifficultyCalculator 使用
                        SuperMarioDifficultyCalculator.CurrentMapInfo = GetMapInfo();
                        
                        var diffCalc = new SuperMarioDifficultyCalculator(Ruleset.RulesetInfo, Beatmap);
                        var task = System.Threading.Tasks.Task.Run(() => diffCalc.CalculateFromBeatmap(Beatmap, Mods?.ToArray()));
                        if (task.Wait(10000)) // 等待最多10秒
                        {
                            var diffAttrs = task.Result;
                            // 直接从 DifficultyAttributes 获取已包含全局系数的值
                            movementPP = diffAttrs.MovementPP;
                            readingPP = diffAttrs.ReadingPP;
                            precisionPP = diffAttrs.PrecisionPP;
                            accuracyPP = diffAttrs.AccuracyPP;
                            totalPP = diffAttrs.MaxPP;

                        // 获取谱面信息
                        currentMapInfo = GetMapInfo();
                        Console.WriteLine($"[SMB] {currentMapInfo} - DifficultyCalculator succeeded! MaxPP={totalPP:F2}");
                        }
                        else
                        {
                            throw new TimeoutException("DifficultyCalculator timeout");
                        }
                    }
                    catch
                    {
                        // 回退到简化计算
                        double jumpCount = Math.Max(1, spinyCount / 5.0);
                        movementPP = jumpCount * 3.0;
                        readingPP = spinyCount * 0.35;
                        precisionPP = goombaCount * 0.08;

                        const double ACCURACY_OD_BASE = 1.52163;
                        const double ACCURACY_POWER = 24.0;
                        const double ACCURACY_BASE = 2.83;

                        double accuracyValue = Math.Pow(ACCURACY_OD_BASE, od) * Math.Pow(1.0, ACCURACY_POWER) * ACCURACY_BASE;

                        if (objectCount < 500)
                            lengthBonus = 0.95 + 0.4 * Math.Min(1.0, objectCount / 1500.0);
                        else if (objectCount <= 1500)
                            lengthBonus = 0.95 + 0.4 * Math.Min(1.0, objectCount / 1500.0);
                        else
                            lengthBonus = Math.Min(1.5, 1.35 + 0.1 * Math.Log10(objectCount / 1500.0));

                        accuracyValue *= lengthBonus;

                        totalPP = Math.Pow(
                            Math.Pow(movementPP, 1.1) + Math.Pow(readingPP, 1.1) +
                            Math.Pow(precisionPP, 1.1) + Math.Pow(accuracyValue, 1.1),
                            1.0 / 1.1);

                        accuracyPP = accuracyValue;
                        currentMapInfo = GetMapInfo();
                        Console.WriteLine($"[SMB] {currentMapInfo} - Using fallback PP calculation. Total={totalPP:F2}");
                    }

                    // 使用调整系数来匹配真实值
                    const double COEFFICIENT = 0.76;

                    // 应用系数
                    movementPP *= COEFFICIENT;
                    readingPP *= COEFFICIENT;
                    precisionPP *= COEFFICIENT;
                    accuracyPP *= COEFFICIENT;

                    // 重新计算总 PP
                    if (objectCount < 500)
                        lengthBonus = 0.95 + 0.4 * Math.Min(1.0, objectCount / 1500.0);
                    else if (objectCount <= 1500)
                        lengthBonus = 0.95 + 0.4 * Math.Min(1.0, objectCount / 1500.0);
                    else
                        lengthBonus = Math.Min(1.5, 1.35 + 0.1 * Math.Log10(objectCount / 1500.0));

                    totalPP = Math.Pow(
                        Math.Pow(movementPP, 1.1) + Math.Pow(readingPP, 1.1) +
                        Math.Pow(precisionPP, 1.1) + Math.Pow(accuracyPP, 1.1),
                        1.0 / 1.1);
                    totalPP *= lengthBonus;
                    
                    currentMapInfo = GetMapInfo();
                    Console.WriteLine($"[SMB] {currentMapInfo} - Adjusted PP: M={movementPP:F2}, R={readingPP:F2}, P={precisionPP:F2}, A={accuracyPP:F2}, Total={totalPP:F2}");

                    // 保存最大PP值
                    maxMovementPP = movementPP;
                    maxReadingPP = readingPP;
                    maxPrecisionPP = precisionPP;
                    maxAccuracyPP = accuracyPP;
                    maxTotalPP = totalPP;

                    // 日志已输出 PP 值

                }

                Console.WriteLine("[SMB] Loading Success!");
                
                // 设置谱面信息供 PerformanceCalculator 使用（结算时调用）
                SuperMarioPerformanceCalculator.CurrentMapInfo = GetMapInfo();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMB] LoadComplete error: {ex.Message}");
            }
        }

        /// <summary>
        /// 计算Movement PP（对数增量）
        /// </summary>
        private double CalculateMovementPP(int goombaCount, int koopaCount, int spinyCount, double ar)
        {
            bool useARBuff = ar >= 9.4;
            double arMultiplier = useARBuff ? 1.2 : 1.0;

            // 连续Goomba的对数增量: log2(n) - log2(n-1)
            double goombaPP = 0;
            for (int i = 1; i <= goombaCount; i++)
            {
                double increment = i > 1 ? Math.Log2(i) - Math.Log2(i - 1) : 1.0;
                goombaPP += increment * arMultiplier;
            }

            // Koopa: n * 0.5
            double koopaPP = koopaCount * 0.5;

            // Spiny: n * 1
            double spinyPP = spinyCount * 1.0;

            return goombaPP + koopaPP + spinyPP;
        }

        /// <summary>
        /// 计算Reading PP（基于Spiny）
        /// </summary>
        private double CalculateReadingPP(int goombaCount, int spinyCount)
        {
            if (spinyCount == 0) return 0;

            // Spiny数量越多，Reading PP越高
            double spinyMultiplier = 1.0 + 0.1 * spinyCount;

            return spinyCount * 1.0 * spinyMultiplier * 0.5;
        }

        /// <summary>
        /// 计算Precision PP（Goomba与Spiny的距离）
        /// </summary>
        private double CalculatePrecisionPP(int goombaCount, int spinyCount)
        {
            if (goombaCount == 0 || spinyCount == 0) return 0;

            // 假设Goomba和Spiny均匀分布，平均距离约为总物件数的一半
            double avgDistance = Math.Max(1, (goombaCount + spinyCount) / 2.0);
            double strain = 100.0 / (avgDistance + 34);

            return goombaCount * strain * 0.3;
        }

        /// <summary>
        /// 使用1.1次方合成总PP
        /// </summary>
        private double CalculateTotalPP(double movement, double reading, double precision, double accuracy)
        {
            double sum = Math.Pow(movement, 1.1) +
                         Math.Pow(reading, 1.1) +
                         Math.Pow(precision, 1.1) +
                         Math.Pow(accuracy, 1.1);

            return Math.Pow(sum, 1.0 / 1.1);
        }

        /// <summary>
        /// ===== 第四十二轮新增：计算长度系数 =====
        /// 参考osu!风格：短图惩罚，标准长度正常，长图对数加成
        /// </summary>
        private double CalculateLengthBonus(int objectCount)
        {
            const double STANDARD_OBJECT_COUNT = 1500.0;

            if (objectCount <= 0)
                return 1.0;

            if (objectCount < 500)
            {
                // 短图惩罚
                return 0.95 + 0.4 * Math.Min(1.0, objectCount / STANDARD_OBJECT_COUNT);
            }
            else if (objectCount <= (int)STANDARD_OBJECT_COUNT)
            {
                // 标准长度
                return 0.95 + 0.4 * Math.Min(1.0, objectCount / STANDARD_OBJECT_COUNT);
            }
            else
            {
                // 长图对数加成（封顶1.5）
                double logBonus = 0.1 * Math.Log10(objectCount / STANDARD_OBJECT_COUNT);
                double lengthBonus = 1.35 + logBonus;
                return Math.Min(1.5, lengthBonus);
            }
        }
        /// <summary>
        /// 实时更新PP显示
        /// </summary>
        private double lastTime = 0;
        
        protected override void Update()
        {
            base.Update();

            // 检测 seek（时间回退）
            if (playfield != null && Time.Current < lastTime - 100) // 回退超过 100ms
            {
                Console.WriteLine($"[SMB] Seek detected: {lastTime} -> {Time.Current}, resetting enemies...");
                playfield.ResetAllEnemies();
                
                // 重置 ScoreProcessor
                if (playfield.GetScoreProcessor() is SuperMarioScoreProcessor sp)
                {
                    sp.Reset();
                }
            }
            
            lastTime = Time.Current;
        }
    }
}
