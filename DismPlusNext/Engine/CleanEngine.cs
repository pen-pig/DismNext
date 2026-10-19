using System;
using System.Collections.Generic;
using System.IO;
using DismPlusNext.Data;
using DismPlusNext.Engine;
using DismPlusNext.Localization;
using DismPlusNext.Models;

namespace DismPlusNext
{
    /// <summary>应用某项时用户的选择（系统开关型开/关、Combo 选项）。</summary>
    public struct ApplyOptions
    {
        /// <summary>开关型：true=应用 True 分支，false=应用 False 分支。</summary>
        public bool IsOn;
        /// <summary>Combo：选中项索引；-1 表示使用推荐默认（Default 分支）。</summary>
        public int ComboIndex;
    }

    /// <summary>编排：加载数据、评估适用性、执行扫描/应用。</summary>
    public class CleanEngine
    {
        public DismData Data { get; private set; } = new DismData();
        public Localizer Loc { get; private set; } = new Localizer();
        public string RepoRoot { get; private set; } = "";
        public string LoadedPack => Loc.LoadedPack;

        /// <summary>初始化。packName 为 null 时自动选择最完整的语言包。</summary>
        public void Initialize(string repoRoot, string packName = null)
        {
            RepoRoot = repoRoot;
            string langDir = Path.Combine(repoRoot, "Languages");
            string pack = packName;
            if (string.IsNullOrEmpty(pack))
            {
                pack = "zh-Hant.xml";
                if (!File.Exists(Path.Combine(langDir, pack))) pack = "en.xml";
            }
            Loc.Load(langDir, pack);
            Data = new DataXmlLoader(Loc).Load(Path.Combine(repoRoot, "Data.xml"));
        }

        /// <summary>切换语言包并重新解析（保留已加载的数据结构，仅更新本地化文本）。</summary>
        public void ReloadLanguage(string packName)
        {
            if (string.IsNullOrEmpty(packName)) return;
            Loc.Load(Path.Combine(RepoRoot, "Languages"), packName);
            Data = new DataXmlLoader(Loc).Load(Path.Combine(RepoRoot, "Data.xml"));
        }

        /// <summary>项是否对当前系统适用（任一 Scan 命中即可）。</summary>
        public bool IsApplicable(RuleItem item)
        {
            foreach (var s in item.Scans)
                if (ConditionEvaluator.Evaluate(s.Applicable)) return true;
            return false;
        }

        public List<ExecResult> ScanItem(RuleItem item, Action<string> log, ApplyOptions opt = default)
        {
            var res = new List<ExecResult>();
            bool scanGate = item.Scans.Count == 0;
            foreach (var s in item.Scans)
                if (ConditionEvaluator.Evaluate(s.Applicable)) { scanGate = true; res.AddRange(ActionExecutor.Execute(s.Activate, false, log)); }
            // 系统优化 Combo 项：按用户选择的选项执行（默认=推荐项）
            if (item.Options.Count > 0)
                res.AddRange(ActionExecutor.Execute(ResolveCombo(item, opt.ComboIndex), false, log));
            // 系统开关型（<System>）：按用户的开/关选择执行对应分支
            else if (item.SystemTrueActivate.Count > 0 || item.SystemFalseActivate.Count > 0)
                res.AddRange(ActionExecutor.Execute(opt.IsOn ? item.SystemTrueActivate : item.SystemFalseActivate, false, log));
            // 普通清理/优化：扫描命中后执行，并应用默认动作
            else if (item.DefaultActivate.Count > 0 && scanGate)
                res.AddRange(ActionExecutor.Execute(item.DefaultActivate, false, log));
            return res;
        }

        public List<ExecResult> ApplyItem(RuleItem item, Action<string> log, ApplyOptions opt = default)
        {
            var res = new List<ExecResult>();
            bool scanGate = item.Scans.Count == 0;
            foreach (var s in item.Scans)
                if (ConditionEvaluator.Evaluate(s.Applicable)) { scanGate = true; res.AddRange(ActionExecutor.Execute(s.Activate, true, log)); }
            if (item.Options.Count > 0)
                res.AddRange(ActionExecutor.Execute(ResolveCombo(item, opt.ComboIndex), true, log));
            else if (item.SystemTrueActivate.Count > 0 || item.SystemFalseActivate.Count > 0)
                res.AddRange(ActionExecutor.Execute(opt.IsOn ? item.SystemTrueActivate : item.SystemFalseActivate, true, log));
            else if (item.DefaultActivate.Count > 0 && scanGate)
                res.AddRange(ActionExecutor.Execute(item.DefaultActivate, true, log));
            return res;
        }

        /// <summary>解析 Combo 项的激活动作：选中有效选项则用该选项，否则回退到推荐默认。</summary>
        private static List<ActionBase> ResolveCombo(RuleItem item, int comboIndex)
        {
            if (comboIndex >= 0 && comboIndex < item.Options.Count)
                return item.Options[comboIndex].Activate;
            return item.DefaultActivate;
        }
    }
}
