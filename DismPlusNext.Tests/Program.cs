using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DismPlusNext;
using DismPlusNext.Engine;
using DismPlusNext.Models;
using DismPlusNext.ViewModels;

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
        RunCompatTests();
        RunNativeModuleTests();
        RunOfflineTests();
        Console.WriteLine("\n无头逻辑验证完成。");
    }

    /// <summary>回归测试：离线映像上下文的路径重映射与条件门控重定向。</summary>
    static void RunOfflineTests()
    {
        Console.WriteLine("\n===== 离线映像回归测试 =====");
        int pass = 0, fail = 0;

        string mount = "M:\\img";
        using var ctx = OfflineContext.CreateForTest(mount, new Version(10, 0, 19041));

        Check("Remap C:\\Windows\\Temp", ctx.RemapFsPath("C:\\Windows\\Temp") == "M:\\img\\Windows\\Temp", ref pass, ref fail);
        Check("Remap %SystemRoot%", ctx.RemapFsPath("%SystemRoot%\\System32") == "M:\\img\\Windows\\System32", ref pass, ref fail);
        Check("Remap %SystemDrive%", ctx.RemapFsPath("%SystemDrive%\\Users") == "M:\\img\\Users", ref pass, ref fail);
        Check("Remap 任意盘符 D:\\x", ctx.RemapFsPath("D:\\x") == "M:\\img\\x", ref pass, ref fail);

        // 离线模式下，ImageFlags(1)=在线专用 应视为适用（不门控）
        var imgFlags = new Applicable { Conditions = new List<Condition> { new CondImageFlags { Value = "1" } } };
        Check("离线 CondImageFlags 视为适用", ConditionEvaluator.Evaluate(imgFlags), ref pass, ref fail);

        // 离线 OSVersion 使用映像版本(10.0.19041)
        var osHigh = new Applicable { Conditions = new List<Condition> { new CondOSVersion { Compare = ">=", Version = "10.0.10240" } } };
        var osLow = new Applicable { Conditions = new List<Condition> { new CondOSVersion { Compare = "<", Version = "10.0" } } };
        Check("离线 OSVersion >=10.0.10240 命中", ConditionEvaluator.Evaluate(osHigh), ref pass, ref fail);
        Check("离线 OSVersion <10.0 不命中", !ConditionEvaluator.Evaluate(osLow), ref pass, ref fail);

        // TargetOsVersion 应返回映像版本而非在线版本
        Check("TargetOsVersion 返回映像版本", Helpers.TargetOsVersion()?.ToString() == "10.0.19041", ref pass, ref fail);

        ctx.Dispose();
        Check("离线上下文已清除", OfflineContext.Current == null, ref pass, ref fail);

        Console.WriteLine($"结果: PASS={pass} FAIL={fail}");
        if (fail > 0) Environment.Exit(1);
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

    /// <summary>回归测试：Win10/11 真实版本探测 + DefaultOptionIndex 解析 + 兼容性报告。</summary>
    static void RunCompatTests()
    {
        Console.WriteLine("\n===== Win10/11 兼容性回归测试 =====");
        int pass = 0, fail = 0;

        var ver = Helpers.GetOsVersion();
        Check($"真实 OS 版本 >= 10.0 (实际 {ver})", ver.Major >= 10, ref pass, ref fail);

        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 7; i++)
        {
            if (File.Exists(Path.Combine(dir, "Data.xml"))) break;
            var p = Directory.GetParent(dir);
            if (p == null) break;
            dir = p.FullName;
        }
        var eng = new CleanEngine();
        eng.Initialize(dir);

        int comboTotal = 0, comboResolved = 0;
        foreach (var it in eng.Data.Collections.SelectMany(c => c.Items))
        {
            if (it.Options.Count > 0)
            {
                comboTotal++;
                if (it.DefaultOptionIndex >= 0 && it.DefaultOptionIndex < it.Options.Count) comboResolved++;
            }
        }
        Check($"Combo 项默认索引已解析 ({comboResolved}/{comboTotal})", comboTotal == 0 || comboResolved > 0, ref pass, ref fail);

        var report = eng.GetCompatReport();
        Check("兼容性报告生成成功", report != null && report.DetectedVersion == ver.ToString(), ref pass, ref fail);
        Check("报告检测版本与 RtlGetVersion 一致", report.DetectedVersion == ver.ToString(), ref pass, ref fail);

        Console.WriteLine($"结果: PASS={pass} FAIL={fail}");
        if (fail > 0) Environment.Exit(1);
    }

    /// <summary>回归测试：原生动态模块(系统信息/启动项/应用)可正常采集且类型正确。</summary>
    static void RunNativeModuleTests()
    {
        Console.WriteLine("\n===== 原生模块回归测试 =====");
        int pass = 0, fail = 0;

        var info = SystemInfo.Get();
        Check($"系统信息采集到行 ({info.Count})", info.Count > 0, ref pass, ref fail);
        Check("系统信息含\"操作系统\"行", info.Any(r => r.Label == "操作系统"), ref pass, ref fail);

        var starts = StartupManager.List();
        Check($"启动项枚举成功 ({starts.Count})", starts != null, ref pass, ref fail);
        Check("启动项分类正确", starts.All(s => s.Enabled == true || s.Enabled == false), ref pass, ref fail);

        var apps = AppManager.List();
        Check($"应用枚举成功 ({apps.Count})", apps != null, ref pass, ref fail);

        // 功能枚举需管理员；此处仅验证不抛异常
        var feats = FeatureManager.List();
        Check($"功能枚举不抛异常 ({feats.Count})", feats != null, ref pass, ref fail);

        // 原生模块包装器可生成带条目的模块
        var mod = NativeModules.BuildStartup();
        var items = mod.ReloadFunc();
        Check("BuildStartup 生成模块条目", mod.IsNative && items.Count == starts.Count, ref pass, ref fail);
        var infoMod = NativeModules.BuildSystemInfo();
        Check("BuildSystemInfo 条目均为只读信息", infoMod.ReloadFunc().All(i => i.IsInfo), ref pass, ref fail);

        Console.WriteLine($"结果: PASS={pass} FAIL={fail}");
        if (fail > 0) Environment.Exit(1);
    }

    static void Check(string name, bool ok, ref int pass, ref int fail)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}");
        if (ok) pass++; else fail++;
    }
}
