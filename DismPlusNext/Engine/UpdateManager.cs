using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DismPlusNext.Engine
{
    /// <summary>单个已安装的 Windows 更新。</summary>
    public class UpdateEntry
    {
        public string Kb = "";            // KB 编号，如 KB5034441
        public string Title = "";         // 标题
        public string Description = "";   // 描述
        public string InstalledOn = "";   // 安装日期
        public string Type = "";          // 分类（安全/常规等）
    }

    /// <summary>更新管理：枚举已安装更新，支持卸载（wusa /uninstall）。</summary>
    public static class UpdateManager
    {
        public static List<UpdateEntry> List()
        {
            var list = new List<UpdateEntry>();
            // 原生读取注册表 HotFix 子键（等价 Get-HotFix 的数据源），不起 powershell 子进程。
            try
            {
                using var baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\HotFix", false);
                if (baseKey == null) return list;
                foreach (var kb in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        using var k = baseKey.OpenSubKey(kb, false);
                        if (k == null) continue;
                        object io = k.GetValue("InstalledOn");
                        string installed = io is byte[] b && b.Length > 0
                            ? Encoding.Unicode.GetString(b).TrimEnd('\0')
                            : (io as string ?? "");
                        string desc = (k.GetValue("Fix Description") as string) ?? "";
                        list.Add(new UpdateEntry
                        {
                            Kb = kb.StartsWith("KB", StringComparison.OrdinalIgnoreCase) ? kb.Substring(2) : kb,
                            Title = kb,
                            Description = desc,
                            InstalledOn = installed,
                            Type = "HotFix",
                        });
                    }
                    catch { }
                }
            }
            catch { }
            return list;
        }

        /// <summary>卸载指定更新（wusa /uninstall /kb:XXXX）。dryRun 时仅返回将要执行的命令。</summary>
        public static List<ExecResult> Uninstall(UpdateEntry u, Action<string> log, bool dryRun = false)
        {
            log ??= _ => { };
            var res = new List<ExecResult>();
            if (string.IsNullOrEmpty(u.Kb)) { res.Add(new ExecResult { Success = false, Target = u.Title, Message = "缺少 KB 编号" }); return res; }
            if (dryRun)
            {
                log($"将卸载更新 KB{u.Kb}（wusa /uninstall /kb:{u.Kb} /quiet /norestart）");
                res.Add(new ExecResult { Success = true, Target = "KB" + u.Kb });
                return res;
            }
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("wusa.exe", $"/uninstall /kb:{u.Kb} /quiet /norestart")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p == null) { res.Add(new ExecResult { Success = false, Target = "KB" + u.Kb, Message = "无法启动 wusa" }); return res; }
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                bool exited = p.WaitForExit(300000);
                if (!exited) { try { p.Kill(); } catch { } }
                int code = p.ExitCode;
                // 0=成功, 3010=需重启, 2359302=无需卸载(已不存在)
                if (code == 0 || code == 3010 || code == 0x80070BC2 - 0x80000000 /* 3010 */)
                {
                    log($"已提交卸载 KB{u.Kb}（重启后生效，若已无需处理也属正常）");
                    res.Add(new ExecResult { Success = true, Target = "KB" + u.Kb });
                }
                else
                {
                    log($"卸载 KB{u.Kb} 返回 0x{code:X8}：{o.Trim()}");
                    res.Add(new ExecResult { Success = false, Target = "KB" + u.Kb, Message = o.Trim() });
                }
            }
            catch (Exception ex)
            {
                log($"卸载异常：{ex.Message}");
                res.Add(new ExecResult { Success = false, Target = "KB" + u.Kb, Message = ex.Message });
            }
            return res;
        }
    }
}
