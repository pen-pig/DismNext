using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DismPlusNext.Engine
{
    /// <summary>系统信息行（只读）。</summary>
    public class SysInfoRow
    {
        public string Label;
        public string Value;
    }

    /// <summary>采集本机系统信息（只读展示，不改动系统）。</summary>
    public static class SystemInfo
    {
        [StructLayout(LayoutKind.Sequential)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public static List<SysInfoRow> Get()
        {
            var rows = new List<SysInfoRow>();
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                string product = key?.GetValue("ProductName") as string ?? "未知";
                string build = key?.GetValue("CurrentBuildNumber") as string ?? "?";
                string displayVer = key?.GetValue("DisplayVersion") as string ?? "";
                var ver = Helpers.GetOsVersion();
                rows.Add(new SysInfoRow { Label = "操作系统", Value = product + (string.IsNullOrEmpty(displayVer) ? "" : " (" + displayVer + ")") });
                rows.Add(new SysInfoRow { Label = "版本 / 构建", Value = $"{ver.Major}.{ver.Minor}.{build}" });
                rows.Add(new SysInfoRow { Label = "系统架构", Value = Environment.Is64BitOperatingSystem ? "64 位" : "32 位" });
                rows.Add(new SysInfoRow { Label = "计算机名", Value = Environment.MachineName });
                rows.Add(new SysInfoRow { Label = "当前用户", Value = Environment.UserDomainName + "\\" + Environment.UserName });

                using var cpu = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                string cpuName = cpu?.GetValue("ProcessorNameString") as string;
                if (!string.IsNullOrWhiteSpace(cpuName)) rows.Add(new SysInfoRow { Label = "处理器", Value = cpuName.Trim() });

                var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (GlobalMemoryStatusEx(ms))
                {
                    double total = ms.ullTotalPhys / 1073741824.0;
                    double avail = ms.ullAvailPhys / 1073741824.0;
                    rows.Add(new SysInfoRow { Label = "物理内存", Value = $"{total:F1} GB 总计 / {avail:F1} GB 可用" });
                }

                var sysDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
                try
                {
                    var di = new System.IO.DriveInfo(sysDrive);
                    double total = di.TotalSize / 1073741824.0;
                    double free = di.AvailableFreeSpace / 1073741824.0;
                    rows.Add(new SysInfoRow { Label = "系统盘 (" + sysDrive + ")", Value = $"{total:F1} GB 总计 / {free:F1} GB 可用" });
                }
                catch { }

                rows.Add(new SysInfoRow { Label = "激活状态", Value = GetActivationStatus() });
                rows.Add(new SysInfoRow { Label = ".NET 运行时", Value = $".NET {Environment.Version}（本程序运行于 {RuntimeInformation.FrameworkDescription}）" });
            }
            catch (Exception ex)
            {
                rows.Add(new SysInfoRow { Label = "采集异常", Value = ex.Message });
            }
            return rows;
        }

        private static string GetActivationStatus()
        {
            EncodingHelper.Ensure();
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c slmgr /xpr")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = System.Text.Encoding.GetEncoding(936)
                };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p == null) return "未知";
                string outp = p.StandardOutput.ReadToEnd();
                p.WaitForExit(8000);
                if (outp.Contains("永久") || outp.Contains("permanently")) return "已永久激活";
                if (outp.Contains("将会在") || outp.Contains("will expire")) return "批量/限时激活";
                return string.IsNullOrWhiteSpace(outp) ? "未知" : outp.Trim();
            }
            catch { return "未知"; }
        }
    }
}
