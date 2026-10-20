using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using Microsoft.Win32;
using DismPlusNext.Models;

namespace DismPlusNext.Engine
{
    /// <summary>单条动作执行结果。</summary>
    public class ExecResult
    {
        public bool Success;
        public string Target = "";
        public string Message = "";
        public long SizeAffected;
    }

    /// <summary>执行 Activate 中的动作。mode=false 为扫描预览（不改动系统），true 为实际应用。</summary>
    public static class ActionExecutor
    {
        public static List<ExecResult> Execute(List<ActionBase> actions, bool apply, Action<string> log)
        {
            log ??= _ => { };
            var results = new List<ExecResult>();
            foreach (var a in actions)
                results.AddRange(ExecOne(a, apply, log));
            return results;
        }

        private static List<ExecResult> ExecOne(ActionBase a, bool apply, Action<string> log)
        {
            try
            {
                switch (a)
                {
                    case ActGeneral g: return DoGeneral(g, apply, log);
                    case ActRegWrite w: return DoRegWrite(w, apply, log);
                    case ActRegDelete d: return DoRegDelete(d, apply, log);
                    case ActRegMove m: return DoRegMove(m, apply, log);
                    case ActCustom c: return DoCustom(c, apply, log);
                    case ActSetServiceStart s: return DoSetServiceStart(s, apply, log);
                    case ActExplorerNotify _: log("ExplorerNotify：刷新资源管理器（预览/无操作）"); return new List<ExecResult>();
                    case ActUnknown u: log($"未支持的动作类型：{u.Tag}（已跳过）"); return new List<ExecResult>();
                    default: return new List<ExecResult>();
                }
            }
            catch (Exception ex)
            {
                log($"动作执行异常：{ex.Message}");
                return new List<ExecResult> { new ExecResult { Success = false, Message = ex.Message } };
            }
        }

        // ---------------- 文件/目录清理 ----------------
        private static List<ExecResult> DoGeneral(ActGeneral g, bool apply, Action<string> log)
        {
            var outp = new List<ExecResult>();
            string root = Helpers.ExpandVars(g.RootPath);
            if (!Directory.Exists(root))
            {
                if (apply) log($"路径不存在，跳过：{root}");
                return outp;
            }
            bool onlyDir = g.Flags.IndexOf("Directory", StringComparison.OrdinalIgnoreCase) >= 0;
            bool onlyFile = g.Flags.IndexOf("File", StringComparison.OrdinalIgnoreCase) >= 0;
            var patterns = g.Query.Count > 0 ? g.Query : new List<string> { "*" };
            foreach (var pat in patterns)
            {
                IEnumerable<string> entries;
                try { entries = Directory.EnumerateFileSystemEntries(root, pat, SearchOption.TopDirectoryOnly); }
                catch { continue; }
                foreach (var entry in entries)
                {
                    string name = Path.GetFileName(entry);
                    if (g.Excluded.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                    bool isDir = Directory.Exists(entry);
                    if (onlyDir && !isDir) continue;
                    if (onlyFile && isDir) continue;
                    long size = isDir ? DirSize(new DirectoryInfo(entry)) : new FileInfo(entry).Length;
                    if (!apply)
                    {
                        log($"{(isDir ? "目录" : "文件")}：{entry}  ({(size / 1024.0 / 1024.0):F2} MB)");
                        outp.Add(new ExecResult { Success = true, Target = entry, SizeAffected = size });
                    }
                    else
                    {
                        try
                        {
                            if (isDir) Directory.Delete(entry, true);
                            else File.Delete(entry);
                            log($"已删除：{entry}");
                            outp.Add(new ExecResult { Success = true, Target = entry, SizeAffected = size });
                        }
                        catch (Exception ex)
                        {
                            log($"删除失败：{entry} - {ex.Message}");
                            outp.Add(new ExecResult { Success = false, Target = entry, Message = ex.Message });
                        }
                    }
                }
            }
            return outp;
        }

        private static long DirSize(DirectoryInfo d)
        {
            long s = 0;
            try
            {
                foreach (var f in d.EnumerateFiles()) s += f.Length;
                foreach (var sub in d.EnumerateDirectories()) s += DirSize(sub);
            }
            catch { }
            return s;
        }

        // ---------------- 注册表 ----------------
        private static List<ExecResult> DoRegWrite(ActRegWrite w, bool apply, Action<string> log)
        {
            string path = w.Key;
            var r = new ExecResult { Target = $"{path}\\{w.Value}" };
            if (!apply)
            {
                log($"将写入注册表：{path}\\{w.Value} = {w.Data} ({w.Type})");
                r.Success = true; return new List<ExecResult> { r };
            }
            try
            {
                using var key = Helpers.OpenRegKey(path, true);
                if (key == null) { log($"注册表键不存在（离线配置单元未加载？）：{path}"); r.Success = false; r.Message = "键不存在"; return new List<ExecResult> { r }; }
                key.SetValue(w.Value, ConvertRegValue(w.Type, w.Data), RegKind(w.Type));
                log($"已写入注册表：{path}\\{w.Value} = {w.Data}");
                r.Success = true;
            }
            catch (Exception ex) { log($"注册表写入失败：{ex.Message}"); r.Success = false; r.Message = ex.Message; }
            return new List<ExecResult> { r };
        }

        private static List<ExecResult> DoRegDelete(ActRegDelete d, bool apply, Action<string> log)
        {
            string path = d.Key;
            var r = new ExecResult { Target = $"{path}\\{d.Value}" };
            if (!apply)
            {
                log($"将删除注册表：{path}\\{d.Value}");
                r.Success = true; return new List<ExecResult> { r };
            }
            try
            {
                if (string.IsNullOrEmpty(d.Value))
                {
                    using (var key = Helpers.OpenRegKey(path, true))
                    {
                        if (key == null) { log($"注册表项不存在：{path}"); r.Success = false; r.Message = "键不存在"; return new List<ExecResult> { r }; }
                    }
                    // DeleteSubKeyTree 需要父键；用 RegDelete 递归删除：通过 OpenRegKey 取得后删除
                    DeleteRegTree(path);
                    log($"已删除注册表项：{path}");
                }
                else
                {
                    using var key = Helpers.OpenRegKey(path, true);
                    if (key == null) { log($"注册表键不存在：{path}"); r.Success = false; r.Message = "键不存在"; return new List<ExecResult> { r }; }
                    key.DeleteValue(d.Value, false);
                    log($"已删除注册表值：{path}\\{d.Value}");
                }
                r.Success = true;
            }
            catch (Exception ex) { log($"注册表删除失败：{ex.Message}"); r.Success = false; r.Message = ex.Message; }
            return new List<ExecResult> { r };
        }

        private static List<ExecResult> DoRegMove(ActRegMove m, bool apply, Action<string> log)
        {
            var r = new ExecResult { Target = $"{m.From} -> {m.To}" };
            if (!apply) { log($"将移动注册表：{m.From} -> {m.To}"); r.Success = true; return new List<ExecResult> { r }; }
            try
            {
                CopyKey(m.From, m.To);
                DeleteRegTree(m.From);
                log($"已移动注册表：{m.From} -> {m.To}");
                r.Success = true;
            }
            catch (Exception ex) { log($"注册表移动失败：{ex.Message}"); r.Success = false; r.Message = ex.Message; }
            return new List<ExecResult> { r };
        }

        private static void DeleteRegTree(string fullPath)
        {
            int idx = fullPath.LastIndexOf('\\');
            if (idx < 0) return;
            string parent = fullPath.Substring(0, idx);
            string child = fullPath.Substring(idx + 1);
            using var pkey = Helpers.OpenRegKey(parent, true);
            try { pkey?.DeleteSubKeyTree(child, false); } catch { }
        }

        private static void CopyKey(string from, string to)
        {
            using var src = Helpers.OpenRegKey(from, false);
            if (src == null) return;
            using var dst = Helpers.OpenRegKey(to, true);
            if (dst == null) return;
            foreach (var v in src.GetValueNames())
                dst.SetValue(v, src.GetValue(v), src.GetValueKind(v));
            foreach (var sk in src.GetSubKeyNames())
                CopyKey(from.TrimEnd('\\') + "\\" + sk, to.TrimEnd('\\') + "\\" + sk);
        }

        // ---------------- 自定义过程（Dism++ 内部例程的开源等效实现） ----------------
        private static List<ExecResult> DoCustom(ActCustom c, bool apply, Action<string> log)
        {
            var r = new ExecResult { Target = c.ProcName };
            // 离线映像模式：自定义过程多为面向"在线系统"的命令（dism /Online、compact.exe、
            // PowerShell 在线清理等），无法直接作用于挂载映像，安全跳过并提示。
            if (OfflineContext.Current != null)
            {
                log($"离线模式下跳过在线专属命令 {c.ProcName}（需针对在线系统执行）。");
                r.Success = true; return new List<ExecResult> { r };
            }
            bool reset = c.Flags == "#1";

            // 标准命令行过程
            string exe = null, args = null;
            string ps1 = null;

            switch (c.ProcName)
            {
                case "DismComponentCleanup":
                    exe = "dism.exe";
                    args = "/Online /Cleanup-Image /StartComponentCleanup" + (reset ? " /ResetBase" : "");
                    break;
                case "DismAppxsCleanup":
                    exe = "dism.exe";
                    args = "/Online /Cleanup-Image /StartComponentCleanup /ResetBase";
                    break;
                case "DismCompactOs":
                    exe = "compact.exe";
                    args = "/CompactOS:always";
                    break;
                case "DismDriverCleanup":
                    // 驱动存储属于组件存储的一部分，用组件清理覆盖
                    exe = "dism.exe";
                    args = "/Online /Cleanup-Image /StartComponentCleanup";
                    break;
                case "DismHardLinkMerge":
                    log("DismHardLinkMerge 为 Dism++ 闭源内部例程，开源引擎暂无等效外部命令（已跳过）。");
                    r.Success = true; return new List<ExecResult> { r };
                case "NCInstallerFolderCleanup":
                case "NCVSPackageCacheCleanup":
                    log($"{c.ProcName} 涉及系统安装缓存，误删可能破坏卸载/修复，开源引擎默认跳过（如需清理请手动评估）。");
                    r.Success = true; return new List<ExecResult> { r };
                case "NCCorruptedAppXOnlineCleanup":
                    ps1 = "Get-AppXPackage -AllUsers | ForEach-Object { Add-AppxPackage -DisableDevelopmentMode -Register ($_.InstallLocation + '\\AppXManifest.xml') -ErrorAction SilentlyContinue }";
                    break;
                case "NCDOCacheOnlineCleanup":
                    ps1 = "Stop-Service -Name DoSvc -Force -ErrorAction SilentlyContinue; " +
                          "Remove-Item -Path \"$env:SystemRoot\\ServiceProfiles\\NetworkService\\AppData\\Local\\Microsoft\\Windows\\DeliveryOptimization\\Cache\\*\" -Recurse -Force -ErrorAction SilentlyContinue; " +
                          "Start-Service -Name DoSvc -ErrorAction SilentlyContinue";
                    break;
                case "NCPackageCacheFolderCleanup":
                    ps1 = "Remove-Item -Path \"$env:ProgramData\\Package Cache\\*\" -Recurse -Force -ErrorAction SilentlyContinue";
                    break;
                case "NCSRPointOnlineCleanup":
                    exe = "vssadmin.exe";
                    args = "delete shadows /for=C: /oldest /quiet";
                    break;
                case "NCWinEventLogOnlineCleanup":
                    ps1 = "wevtutil el | ForEach-Object { wevtutil cl $_ }";
                    break;
                default:
                    log($"未实现的自定义过程：{c.ProcName}（已跳过）");
                    r.Success = true; return new List<ExecResult> { r };
            }

            if (exe != null)
            {
                if (!apply) { log($"将执行：{exe} {args}"); r.Success = true; return new List<ExecResult> { r }; }
                log($"执行：{exe} {args}");
                var (ok, outp) = RunProcess(exe, args);
                log(Trim(outp, 600));
                r.Success = ok;
                return new List<ExecResult> { r };
            }
            else
            {
                if (!apply) { log($"将执行 PowerShell 清理脚本（{c.ProcName}）"); r.Success = true; return new List<ExecResult> { r }; }
                log($"执行 PowerShell 脚本（{c.ProcName}）");
                var (ok, outp) = RunPs1(ps1);
                log(Trim(outp, 600));
                r.Success = ok;
                return new List<ExecResult> { r };
            }
        }

        private static List<ExecResult> DoSetServiceStart(ActSetServiceStart s, bool apply, Action<string> log)
        {
            var r = new ExecResult { Target = s.Service };
            string key = $"HKLM\\SYSTEM\\CurrentControlSet\\Services\\{s.Service}\\Start";
            if (!apply) { log($"将设置服务 {s.Service} 启动类型为 {s.Start} (注册表 {key})"); r.Success = true; return new List<ExecResult> { r }; }
            try
            {
                using var k = Helpers.OpenRegKey($"HKLM\\SYSTEM\\CurrentControlSet\\Services\\{s.Service}", true);
                if (k != null) { k.SetValue("Start", int.Parse(s.Start), RegistryValueKind.DWord); log($"已设置服务 {s.Service} 启动类型={s.Start}"); r.Success = true; }
                else log($"服务不存在：{s.Service}");
            }
            catch (Exception ex) { log($"设置服务失败：{ex.Message}"); r.Success = false; r.Message = ex.Message; }
            return new List<ExecResult> { r };
        }

        // ---------------- 工具 ----------------
        private static (bool, string) RunProcess(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return (false, "无法启动进程");
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(600000);
                return (p.ExitCode == 0, o);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private static (bool, string) RunPs1(string script)
        {
            try
            {
                string tmp = Path.Combine(Path.GetTempPath(), "dismnext_" + Guid.NewGuid().ToString("N") + ".ps1");
                File.WriteAllText(tmp, script, Encoding.UTF8);
                var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{tmp}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return (false, "无法启动 PowerShell");
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(600000);
                try { File.Delete(tmp); } catch { }
                return (p.ExitCode == 0, o);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            return s.Length > max ? s.Substring(s.Length - max) : s;
        }

        private static RegistryValueKind RegKind(string type)
        {
            switch ((type ?? "").ToUpperInvariant())
            {
                case "REG_DWORD": return RegistryValueKind.DWord;
                case "REG_QWORD": return RegistryValueKind.QWord;
                case "REG_BINARY": return RegistryValueKind.Binary;
                case "REG_MULTI_SZ": return RegistryValueKind.MultiString;
                case "REG_EXPAND_SZ": return RegistryValueKind.ExpandString;
                default: return RegistryValueKind.String;
            }
        }
        private static object ConvertRegValue(string type, string data)
        {
            if (string.IsNullOrEmpty(data)) return "";
            switch ((type ?? "").ToUpperInvariant())
            {
                case "REG_DWORD": return int.TryParse(data.Replace("#", ""), out int i) ? i : 0;
                case "REG_QWORD": return long.TryParse(data.Replace("#", ""), out long l) ? l : 0L;
                case "REG_MULTI_SZ": return data.Split('\n');
                default: return data;
            }
        }
    }
}
