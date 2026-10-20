using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DismPlusNext.Engine
{
    /// <summary>
    /// 离线映像服务（Dism++ 招牌功能）：基于系统自带 dism.exe 挂载/卸载 WIM/ESD 映像，
    /// 配合 OfflineContext 把 Data.xml 的清理/优化重定向到挂载映像。
    /// 所有挂载/卸载均需管理员权限，由调用方确认。
    /// </summary>
    public static class ImageServicing
    {
        public class WimIndexInfo
        {
            public int Index;
            public string Name = "";
            public string Description = "";
            public string Version = "";
            public string Architecture = "";
        }

        /// <summary>列出 WIM/ESD 映像中的卷信息（只读，无副作用）。</summary>
        public static (bool ok, List<WimIndexInfo> indices, string output) GetWimInfo(string wimPath)
        {
            var list = new List<WimIndexInfo>();
            if (string.IsNullOrWhiteSpace(wimPath)) return (false, list, "映像路径为空");
            if (!File.Exists(wimPath)) return (false, list, "映像文件不存在：" + wimPath);
            var (ok, outp) = RunDism($"/Get-WimInfo /WimFile:\"{wimPath}\"", null);
            if (ok) ParseWimInfo(outp, list);
            return (ok, list, outp);
        }

        /// <summary>挂载映像到指定目录（readOnly=true 时只读，否则可读写以应用更改）。</summary>
        public static (bool ok, string output) Mount(string wimPath, int index, string mountDir, bool readOnly)
        {
            if (!File.Exists(wimPath)) return (false, "映像文件不存在：" + wimPath);
            try { if (!Directory.Exists(mountDir)) Directory.CreateDirectory(mountDir); }
            catch (Exception ex) { return (false, "无法创建挂载目录：" + ex.Message); }
            string args = $"/Mount-Image /ImageFile:\"{wimPath}\" /Index:{index} /MountDir:\"{mountDir}\"" + (readOnly ? " /ReadOnly" : "");
            return RunDism(args, null);
        }

        /// <summary>卸载挂载目录。commit=true 提交更改，false 丢弃。</summary>
        public static (bool ok, string output) Unmount(string mountDir, bool commit)
        {
            if (string.IsNullOrWhiteSpace(mountDir)) return (false, "挂载目录为空");
            string args = $"/Unmount-Image /MountDir:\"{mountDir}\"" + (commit ? " /Commit" : " /Discard");
            return RunDism(args, null);
        }

        /// <summary>列出当前已挂载的映像（只读）。</summary>
        public static (bool ok, string output) ListMounted()
        {
            return RunDism("/Get-MountedImageInfo", null);
        }

        private static void ParseWimInfo(string text, List<WimIndexInfo> outList)
        {
            int idx = 0; string name = "", desc = "", ver = "", arch = "";
            void Flush()
            {
                if (idx > 0)
                    outList.Add(new WimIndexInfo { Index = idx, Name = name, Description = desc, Version = ver, Architecture = arch });
            }
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("Index :", StringComparison.OrdinalIgnoreCase))
                {
                    Flush(); // 遇到新的 Index，先收尾上一个
                    idx = 0; name = desc = ver = arch = "";
                    int.TryParse(line.Substring(line.IndexOf(':') + 1).Trim(), out idx);
                }
                else if (line.StartsWith("Name :", StringComparison.OrdinalIgnoreCase)) name = line.Substring(line.IndexOf(':') + 1).Trim();
                else if (line.StartsWith("Description :", StringComparison.OrdinalIgnoreCase)) desc = line.Substring(line.IndexOf(':') + 1).Trim();
                else if (line.StartsWith("Version :", StringComparison.OrdinalIgnoreCase)) ver = line.Substring(line.IndexOf(':') + 1).Trim();
                else if (line.StartsWith("Architecture :", StringComparison.OrdinalIgnoreCase)) arch = line.Substring(line.IndexOf(':') + 1).Trim();
            }
            Flush(); // 收尾最后一条
        }

        private static (bool, string) RunDism(string args, Action<string> log)
        {
            try
            {
                var psi = new ProcessStartInfo("dism.exe", args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return (false, "无法启动 dism.exe（可能缺少管理员权限）");
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(1800000);
                log?.Invoke(o.Length > 800 ? o.Substring(o.Length - 800) : o);
                return (p.ExitCode == 0, o);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }
    }
}
