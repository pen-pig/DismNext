using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DismPlusNext;
using DismPlusNext.Engine;
using DismPlusNext.Models;
using DismPlusNext.ViewModels;

namespace DismPlusNext.Cli
{
    /// <summary>
    /// Dism-Plus-Next 命令行界面。
    /// 用法：
    ///   DismPlusNext.Cli list
    ///   DismPlusNext.Cli scan  &lt;CleanCollection4|SystemOptimization&gt; [-l zh-Hans.xml]
    ///   DismPlusNext.Cli apply &lt;模块&gt; [-l 语言] --yes
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0) { PrintUsage(); return 1; }

            string root = FindRepoRoot();
            if (!File.Exists(Path.Combine(root, "Data.xml")))
            {
                Console.Error.WriteLine($"错误：在 {root} 找不到 Data.xml");
                return 2;
            }

            string lang = "zh-Hant.xml";
            var rest = new List<string>();
            bool yes = false;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "-l" && i + 1 < args.Length) lang = args[++i];
                else if (args[i] == "--yes") yes = true;
                else rest.Add(args[i]);
            }

            var eng = new CleanEngine();
            eng.Initialize(root, lang);

            switch (args[0].ToLowerInvariant())
            {
                case "list": return CmdList(eng);
                case "scan": return CmdScan(eng, rest.FirstOrDefault());
                case "apply": return CmdApply(eng, rest.FirstOrDefault(), yes);
                case "backup": return CmdBackup(rest);
                case "restore": return CmdRestore(rest, yes);
                case "images": return CmdImages(rest.FirstOrDefault());
                case "report": return CmdReport(eng, rest.FirstOrDefault());
                case "info": return CmdInfo();
                case "startup": return CmdStartup(rest, yes);
                case "apps": return CmdApps(rest, yes);
                case "features": return CmdFeatures(rest, yes);
                default: PrintUsage(); return 1;
            }
        }

        static int CmdList(CleanEngine eng)
        {
            Console.WriteLine($"语言包：{eng.LoadedPack}");
            foreach (var c in eng.Data.Collections)
            {
                int ap = c.Items.Count(it => eng.IsApplicable(it));
                Console.WriteLine($"  {c.Kind,-20} {c.DisplayName,-12} 条目={c.Items.Count} 适用={ap}");
            }
            return 0;
        }

        static int CmdScan(CleanEngine eng, string kind)
        {
            var coll = eng.Data.Collections.FirstOrDefault(c => c.Kind == kind);
            if (coll == null) { Console.Error.WriteLine($"未知模块：{kind}"); return 2; }
            Console.WriteLine($"扫描模块：{coll.DisplayName} ({coll.Kind})");
            long total = 0;
            int items = 0;
            foreach (var it in coll.Items)
            {
                if (!eng.IsApplicable(it)) continue;
                var res = eng.ScanItem(it, null);
                long sz = res.Sum(r => r.SizeAffected);
                total += sz; items++;
                Console.WriteLine($"  [{(res.Count > 0 ? "✓" : "·")}] {it.DisplayName,-40} {sz / 1048576.0:F2} MB  ({res.Count} 操作)");
            }
            Console.WriteLine($"---- 适用 {items} 项，预计可回收/影响：{total / 1048576.0:F2} MB ----");
            return 0;
        }

        static int CmdApply(CleanEngine eng, string kind, bool yes)
        {
            var coll = eng.Data.Collections.FirstOrDefault(c => c.Kind == kind);
            if (coll == null) { Console.Error.WriteLine($"未知模块：{kind}"); return 2; }
            if (!yes)
            {
                Console.Error.WriteLine("这是破坏性操作。请加 --yes 确认。已取消。");
                return 3;
            }
            Console.WriteLine($"应用模块：{coll.DisplayName}");
            int ok = 0, fail = 0;
            foreach (var it in coll.Items)
            {
                if (!eng.IsApplicable(it)) continue;
                var res = eng.ApplyItem(it, s => Console.WriteLine("    " + s));
                ok += res.Count(r => r.Success);
                fail += res.Count(r => !r.Success);
            }
            Console.WriteLine($"完成：成功 {ok} / 失败 {fail}");
            return 0;
        }

        static int CmdBackup(List<string> rest)
        {
            string image = rest.FirstOrDefault();
            if (string.IsNullOrEmpty(image)) { Console.Error.WriteLine("用法: backup <image.wim> [-d C:\\] [-n 名称]"); return 2; }
            string dir = "C:\\";
            string name = "DismPlusNext_Backup_" + DateTime.Now.ToString("yyyyMMdd");
            for (int i = 1; i < rest.Count; i++)
            {
                if (rest[i] == "-d" && i + 1 < rest.Count) dir = rest[++i];
                else if (rest[i] == "-n" && i + 1 < rest.Count) name = rest[++i];
            }
            Console.WriteLine($"捕获 {dir} -> {image}（名称：{name}）");
            var (ok, outp) = BackupRestore.Capture(image, dir, name, s => Console.WriteLine("    " + s));
            Console.WriteLine(ok ? "完成。" : "失败：" + outp);
            return ok ? 0 : 1;
        }

        static int CmdRestore(List<string> rest, bool yes)
        {
            string image = rest.FirstOrDefault();
            if (string.IsNullOrEmpty(image)) { Console.Error.WriteLine("用法: restore <image.wim> [-i 1] [-d C:\\]"); return 2; }
            int idx = 1; string dir = "C:\\";
            for (int i = 1; i < rest.Count; i++)
            {
                if (rest[i] == "-i" && i + 1 < rest.Count) int.TryParse(rest[++i], out idx);
                else if (rest[i] == "-d" && i + 1 < rest.Count) dir = rest[++i];
            }
            if (!yes) { Console.Error.WriteLine("还原会覆盖目标驱动器，请加 --yes 确认。已取消。"); return 3; }
            Console.WriteLine($"还原 {image} (索引 {idx}) -> {dir}");
            var (ok, outp) = BackupRestore.Apply(image, idx, dir, s => Console.WriteLine("    " + s));
            Console.WriteLine(ok ? "完成。" : "失败：" + outp);
            return ok ? 0 : 1;
        }

        static int CmdImages(string image)
        {
            if (string.IsNullOrEmpty(image)) { Console.Error.WriteLine("用法: images <image.wim>"); return 2; }
            var (ok, imgs, outp) = BackupRestore.ListImages(image);
            if (!ok) { Console.Error.WriteLine("读取失败：" + outp); return 1; }
            foreach (var im in imgs)
                Console.WriteLine($"  [{im.Index}] {im.Name}  ({im.Version})");
            if (imgs.Count == 0) Console.WriteLine("（无卷信息）");
            return 0;
        }

        static int CmdReport(CleanEngine eng, string outPath)
        {
            var r = eng.GetCompatReport();
            if (string.IsNullOrEmpty(outPath))
                outPath = $"CompatReport_{r.DetectedVersion.Replace('.', '_')}.md";
            var sb = new StringBuilder();
            sb.AppendLine("# Dism-Plus-Next 系统兼容性报告");
            sb.AppendLine();
            sb.AppendLine($"- 检测时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- 真实 OS 版本：**{r.DetectedVersion}**（RtlGetVersion）");
            sb.AppendLine($"- .NET 报告版本：{r.ReportedVersion}（Environment.OSVersion）");
            sb.AppendLine($"- 总条目：{r.TotalItems} · 适用：{r.ApplicableItems} · 因系统版本/架构排除：{r.OsExcludedCount}");
            sb.AppendLine();
            foreach (var c in r.Collections)
            {
                sb.AppendLine($"## {c.Name}（`{c.Kind}`）");
                sb.AppendLine($"- 条目 {c.Total} · 适用 {c.Applicable} · 因系统版本排除 {c.OsExcluded.Count}");
                if (c.OsExcluded.Count > 0)
                {
                    sb.AppendLine("- 因 OSVersion/Arch 不适用：");
                    foreach (var n in c.OsExcluded) sb.AppendLine($"  - {n}");
                }
                sb.AppendLine();
            }
            File.WriteAllText(outPath, sb.ToString());
            Console.WriteLine($"报告已生成：{outPath}");
            Console.WriteLine($"真实版本 {r.DetectedVersion} · 适用 {r.ApplicableItems}/{r.TotalItems} · 排除 {r.OsExcludedCount}");
            return 0;
        }

        static int CmdInfo()
        {
            Console.WriteLine("系统信息：");
            foreach (var r in SystemInfo.Get())
                Console.WriteLine($"  {r.Label,-14}: {r.Value}");
            return 0;
        }

        static int CmdStartup(List<string> rest, bool yes)
        {
            string action = rest.FirstOrDefault()?.ToLowerInvariant() ?? "list";
            var list = StartupManager.List();
            if (action == "list")
            {
                foreach (var e in list)
                    Console.WriteLine($"  [{(e.Enabled ? "启用" : "禁用")}] {e.Name,-32} {e.Location}");
                Console.WriteLine($"---- 共 {list.Count} 项 ----");
                return 0;
            }
            string target = string.Join(" ", rest.Skip(1));
            return ActOnNamed(target, action, yes,
                name => list.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)),
                e => StartupManager.Toggle((StartupEntry)e, action == "enable", s => Console.WriteLine("    " + s)));
        }

        static int CmdApps(List<string> rest, bool yes)
        {
            string action = rest.FirstOrDefault()?.ToLowerInvariant() ?? "list";
            var list = AppManager.List();
            if (action == "list")
            {
                foreach (var a in list)
                    Console.WriteLine($"  [{(a.IsUwp ? "UWP" : "程序")}] {a.Name,-40} {(string.IsNullOrEmpty(a.Version) ? "" : "v" + a.Version)}");
                Console.WriteLine($"---- 共 {list.Count} 项 ----");
                return 0;
            }
            string target = string.Join(" ", rest.Skip(1));
            return ActOnNamed(target, action, yes,
                name => list.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)),
                a => AppManager.Uninstall((AppEntry)a, s => Console.WriteLine("    " + s)));
        }

        static int CmdFeatures(List<string> rest, bool yes)
        {
            string action = rest.FirstOrDefault()?.ToLowerInvariant() ?? "list";
            var list = FeatureManager.List();
            if (action == "list")
            {
                foreach (var f in list)
                    Console.WriteLine($"  [{(f.Enabled ? "启用" : "禁用")}] {f.Name}");
                Console.WriteLine($"---- 共 {list.Count} 项 ----");
                return 0;
            }
            string target = string.Join(" ", rest.Skip(1));
            return ActOnNamed(target, action, yes,
                name => list.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)),
                f => FeatureManager.SetState((FeatureEntry)f, action == "enable", s => Console.WriteLine("    " + s)));
        }

        static int ActOnNamed(string target, string action, bool yes, Func<string, object> find, Func<object, List<ExecResult>> run)
        {
            if (string.IsNullOrWhiteSpace(target)) { Console.Error.WriteLine("请指定名称。"); return 2; }
            var e = find(target);
            if (e == null) { Console.Error.WriteLine($"未找到：{target}"); return 2; }
            if (!yes) { Console.Error.WriteLine("破坏性操作，请加 --yes 确认。已取消。"); return 3; }
            int ok = 0, fail = 0;
            var res = run(e);
            foreach (var r in res) { if (r.Success) ok++; else fail++; Console.WriteLine($"  [{ (r.Success ? "✓" : "✗") }] {r.Target} {r.Message}"); }
            Console.WriteLine($"完成：成功 {ok} / 失败 {fail}");
            return fail > 0 ? 1 : 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine("Dism-Plus-Next CLI");
            Console.WriteLine("  list");
            Console.WriteLine("  scan  <CleanCollection4|SystemOptimization> [-l <语言包.xml>]");
            Console.WriteLine("  apply <模块> [-l <语言包.xml>] --yes");
            Console.WriteLine("  backup  <image.wim> [-d C:\\] [-n 名称]");
            Console.WriteLine("  restore <image.wim> [-i 1] [-d C:\\] --yes");
            Console.WriteLine("  images  <image.wim>");
            Console.WriteLine("  report  [输出md路径]   生成 Win10/11 兼容性报告");
            Console.WriteLine("  info                        显示系统信息");
            Console.WriteLine("  startup [list|disable|enable <名称>] [--yes]");
            Console.WriteLine("  apps    [list|uninstall <名称>] [--yes]");
            Console.WriteLine("  features[list|enable|disable <名称>] [--yes]");
        }

        static string FindRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                if (File.Exists(Path.Combine(dir, "Data.xml"))) return dir;
                var p = Directory.GetParent(dir);
                if (p == null) break;
                dir = p.FullName;
            }
            return AppContext.BaseDirectory;
        }
    }
}
