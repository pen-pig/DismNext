using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DismPlusNext.ViewModels
{
    /// <summary>一个功能模块（空间回收 / 系统优化 / 系统信息 / 启动项 / 应用 / 可选功能 …）。</summary>
    public class ModuleEntry : ViewModelBase
    {
        public string Kind { get; set; } = "";
        public string DisplayName { get; set; } = "";

        public ObservableCollection<ItemEntry> Items { get; } = new ObservableCollection<ItemEntry>();

        /// <summary>原生动态模块（非 Data.xml 规则驱动），如系统信息/启动项/应用/功能。</summary>
        public bool IsNative { get; set; }
        /// <summary>原生模块刷新回调：返回最新条目列表（Scan 时调用）。</summary>
        public Func<List<ItemEntry>> ReloadFunc { get; set; }

        public bool IsRecovery => Kind == "CleanCollection4";
        public bool IsOptimization => Kind == "SystemOptimization";
    }
}
