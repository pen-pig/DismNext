using System.Collections.ObjectModel;

namespace DismPlusNext.ViewModels
{
    /// <summary>一个功能模块（空间回收 / 系统优化 …）。</summary>
    public class ModuleEntry : ViewModelBase
    {
        public string Kind { get; set; } = "";
        public string DisplayName { get; set; } = "";

        public ObservableCollection<ItemEntry> Items { get; } = new ObservableCollection<ItemEntry>();

        public bool IsRecovery => Kind == "CleanCollection4";
        public bool IsOptimization => Kind == "SystemOptimization";
    }
}
