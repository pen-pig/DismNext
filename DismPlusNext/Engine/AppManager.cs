using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace DismPlusNext.Engine
{
    /// <summary>单个已安装应用。</summary>
    public class AppEntry
    {
        public string Name = "";
        public string Version = "";
        public string Publisher = "";
        public string UninstallString = "";
        public string QuietUninstallString = "";
        public bool IsUwp;
        public string PackageFullName = "";
    }

    /// <summary>应用管理：枚举已安装 Win32 程序(注册表卸载项) 与 UWP，支持卸载。</summary>
    public static class AppManager
    {
        private static readonly (RegistryHive hive, RegistryView view, string subKey)[] UninstallLocations =
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        };

        public static List<AppEntry> List()
        {
            var list = new List<AppEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var loc in UninstallLocations)
            {
                try
                {
                    using var key = RegistryKey.OpenBaseKey(loc.hive, loc.view).OpenSubKey(loc.subKey, false);
                    if (key == null) continue;
                    foreach (var sk in key.GetSubKeyNames())
                    {
                        using var app = key.OpenSubKey(sk);
                        if (app == null) continue;
                        if ((app.GetValue("SystemComponent") as int?) == 1) continue;
                        if (!string.IsNullOrEmpty(app.GetValue("ParentKeyName") as string)) continue;
                        var rel = app.GetValue("ReleaseType") as string;
                        if (rel == "Hotfix" || rel == "Security Update" || rel == "Update" || rel == "Service Pack") continue;
                        var name = app.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        var uninst = (app.GetValue("UninstallString") as string) ?? "";
                        if (string.IsNullOrWhiteSpace(uninst)) continue; // 无卸载命令则跳过
                        var key2 = name + "|" + (app.GetValue("DisplayVersion") as string);
                        if (!seen.Add(key2)) continue;
                        list.Add(new AppEntry
                        {
                            Name = name,
                            Version = (app.GetValue("DisplayVersion") as string) ?? "",
                            Publisher = (app.GetValue("Publisher") as string) ?? "",
                            UninstallString = uninst,
                            QuietUninstallString = (app.GetValue("QuietUninstallString") as string) ?? "",
                        });
                    }
                }
                catch (Exception ex) { Console.Error.WriteLine("AppManager List 异常：" + ex.Message); }
            }
            list.AddRange(ListUwp());
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static List<AppEntry> ListUwp()
        {
            var outp = new List<AppEntry>();
            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -Command \"Get-AppxPackage | ForEach-Object { $_.Name + '||' + $_.Version + '||' + $_.Publisher + '||' + $_.PackageFullName }\"" )
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    string line;
                    while ((line = p.StandardOutput.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var parts = line.Split(new[] { "||" }, 4, StringSplitOptions.None);
                        if (parts.Length < 4) continue;
                        outp.Add(new AppEntry
                        {
                            Name = parts[0],
                            Version = parts[1],
                            Publisher = parts[2],
                            PackageFullName = parts[3],
                            IsUwp = true,
                        });
                    }
                    p.WaitForExit(20000);
                }
            }
            catch (Exception ex) { Console.Error.WriteLine("UWP 枚举异常：" + ex.Message); }
            return outp;
        }

        public static List<ExecResult> Uninstall(AppEntry app, Action<string> log)
        {
            log ??= _ => { };
            var res = new List<ExecResult>();
            try
            {
                if (app.IsUwp)
                {
                    var psi = new ProcessStartInfo("powershell.exe",
                        $"-NoProfile -Command \"Remove-AppxPackage '{app.PackageFullName.Replace("'", "''")}'\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(60000);
                    string err = p != null ? p.StandardError.ReadToEnd() : "";
                    bool ok = p != null && p.ExitCode == 0;
                    log(ok ? $"已卸载 UWP：{app.Name}" : $"卸载失败({app.Name})：{err}");
                    res.Add(new ExecResult { Success = ok, Target = app.Name, Message = err });
                }
                else
                {
                    string cmd = !string.IsNullOrWhiteSpace(app.QuietUninstallString) ? app.QuietUninstallString : app.UninstallString;
                    log($"执行卸载命令：{cmd}");
                    var exit = RunUninstall(cmd, log);
                    res.Add(new ExecResult { Success = exit == 0, Target = app.Name, Message = "退出码 " + exit });
                }
            }
            catch (Exception ex)
            {
                res.Add(new ExecResult { Success = false, Target = app.Name, Message = ex.Message });
                log("异常：" + ex.Message);
            }
            return res;
        }

        private static int RunUninstall(string cmd, Action<string> log)
        {
            EncodingHelper.Ensure();
            // 尝试以静默方式执行 MSI 卸载
            string exec;
            string args;
            string trimmed = cmd.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                exec = "msiexec.exe";
                args = $"/x {trimmed} /qn /norestart";
            }
            else if (trimmed.IndexOf("MsiExec", StringComparison.OrdinalIgnoreCase) >= 0 && trimmed.IndexOf("/I", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int i = trimmed.IndexOf("/I", StringComparison.OrdinalIgnoreCase);
                string guid = trimmed.Substring(i + 2).Trim().Trim('{', '}', '"');
                exec = "msiexec.exe";
                args = $"/x {{{guid}}} /qn /norestart";
            }
            else
            {
                // 直接以 cmd /c 运行原始卸载命令（保留引号）
                exec = "cmd.exe";
                args = "/c " + cmd;
            }
            var psi = new ProcessStartInfo(exec, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.GetEncoding(936),
                StandardErrorEncoding = Encoding.GetEncoding(936),
            };
            using var p = Process.Start(psi);
            if (p == null) return -1;
            p.WaitForExit(120000);
            string outp = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            if (!string.IsNullOrWhiteSpace(outp)) log(outp.Trim());
            return p.ExitCode;
        }
    }
}
