using System;
using System.Collections.Generic;
using System.Linq;

namespace DismPlusNext.Engine
{
    /// <summary>单个计划任务。</summary>
    public class TaskEntry
    {
        public string Name = "";       // 任务名
        public string Path = "";       // 任务路径（含末尾反斜杠）
        public string State = "";      // Ready / Disabled / Running ...
        public string LastResult = ""; // 上次运行结果
        public string NextRun = "";    // 下次运行时间
        public string Command = "";    // 执行命令
        public bool Enabled => !State.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>计划任务管理：枚举全部计划任务，支持启用/禁用/运行/删除。</summary>
    public static class TaskManager
    {
        public static List<TaskEntry> List()
        {
            var list = new List<TaskEntry>();
            const string ps = "Get-ScheduledTask | ForEach-Object { " +
                "$t = $_; $a = $t.Actions | Select-Object -First 1; " +
                "$cmd = if ($a) { ($a.Execute + ' ' + $a.Arguments).Trim() } else { '' }; " +
                "$next = if ($t.NextRunTime) { $t.NextRunTime.ToString('yyyy-MM-dd HH:mm') } else { '-' }; " +
                "\"[T]|$($t.TaskName)|$($t.TaskPath)|$($t.State)|$($t.LastTaskResult)|$next|$cmd\" }";
            var (ok, text) = Helpers.RunPowerShell(ps);
            if (!ok) return list;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (!line.StartsWith("[T]|")) continue;
                var p = line.Split('|');
                if (p.Length < 7) continue;
                list.Add(new TaskEntry
                {
                    Name = p[1],
                    Path = p[2],
                    State = p[3],
                    LastResult = p[4],
                    NextRun = p[5],
                    Command = p[6],
                });
            }
            return list;
        }

        public static List<ExecResult> Enable(TaskEntry t, Action<string> log)
            => SetState(t, true, log);
        public static List<ExecResult> Disable(TaskEntry t, Action<string> log)
            => SetState(t, false, log);

        private static List<ExecResult> SetState(TaskEntry t, bool enable, Action<string> log)
        {
            log ??= _ => { };
            string verb = enable ? "Enable-ScheduledTask" : "Disable-ScheduledTask";
            string ps = $"{verb} -TaskName '{t.Name.Replace("'", "''")}' -TaskPath '{t.Path.Replace("'", "''")}'";
            var (ok, text) = Helpers.RunPowerShell(ps);
            var res = new List<ExecResult>();
            if (ok)
            {
                log($"{(enable ? "已启用" : "已禁用")}计划任务：{t.Name}");
                res.Add(new ExecResult { Success = true, Target = t.Name });
            }
            else
            {
                log($"操作失败：{t.Name} - {text}");
                res.Add(new ExecResult { Success = false, Target = t.Name, Message = text });
            }
            return res;
        }

        public static List<ExecResult> Run(TaskEntry t, Action<string> log)
        {
            log ??= _ => { };
            string ps = $"Start-ScheduledTask -TaskName '{t.Name.Replace("'", "''")}' -TaskPath '{t.Path.Replace("'", "''")}'";
            var (ok, text) = Helpers.RunPowerShell(ps);
            var res = new List<ExecResult>();
            if (ok) { log($"已触发运行：{t.Name}"); res.Add(new ExecResult { Success = true, Target = t.Name }); }
            else { log($"运行失败：{t.Name} - {text}"); res.Add(new ExecResult { Success = false, Target = t.Name, Message = text }); }
            return res;
        }

        public static List<ExecResult> Delete(TaskEntry t, Action<string> log)
        {
            log ??= _ => { };
            string ps = $"Unregister-ScheduledTask -TaskName '{t.Name.Replace("'", "''")}' -TaskPath '{t.Path.Replace("'", "''")}' -Confirm:$false";
            var (ok, text) = Helpers.RunPowerShell(ps);
            var res = new List<ExecResult>();
            if (ok) { log($"已删除计划任务：{t.Name}"); res.Add(new ExecResult { Success = true, Target = t.Name }); }
            else { log($"删除失败：{t.Name} - {text}"); res.Add(new ExecResult { Success = false, Target = t.Name, Message = text }); }
            return res;
        }
    }
}
