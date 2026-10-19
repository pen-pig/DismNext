using System;
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
                if (!Eval(c)) return false; // 多个条件按 AND 处理
            return true;
        }

        private static bool Eval(Condition c)
        {
            switch (c)
            {
                case CondFileExist x:
                    {
                        string p = Helpers.ExpandVars(x.FilePath);
                        return File.Exists(p) || Directory.Exists(p);
                    }
                case CondImageFlags x:
                    // Dism++：1=在线映像。MVP 仅支持在线系统，故只有 1 命中。
                    return x.Value == "1";
                case CondRegExist x:
                    return Helpers.RegExists(Helpers.ExpandVars(x.Key), x.Value);
                case CondOSVersion x:
                    return Helpers.CompareVersions(x.Compare, Environment.OSVersion.Version.ToString(), x.Version);
                case CondArch x:
                    return string.Equals(x.Value, Helpers.CurrentArch(), StringComparison.OrdinalIgnoreCase);
                case CondFileVersion x:
                    {
                        string p = Helpers.ExpandVars(x.FilePath);
                        if (!File.Exists(p)) return false;
                        var v = System.Diagnostics.FileVersionInfo.GetVersionInfo(p).FileVersion;
                        return !string.IsNullOrEmpty(v) && Helpers.CompareVersions(x.Compare, v, x.Version);
                    }
                case CondNot x:
                    return x.Inner != null && !Eval(x.Inner);
                case CondOr x:
                    {
                        foreach (var i in x.Items) if (Eval(i)) return true;
                        return false;
                    }
                case CondAnd x:
                    {
                        foreach (var i in x.Items) if (!Eval(i)) return false;
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
