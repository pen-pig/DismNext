using System;
using System.Collections.Generic;
using System.Linq;

namespace DismPlusNext.Engine
{
    /// <summary>单个第三方驱动包（由 Get-WindowsDriver 枚举）。</summary>
    public class DriverEntry
    {
        public string PublishedName = "";   // oemXX.inf
        public string ClassName = "";       // 设备类别，如 Net / Display
        public string ProviderName = "";    // 厂商，如 Microsoft / Intel
        public string Version = "";
        public string Date = "";
        public string SignerName = "";
        public string FileName = "";        // 原始 inf 文件名
    }

    /// <summary>驱动管理：枚举在线系统的第三方驱动包，支持删除（卸载）。</summary>
    public static class DriverManager
    {
        public static List<DriverEntry> List()
        {
            var list = new List<DriverEntry>();
            const string ps =
                "Get-WindowsDriver -Online -All -ErrorAction SilentlyContinue | ForEach-Object { " +
                "\"[D]|$(if($_.Driver){$_.Driver}else{'-'})|$(if($_.ClassName){$_.ClassName}else{'-'})|$(if($_.ProviderName){($_.ProviderName -replace '[\\r\\n\\|]',' ')}else{'-'})|\" + " +
                "\"$(if($_.Version){$_.Version}else{'-'})|$(if($_.Date){$_.Date.ToString('yyyy-MM-dd')}else{'-'})|$(if($_.SignerName){($_.SignerName -replace '[\\r\\n\\|]',' ')}else{'-'})|$(if($_.OriginalFileName){Split-Path $_.OriginalFileName -Leaf}else{'-'})\" }";
            var (ok, text) = Helpers.RunPowerShell(ps);
            if (!ok) return list;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (!line.StartsWith("[D]|")) continue;
                var p = line.Split('|');
                if (p.Length < 8) continue;
                list.Add(new DriverEntry
                {
                    PublishedName = p[1],
                    ClassName = p[2],
                    ProviderName = p[3],
                    Version = p[4],
                    Date = p[5],
                    SignerName = p[6],
                    FileName = p[7],
                });
            }
            return list;
        }

        public static List<ExecResult> Delete(DriverEntry d, Action<string> log) => Uninstall(d, log);

        private static List<ExecResult> Uninstall(DriverEntry d, Action<string> log)
        {
            log ??= _ => { };
            var res = new List<ExecResult>();
            string ps = $"pnputil /delete-driver \"{d.PublishedName.Replace("\"", "")}\" /uninstall";
            var (ok, text) = Helpers.RunPowerShell(ps);
            if (ok) { log($"已删除驱动：{d.PublishedName}（{d.ProviderName} {d.FileName}）"); res.Add(new ExecResult { Success = true, Target = d.PublishedName }); }
            else { log($"删除失败：{d.PublishedName} - {text}"); res.Add(new ExecResult { Success = false, Target = d.PublishedName, Message = text }); }
            return res;
        }
    }
}
