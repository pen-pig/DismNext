using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Win32;

[assembly: InternalsVisibleTo("DismPlusNext.Tests")]

namespace DismPlusNext.Engine
{
    /// <summary>
    /// 离线映像上下文。挂载映像后创建，加载其注册表配置单元，
    /// 并把引擎的文件路径/注册表路径/系统版本重定向到该映像。
    /// 通过静态 <see cref="Current"/> 作为"环境上下文"生效：非 null 时，
    /// Helpers / ConditionEvaluator / ActionExecutor 自动重定向，在线逻辑不受影响。
    /// </summary>
    public class OfflineContext : IDisposable
    {
        /// <summary>当前生效的离线映像上下文；为 null 表示操作在线系统。</summary>
        public static OfflineContext Current;

        public string MountDir { get; }

        /// <summary>映像的内核版本（从 SOFTWARE 配置单元读取），用于 OSVersion 条件评估。</summary>
        public Version ImageVersion { get; internal set; }

        private readonly List<string> _loadedKeys = new List<string>();

        private OfflineContext(string mountDir) { MountDir = mountDir; }

        /// <summary>创建离线上下文：加载注册表配置单元并设为当前上下文。loadHives=false 仅用于单元测试路径重映射。</summary>
        public static (bool ok, string msg) Create(string mountDir, bool loadHives = true)
        {
            var ctx = new OfflineContext(mountDir);
            if (loadHives)
            {
                string err = "";
                if (!ctx.LoadHives(ref err)) { ctx.UnloadAll(); return (false, err); }
            }
            ctx.ReadImageVersion();
            Current = ctx;
            return (true, "");
        }

        /// <summary>单元测试用：不加载配置单元，仅初始化路径重映射与版本。</summary>
        internal static OfflineContext CreateForTest(string mountDir, Version ver)
        {
            var ctx = new OfflineContext(mountDir) { ImageVersion = ver };
            Current = ctx;
            return ctx;
        }

        private bool LoadHives(ref string err)
        {
            var config = Path.Combine(MountDir, "Windows", "System32", "config");
            TryLoad("DismOffline_Software", Path.Combine(config, "SOFTWARE"));
            TryLoad("DismOffline_System", Path.Combine(config, "SYSTEM"));
            var defaultUser = Path.Combine(MountDir, "Users", "Default", "NTUSER.DAT");
            TryLoad("DismOffline_Default", defaultUser);
            if (_loadedKeys.Count == 0)
            {
                err = "无法加载任何注册表配置单元（映像路径是否正确？需包含 Windows\\System32\\config 与 Users\\Default\\NTUSER.DAT）";
                return false;
            }
            return true;
        }

        private void TryLoad(string subKey, string hiveFile)
        {
            if (!File.Exists(hiveFile)) return;
            try
            {
                // 先尝试卸载可能残留的同名键，保证幂等
                RunReg($"unload \"HKLM\\{subKey}\"");
                var (ok, _) = RunReg($"load \"HKLM\\{subKey}\" \"{hiveFile}\"");
                if (ok) _loadedKeys.Add(subKey);
            }
            catch { }
        }

        private void ReadImageVersion()
        {
            try
            {
                using var key = OpenReg("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", false);
                if (key == null) return;
                var cv = key.GetValue("CurrentVersion") as string;     // "10.0"
                var cb = key.GetValue("CurrentBuildNumber") as string;  // "26340"
                if (Version.TryParse(cv, out var v1) && int.TryParse(cb, out int build))
                    ImageVersion = new Version(v1.Major, v1.Minor, build);
                else if (int.TryParse(cb, out int build2))
                    ImageVersion = new Version(10, 0, build2);
            }
            catch { }
        }

        /// <summary>把文件路径（含驱动器前缀 / %SystemRoot% / %SystemDrive%）重映射到挂载根。</summary>
        public string RemapFsPath(string p)
        {
            if (string.IsNullOrEmpty(p)) return p ?? "";
            string s = p.Replace("%0%", MountDir.TrimEnd('\\'));
            // 先处理驱动器前缀（如 C:\ 或 X:\ ）→ 挂载根。必须在 %SystemRoot% 展开之前，
            // 否则展开后路径以挂载盘符开头会再次触发此分支导致重复前缀。
            if (s.Length >= 2 && s[1] == ':' && (s.Length == 2 || s[2] == '\\' || s[2] == '/'))
            {
                s = MountDir.TrimEnd('\\') + s.Substring(2).Replace('/', '\\');
            }
            // 再处理 %SystemRoot% / %SystemDrive%
            s = s.Replace("%SystemRoot%", Path.Combine(MountDir, "Windows"))
                 .Replace("%systemroot%", Path.Combine(MountDir, "Windows"))
                 .Replace("%SystemDrive%", MountDir.TrimEnd('\\'))
                 .Replace("%systemdrive%", MountDir.TrimEnd('\\'));
            // 其余环境变量按本机展开（离线场景多数无关，但保留兼容）
            s = Environment.ExpandEnvironmentVariables(s);
            return s;
        }

        /// <summary>按完整注册表路径打开离线配置单元中的子键；离线模式下 HKLM/HKCU 重定向到已加载 hive。</summary>
        public RegistryKey OpenReg(string fullPath, bool writable)
        {
            if (string.IsNullOrEmpty(fullPath)) return null;
            string rem = fullPath.Replace('/', '\\').Trim();
            string rootSubKey = null;
            string remainder = null;
            if (rem.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase) || rem.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase))
            {
                var after = StripHivePrefix(rem);
                if (after.StartsWith("SOFTWARE", StringComparison.OrdinalIgnoreCase)) { rootSubKey = "DismOffline_Software"; remainder = after.Substring("SOFTWARE".Length).TrimStart('\\'); }
                else if (after.StartsWith("SYSTEM", StringComparison.OrdinalIgnoreCase)) { rootSubKey = "DismOffline_System"; remainder = after.Substring("SYSTEM".Length).TrimStart('\\'); }
                else if (after.StartsWith("SECURITY", StringComparison.OrdinalIgnoreCase)) { rootSubKey = "DismOffline_Security"; remainder = after.Substring("SECURITY".Length).TrimStart('\\'); }
                else if (after.StartsWith("SAM", StringComparison.OrdinalIgnoreCase)) { rootSubKey = "DismOffline_Sam"; remainder = after.Substring("SAM".Length).TrimStart('\\'); }
                else { rootSubKey = "DismOffline_Software"; remainder = after; } // 兜底：写入 SOFTWARE
            }
            else if (rem.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) || rem.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase)
                  || rem.StartsWith("HKEY_USERS", StringComparison.OrdinalIgnoreCase) || rem.StartsWith("HKU", StringComparison.OrdinalIgnoreCase))
            {
                remainder = StripHivePrefix(rem);
                rootSubKey = "DismOffline_Default";
            }
            if (rootSubKey == null) return null;
            string full = rootSubKey + (string.IsNullOrEmpty(remainder) ? "" : "\\" + remainder);
            return Registry.LocalMachine.OpenSubKey(full, writable);
        }

        private static string StripHivePrefix(string rem)
        {
            int idx = rem.IndexOf('\\');
            return idx >= 0 ? rem.Substring(idx + 1) : "";
        }

        private static (bool, string) RunReg(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("reg.exe", args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return (false, "无法启动 reg.exe");
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(30000);
                return (p.ExitCode == 0, o);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private void UnloadAll()
        {
            foreach (var k in _loadedKeys)
            {
                try { RunReg($"unload \"HKLM\\{k}\""); }
                catch { }
            }
            _loadedKeys.Clear();
        }

        public void Dispose()
        {
            UnloadAll();
            if (Current == this) Current = null;
        }
    }
}
