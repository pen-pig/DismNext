using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DismPlusNext.Engine
{
    /// <summary>单个 Windows 可选功能。</summary>
    public class FeatureEntry
    {
        public string Name = "";
        public bool Enabled;
    }

    /// <summary>可选功能管理：通过 DISM 列出/启用/禁用 Windows 功能。</summary>
    public static class FeatureManager
    {
        public static List<FeatureEntry> List()
        {
            EncodingHelper.Ensure();
            var list = new List<FeatureEntry>();
            try
            {
                var psi = new ProcessStartInfo("dism.exe", "/Online /Get-Features /Format:Table")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.GetEncoding(936),
                };
                using var p = Process.Start(psi);
                if (p == null) return list;
                string line;
                while ((line = p.StandardOutput.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.Contains("功能名称") || line.Contains("----") || line.StartsWith("部署映像")) continue;
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;
                    // 功能名由连字符组成，无空格；取最后一个词为状态，其余为名称
                    int idx = line.LastIndexOf(' ');
                    if (idx <= 0) continue;
                    string fname = line.Substring(0, idx).Trim();
                    string fstate = line.Substring(idx + 1).Trim();
                    bool enabled = fstate == "已启用" || fstate.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
                    list.Add(new FeatureEntry { Name = fname, Enabled = enabled });
                }
                p.WaitForExit(60000);
            }
            catch (Exception ex) { Console.Error.WriteLine("FeatureManager List 异常：" + ex.Message); }
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        public static List<ExecResult> SetState(FeatureEntry f, bool enable, Action<string> log)
        {
            EncodingHelper.Ensure();
            log ??= _ => { };
            var res = new List<ExecResult>();
            try
            {
                string verb = enable ? "/Enable-Feature" : "/Disable-Feature";
                var psi = new ProcessStartInfo("dism.exe", $"/Online {verb} /FeatureName:{f.Name} /NoRestart")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.GetEncoding(936),
                    StandardErrorEncoding = Encoding.GetEncoding(936),
                };
                using var p = Process.Start(psi);
                if (p == null) { res.Add(new ExecResult { Success = false, Target = f.Name, Message = "无法启动 dism" }); return res; }
                string outp = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(180000);
                bool ok = p.ExitCode == 0;
                log(ok ? $"{(enable ? "已启用" : "已禁用")}：{f.Name}" : $"操作失败({f.Name})：{outp.Trim()}");
                res.Add(new ExecResult { Success = ok, Target = f.Name, Message = outp.Trim() });
            }
            catch (Exception ex)
            {
                res.Add(new ExecResult { Success = false, Target = f.Name, Message = ex.Message });
                log("异常：" + ex.Message);
            }
            return res;
        }
    }
}
