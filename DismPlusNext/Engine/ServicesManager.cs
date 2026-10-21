using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DismPlusNext.Engine
{
    /// <summary>单个 Windows 服务。</summary>
    public class ServiceEntry
    {
        public string Name = "";            // 服务名（短名）
        public string DisplayName = "";     // 显示名
        public string StartType = "";      // Auto / Manual / Disabled / Boot / System
        public string Status = "";         // Running / Stopped / ...
        public string Description = "";
    }

    /// <summary>
    /// 服务管理：原生 advapi32（服务控制管理器）+ 注册表实现，不调用 powershell / 不起子进程。
    /// 枚举走 EnumServicesStatusExW（名称/显示名/当前状态），启动类型与描述走注册表。
    /// </summary>
    public static class ServicesManager
    {
        const uint SERVICE_WIN32 = 0x30;
        const uint SERVICE_STATE_ALL = 0x3;
        const int SC_ENUM_PROCESS_INFO = 0;
        const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
        const uint SERVICE_START = 0x0010;
        const uint SERVICE_STOP = 0x0020;
        const uint SERVICE_QUERY_CONFIG = 0x0001;
        const uint SERVICE_CHANGE_CONFIG = 0x0002;
        const uint SERVICE_ALL_ACCESS = 0xF01FF;
        const uint SERVICE_CONTROL_STOP = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        struct SERVICE_STATUS_PROCESS
        {
            public uint dwServiceType, dwCurrentState, dwControlsAccepted, dwWin32ExitCode;
            public uint dwServiceSpecificExitCode, dwCheckPoint, dwWaitHint, dwProcessId, dwServiceFlags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ENUM_SERVICE_STATUS_PROCESS
        {
            public string lpServiceName;
            public string lpDisplayName;
            public SERVICE_STATUS_PROCESS ServiceStatus;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr OpenSCManagerW(string machine, string database, uint access);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool EnumServicesStatusExW(IntPtr hSCManager, int infoLevel, uint serviceType, uint serviceState,
            IntPtr lpServices, int cbBufSize, out int bytesNeeded, out int servicesReturned,
            ref int resumeHandle, string groupName);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr OpenServiceW(IntPtr hSCManager, string serviceName, uint access);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool CloseServiceHandle(IntPtr handle);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool StartServiceW(IntPtr hService, int argc, string argv);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool ControlService(IntPtr hService, uint control, ref SERVICE_STATUS_PROCESS status);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool ChangeServiceConfigW(IntPtr hService, uint serviceType, uint startType, uint errorControl,
            string binaryPath, string loadOrderGroup, IntPtr tagId, string dependencies, string serviceStartName,
            string password, string displayName);

        static string StateText(uint state) => state switch
        {
            1 => "Stopped",
            2 => "StartPending",
            3 => "StopPending",
            4 => "Running",
            5 => "ContinuePending",
            6 => "PausePending",
            7 => "Paused",
            _ => "Unknown",
        };

        static string StartTypeFromReg(string name)
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name, false);
                if (k == null) return "";
                object v = k.GetValue("Start");
                if (v is int i) return i switch { 0 => "Boot", 1 => "System", 2 => "Auto", 3 => "Manual", 4 => "Disabled", _ => "" };
            }
            catch { }
            return "";
        }

        static string DescFromReg(string name)
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name, false);
                return (k?.GetValue("Description") as string) ?? "";
            }
            catch { return ""; }
        }

        public static List<ServiceEntry> List()
        {
            var list = new List<ServiceEntry>();
            IntPtr scm = OpenSCManagerW(null, null, SERVICE_ALL_ACCESS);
            if (scm == IntPtr.Zero) scm = OpenSCManagerW(null, null, SC_MANAGER_ENUMERATE_SERVICE);
            if (scm == IntPtr.Zero) return list;
            try
            {
                int needed = 0, returned = 0, resume = 0;
                EnumServicesStatusExW(scm, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_STATE_ALL,
                    IntPtr.Zero, 0, out needed, out returned, ref resume, null);
                if (needed <= 0) return list;
                IntPtr buf = Marshal.AllocHGlobal(needed);
                try
                {
                    resume = 0;
                    if (!EnumServicesStatusExW(scm, SC_ENUM_PROCESS_INFO, SERVICE_WIN32, SERVICE_STATE_ALL,
                            buf, needed, out needed, out returned, ref resume, null))
                        return list;
                    int size = Marshal.SizeOf<ENUM_SERVICE_STATUS_PROCESS>();
                    for (int i = 0; i < returned; i++)
                    {
                        var s = Marshal.PtrToStructure<ENUM_SERVICE_STATUS_PROCESS>(buf + i * size);
                        list.Add(new ServiceEntry
                        {
                            Name = s.lpServiceName ?? "",
                            DisplayName = s.lpDisplayName ?? s.lpServiceName ?? "",
                            Status = StateText(s.ServiceStatus.dwCurrentState),
                            StartType = StartTypeFromReg(s.lpServiceName),
                            Description = DescFromReg(s.lpServiceName),
                        });
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            finally { CloseServiceHandle(scm); }
            return list.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        const uint SC_MANAGER_ENUMERATE_SERVICE = 0x0004;

        public static List<ExecResult> Start(ServiceEntry s, Action<string> log) => SetRunning(s, true, log);
        public static List<ExecResult> Stop(ServiceEntry s, Action<string> log) => SetRunning(s, false, log);

        private static List<ExecResult> SetRunning(ServiceEntry s, bool start, Action<string> log)
        {
            log ??= _ => { };
            var res = new List<ExecResult>();
            IntPtr scm = OpenSCManagerW(null, null, SC_MANAGER_CONNECT);
            if (scm == IntPtr.Zero)
            {
                string e = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                res.Add(new ExecResult { Success = false, Target = s.Name, Message = e }); return res;
            }
            IntPtr svc = OpenServiceW(scm, s.Name, start ? SERVICE_START : SERVICE_STOP);
            if (svc == IntPtr.Zero)
            {
                string e = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                CloseServiceHandle(scm);
                res.Add(new ExecResult { Success = false, Target = s.Name, Message = e }); return res;
            }
            bool ok;
            if (start) ok = StartServiceW(svc, 0, null);
            else { var st = new SERVICE_STATUS_PROCESS(); ok = ControlService(svc, SERVICE_CONTROL_STOP, ref st); }
            string err = ok ? "" : new Win32Exception(Marshal.GetLastWin32Error()).Message;
            CloseServiceHandle(svc); CloseServiceHandle(scm);
            if (ok) { log($"已{(start ? "启动" : "停止")}服务：{s.DisplayName}"); res.Add(new ExecResult { Success = true, Target = s.Name }); }
            else { log($"操作失败：{s.DisplayName} - {err}"); res.Add(new ExecResult { Success = false, Target = s.Name, Message = err }); }
            return res;
        }

        const uint SC_MANAGER_CONNECT = 0x0001;

        /// <summary>设置启动类型：Auto / Manual / Disabled / Boot / System。</summary>
        public static List<ExecResult> SetStartType(ServiceEntry s, string startType, Action<string> log)
        {
            log ??= _ => { };
            var res = new List<ExecResult>();
            uint st = startType switch { "Boot" => 0u, "System" => 1u, "Auto" => 2u, "Manual" => 3u, "Disabled" => 4u, _ => 0xFFFFFFFFu };
            if (st == 0xFFFFFFFF)
            {
                res.Add(new ExecResult { Success = false, Target = s.Name, Message = "非法启动类型" });
                return res;
            }
            IntPtr scm = OpenSCManagerW(null, null, SC_MANAGER_CONNECT);
            if (scm == IntPtr.Zero)
            {
                string e = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                res.Add(new ExecResult { Success = false, Target = s.Name, Message = e }); return res;
            }
            IntPtr svc = OpenServiceW(scm, s.Name, SERVICE_CHANGE_CONFIG);
            if (svc == IntPtr.Zero)
            {
                string e = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                CloseServiceHandle(scm);
                res.Add(new ExecResult { Success = false, Target = s.Name, Message = e }); return res;
            }
            bool ok = ChangeServiceConfigW(svc, SERVICE_NO_CHANGE, st, SERVICE_NO_CHANGE, null, null, IntPtr.Zero, null, null, null, null);
            string err = ok ? "" : new Win32Exception(Marshal.GetLastWin32Error()).Message;
            CloseServiceHandle(svc); CloseServiceHandle(scm);
            if (ok) { log($"已设置服务 {s.DisplayName} 启动类型={startType}"); res.Add(new ExecResult { Success = true, Target = s.Name }); }
            else { log($"设置失败：{s.DisplayName} - {err}"); res.Add(new ExecResult { Success = false, Target = s.Name, Message = err }); }
            return res;
        }
    }
}
