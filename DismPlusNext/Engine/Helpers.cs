using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace DismPlusNext.Engine
{
    /// <summary>Windows 系统相关的辅助方法（环境变量展开、注册表访问）。</summary>
    public static class Helpers
    {
        /// <summary>展开 %VAR% 与 Dism++ 专用的 %0%（程序目录）。离线映像模式下重定向到挂载根。</summary>
        public static string ExpandVars(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            if (OfflineContext.Current != null) return OfflineContext.Current.RemapFsPath(s);
            s = s.Replace("%0%", AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            s = Environment.ExpandEnvironmentVariables(s);
            return s;
        }

        /// <summary>按完整注册表路径打开子键（离线模式下重定向到离线配置单元）。</summary>
        public static RegistryKey OpenRegKey(string fullPath, bool writable)
        {
            if (OfflineContext.Current != null) return OfflineContext.Current.OpenReg(fullPath, writable);
            var hive = HiveFromPath(fullPath, out string sub);
            return hive.OpenSubKey(sub, writable);
        }

        /// <summary>用于条件评估与目标判断的系统版本：离线模式下取映像版本。</summary>
        public static Version TargetOsVersion() => OfflineContext.Current?.ImageVersion ?? GetOsVersion();

        public static RegistryKey HiveFromPath(string path, out string subPath)
        {
            subPath = path;
            if (path.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase) || path.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase))
                return Registry.LocalMachine;
            if (path.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) || path.StartsWith("HKCU", StringComparison.OrdinalIgnoreCase))
                return Registry.CurrentUser;
            if (path.StartsWith("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase) || path.StartsWith("HKCR", StringComparison.OrdinalIgnoreCase))
                return Registry.ClassesRoot;
            if (path.StartsWith("HKEY_USERS", StringComparison.OrdinalIgnoreCase) || path.StartsWith("HKU", StringComparison.OrdinalIgnoreCase))
                return Registry.Users;
            if (path.StartsWith("HKEY_CURRENT_CONFIG", StringComparison.OrdinalIgnoreCase) || path.StartsWith("HKCC", StringComparison.OrdinalIgnoreCase))
                return Registry.CurrentConfig;
            return Registry.LocalMachine;
        }

        /// <summary>判断注册表键(及可选值)是否存在（离线模式下查离线配置单元）。</summary>
        public static bool RegExists(string keyPath, string valueName)
        {
            if (string.IsNullOrEmpty(keyPath)) return false;
            using var key = OpenRegKey(keyPath, false);
            if (key == null) return false;
            if (string.IsNullOrEmpty(valueName)) return true;
            return key.GetValue(valueName) != null;
        }

        public static string CurrentArch()
        {
            var a = RuntimeInformation.OSArchitecture;
            if (a == Architecture.X64) return "AMD64";
            if (a == Architecture.X86) return "x86";
            if (a == Architecture.Arm64) return "ARM64";
            if (a == Architecture.Arm) return "ARM";
            return a.ToString();
        }

        // ---- 真实 OS 版本探测（不受 app.manifest 兼容性限制）----
        [StructLayout(LayoutKind.Sequential)]
        private struct RtlOsVersionInfo
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
        }

        [DllImport("ntdll.dll", CharSet = CharSet.Unicode)]
        private static extern int RtlGetVersion(ref RtlOsVersionInfo lpVersionInformation);

        /// <summary>
        /// 返回真实 Windows 版本（RtlGetVersion，始终返回内核真实版本，不受清单兼容性影响）。
        /// .NET 的 Environment.OSVersion 在无 supportedOS 清单时会被压成 6.2，
        /// 导致 Win10/11 的 OSVersion 条件误判为不适用。本方法修复该问题。
        /// </summary>
        public static Version GetOsVersion()
        {
            try
            {
                var info = new RtlOsVersionInfo { dwOSVersionInfoSize = Marshal.SizeOf<RtlOsVersionInfo>() };
                if (RtlGetVersion(ref info) == 0)
                    return new Version(info.dwMajorVersion, info.dwMinorVersion, info.dwBuildNumber);
            }
            catch { }
            return Environment.OSVersion.Version;
        }

        public static bool CompareVersions(string op, string left, string right)
        {
            if (!Version.TryParse(left, out Version l)) Version.TryParse(left + ".0", out l);
            if (!Version.TryParse(right, out Version r)) Version.TryParse(right + ".0", out r);
            if (l == null || r == null) return false;
            int c = l.CompareTo(r);
            switch (op)
            {
                case "<": return c < 0;
                case "<=": return c <= 0;
                case ">": return c > 0;
                case ">=": return c >= 0;
                case "==": case "=": return c == 0;
                case "!=": return c != 0;
                default: return false;
            }
        }

        /// <summary>运行一段 PowerShell 脚本并返回 (成功, 全部输出)。用于封装 wusa / DISM / Get-* 等系统命令。</summary>
        public static (bool ok, string output) RunPowerShell(string script, int timeoutMs = 180000)
        {
            try
            {
                string tmp = Path.Combine(Path.GetTempPath(), "dismnext_ps_" + Guid.NewGuid().ToString("N") + ".ps1");
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
                bool exited = p.WaitForExit(timeoutMs);
                if (!exited) { try { p.Kill(); } catch { } }
                try { File.Delete(tmp); } catch { }
                return (p.ExitCode == 0 || exited, o);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }
    }
}
