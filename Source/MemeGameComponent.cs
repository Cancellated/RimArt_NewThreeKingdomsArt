using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Grammar;

namespace NewThreeKingdomsArt
{
    /// <summary>
    /// 游戏启动/读档后自动应用权重，并注册 Harmony Include Patch。
    /// </summary>
    public class MemeGameComponent : GameComponent
    {
        private static bool _patchRegistered;

        public MemeGameComponent(Game game) { }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            EnsurePatchRegistered();
            MemeWeightApplier.Apply();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            EnsurePatchRegistered();
            MemeWeightApplier.Apply();
        }

        private static void EnsurePatchRegistered()
        {
            if (_patchRegistered) return;
            _patchRegistered = true;

            try
            {
                var original = AccessTools.Method(
                    typeof(TaleTextGenerator), "GenerateTextFromTale",
                    new Type[] {
                        typeof(TextGenerationPurpose), typeof(Tale), typeof(int),
                        typeof(List<RulePackDef>), typeof(List<Rule>), typeof(Dictionary<string, string>)
                    }
                );

                if (original == null)
                {
                    Log.Error("[新三艺术] ✗ 找不到 TaleTextGenerator.GenerateTextFromTale 方法，检查类型参数");
                    return;
                }

                var harmony = new Harmony("Cancelation.NewThreeKingdomsArt_Include");
                harmony.Patch(original,
                    prefix: new HarmonyMethod(typeof(MemeGameComponent), nameof(Prefix_GenerateTextFromTale))
                );

                Log.Message("[新三艺术] ✓ Harmony Include Patch 已注册");
            }
            catch (Exception e)
            {
                Log.Error($"[新三艺术] Harmony 注册异常: {e}");
            }
        }

        private static void Prefix_GenerateTextFromTale(TextGenerationPurpose purpose, List<RulePackDef> extraInclude)
        {
            if (purpose != TextGenerationPurpose.ArtDescription) return;
            if (extraInclude == null || extraInclude.Count == 0) return;

            string descMakerName = extraInclude[0].defName;

            AddDefSafely(extraInclude, "NewThreeKingdomsArt_SharedRules");

            if (descMakerName == "ArtDescription_VoidSculpture")
            {
                return;
            }

            if (descMakerName == "ArtDescription_CubeSculpture")
            {
                AddDefSafely(extraInclude, "NewThreeKingdomsArt_CubeArtRules");
                LogWeightBreakdown(extraInclude);
                return;
            }

            AddDefSafely(extraInclude, "NewThreeKingdomsArt_MemeArtRules");
            LogWeightBreakdown(extraInclude);
        }

        /// <summary>
        /// 调试用：打印所有 extraInclude 中 r_art_description 规则的权重明细。
        /// 注意：不含 Root (Taleless/HasTale)，它在 Prefix 之后才被原版代码加入。
        /// </summary>
        private static void LogWeightBreakdown(List<RulePackDef> defs)
        {
            float totalWeight = 0f;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[新三艺术:权重] {defs[0].defName} 的 r_art_description 候选：");

            // 遍历所有 Def，收集 r_art_description 规则及其权重
            foreach (var def in defs)
            {
                var entryWeight = CollectEntryWeights(def);
                foreach (var w in entryWeight)
                {
                    totalWeight += w;
                    sb.AppendLine($"  p={w,-4} ← {def.defName}");
                }
            }

            // Root 固定 p=1（Prefix 之后由原版代码加入，此处手动补）
            sb.AppendLine($"  p=1    ← ArtDescriptionRoot_Taleless (原版Root, 固定)");
            totalWeight += 1f;

            // 计算本mod的占比
            float ourWeight = 0f;
            foreach (var def in defs)
            {
                if (def.defName.StartsWith("NewThreeKingdomsArt_"))
                    ourWeight += CollectEntryWeights(def).Sum();
            }

            sb.AppendLine($"  总权重={totalWeight}, 新三占比≈{ourWeight / totalWeight:P0}");
            Log.Message(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// 从某个 RulePackDef 的 rulesStrings 中提取所有 r_art_description(p=N) 的权重值。
        /// </summary>
        private static List<float> CollectEntryWeights(RulePackDef def)
        {
            var result = new List<float>();
            var rulePack = Traverse.Create(def).Field<RulePack>("rulePack").Value;
            if (rulePack == null) return result;

            var strings = Traverse.Create(rulePack).Field<List<string>>("rulesStrings").Value;
            if (strings == null) return result;

            foreach (var rule in strings)
            {
                if (!rule.StartsWith("r_art_description")) continue;

                float weight = 1f; // 无 (p=N) 时默认 p=1
                var match = System.Text.RegularExpressions.Regex.Match(rule, @"\(p=([\d.]+)\)");
                if (match.Success)
                    float.TryParse(match.Groups[1].Value, out weight);

                result.Add(weight);
            }
            return result;
        }

        private static void AddDefSafely(List<RulePackDef> list, string defName)
        {
            var def = DefDatabase<RulePackDef>.GetNamedSilentFail(defName);
            if (def != null && !list.Contains(def))
                list.Add(def);
        }
    }
}
