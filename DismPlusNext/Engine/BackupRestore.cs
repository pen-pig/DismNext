using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DismPlusNext.Engine
{
    /// <summary>
    /// 基于 DISM 的系统备份/还原（WIM 映像）。
    /// 对应 Dism++ 的"备份还原"模块，采用干净室重新实现，仅依赖系统自带 dism.exe。
    /// 所有操作均需管理员权限，且会改动磁盘，调用方必须显式确认。
    /// </summary>
    public static class BackupRestore
    {
        public class ImageInfo
        {
            public int Index;
            public string Name = "";
            public string Version = "";
        }

        /// <summary>捕获当前系统（或指定目录）为 WIM 映像。</summary>
        public static (bool ok, string output) Capture(string imagePath, string captureDir, string name, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || string.IsNullOrWhiteSpace(captureDir))
                return (false, "映像路径与捕获目录均不能为空");
            string dir = Path.GetDirectoryName(Path.GetFullPath(imagePath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                try { Directory.CreateDirectory(dir); } catch (Exception ex) { return (false, "无法创建输出目录：" + ex.Message); }

            // dism /Capture-Image /ImageFile:path /CaptureDir:src /Name:name /Compress:max
            string args = $"/Capture-Image /ImageFile:\"{imagePath}\" /CaptureDir:\"{captureDir}\" /Name:\"{name}\" /Compress:max /CheckIntegrity";
            return RunDism(args, log);
        }

        /// <summary>把 WIM 映像应用到目标驱动器（会覆盖目标）。</summary>
        public static (bool ok, string output) Apply(string imagePath, int index, string applyDir, Action<string> log)
        {
            if (!File.Exists(imagePath)) return (false, "映像文件不存在：" + imagePath);
            string args = $"/Apply-Image /ImageFile:\"{imagePath}\" /Index:{index} /ApplyDir:\"{applyDir}\" /CheckIntegrity";
            return RunDism(args, log);
        }

        /// <summary>列出 WIM 映像中的卷信息（只读，无副作用）。</summary>
        public static (bool ok, List<ImageInfo> images, string output) ListImages(string imagePath)
        {
            var list = new List<ImageInfo>();
            if (!File.Exists(imagePath)) return (false, list, "映像文件不存在：" + imagePath);
            var (ok, outp) = RunDism($"/Get-ImageInfo /ImageFile:\"{imagePath}\"", null);
            if (ok) ParseImageInfo(outp, list);
            return (ok, list, outp);
        }

        private static void ParseImageInfo(string text, List<ImageInfo> outList)
        {
            int curIndex = 0; string curName = "", curVer = "";
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("Index :", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line.Substring(line.IndexOf(':') + 1).Trim(), out curIndex);
                else if (line.StartsWith("Name :", StringComparison.OrdinalIgnoreCase))
                    curName = line.Substring(line.IndexOf(':') + 1).Trim();
                else if (line.StartsWith("Version :", StringComparison.OrdinalIgnoreCase))
                { curVer = line.Substring(line.IndexOf(':') + 1).Trim(); outList.Add(new ImageInfo { Index = curIndex, Name = curName, Version = curVer }); }
            }
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
