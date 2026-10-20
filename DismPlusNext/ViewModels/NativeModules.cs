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

        public static ModuleEntry BuildUpdates()
        {
            var mod = new ModuleEntry { Kind = "Updates", DisplayName = "更新管理", IsNative = true };
            mod.ReloadFunc = () => UpdateManager.List()
                .Select(u => new ItemEntry(null)
                {
                    DisplayName = u.Title,
                    Description = (string.IsNullOrEmpty(u.Type) ? "" : "[" + u.Type + "] ") + (string.IsNullOrEmpty(u.Description) ? "" : u.Description),
                    Group = "安装于 " + u.InstalledOn,
                    State = u.Kb,
                    ActionLabel = "卸载",
                    CustomApply = log => UpdateManager.Uninstall(u, log),
                })
                .ToList();
            return mod;
        }

        public static ModuleEntry BuildTasks()
        {
            var mod = new ModuleEntry { Kind = "Tasks", DisplayName = "计划任务管理", IsNative = true };
            mod.ReloadFunc = () => TaskManager.List()
                .Select(t => new ItemEntry(null)
                {
                    DisplayName = t.Name,
                    Description = (string.IsNullOrEmpty(t.Path) ? "" : t.Path + "  ") + t.Command,
                    Group = "上次结果 " + t.LastResult + " · 下次 " + t.NextRun,
                    State = t.State,
                    ActionLabel = t.Enabled ? "禁用" : "启用",
                    CustomApply = log => (t.Enabled ? TaskManager.Disable(t, log) : TaskManager.Enable(t, log)),
                })
                .ToList();
            return mod;
        }

        public static ModuleEntry BuildServices()
        {
            var mod = new ModuleEntry { Kind = "Services", DisplayName = "服务管理", IsNative = true };
            mod.ReloadFunc = () => ServicesManager.List()
                .Select(s => new ItemEntry(null)
                {
                    DisplayName = s.DisplayName,
                    Description = s.Name + (string.IsNullOrEmpty(s.Description) ? "" : " - " + s.Description),
                    Group = "启动类型 " + s.StartType,
                    State = s.Status,
                    ActionLabel = s.Status == "Running" ? "停止" : "启动",
                    CustomApply = log => (s.Status == "Running" ? ServicesManager.Stop(s, log) : ServicesManager.Start(s, log)),
                })
                .ToList();
            return mod;
        }

        public static ModuleEntry BuildHosts()
        {
            var mod = new ModuleEntry { Kind = "Hosts", DisplayName = "Hosts 编辑", IsNative = true };
            mod.ReloadFunc = () => HostsManager.Parse()
                .Where(h => !h.IsBlank)
                .Select(h => new ItemEntry(null)
                {
                    DisplayName = h.Hostname,
                    Description = h.Ip + (string.IsNullOrEmpty(h.Comment) ? "" : "  # " + h.Comment),
                    Group = "hosts",
                    State = h.Enabled ? "生效" : "已注释",
                    ActionLabel = h.Enabled ? "禁用" : "启用",
                    CustomApply = log => HostsManager.SetEnabled(h, !h.Enabled, log),
                })
                .ToList();
            return mod;
        }

        public static ModuleEntry BuildDrivers()
        {
            var mod = new ModuleEntry { Kind = "Drivers", DisplayName = "驱动管理", IsNative = true };
            mod.ReloadFunc = () => DriverManager.List()
                .Select(d => new ItemEntry(null)
                {
                    DisplayName = d.PublishedName,
                    Description = (string.IsNullOrEmpty(d.ProviderName) ? "" : d.ProviderName + "  ") +
                        (string.IsNullOrEmpty(d.FileName) ? "" : d.FileName + "  ") +
                        (string.IsNullOrEmpty(d.Version) ? "" : "v" + d.Version),
                    Group = string.IsNullOrEmpty(d.ClassName) ? "驱动" : d.ClassName,
                    State = d.SignerName,
                    ActionLabel = "删除",
                    CustomApply = log => DriverManager.Delete(d, log),
                })
                .ToList();
            return mod;
        }

        public static ModuleEntry BuildAccounts()
        {
            var mod = new ModuleEntry { Kind = "Accounts", DisplayName = "账户管理", IsNative = true };
            mod.ReloadFunc = () => AccountManager.List()
                .Select(a => new ItemEntry(null)
                {
                    DisplayName = a.Name,
                    Description = (string.IsNullOrEmpty(a.Description) ? "" : a.Description + "  ") + "SID:" + a.Sid,
                    Group = "上次登录 " + a.LastLogon,
                    State = a.Enabled ? "已启用" : "已禁用",
                    ActionLabel = a.Enabled ? "禁用" : "启用",
                    CustomApply = log => AccountManager.Toggle(a, !a.Enabled, log),
                })
                .ToList();
            return mod;
        }

        public static ModuleEntry BuildFileAssoc()
        {
            var mod = new ModuleEntry { Kind = "FileAssoc", DisplayName = "文件关联", IsNative = true };
            mod.ReloadFunc = () => FileAssociationManager.List()
                .Select(f => new ItemEntry(null)
                {
                    IsInfo = true,
                    DisplayName = f.Extension + "  →  " + f.DefaultApp,
                    Description = "ProgId: " + f.ProgId + (f.UserOverridden ? "（用户已通过“打开方式”覆盖）" : ""),
                    Group = "文件关联",
                })
                .ToList();
            return mod;
        }
    }
}
