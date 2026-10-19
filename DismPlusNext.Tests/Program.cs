using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DismPlusNext;
using DismPlusNext.Models;

class Program
{
    static void Main()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 7; i++)
        {
            if (File.Exists(Path.Combine(dir, "Data.xml"))) break;
            var p = Directory.GetParent(dir);
            if (p == null) break;
            dir = p.FullName;
        }
        Console.WriteLine("Repo root: " + dir);

        var eng = new CleanEngine();
        eng.Initialize(dir);
        Console.WriteLine($"语言包: {eng.Loc.LoadedPack}");
        Console.WriteLine($"模块数: {eng.Data.Collections.Count}");
        foreach (var c in eng.Data.Collections)
            Console.WriteLine($"  {c.Kind}: {c.Items.Count} 项");

        var first = eng.Data.Collections.SelectMany(c => c.Items).First();
        Console.WriteLine($"\n首条本地化: {first.DisplayName}");
        Console.WriteLine($"  分组: {first.Group}");
        Console.WriteLine($"  描述: {first.Description}");

        int applicable = 0, ops = 0;
        foreach (var c in eng.Data.Collections)
        {
            int ca = 0, co = 0;
            foreach (var it in c.Items)
                if (eng.IsApplicable(it)) { ca++; co += eng.ScanItem(it, _ => { }).Count; }
            Console.WriteLine($"  [{c.Kind}] 适用={ca} 预览操作={co}");
            applicable += ca; ops += co;
        }
        Console.WriteLine($"\n适用项: {applicable}，预览操作总数: {ops}");

        var sample = eng.Data.Collections.SelectMany(c => c.Items).First(it => eng.IsApplicable(it));
        Console.WriteLine($"\n抽样适用项: {sample.DisplayName}");
        Console.WriteLine($"  警告: {sample.Warning}");
        int n = 0;
        var res = eng.ScanItem(sample, s => { if (n++ < 8) Console.WriteLine("   " + s); });
        Console.WriteLine($"  该项的预览操作数: {res.Count}");

        RunApplyOptionsTests();
        Console.WriteLine("\n无头逻辑验证完成。");
    }

    /// <summary>回归测试：ApplyItem 必须按用户的 Combo 选项 / 开关选择执行对应分支。</summary>
    static void RunApplyOptionsTests()
    {
        Console.WriteLine("\n===== ApplyOptions 回归测试 =====");
        int pass = 0, fail = 0;
        var eng = new CleanEngine();

        // Combo 项：A=Data1, B=Data2, 默认=Data9
        var combo = new RuleItem();
        combo.Options.Add(new ComboOption { Name = "A", Activate = new List<ActionBase> { new ActRegWrite { Key = "HKCU\\T", Value = "v", Type = "REG_DWORD", Data = "1" } } });
        combo.Options.Add(new ComboOption { Name = "B", Activate = new List<ActionBase> { new ActRegWrite { Key = "HKCU\\T", Value = "v", Type = "REG_DWORD", Data = "2" } } });
        combo.DefaultActivate = new List<ActionBase> { new ActRegWrite { Key = "HKCU\\T", Value = "v", Type = "REG_DWORD", Data = "9" } };

        List<string> cap;
        cap = new List<string>(); eng.ApplyItem(combo, s => cap.Add(s), new ApplyOptions { ComboIndex = 0 });
        Check("Combo idx0 -> Data 1", cap.Any(x => x.Contains("= 1")), ref pass, ref fail);
        cap = new List<string>(); eng.ApplyItem(combo, s => cap.Add(s), new ApplyOptions { ComboIndex = 1 });
        Check("Combo idx1 -> Data 2", cap.Any(x => x.Contains("= 2")), ref pass, ref fail);
        cap = new List<string>(); eng.ApplyItem(combo, s => cap.Add(s), new ApplyOptions { ComboIndex = -1 });
        Check("Combo 默认 -> Data 9", cap.Any(x => x.Contains("= 9")), ref pass, ref fail);

        // 开关型：True=T, False=F
        var sw = new RuleItem();
        sw.SystemTrueActivate = new List<ActionBase> { new ActRegWrite { Key = "HKCU\\T", Value = "v", Type = "REG_DWORD", Data = "T" } };
        sw.SystemFalseActivate = new List<ActionBase> { new ActRegWrite { Key = "HKCU\\T", Value = "v", Type = "REG_DWORD", Data = "F" } };
        cap = new List<string>(); eng.ApplyItem(sw, s => cap.Add(s), new ApplyOptions { IsOn = true });
        Check("开关 开 -> Data T", cap.Any(x => x.Contains("= T")), ref pass, ref fail);
        cap = new List<string>(); eng.ApplyItem(sw, s => cap.Add(s), new ApplyOptions { IsOn = false });
        Check("开关 关 -> Data F", cap.Any(x => x.Contains("= F")), ref pass, ref fail);

        Console.WriteLine($"结果: PASS={pass} FAIL={fail}");
        if (fail > 0) Environment.Exit(1);
    }

    static void Check(string name, bool ok, ref int pass, ref int fail)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}");
        if (ok) pass++; else fail++;
    }
}
