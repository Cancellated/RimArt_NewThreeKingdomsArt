using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using Verse;
using Verse.Grammar;

namespace NewThreeKingdomsArt
{
    /// <summary>
    /// 运行时修改新三入口规则权重。
    /// 
    ///   RulePackDef.rulePack → RulePack (private)
    ///   RulePackDef.cachedRules / cachedUntranslatedRules → List&lt;Rule&gt; (private, Def级缓存)
    ///   RulePack.rulesStrings → List&lt;string&gt; (private, 翻译后)
    ///   RulePack.untranslatedRulesStrings → List&lt;string&gt; (private, 英文原文)
    ///   RulePack.rulesRaw / untranslatedRulesRaw → List&lt;Rule&gt; (private, 解析后)
    ///   RulePack.rulesResolved / untranslatedRulesResolved → List&lt;Rule&gt; (private, 合并include后)
    /// 
    /// 缓存重建链：
    ///   rulesStrings → rulesRaw → rulesResolved(含include) → cachedRules(Def级)
    ///   
    /// </summary>
    public static class MemeWeightApplier
    {
        private const string DefName = "NewThreeKingdomsArt_MemeArtRules";
        private const string Tag = "[新三艺术]";

        private static readonly Regex PWeightRegex = new Regex(@"\(p=[\d.]+\)");

        private static readonly string[] ParentDefNames =
            { "ArtDescriptionRoot_HasTale", "ArtDescriptionRoot_Taleless" };

        public static void Apply()
        {
            float p = NewThreeKingdomsArtMod.Settings.memeWeight;
            Log.Message($"{Tag} Apply() 开始，目标 p={p}（概率={p / (1f + p):P1}）");

            var ourDef = DefDatabase<RulePackDef>.GetNamedSilentFail(DefName);
            if (ourDef == null) { Log.Error($"{Tag} 找不到 Def: {DefName}"); return; }

            var rulePack = Traverse.Create(ourDef).Field<RulePack>("rulePack").Value;
            if (rulePack == null) { Log.Error($"{Tag} rulePack 为 null"); return; }

            // 原地替换 rulesStrings 中的权重文本
            ReplaceWeightInStrings(rulePack, "rulesStrings", p);
            ReplaceWeightInStrings(rulePack, "untranslatedRulesStrings", p);

            // 清空自己的缓存
            Log.Message($"{Tag} 清除缓存");
            ClearAllCaches(ourDef, DefName);

            // 清父级缓存
            Log.Message($"{Tag} 清除父级缓存");
            foreach (var parentName in ParentDefNames)
            {
                var parent = DefDatabase<RulePackDef>.GetNamedSilentFail(parentName);
                if (parent != null) ClearAllCaches(parent, parentName);
            }

            Log.Message($"{Tag} Apply() 完成");
        }

        // 替换 rulesStrings 中 r_art_description(p=N) 的权重。
        private static void ReplaceWeightInStrings(RulePack rp, string fieldName, float p)
        {
            var list = Traverse.Create(rp).Field<List<string>>(fieldName).Value;
            if (list == null)
            {
                Log.Message($"{Tag}   RulePack.{fieldName} = null");
                return;
            }

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
                        Log.Message($"{Tag}   [{fieldName}][{i}]: '{oldStr}' → '{newStr}'");
                    }
                }
            }
            Log.Message($"{Tag}   [{fieldName}] 共 {list.Count} 条，修改了 {modified} 条");
        }

        /// <summary>
        /// 清除 RulePackDef 的所有缓存层：
        ///   Def 级: cachedRules, cachedUntranslatedRules
        ///   RulePack 级: rulesRaw, untranslatedRulesRaw, rulesResolved, untranslatedRulesResolved
        /// </summary>
        private static void ClearAllCaches(RulePackDef def, string defName)
        {
            // Def 级缓存
            Traverse.Create(def).Field<List<Rule>>("cachedRules").Value = null;
            Traverse.Create(def).Field<List<Rule>>("cachedUntranslatedRules").Value = null;

            // RulePack 级
            var rp = Traverse.Create(def).Field<RulePack>("rulePack").Value;
            if (rp != null)
            {
                var rpT = Traverse.Create(rp);
                rpT.Field<List<Rule>>("rulesRaw").Value = null;
                rpT.Field<List<Rule>>("untranslatedRulesRaw").Value = null;
                rpT.Field<List<Rule>>("rulesResolved").Value = null;
                rpT.Field<List<Rule>>("untranslatedRulesResolved").Value = null;
            }

            Log.Message($"{Tag}   {defName}: 全部缓存已清空(设为null)");
        }
    }
}
