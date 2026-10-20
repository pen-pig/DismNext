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
            // Get-HotFix 返回已安装的快速修补程序（含 KB 号、安装日期、描述）
            const string ps = "Get-HotFix | ForEach-Object { " +
                "$kb = ($_.HotFixID -replace 'KB',''); " +
                "\"[U]|$($_.HotFixID)|$(($_.Description -replace '\\|','/') -replace '[\\r\\n]',' ')|$($_.InstalledOn.ToString('yyyy-MM-dd'))|$kb\" }";
            var (ok, text) = Helpers.RunPowerShell(ps);
            if (!ok) return list;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (!line.StartsWith("[U]|")) continue;
                var p = line.Split('|');
                if (p.Length < 5) continue;
                list.Add(new UpdateEntry
                {
                    Kb = p[4],
                    Title = p[1],
                    Description = p[2],
                    InstalledOn = p[3],
                    Type = "HotFix",
                });
            }
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
