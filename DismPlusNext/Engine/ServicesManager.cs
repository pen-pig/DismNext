using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>服务管理：枚举系统服务，支持启动/停止/设置启动类型。</summary>
    public static class ServicesManager
    {
        public static List<ServiceEntry> List()
        {
            var list = new List<ServiceEntry>();
            // Get-CimInstance 取 StartMode（启动类型），Get-Service 取状态
            const string ps = "Get-CimInstance -ClassName Win32_Service | ForEach-Object { " +
                "$svc = $_; $gs = Get-Service -Name $svc.Name -ErrorAction SilentlyContinue; " +
                "\"[S]|$($svc.Name)|$(($svc.DisplayName -replace '[\\r\\n\\|]',' '))|$($svc.StartMode)|$(if($gs){$gs.Status}else{'-'})|$(($svc.Description -replace '[\\r\\n\\|]',' '))\" }";
            var (ok, text) = Helpers.RunPowerShell(ps);
            if (!ok) return list;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (!line.StartsWith("[S]|")) continue;
                var p = line.Split('|');
                if (p.Length < 6) continue;
                list.Add(new ServiceEntry
                {
                    Name = p[1],
                    DisplayName = p[2],
                    StartType = p[3],
                    Status = p[4],
                    Description = p[5],
                });
            }
            return list;
        }

        public static List<ExecResult> Start(ServiceEntry s, Action<string> log) => SetRunning(s, true, log);
        public static List<ExecResult> Stop(ServiceEntry s, Action<string> log) => SetRunning(s, false, log);

        private static List<ExecResult> SetRunning(ServiceEntry s, bool start, Action<string> log)
        {
            log ??= _ => { };
            string ps = $"{(start ? "Start" : "Stop")}-Service -Name '{s.Name.Replace("'", "''")}'";
            var (ok, text) = Helpers.RunPowerShell(ps);
            var res = new List<ExecResult>();
            if (ok) { log($"已{(start ? "启动" : "停止")}服务：{s.DisplayName}"); res.Add(new ExecResult { Success = true, Target = s.Name }); }
            else { log($"操作失败：{s.DisplayName} - {text}"); res.Add(new ExecResult { Success = false, Target = s.Name, Message = text }); }
            return res;
        }

        /// <summary>设置启动类型：Auto / Manual / Disabled / Boot / System。</summary>
        public static List<ExecResult> SetStartType(ServiceEntry s, string startType, Action<string> log)
        {
            log ??= _ => { };
            var res = new List<ExecResult>();
            if (startType != "Auto" && startType != "Manual" && startType != "Disabled" && startType != "Boot" && startType != "System")
            {
                res.Add(new ExecResult { Success = false, Target = s.Name, Message = "非法启动类型" });
                return res;
            }
            string ps = $"Set-Service -Name '{s.Name.Replace("'", "''")}' -StartupType {startType}";
            var (ok, text) = Helpers.RunPowerShell(ps);
            if (ok) { log($"已设置服务 {s.DisplayName} 启动类型={startType}"); res.Add(new ExecResult { Success = true, Target = s.Name }); }
            else { log($"设置失败：{s.DisplayName} - {text}"); res.Add(new ExecResult { Success = false, Target = s.Name, Message = text }); }
            return res;
        }
    }
}
