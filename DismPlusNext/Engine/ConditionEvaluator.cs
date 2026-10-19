using System;
using System.Collections.Generic;
using System.IO;
using DismPlusNext.Models;

namespace DismPlusNext.Engine
{
    /// <summary>评估规则的适用条件（Applicable）是否命中当前系统。</summary>
    public static class ConditionEvaluator
    {
        public static bool Evaluate(Applicable appl)
        {
            if (appl == null || appl.Conditions.Count == 0) return true; // 无条件即默认适用
            foreach (var c in appl.Conditions)
                if (!Eval(c, null)) return false; // 多个条件按 AND 处理
            return true;
        }

        /// <summary>评估并返回未命中的原因（用于兼容性报告）。</summary>
        public static (bool ok, List<string> reasons) EvaluateWithReasons(Applicable appl)
        {
            var reasons = new List<string>();
            if (appl == null || appl.Conditions.Count == 0) return (true, reasons);
            foreach (var c in appl.Conditions)
                if (!Eval(c, reasons)) return (false, reasons);
            return (true, reasons);
        }

        private static bool Eval(Condition c, List<string> reasons)
        {
            switch (c)
            {
                case CondFileExist x:
                    {
                        string p = Helpers.ExpandVars(x.FilePath);
                        bool ok = File.Exists(p) || Directory.Exists(p);
                        if (!ok) reasons?.Add($"文件/目录不存在：{p}");
                        return ok;
                    }
                case CondImageFlags x:
                    {
                        bool ok = x.Value == "1";
                        if (!ok) reasons?.Add("映像标志不符（要求在线系统）");
                        return ok;
                    }
                case CondRegExist x:
                    {
                        bool ok = Helpers.RegExists(Helpers.ExpandVars(x.Key), x.Value);
                        if (!ok) reasons?.Add($"注册表不存在：{Helpers.ExpandVars(x.Key)}");
                        return ok;
                    }
                case CondOSVersion x:
                    {
                        var ver = Helpers.GetOsVersion();
                        bool ok = Helpers.CompareVersions(x.Compare, ver.ToString(), x.Version);
                        if (!ok) reasons?.Add($"OSVersion 不满足：本机 {ver} 不满足 {x.Compare} {x.Version}");
                        return ok;
                    }
                case CondArch x:
                    {
                        bool ok = string.Equals(x.Value, Helpers.CurrentArch(), StringComparison.OrdinalIgnoreCase);
                        if (!ok) reasons?.Add($"架构不符：要求 {x.Value}，本机 {Helpers.CurrentArch()}");
                        return ok;
                    }
                case CondFileVersion x:
                    {
                        string p = Helpers.ExpandVars(x.FilePath);
                        if (!File.Exists(p)) { reasons?.Add($"文件不存在：{p}"); return false; }
                        var v = System.Diagnostics.FileVersionInfo.GetVersionInfo(p).FileVersion;
                        bool ok = !string.IsNullOrEmpty(v) && Helpers.CompareVersions(x.Compare, v, x.Version);
                        if (!ok) reasons?.Add($"文件版本不满足：{p} = {v} 不满足 {x.Compare} {x.Version}");
                        return ok;
                    }
                case CondNot x:
                    return x.Inner != null && !Eval(x.Inner, null);
                case CondOr x:
                    {
                        foreach (var i in x.Items) if (Eval(i, null)) return true;
                        return false;
                    }
                case CondAnd x:
                    {
                        foreach (var i in x.Items) if (!Eval(i, reasons)) return false;
                        return true;
                    }
                case CondFalse _:
                    return false;
                case CondQueryServiceStart _:
                    return true; // 服务存在性在动作执行阶段判断
                default:
                    return true;
            }
        }
    }
}
