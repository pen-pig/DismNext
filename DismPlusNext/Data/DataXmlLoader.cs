using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using DismPlusNext.Localization;
using DismPlusNext.Models;

namespace DismPlusNext.Data
{
    /// <summary>把仓库根目录的 Data.xml 解析为 DismData 模型。</summary>
    public class DataXmlLoader
    {
        private readonly Localizer _loc;

        public DataXmlLoader(Localizer loc) { _loc = loc; }

        private static string Attr(XElement e, string name)
        {
            var a = e.Attribute(name);
            return a == null ? "" : a.Value;
        }
        private static string ElemText(XElement parent, string name)
        {
            var c = parent.Element(name);
            return c == null ? "" : (c.Value ?? "").Trim();
        }

        public DismData Load(string dataXmlPath)
        {
            var data = new DismData();
            if (!File.Exists(dataXmlPath)) return data;
            XDocument doc;
            try { doc = XDocument.Load(dataXmlPath); }
            catch { return data; }
            var root = doc.Root;
            if (root == null) return data;

            string[] knownKinds = { "CleanCollection4", "SystemOptimization", "ESDDecryptKey", "WSUS", "DefaultFilterList", "TimeZoneMap" };
            foreach (var collEl in root.Elements())
            {
                string kind = collEl.Name.LocalName;
                if (!knownKinds.Contains(kind) && collEl.Name.LocalName != "CleanCollection4") { /* 容错：仍尝试解析 */ }
                var coll = new RuleCollection { Kind = kind, DisplayName = _loc.Localize("#" + kind) };
                // Item 可能直接是子元素（CleanCollection4），也可能嵌套在 <Group> 下（SystemOptimization）
                foreach (var itemEl in collEl.Descendants("Item"))
                {
                    string parentGroup = "";
                    var p = itemEl.Parent;
                    if (p != null && p.Name.LocalName == "Group" && p.Attribute("Name") != null)
                        parentGroup = p.Attribute("Name").Value;
                    coll.Items.Add(ParseItem(itemEl, parentGroup));
                }
                if (coll.Items.Count > 0)
                    data.Collections.Add(coll);
            }
            return data;
        }

        private RuleItem ParseItem(XElement itemEl, string parentGroup)
        {
            var item = new RuleItem
            {
                NameKey = Attr(itemEl, "Name"),
                Level = Attr(itemEl, "Level"),
                Type = Attr(itemEl, "Type"),
                DescriptionKey = ElemText(itemEl, "Description"),
                GroupKey = ElemText(itemEl, "Group"),
                WarningKey = ElemText(itemEl, "Warning"),
            };
            // SystemOptimization 的分类名来自父级 <Group Name="...">
            if (string.IsNullOrEmpty(item.GroupKey) && !string.IsNullOrEmpty(parentGroup))
                item.GroupKey = parentGroup;
            item.DisplayName = _loc.Localize(item.NameKey);
            item.Description = _loc.Localize(item.DescriptionKey);
            item.Group = _loc.Localize(item.GroupKey);
            item.Warning = _loc.Localize(item.WarningKey);

            // 收集所有 <Scan>（可能在 <ScanCollection> 下，也可能直接是子元素）
            var scans = itemEl.Descendants("Scan").ToList();
            if (scans.Count == 0)
            {
                // 直接带有 Applicable/Activate（如 SystemOptimization）
                var direct = new ScanRule
                {
                    Applicable = ParseApplicable(itemEl.Element("Applicable")),
                    Warning = _loc.Localize(ElemText(itemEl, "Warning")),
                };
                direct.Activate = ParseActivate(itemEl.Element("Activate"));
                if (direct.Applicable.Conditions.Count > 0 || direct.Activate.Count > 0)
                    item.Scans.Add(direct);
            }
            else
            {
                foreach (var s in scans)
                {
                    var sr = new ScanRule
                    {
                        Work = Attr(s, "Work"),
                        NameKey = ElemText(s, "Name"),
                        Applicable = ParseApplicable(s.Element("Applicable")),
                        WarningKey = ElemText(s, "Warning"),
                    };
                    sr.Name = _loc.Localize(sr.NameKey);
                    sr.Warning = _loc.Localize(sr.WarningKey);
                    sr.Activate = ParseActivate(s.Element("Activate"));
                    item.Scans.Add(sr);
                }
            }
            // 系统优化 Combo 项：选项藏在 <Current>/<Dropdown> 与 <Default>/<Dropdown> 下
            var current = itemEl.Element("Current");
            if (current != null)
            {
                foreach (var dd in current.Elements("Dropdown"))
                {
                    item.Options.Add(new ComboOption
                    {
                        NameKey = Attr(dd, "Name"),
                        Name = _loc.Localize(Attr(dd, "Name")),
                        Applicable = ParseApplicable(dd.Element("Applicable")),
                        Activate = ParseActivate(dd.Element("Activate")),
                    });
                }
            }
            var def = itemEl.Element("Default");
            if (def != null)
            {
                foreach (var dd in def.Elements("Dropdown"))
                {
                    var dacts = ParseActivate(dd.Element("Activate"));
                    if (dacts.Count > 0 && item.DefaultActivate.Count == 0)
                        item.DefaultActivate = dacts;
                }
            }
            // 若没有 Default，则用 Current 首个选项的动作为默认
            if (item.DefaultActivate.Count == 0 && item.Options.Count > 0)
                item.DefaultActivate = item.Options[0].Activate;
            // 计算默认选项索引（用于"恢复默认"UI）：将 Default 动作签名与各选项匹配
            if (item.Options.Count > 0)
            {
                var sig = ActivateSignature(item.DefaultActivate);
                int idx = -1;
                for (int i = 0; i < item.Options.Count; i++)
                    if (ActivateSignature(item.Options[i].Activate) == sig) { idx = i; break; }
                item.DefaultOptionIndex = idx >= 0 ? idx : 0;
            }
            // 系统开关型（<System>）：True/False 各含一组 Activate
            var sys = itemEl.Element("System");
            if (sys != null)
            {
                var t = sys.Element("True"); if (t != null) item.SystemTrueActivate = ParseActivate(t.Element("Activate"));
                var f = sys.Element("False"); if (f != null) item.SystemFalseActivate = ParseActivate(f.Element("Activate"));
            }
            return item;
        }

        /// <summary>生成动作序列的签名（用于匹配 Combo 项的推荐默认选项）。</summary>
        private static string ActivateSignature(List<ActionBase> srcActions)
        {
            var segs = new List<string>();
            foreach (var itm in srcActions)
            {
                if (itm is ActRegWrite aw) segs.Add($"RW|{aw.Key}|{aw.Value}|{aw.Type}|{aw.Data}");
                else if (itm is ActRegDelete ad) segs.Add($"RD|{ad.Key}|{ad.Value}");
                else if (itm is ActRegMove am) segs.Add($"RM|{am.From}|{am.To}");
                else if (itm is ActSetServiceStart ass) segs.Add($"SS|{ass.Service}|{ass.Start}");
                else if (itm is ActGeneral ag) segs.Add($"GN|{ag.RootPath}|{ag.Flags}|{string.Join(",", ag.Query)}");
                else if (itm is ActCustom ac) segs.Add($"CU|{ac.ProcName}|{ac.Flags}");
                else if (itm is ActExplorerNotify ae) segs.Add($"EN|{ae.Value}");
                else segs.Add("??");
            }
            return string.Join(";", segs);
        }

        private Applicable ParseApplicable(XElement applEl)
        {
            var a = new Applicable();
            if (applEl == null) return a;
            foreach (var c in applEl.Elements())
                a.Conditions.Add(ParseCondition(c));
            return a;
        }

        private Condition ParseCondition(XElement e)
        {
            string tag = e.Name.LocalName;
            switch (tag)
            {
                case "FileExist": return new CondFileExist { FilePath = Attr(e, "FilePath") };
                case "ImageFlags": return new CondImageFlags { Value = (e.Value ?? "").Trim() };
                case "RegExist": return new CondRegExist { Key = Attr(e, "Key"), Value = Attr(e, "Value") };
                case "OSVersion": return new CondOSVersion { Compare = Attr(e, "Compare"), Version = (e.Value ?? "").Trim() };
                case "Arch": return new CondArch { Value = (e.Value ?? "").Trim() };
                case "FileVersion": return new CondFileVersion { FilePath = Attr(e, "FilePath"), Compare = Attr(e, "Compare"), Version = (e.Value ?? "").Trim() };
                case "Not": return new CondNot { Inner = e.Elements().Any() ? ParseCondition(e.Elements().First()) : new CondFalse() };
                case "Or": { var o = new CondOr(); foreach (var c in e.Elements()) o.Items.Add(ParseCondition(c)); return o; }
                case "And": { var an = new CondAnd(); foreach (var c in e.Elements()) an.Items.Add(ParseCondition(c)); return an; }
                case "False": return new CondFalse();
                case "QueryServiceStart": return new CondQueryServiceStart { Service = Attr(e, "Service") };
                default: return new CondFalse();
            }
        }

        private List<ActionBase> ParseActivate(XElement actEl)
        {
            var list = new List<ActionBase>();
            if (actEl == null) return list;
            foreach (var e in actEl.Elements())
            {
                string tag = e.Name.LocalName;
                switch (tag)
                {
                    case "General":
                        list.Add(new ActGeneral
                        {
                            RootPath = Attr(e, "RootPath"),
                            Flags = Attr(e, "Flags"),
                            Query = e.Elements("Query").Select(x => (x.Value ?? "").Trim()).Where(x => x != "").ToList(),
                            Excluded = e.Elements("Excluded").Select(x => (x.Value ?? "").Trim()).Where(x => x != "").ToList(),
                        });
                        break;
                    case "Custom":
                        list.Add(new ActCustom { ProcName = Attr(e, "ProcName"), Flags = Attr(e, "Flags") });
                        break;
                    case "ExplorerNotify":
                        list.Add(new ActExplorerNotify { Value = Attr(e, "Value") });
                        break;
                    case "RegWrite":
                        list.Add(new ActRegWrite { Key = Attr(e, "Key"), Value = Attr(e, "Value"), Type = Attr(e, "Type"), Data = Attr(e, "Data") });
                        break;
                    case "RegDelete":
                        list.Add(new ActRegDelete { Key = Attr(e, "Key"), Value = Attr(e, "Value") });
                        break;
                    case "RegMove":
                        list.Add(new ActRegMove { From = Attr(e, "From"), To = Attr(e, "To") });
                        break;
                    case "SetServiceStart":
                        list.Add(new ActSetServiceStart { Service = Attr(e, "Service"), Start = Attr(e, "Start") });
                        break;
                    default:
                        list.Add(new ActUnknown { Tag = tag });
                        break;
                }
            }
            return list;
        }
    }
}
