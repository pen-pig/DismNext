using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace DismPlusNext.Engine
{
    /// <summary>单个启动项。</summary>
    public class StartupEntry
    {
        public string Name = "";
        public string Command = "";
        public string Location = "";      // 展示用位置描述
        public bool Enabled;
        public bool IsFolder;             // true=启动文件夹快捷方式；false=注册表 Run 值
        public string Hive = "";          // HKCU / HKLM
        public string SubKey = "";
        public string ValueName = "";
        public string OriginalPath = "";  // 文件夹项原始完整路径
        public string BackupId = "";      // 注册表项备份子键名 / 文件夹项文件名
    }

    /// <summary>启动项管理：枚举注册表 Run 键与启动文件夹，支持禁用(备份并删除)/启用(还原)。</summary>
    public static class StartupManager
    {
        private const string BackupRegRoot = @"Software\DismPlusNext\StartupBackup";
        private static readonly string BackupFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DismPlusNext", "StartupBackup");

        private static readonly (RegistryHive hive, RegistryView view, string subKey, string tag)[] RegLocations =
        {
            (RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\Run", "当前用户\\Run"),
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", "本机\\Run (64位)"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run", "本机\\Run (32位)"),
        };

        public static List<StartupEntry> List()
        {
            var list = new List<StartupEntry>();
            // 已启用的注册表 Run 项
            foreach (var loc in RegLocations)
            {
                try
                {
                    using var key = RegistryKey.OpenBaseKey(loc.hive, loc.view).OpenSubKey(loc.subKey, false);
                    if (key == null) continue;
                    foreach (var name in key.GetValueNames())
                    {
                        if (name.Length == 0) continue;
                        var cmd = key.GetValue(name) as string;
                        if (string.IsNullOrWhiteSpace(cmd)) continue;
                        list.Add(new StartupEntry
                        {
                            Name = name,
                            Command = cmd,
                            Location = loc.tag,
                            Enabled = true,
                            IsFolder = false,
                            Hive = loc.hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU",
                            SubKey = loc.subKey,
                            ValueName = name,
                        });
                    }
                }
                catch (Exception ex) { Log(null, "读取 " + loc.tag + " 失败：" + ex.Message); }
            }
            // 已启用的启动文件夹快捷方式
            foreach (var folder in StartupFolders())
            {
                try
                {
                    if (!Directory.Exists(folder)) continue;
                    foreach (var f in Directory.GetFiles(folder, "*.lnk"))
                        list.Add(new StartupEntry
                        {
                            Name = Path.GetFileNameWithoutExtension(f),
                            Command = f,
                            Location = "启动文件夹\\" + Path.GetFileName(folder),
                            Enabled = true,
                            IsFolder = true,
                            OriginalPath = f,
                            BackupId = Path.GetFileName(f),
                        });
                }
                catch (Exception ex) { Log(null, "读取启动文件夹失败：" + ex.Message); }
            }
            // 已禁用的（备份中的）项，供用户重新启用
            list.AddRange(ListDisabled());
            return list;
        }

        private static List<StartupEntry> ListDisabled()
        {
            var outp = new List<StartupEntry>();
            // 注册表备份
            try
            {
                using var root = Registry.CurrentUser.OpenSubKey(BackupRegRoot, false);
                if (root != null)
                    foreach (var id in root.GetSubKeyNames())
                    {
                        using var bk = root.OpenSubKey(id);
                        if (bk == null) continue;
                        outp.Add(new StartupEntry
                        {
                            Name = (bk.GetValue("ValueName") as string) ?? id,
                            Command = (bk.GetValue("Command") as string) ?? "",
                            Location = (bk.GetValue("Location") as string) ?? "已禁用",
                            Enabled = false,
                            IsFolder = false,
                            Hive = (bk.GetValue("Hive") as string) ?? "HKCU",
                            SubKey = (bk.GetValue("SubKey") as string) ?? "",
                            ValueName = (bk.GetValue("ValueName") as string) ?? "",
                            BackupId = id,
                        });
                    }
            }
            catch { }
            // 文件夹备份
            try
            {
                if (Directory.Exists(BackupFolder))
                    foreach (var f in Directory.GetFiles(BackupFolder, "*.lnk"))
                        outp.Add(new StartupEntry
                        {
                            Name = Path.GetFileNameWithoutExtension(f),
                            Command = f,
                            Location = "已禁用(启动文件夹)",
                            Enabled = false,
                            IsFolder = true,
                            OriginalPath = f,
                            BackupId = Path.GetFileName(f),
                        });
            }
            catch { }
            return outp;
        }

        public static List<ExecResult> Toggle(StartupEntry e, bool enable, Action<string> log)
        {
            log ??= _ => { };
            var res = new List<ExecResult>();
            try
            {
                if (e.IsFolder) ToggleFolder(e, enable, log, res);
                else ToggleReg(e, enable, log, res);
            }
            catch (Exception ex)
            {
                res.Add(new ExecResult { Success = false, Target = e.Name, Message = ex.Message });
                log("异常：" + ex.Message);
            }
            return res;
        }

        private static void ToggleReg(StartupEntry e, bool enable, Action<string> log, List<ExecResult> res)
        {
            if (enable)
            {
                using var root = Registry.CurrentUser.OpenSubKey(BackupRegRoot, true);
                if (root == null) { res.Add(new ExecResult { Success = false, Target = e.Name, Message = "找不到备份记录" }); return; }
                using var bk = root.OpenSubKey(e.BackupId, true);
                if (bk == null) { res.Add(new ExecResult { Success = false, Target = e.Name, Message = "备份子键丢失" }); return; }
                var hive = e.Hive == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
                var view = e.Hive == "HKLM" ? RegistryView.Registry64 : RegistryView.Default;
                using var target = RegistryKey.OpenBaseKey(hive, view).CreateSubKey(e.SubKey, true);
                target.SetValue(e.ValueName, bk.GetValue("Command"));
                root.DeleteSubKeyTree(e.BackupId, false);
                log($"已启用：{e.Name}");
                res.Add(new ExecResult { Success = true, Target = e.Name });
            }
            else
            {
                var hive = e.Hive == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
                var view = e.Hive == "HKLM" ? RegistryView.Registry64 : RegistryView.Default;
                using var key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(e.SubKey, true);
                if (key == null) { res.Add(new ExecResult { Success = false, Target = e.Name, Message = "原键不存在" }); return; }
                var cmd = key.GetValue(e.ValueName) as string;
                if (cmd == null) { res.Add(new ExecResult { Success = false, Target = e.Name, Message = "值不存在" }); return; }
                string id = Guid.NewGuid().ToString("N");
                using (var root = Registry.CurrentUser.CreateSubKey(BackupRegRoot, true))
                using (var bk = root.CreateSubKey(id))
                {
                    bk.SetValue("Hive", e.Hive);
                    bk.SetValue("SubKey", e.SubKey);
                    bk.SetValue("ValueName", e.ValueName);
                    bk.SetValue("Command", cmd);
                    bk.SetValue("Location", e.Location);
                }
                key.DeleteValue(e.ValueName, false);
                log($"已禁用并备份：{e.Name}");
                res.Add(new ExecResult { Success = true, Target = e.Name });
            }
        }

        private static void ToggleFolder(StartupEntry e, bool enable, Action<string> log, List<ExecResult> res)
        {
            if (enable)
            {
                var dest = FindOriginalFolder(e);
                if (dest == null) { res.Add(new ExecResult { Success = false, Target = e.Name, Message = "找不到原始启动文件夹" }); return; }
                Directory.CreateDirectory(dest);
                var target = Path.Combine(dest, e.BackupId);
                File.Move(e.OriginalPath, target, true);
                log($"已启用：{e.Name}");
                res.Add(new ExecResult { Success = true, Target = e.Name });
            }
            else
            {
                Directory.CreateDirectory(BackupFolder);
                var dest = Path.Combine(BackupFolder, e.BackupId);
                File.Move(e.OriginalPath, dest, true);
                log($"已禁用并备份：{e.Name}");
                res.Add(new ExecResult { Success = true, Target = e.Name });
            }
        }

        private static string FindOriginalFolder(StartupEntry e)
        {
            foreach (var f in StartupFolders())
                if (e.OriginalPath.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return f;
            // 备份文件名可能已变化，回退到当前用户启动文件夹
            return StartupFolders().FirstOrDefault();
        }

        private static IEnumerable<string> StartupFolders()
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));
        }

        private static void Log(Action<string> log, string s) => (log ?? (_ => { }))(s);
    }
}
