using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DismPlusNext.Models;

namespace DismPlusNext.ViewModels
{
    /// <summary>单个规则条目（空间回收项/系统优化项）在 UI 中的可绑定包装。</summary>
    public class ItemEntry : ViewModelBase
    {
        public RuleItem Rule { get; }

        public ItemEntry(RuleItem rule) { Rule = rule; }

        public string DisplayName => Rule.DisplayName;
        public string Group => Rule.Group;
        public string Description => Rule.Description;
        public string Warning => Rule.Warning;

        private static bool HasRegAction(IEnumerable<ActionBase> acts)
            => acts.Any(a => a is ActRegWrite || a is ActRegDelete || a is ActRegMove || a is ActSetServiceStart);
        private static bool HasFileAction(IEnumerable<ActionBase> acts)
            => acts.Any(a => a is ActGeneral || a is ActCustom || a is ActExplorerNotify);

        /// <summary>是否为系统优化项（通过注册表/服务切换实现，而非文件清理）。</summary>
        public bool IsOptimization => Rule.Scans.Any(s => HasRegAction(s.Activate))
            || HasRegAction(Rule.DefaultActivate)
            || HasRegAction(Rule.SystemTrueActivate)
            || Rule.Options.Any(o => HasRegAction(o.Activate));

        /// <summary>是否为空间回收项（文件/组件清理）。</summary>
        public bool IsRecovery => !IsOptimization && (Rule.Scans.Any(s => HasFileAction(s.Activate)) || HasFileAction(Rule.DefaultActivate));

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
