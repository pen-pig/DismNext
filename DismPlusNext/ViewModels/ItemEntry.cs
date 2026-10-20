using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DismPlusNext.Engine;
using DismPlusNext.Models;

namespace DismPlusNext.ViewModels
{
    /// <summary>单个条目在 UI 中的可绑定包装。既承载 Data.xml 规则项，也承载原生动态模块项（启动项/应用/功能等）。</summary>
    public class ItemEntry : ViewModelBase
    {
        public RuleItem Rule { get; }

        public ItemEntry(RuleItem rule)
        {
            Rule = rule;
            if (rule != null)
            {
                foreach (var o in rule.Options) OptionNames.Add(o.Name);
                if (rule.Options.Count > 0)
                    _comboIndex = rule.DefaultOptionIndex >= 0 ? rule.DefaultOptionIndex : 0;
            }
        }

        // 原生模块（Rule 为 null）可显式设置这些文本；规则模块自动从 Rule 取。
        private string _display, _group, _desc, _warn;
        public string DisplayName { get => _display ?? Rule?.DisplayName ?? ""; set => Set(ref _display, value); }
        public string Group { get => _group ?? Rule?.Group ?? ""; set => Set(ref _group, value); }
        public string Description { get => _desc ?? Rule?.Description ?? ""; set => Set(ref _desc, value); }
        public string Warning { get => _warn ?? Rule?.Warning ?? ""; set => Set(ref _warn, value); }

        /// <summary>只读信息项（如系统信息），不显示勾选框、不可应用。</summary>
        public bool IsInfo { get; set; }
        /// <summary>原生动态模块的自定义执行逻辑（替代 RuleItem 引擎）。</summary>
        public Func<Action<string>, List<ExecResult>> CustomApply { get; set; }
        /// <summary>该条目可执行动作的描述（如"禁用"/"卸载"/"启用功能"）。</summary>
        public string ActionLabel { get; set; }
        /// <summary>原生条目当前状态文案（如"已启用"/"已禁用"/"已安装"）。</summary>
        public string State { get; set; }

        /// <summary>是否为空间回收项（文件/组件清理）。</summary>
        public bool IsRecovery => !IsInfo && !IsNative && (Rule != null) && (!IsOptimization && (Rule.Scans.Any(s => HasFileAction(s.Activate)) || HasFileAction(Rule.DefaultActivate)));
        /// <summary>是否为系统优化项（通过注册表/服务切换实现）。</summary>
        public bool IsOptimization => !IsInfo && !IsNative && (Rule != null) && (Rule.Scans.Any(s => HasRegAction(s.Activate))
            || HasRegAction(Rule.DefaultActivate)
            || HasRegAction(Rule.SystemTrueActivate)
            || Rule.Options.Any(o => HasRegAction(o.Activate)));
        /// <summary>是否为原生动态模块项（启动项/应用/功能等）。</summary>
        public bool IsNative => Rule == null && !IsInfo;

        /// <summary>是否为 Combo（下拉多选一）型优化项。</summary>
        public bool IsCombo => Rule.Options.Count > 0;
        /// <summary>是否为开关型（True/False）优化项。</summary>
        public bool IsSystemSwitch => Rule.SystemTrueActivate.Count > 0 || Rule.SystemFalseActivate.Count > 0;
        /// <summary>Combo 选项名称列表（本地化后）。</summary>
        public ObservableCollection<string> OptionNames { get; } = new ObservableCollection<string>();
        /// <summary>用户选中的 Combo 选项索引；-1 表示使用推荐默认。</summary>
        private int _comboIndex = -1;
        public int ComboIndex { get => _comboIndex; set => Set(ref _comboIndex, value); }

        private static bool HasRegAction(IEnumerable<ActionBase> acts)
            => acts.Any(a => a is ActRegWrite || a is ActRegDelete || a is ActRegMove || a is ActSetServiceStart);
        private static bool HasFileAction(IEnumerable<ActionBase> acts)
            => acts.Any(a => a is ActGeneral || a is ActCustom || a is ActExplorerNotify);

        private bool _isChecked;
        public bool IsChecked { get => _isChecked; set => Set(ref _isChecked, value); }

        private bool _isApplicable = true;
        public bool IsApplicable { get => _isApplicable; set => Set(ref _isApplicable, value); }

        private string _status = "";
        public string Status { get => _status; set => Set(ref _status, value); }

        private long _foundSize;
        public long FoundSize { get => _foundSize; set { if (Set(ref _foundSize, value)) OnChanged(nameof(FoundSizeText)); } }
        public string FoundSizeText => FoundSize > 0 ? $"{(FoundSize / 1048576.0):F2} MB" : "";

        public ObservableCollection<string> FoundTargets { get; } = new ObservableCollection<string>();

        public void ClearFound()
        {
            FoundTargets.Clear();
            FoundSize = 0;
            Status = "";
        }
    }
}
