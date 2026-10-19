using System;
using System.Collections.Generic;
using System.IO;
using DismPlusNext.Data;
using DismPlusNext.Engine;
using DismPlusNext.Localization;
using DismPlusNext.Models;

namespace DismPlusNext
{
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

        public List<ExecResult> ScanItem(RuleItem item, Action<string> log)
        {
            var res = new List<ExecResult>();
            bool scanGate = item.Scans.Count == 0;
            foreach (var s in item.Scans)
                if (ConditionEvaluator.Evaluate(s.Applicable)) { scanGate = true; res.AddRange(ActionExecutor.Execute(s.Activate, false, log)); }
            // 系统优化 Combo 项：执行默认（推荐）选项的激活动作
            if (item.DefaultActivate.Count > 0 && scanGate)
                res.AddRange(ActionExecutor.Execute(item.DefaultActivate, false, log));
            // 系统开关型（<System>）：默认执行开启（True）分支的动作
            if (item.SystemTrueActivate.Count > 0 && scanGate)
                res.AddRange(ActionExecutor.Execute(item.SystemTrueActivate, false, log));
            return res;
        }

        public List<ExecResult> ApplyItem(RuleItem item, Action<string> log)
        {
            var res = new List<ExecResult>();
            bool scanGate = item.Scans.Count == 0;
            foreach (var s in item.Scans)
                if (ConditionEvaluator.Evaluate(s.Applicable)) { scanGate = true; res.AddRange(ActionExecutor.Execute(s.Activate, true, log)); }
            if (item.DefaultActivate.Count > 0 && scanGate)
                res.AddRange(ActionExecutor.Execute(item.DefaultActivate, true, log));
            if (item.SystemTrueActivate.Count > 0 && scanGate)
                res.AddRange(ActionExecutor.Execute(item.SystemTrueActivate, true, log));
            return res;
        }
    }
}
