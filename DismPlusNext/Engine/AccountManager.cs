using System;
using System.Collections.Generic;
using System.Linq;

namespace DismPlusNext.Engine
{
    /// <summary>单个本地用户账户。</summary>
    public class AccountEntry
    {
        public string Name = "";
        public bool Enabled;
        public string Description = "";
        public string Sid = "";
        public string LastLogon = "";
    }

    /// <summary>账户管理：枚举本地用户，支持启用/禁用（对应 Dism++ 工具箱“账户管理”）。</summary>
    public static class AccountManager
    {
        public static List<AccountEntry> List()
        {
            var list = new List<AccountEntry>();
            const string ps =
                "Get-LocalUser -ErrorAction SilentlyContinue | ForEach-Object { " +
                "\"[U]|$(if($_.Name){($_.Name -replace '[\\r\\n\\|]',' ')}else{'-'})|$(if($_.Enabled){'1'}else{'0'})|\" + " +
                "\"$(if($_.Description){($_.Description -replace '[\\r\\n\\|]',' ')}else{'-'})|$(if($_.SID){$_.SID.Value}else{'-'})|$(if($_.LastLogon){$_.LastLogon.ToString('yyyy-MM-dd HH:mm')}else{'-'})\" }";
            var (ok, text) = Helpers.RunPowerShell(ps);
            if (!ok) return list;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (!line.StartsWith("[U]|")) continue;
                var p = line.Split('|');
                if (p.Length < 6) continue;
                list.Add(new AccountEntry
                {
                    Name = p[1],
                    Enabled = p[2] == "1",
                    Description = p[3],
                    Sid = p[4],
                    LastLogon = p[5],
                });
            }
            return list;
        }

        public static List<ExecResult> Toggle(AccountEntry a, bool enable, Action<string> log)
        {
            log ??= _ => { };
            var res = new List<ExecResult>();
            string ps = enable
                ? $"Enable-LocalUser -Name '{a.Name.Replace("'", "''")}'"
                : $"Disable-LocalUser -Name '{a.Name.Replace("'", "''")}'";
            var (ok, text) = Helpers.RunPowerShell(ps);
            if (ok) { log($"已{(enable ? "启用" : "禁用")}账户：{a.Name}"); res.Add(new ExecResult { Success = true, Target = a.Name }); }
            else { log($"操作失败：{a.Name} - {text}"); res.Add(new ExecResult { Success = false, Target = a.Name, Message = text }); }
            return res;
        }
    }
}
