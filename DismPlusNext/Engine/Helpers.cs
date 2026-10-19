using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DismPlusNext.Engine
{
    /// <summary>Windows 系统相关的辅助方法（环境变量展开、注册表访问）。</summary>
    public static class Helpers
    {
        /// <summary>展开 %VAR% 与 Dism++ 专用的 %0%（程序目录）。</summary>
        public static string ExpandVars(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            s = s.Replace("%0%", AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            s = Environment.ExpandEnvironmentVariables(s);
            return s;
        }

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

        /// <summary>判断注册表键(及可选值)是否存在。</summary>
        public static bool RegExists(string keyPath, string valueName)
        {
            if (string.IsNullOrEmpty(keyPath)) return false;
            var hive = HiveFromPath(keyPath, out string sub);
            using var key = hive.OpenSubKey(sub);
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
    }
}
