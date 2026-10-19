using System.Collections.Generic;

namespace DismPlusNext.Models
{
    /// <summary>整个 Data.xml 解析后的根对象。</summary>
    public class DismData
    {
        public List<RuleCollection> Collections { get; set; } = new List<RuleCollection>();
    }

    /// <summary>一个规则集合（CleanCollection4=空间回收 / SystemOptimization=系统优化 等）。</summary>
    public class RuleCollection
    {
        public string Kind { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public List<RuleItem> Items { get; set; } = new List<RuleItem>();
    }

    /// <summary>一个清理/优化项。</summary>
    public class RuleItem
    {
        public string NameKey { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Level { get; set; } = "";
        public string Type { get; set; } = "";
        public string DescriptionKey { get; set; } = "";
        public string Description { get; set; } = "";
        public string GroupKey { get; set; } = "";
        public string Group { get; set; } = "";
        public string WarningKey { get; set; } = "";
        public string Warning { get; set; } = "";
        public List<ScanRule> Scans { get; set; } = new List<ScanRule>();
        /// <summary>系统优化 Combo 项的可选项（如任务栏 Cortana 的"隐藏/显示搜索框"）。</summary>
        public List<ComboOption> Options { get; set; } = new List<ComboOption>();
        /// <summary>该优化项的"推荐默认"动作（来自 Default Dropdown 的 Activate），应用优化时执行。</summary>
        public List<ActionBase> DefaultActivate { get; set; } = new List<ActionBase>();
        /// <summary>系统开关型（&lt;System&gt;）开启时执行的动作（True 分支的 Activate）。</summary>
        public List<ActionBase> SystemTrueActivate { get; set; } = new List<ActionBase>();
        /// <summary>系统开关型关闭时执行的动作（False 分支的 Activate）。</summary>
        public List<ActionBase> SystemFalseActivate { get; set; } = new List<ActionBase>();
        public bool HasScans => Scans.Count > 0;
        public bool IsCombo => Options.Count > 0;
    }

    /// <summary>一条扫描规则：包含适用条件与要执行的动作。</summary>
    public class ScanRule
    {
        public string Work { get; set; } = "";
        public string NameKey { get; set; } = "";
        public string Name { get; set; } = "";
        public Applicable Applicable { get; set; } = new Applicable();
        public string WarningKey { get; set; } = "";
        public string Warning { get; set; } = "";
        public List<ActionBase> Activate { get; set; } = new List<ActionBase>();
    }

    public class Applicable
    {
        public List<Condition> Conditions { get; set; } = new List<Condition>();
    }

    /// <summary>系统优化 Combo 项的一个可选项（含其触发条件与要执行的动作）。</summary>
    public class ComboOption
    {
        public string NameKey { get; set; } = "";
        public string Name { get; set; } = "";
        public Applicable Applicable { get; set; } = new Applicable();
        public List<ActionBase> Activate { get; set; } = new List<ActionBase>();
    }

    // ---------- 条件（Applicable）----------
    public abstract class Condition { }
    public class CondFileExist : Condition { public string FilePath { get; set; } = ""; }
    public class CondImageFlags : Condition { public string Value { get; set; } = ""; }
    public class CondRegExist : Condition { public string Key { get; set; } = ""; public string Value { get; set; } = ""; }
    public class CondOSVersion : Condition { public string Compare { get; set; } = ""; public string Version { get; set; } = ""; }
    public class CondArch : Condition { public string Value { get; set; } = ""; }
    public class CondFileVersion : Condition { public string FilePath { get; set; } = ""; public string Compare { get; set; } = ""; public string Version { get; set; } = ""; }
    public class CondNot : Condition { public Condition Inner { get; set; } }
    public class CondOr : Condition { public List<Condition> Items { get; set; } = new List<Condition>(); }
    public class CondAnd : Condition { public List<Condition> Items { get; set; } = new List<Condition>(); }
    public class CondFalse : Condition { }
    public class CondQueryServiceStart : Condition { public string Service { get; set; } = ""; }

    // ---------- 动作（Activate）----------
    public abstract class ActionBase { }
    public class ActGeneral : ActionBase
    {
        public string RootPath { get; set; } = "";
        public string Flags { get; set; } = "";
        public List<string> Query { get; set; } = new List<string>();
        public List<string> Excluded { get; set; } = new List<string>();
    }
    public class ActCustom : ActionBase { public string ProcName { get; set; } = ""; public string Flags { get; set; } = ""; }
    public class ActExplorerNotify : ActionBase { public string Value { get; set; } = ""; }
    public class ActRegWrite : ActionBase { public string Key { get; set; } = ""; public string Value { get; set; } = ""; public string Type { get; set; } = ""; public string Data { get; set; } = ""; }
    public class ActRegDelete : ActionBase { public string Key { get; set; } = ""; public string Value { get; set; } = ""; }
    public class ActRegMove : ActionBase { public string From { get; set; } = ""; public string To { get; set; } = ""; }
    public class ActSetServiceStart : ActionBase { public string Service { get; set; } = ""; public string Start { get; set; } = ""; }
    public class ActUnknown : ActionBase { public string Tag { get; set; } = ""; }
}
