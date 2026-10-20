using System.Collections.Generic;
using System.Linq;
using DismPlusNext.Engine;

namespace DismPlusNext.ViewModels
{
    /// <summary>把引擎中的原生模块(系统信息/启动项/应用/可选功能)包装为可绑定的 ModuleEntry/ItemEntry。</summary>
    public static class NativeModules
    {
        public static ModuleEntry BuildSystemInfo()
        {
            var mod = new ModuleEntry { Kind = "SystemInfo", DisplayName = "系统信息", IsNative = true };
            mod.ReloadFunc = () => SystemInfo.Get()
                .Select(r => new ItemEntry(null) { IsInfo = true, DisplayName = r.Label, Description = r.Value })
                .ToList();
            return mod;
        }

        public static ModuleEntry BuildStartup()
        {
            var mod = new ModuleEntry { Kind = "Startup", DisplayName = "启动项管理", IsNative = true };
            mod.ReloadFunc = () => StartupManager.List()
                .Select(e => new ItemEntry(null)
                {
                    DisplayName = e.Name,
                    Description = e.Command,
                    Group = e.Location,
                    State = e.Enabled ? "已启用" : "已禁用",
                    ActionLabel = e.Enabled ? "禁用" : "启用",
                    CustomApply = log => StartupManager.Toggle(e, !e.Enabled, log),
                })
                .ToList();
            return mod;
        }

        public static ModuleEntry BuildApps()
        {
            var mod = new ModuleEntry { Kind = "Apps", DisplayName = "应用管理", IsNative = true };
            mod.ReloadFunc = () => AppManager.List()
                .Select(a => new ItemEntry(null)
                {
                    DisplayName = a.Name,
                    Description = (a.IsUwp ? "[UWP] " : "") + (string.IsNullOrEmpty(a.Version) ? "" : "v" + a.Version + "  ") + a.Publisher,
                    Group = a.IsUwp ? "UWP 应用" : "已安装程序",
                    State = "已安装",
                    ActionLabel = "卸载",
                    CustomApply = log => AppManager.Uninstall(a, log),
                })
                .ToList();
            return mod;
        }

        public static ModuleEntry BuildFeatures()
        {
            var mod = new ModuleEntry { Kind = "Features", DisplayName = "可选功能", IsNative = true };
            mod.ReloadFunc = () => FeatureManager.List()
                .Select(f => new ItemEntry(null)
                {
                    DisplayName = f.Name,
                    State = f.Enabled ? "已启用" : "已禁用",
                    ActionLabel = f.Enabled ? "禁用" : "启用",
                    CustomApply = log => FeatureManager.SetState(f, !f.Enabled, log),
                })
                .ToList();
            return mod;
        }
    }
}
