using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DismPlusNext.Engine
{
    /// <summary>hosts 文件中的一条记录。</summary>
    public class HostsEntry
    {
        public bool Enabled;     // true=生效；false=被注释(#)禁用
        public string Ip = "";
        public string Hostname = "";
        public string Comment = "";
        public bool IsBlank;     // 原文件中的空行/纯注释分隔行
    }

    /// <summary>hosts 文件编辑：解析、启用/禁用、新增、删除。文件位于 %SystemRoot%\System32\drivers\etc\hosts（离线模式重定向）。</summary>
    public static class HostsManager
    {
        private static string HostsPath()
            => Helpers.ExpandVars(@"%SystemRoot%\System32\drivers\etc\hosts");

        public static List<HostsEntry> Parse()
        {
            EncodingHelper.Ensure();
            var list = new List<HostsEntry>();
            string path = HostsPath();
            if (!File.Exists(path)) return list;
            var lines = File.ReadAllLines(path, Encoding.GetEncoding(936));
            foreach (var raw in lines)
            {
                var line = raw;
                if (line.Length == 0) { list.Add(new HostsEntry { IsBlank = true }); continue; }
                string comment = "";
                int hash = line.IndexOf('#');
                if (hash >= 0) { comment = line.Substring(hash + 1).Trim(); line = line.Substring(0, hash).Trim(); }
                if (line.Length == 0)
                {
                    list.Add(new HostsEntry { IsBlank = true, Comment = comment });
                    continue;
                }
                bool enabled = true;
                if (line.StartsWith("#"))
                {
                    enabled = false;
                    line = line.Substring(1).Trim();
                }
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    list.Add(new HostsEntry { IsBlank = true, Comment = comment });
                    continue;
                }
                list.Add(new HostsEntry
                {
                    Enabled = enabled,
                    Ip = parts[0],
                    Hostname = parts[1],
                    Comment = comment,
                });
            }
            return list;
        }

        /// <summary>写回 hosts 文件（重建全部内容）。</summary>
        private static List<ExecResult> WriteAll(List<HostsEntry> entries, Action<string> log)
        {
            var res = new List<ExecResult>();
            EncodingHelper.Ensure();
            string path = HostsPath();
            try
            {
                var sb = new StringBuilder();
                foreach (var e in entries)
                {
                    if (e.IsBlank) { sb.AppendLine(string.IsNullOrEmpty(e.Comment) ? "" : "# " + e.Comment); continue; }
                    string body = $"{e.Ip}\t{e.Hostname}";
                    if (!string.IsNullOrEmpty(e.Comment)) body += " # " + e.Comment;
                    sb.AppendLine(e.Enabled ? body : "#" + body);
                }
                File.WriteAllText(path, sb.ToString(), Encoding.GetEncoding(936));
                log($"已写入 hosts：{entries.Count(e => !e.IsBlank)} 条记录");
                res.Add(new ExecResult { Success = true, Target = path });
            }
            catch (Exception ex)
            {
                log($"写入 hosts 失败：{ex.Message}（需管理员权限）");
                res.Add(new ExecResult { Success = false, Target = path, Message = ex.Message });
            }
            return res;
        }

        public static List<ExecResult> SetEnabled(HostsEntry target, bool enable, Action<string> log)
        {
            var list = Parse();
            var hit = list.FirstOrDefault(e => !e.IsBlank && e.Ip == target.Ip && e.Hostname == target.Hostname);
            if (hit == null) { var r = new List<ExecResult> { new ExecResult { Success = false, Target = target.Hostname, Message = "未找到记录" } }; (log ?? (_ => { }))("未找到记录：" + target.Hostname); return r; }
            hit.Enabled = enable;
            return WriteAll(list, log ?? (_ => { }));
        }

        public static List<ExecResult> Add(string ip, string hostname, Action<string> log)
        {
            var list = Parse();
            if (list.Any(e => !e.IsBlank && e.Ip == ip && e.Hostname == hostname))
            { var r = new List<ExecResult> { new ExecResult { Success = false, Target = hostname, Message = "已存在" } }; (log ?? (_ => { }))("已存在：" + hostname); return r; }
            list.Add(new HostsEntry { Enabled = true, Ip = ip, Hostname = hostname });
            return WriteAll(list, log ?? (_ => { }));
        }

        public static List<ExecResult> Remove(string ip, string hostname, Action<string> log)
        {
            var list = Parse();
            var before = list.Count;
            list.RemoveAll(e => !e.IsBlank && e.Ip == ip && e.Hostname == hostname);
            if (list.Count == before) { var r = new List<ExecResult> { new ExecResult { Success = false, Target = hostname, Message = "未找到记录" } }; (log ?? (_ => { }))("未找到记录：" + hostname); return r; }
            return WriteAll(list, log ?? (_ => { }));
        }
    }
}
