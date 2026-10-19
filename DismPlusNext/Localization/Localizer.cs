using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace DismPlusNext.Localization
{
    /// <summary>
    /// 语言包加载与字符串本地化。
    /// 语言包格式：&lt;Data&gt;&lt;String Target="中文源串" Link="该语言译文"/&gt;...&lt;/Data&gt;
    /// Data.xml 中的字符串以 '#' 开头（如 "#被取代的WinSxS组件"），'#' 后的内容即 Target 键。
    /// </summary>
    public class Localizer
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>();
        public string LoadedPack { get; private set; } = "";

        public void Load(string languagesDir, string packFileName)
        {
            _map.Clear();
            LoadedPack = packFileName;
            string path = Path.Combine(languagesDir, packFileName);
            if (!File.Exists(path)) return;
            try
            {
                XDocument doc = XDocument.Load(path);
                foreach (var s in doc.Descendants("String"))
                {
                    string target = (string)s.Attribute("Target");
                    string link = (string)s.Attribute("Link");
                    if (!string.IsNullOrEmpty(target) && link != null)
                        _map[target] = link;
                }
            }
            catch { /* 忽略损坏的语言包 */ }
        }

        /// <summary>把 Data.xml 中的原始字符串本地化；找不到译文时回退为去掉 '#' 的原文。</summary>
        public string Localize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? "";
            if (raw.StartsWith("#"))
            {
                string key = raw.Substring(1);
                if (_map.TryGetValue(key, out string v) && !string.IsNullOrEmpty(v))
                    return v;
                return key; // 回退为原文（中文源串）
            }
            return raw;
        }
    }
}
