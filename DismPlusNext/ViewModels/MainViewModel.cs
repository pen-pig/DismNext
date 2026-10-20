using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using DismPlusNext.Engine;
using DismPlusNext.Models;

namespace DismPlusNext.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly CleanEngine _engine = new CleanEngine();

        public ObservableCollection<ModuleEntry> Modules { get; } = new ObservableCollection<ModuleEntry>();
        public ObservableCollection<string> Languages { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> LogLines { get; } = new ObservableCollection<string>();

        private ModuleEntry _selectedModule;
        public ModuleEntry SelectedModule
        {
            get => _selectedModule;
            set { if (Set(ref _selectedModule, value)) { OnChanged(nameof(IsRecoveryModule)); OnChanged(nameof(ExecuteButtonText)); OnChanged(nameof(ShowRestoreDefaults)); } }
        }

        private ItemEntry _selectedItem;
        public ItemEntry SelectedItem { get => _selectedItem; set => Set(ref _selectedItem, value); }

        private string _statusText = "";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private string _selectedLanguage = "";
        public string SelectedLanguage { get => _selectedLanguage; set { if (Set(ref _selectedLanguage, value)) SwitchLanguage(value); } }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }

        private long _totalFoundSize;
        public long TotalFoundSize { get => _totalFoundSize; set { if (Set(ref _totalFoundSize, value)) OnChanged(nameof(TotalFoundText)); } }
        public string TotalFoundText => TotalFoundSize > 0 ? $"可清理/影响：{(TotalFoundSize / 1048576.0):F2} MB" : "";

        public bool IsRecoveryModule => SelectedModule?.IsRecovery == true;
        public string ExecuteButtonText => IsRecoveryModule ? "清理选中" : "应用选中";
        public bool ShowRestoreDefaults => SelectedModule?.IsOptimization == true;

        public RelayCommand ScanCommand { get; }
        public RelayCommand ExecuteCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand RestoreDefaultsCommand { get; }

        /// <summary>窗口注入的确认回调（用于破坏性操作前的二次确认）。</summary>
        public Func<string, bool> RequestConfirm { get; set; }

        public MainViewModel()
        {
            ScanCommand = new RelayCommand(Scan, () => !IsBusy && SelectedModule != null);
            ExecuteCommand = new RelayCommand(Execute, () => !IsBusy && SelectedModule != null);
            RefreshCommand = new RelayCommand(() => Reload());
            RestoreDefaultsCommand = new RelayCommand(RestoreDefaults, () => !IsBusy && SelectedModule != null && !IsRecoveryModule);
        }

        public void Init()
        {
            string root = FindRepoRoot();
            _engine.Initialize(root);
            LoadLanguages();
            _selectedLanguage = _engine.LoadedPack;
            RebuildModules();
            int total = Modules.Sum(m => m.Items.Count);
            StatusText = $"已加载 {Modules.Count} 个模块 / {total} 个条目 · 语言包：{_engine.LoadedPack}";
            if (Modules.Count > 0) SelectedModule = Modules[0];
        }

        private void LoadLanguages()
        {
            Languages.Clear();
            string dir = Path.Combine(_engine.RepoRoot, "Languages");
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "*.xml"))
                Languages.Add(Path.GetFileName(f));
        }

        private void RebuildModules()
        {
            Modules.Clear();
            foreach (var coll in _engine.Data.Collections)
            {
                if (coll.Kind != "CleanCollection4" && coll.Kind != "SystemOptimization") continue;
                var mod = new ModuleEntry { Kind = coll.Kind, DisplayName = coll.DisplayName };
                foreach (var it in coll.Items)
                {
                    var e = new ItemEntry(it) { IsApplicable = _engine.IsApplicable(it) };
                    // 空间回收项默认全选（即清理）；系统优化项默认不勾选，需用户主动 opt-in，避免误改系统。
                    e.IsChecked = e.IsRecovery && e.IsApplicable;
                    mod.Items.Add(e);
                }
                Modules.Add(mod);
            }
            // 原生动态模块（非 Data.xml 规则驱动）
            Modules.Add(NativeModules.BuildSystemInfo());
            Modules.Add(NativeModules.BuildStartup());
            Modules.Add(NativeModules.BuildApps());
            Modules.Add(NativeModules.BuildFeatures());
        }

        private void SwitchLanguage(string pack)
        {
            if (string.IsNullOrEmpty(pack)) return;
            _engine.ReloadLanguage(pack);
            RebuildModules();
            if (Modules.Count > 0) SelectedModule = Modules[0];
            StatusText = $"已切换语言包：{pack} · 模块 {Modules.Count} 个";
        }

        private void Scan()
        {
            if (SelectedModule == null) return;
            // 原生动态模块：直接刷新条目列表（信息项只读，启动项/应用/功能重新枚举）
            if (SelectedModule.IsNative && SelectedModule.ReloadFunc != null)
            {
                IsBusy = true;
                var fresh = SelectedModule.ReloadFunc();
                SelectedModule.Items.Clear();
                foreach (var it in fresh) SelectedModule.Items.Add(it);
                Log($"===== 已刷新：{SelectedModule.DisplayName}（{fresh.Count} 项）=====");
                StatusText = $"{SelectedModule.DisplayName}：共 {fresh.Count} 项";
                IsBusy = false;
                return;
            }
            IsBusy = true;
            TotalFoundSize = 0;
            long total = 0;
            Log($"===== 开始扫描：{SelectedModule.DisplayName} =====");
            foreach (var e in SelectedModule.Items)
            {
                e.ClearFound();
                if (!e.IsApplicable) { e.Status = "不适用当前系统"; continue; }
                try
                {
                    var opt = new ApplyOptions { IsOn = e.IsChecked, ComboIndex = e.ComboIndex };
                    var res = _engine.ScanItem(e.Rule, s => Log("  " + s), opt);
                    foreach (var r in res)
                    {
                        e.FoundTargets.Add(r.Target);
                        e.FoundSize += r.SizeAffected;
                    }
                    total += e.FoundSize;
                    int ops = res.Count;
                    if (e.FoundTargets.Count > 0)
                        e.Status = $"发现 {e.FoundTargets.Count} 项 · {e.FoundSizeText}";
                    else if (ops > 0)
                        e.Status = $"将执行 {ops} 项操作（注册表/服务）";
                    else
                        e.Status = "无需处理";
                }
                catch (Exception ex) { e.Status = "扫描异常：" + ex.Message; Log("  异常：" + ex.Message); }
            }
            TotalFoundSize = total;
            Log($"===== 扫描完成：合计 {TotalFoundText} =====");
            IsBusy = false;
        }

        private void Execute()
        {
            if (SelectedModule == null) return;
            // 原生动态模块：执行每个条目的自定义逻辑（启动项/应用/可选功能）
            if (SelectedModule.IsNative)
            {
                var targets = SelectedModule.Items.Where(e => e.IsChecked && !e.IsInfo).ToList();
                if (targets.Count == 0) { Log("未选择任何条目。"); return; }
                string msg = $"将对选中的 {targets.Count} 个{SelectedModule.DisplayName}条目执行操作（{targets[0].ActionLabel}等），确认继续？";
                if (RequestConfirm != null && !RequestConfirm(msg)) { Log("已取消。"); return; }
                IsBusy = true;
                Log($"===== 开始操作：{SelectedModule.DisplayName}（{targets.Count} 项）=====");
                int ok = 0, fail = 0;
                foreach (var e in targets)
                {
                    Log($"-- {e.DisplayName} --");
                    try
                    {
                        var res = e.CustomApply?.Invoke(s => Log("  " + s)) ?? new List<ExecResult>();
                        ok += res.Count(r => r.Success);
                        fail += res.Count(r => !r.Success);
                        e.Status = $"完成（成功 {res.Count(r => r.Success)} / 失败 {res.Count(r => !r.Success)}）";
                    }
                    catch (Exception ex) { fail++; e.Status = "执行异常：" + ex.Message; Log("  异常：" + ex.Message); }
                }
                Log($"===== 操作结束：成功 {ok} / 失败 {fail} =====");
                IsBusy = false;
                Scan(); // 刷新状态
                return;
            }
            var ruleTargets = SelectedModule.Items.Where(e => e.IsChecked && e.IsApplicable).ToList();
            if (ruleTargets.Count == 0) { Log("未选择任何条目。"); return; }

            string verb = IsRecoveryModule ? "清理" : "应用";
            string msg2 = IsRecoveryModule
                ? $"将对选中的 {ruleTargets.Count} 个空间回收项执行实际删除，确认继续？"
                : $"将对选中的 {ruleTargets.Count} 个系统优化项写入注册表/服务，确认继续？";
            if (RequestConfirm != null && !RequestConfirm(msg2)) { Log("已取消。"); return; }

            IsBusy = true;
            Log($"===== 开始{verb}：{SelectedModule.DisplayName}（{ruleTargets.Count} 项）=====");
            int ok2 = 0, fail2 = 0;
            foreach (var e in ruleTargets)
            {
                Log($"-- {e.DisplayName} --");
                try
                {
                    var opt = new ApplyOptions { IsOn = e.IsChecked, ComboIndex = e.ComboIndex };
                    var res = _engine.ApplyItem(e.Rule, s => Log("  " + s), opt);
                    ok2 += res.Count(r => r.Success);
                    fail2 += res.Count(r => !r.Success);
                    e.Status = $"完成（成功 {res.Count(r => r.Success)} / 失败 {res.Count(r => !r.Success)}）";
                }
                catch (Exception ex) { fail2++; e.Status = "执行异常：" + ex.Message; Log("  异常：" + ex.Message); }
            }
            Log($"===== {verb}结束：成功 {ok2} / 失败 {fail2} =====");
            IsBusy = false;
            // 重新扫描以刷新状态
            Scan();
        }

        private void RestoreDefaults()
        {
            if (SelectedModule == null) return;
            var targets = SelectedModule.Items.Where(e => e.IsChecked && e.IsApplicable && e.IsOptimization).ToList();
            if (targets.Count == 0) { Log("未选择任何优化项。"); return; }
            if (RequestConfirm != null && !RequestConfirm($"将把选中的 {targets.Count} 个优化项重置为软件推荐状态（Combo 选推荐项、开关恢复开启），确认继续？"))
            { Log("已取消。"); return; }

            IsBusy = true;
            Log($"===== 批量恢复默认（{targets.Count} 项）=====");
            int ok = 0, fail = 0;
            foreach (var e in targets)
            {
                Log($"-- {e.DisplayName} --");
                var opt = new ApplyOptions { ComboIndex = e.IsCombo ? -1 : 0, IsOn = true };
                try
                {
                    var res = _engine.ApplyItem(e.Rule, s => Log("  " + s), opt);
                    ok += res.Count(r => r.Success);
                    fail += res.Count(r => !r.Success);
                    if (e.IsCombo) e.ComboIndex = e.Rule.DefaultOptionIndex >= 0 ? e.Rule.DefaultOptionIndex : 0;
                    e.Status = $"已恢复默认（成功 {res.Count(r => r.Success)} / 失败 {res.Count(r => !r.Success)}）";
                }
                catch (Exception ex) { fail++; e.Status = "执行异常：" + ex.Message; Log("  异常：" + ex.Message); }
            }
            Log($"===== 完成：成功 {ok} / 失败 {fail} =====");
            IsBusy = false;
            Scan();
        }

        private void Reload()
        {
            _engine.Initialize(_engine.RepoRoot, _selectedLanguage);
            RebuildModules();
            if (Modules.Count > 0) SelectedModule = Modules[0];
            StatusText = $"已重新加载 · 模块 {Modules.Count} 个 · 语言包：{_selectedLanguage}";
        }

        private void Log(string line)
        {
            string prefix = DateTime.Now.ToString("HH:mm:ss");
            LogLines.Add($"{prefix}  {line}");
        }

        private static string FindRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                if (File.Exists(Path.Combine(dir, "Data.xml"))) return dir;
                var p = Directory.GetParent(dir);
                if (p == null) break;
                dir = p.FullName;
            }
            return AppContext.BaseDirectory;
        }
    }
}
