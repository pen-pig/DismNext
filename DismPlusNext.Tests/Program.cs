using System;
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
        Console.WriteLine("\n无头逻辑验证完成。");
    }
}
