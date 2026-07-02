using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Verse;
using Verse.Grammar;

namespace NewThreeKingdomsArt
{
    /// <summary>
    /// 运行时修改新三入口规则权重，并注入动态派系名称。
    /// include 注入已由 MemeGameComponent 的 Harmony Prefix 处理，本类不再负责。
    /// </summary>
    public static class MemeWeightApplier
    {
        private const string NTKDefName = "NewThreeKingdomsArt_MemeArtRules";
        private const string NTKCubeDefName = "NewThreeKingdomsArt_CubeArtRules";
        private const string SharedRulesDefName = "NewThreeKingdomsArt_SharedRules";
        private const string Tag = "[新三艺术]";

        private static readonly Regex PWeightRegex = new Regex(@"\(p=[\d.]+\)");

        public static void Apply()
        {
            float p = NewThreeKingdomsArtMod.Settings.memeWeight;
            Log.Message($"{Tag} Apply() p={p}（概率={p / (1f + p):P1}）");

            // 注入动态派系名称
            InjectFactionNames();

            // 替换入口权重
            ReplaceEntryWeight(NTKDefName, p);
            ReplaceEntryWeight(NTKCubeDefName, p);

            Log.Message($"{Tag} Apply() 完成");
        }

        /// <summary>
        /// 从游戏存档中扫描所有可见派系名称，动态注入到 SharedRules 的 ntkmeme_faction 词池。
        /// </summary>
        private static void InjectFactionNames()
        {
            var sharedDef = DefDatabase<RulePackDef>.GetNamedSilentFail(SharedRulesDefName);
            if (sharedDef == null) return;

            var rulePack = Traverse.Create(sharedDef).Field<RulePack>("rulePack").Value;
            if (rulePack == null) return;

            var list = Traverse.Create(rulePack).Field<List<string>>("rulesStrings").Value;
            if (list == null) return;

            list.RemoveAll(s => s.StartsWith("ntkmeme_faction->"));

            int injected = 0;
            foreach (var faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction.IsPlayer || faction.Hidden) continue;
                string name = faction.Name;
                if (string.IsNullOrEmpty(name)) continue;
                list.Add($"ntkmeme_faction->{name}");
                injected++;
            }

            Log.Message($"{Tag} 注入 {injected} 个派系名称到 ntkmeme_faction");
            ClearDefCaches(sharedDef, SharedRulesDefName);
        }

        /// <summary>
        /// 替换单个 Def 中 r_art_description(p=N) 的权重并清除缓存。
        /// </summary>
        private static void ReplaceEntryWeight(string defName, float p)
        {
            var def = DefDatabase<RulePackDef>.GetNamedSilentFail(defName);
            if (def == null) return;

            var rulePack = Traverse.Create(def).Field<RulePack>("rulePack").Value;
            if (rulePack == null) return;

            ReplaceInStrings(rulePack, "rulesStrings", p);
            ReplaceInStrings(rulePack, "untranslatedRulesStrings", p);

            ClearDefCaches(def, defName);
        }

        private static void ReplaceInStrings(RulePack rp, string fieldName, float p)
        {
            var list = Traverse.Create(rp).Field<List<string>>(fieldName).Value;
            if (list == null) return;

            int modified = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].StartsWith("r_art_description"))
                {
                    string oldStr = list[i];
                    string newStr = PWeightRegex.Replace(oldStr, $"(p={p})");
                    if (oldStr != newStr)
                    {
                        list[i] = newStr;
                        modified++;
                    }
                }
            }
            if (modified > 0)
                Log.Message($"{Tag}   [{fieldName}] 修改了 {modified} 条");
        }

        /// <summary>
        /// 清除 RulePackDef 的所有缓存层。
        /// </summary>
        private static void ClearDefCaches(RulePackDef def, string defName)
        {
            Traverse.Create(def).Field<List<Rule>>("cachedRules").Value = null;
            Traverse.Create(def).Field<List<Rule>>("cachedUntranslatedRules").Value = null;

            var rp = Traverse.Create(def).Field<RulePack>("rulePack").Value;
            if (rp != null)
            {
                var rpT = Traverse.Create(rp);
                rpT.Field<List<Rule>>("rulesRaw").Value = null;
                rpT.Field<List<Rule>>("untranslatedRulesRaw").Value = null;
                rpT.Field<List<Rule>>("rulesResolved").Value = null;
                rpT.Field<List<Rule>>("untranslatedRulesResolved").Value = null;
            }
        }
    }
}
