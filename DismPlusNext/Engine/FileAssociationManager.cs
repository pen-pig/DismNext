using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace DismPlusNext.Engine
{
    /// <summary>单个文件扩展名及其当前默认的关联程序信息。</summary>
    public class FileAssocEntry
    {
        public string Extension = "";
        public string ProgId = "";          // HKCR\.ext 默认指向的 ProgId
        public string DefaultApp = "";       // 由 ProgId 解析出的可读程序名
        public bool UserOverridden;          // 当前用户是否通过“打开方式”覆盖过
    }

    /// <summary>
    /// 文件关联浏览（对应 Dism++ 工具箱“文件关联/默认应用”）。
    /// 只读枚举常见扩展名的默认处理程序，便于排查“打开方式”错乱。
    /// </summary>
    public static class FileAssociationManager
    {
        /// <summary>重点关注的一组常见扩展名（覆盖文档/图片/媒体/压缩/脚本）。</summary>
        public static readonly string[] WatchedExtensions =
        {
            ".txt", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
            ".mp3", ".mp4", ".wav", ".avi", ".mkv",
            ".zip", ".rar", ".7z",
            ".html", ".htm", ".json", ".xml", ".csv",
            ".exe", ".msi", ".bat", ".ps1", ".reg",
        };

        public static List<FileAssocEntry> List()
        {
            var list = new List<FileAssocEntry>();
            foreach (var ext in WatchedExtensions)
            {
                var e = Inspect(ext);
                if (e != null) list.Add(e);
            }
            return list;
        }

        private static FileAssocEntry Inspect(string ext)
        {
            try
            {
                using var key = Helpers.OpenRegKey("HKCR\\" + ext, false);
                var progId = key?.GetValue("") as string ?? "";
                bool userOverridden = false;
                // 用户级“打开方式”覆盖记录在 HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\<ext>\UserChoice
                using var uc = Helpers.OpenRegKey("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\" + ext + "\\UserChoice", false);
                if (uc != null)
                {
                    var pid = uc.GetValue("ProgId") as string;
                    if (!string.IsNullOrEmpty(pid))
                    {
                        userOverridden = true;
                        progId = pid;
                    }
                }
                if (string.IsNullOrEmpty(progId)) return new FileAssocEntry { Extension = ext, ProgId = "(未关联)", DefaultApp = "—", UserOverridden = userOverridden };
                var appName = ResolveAppName(progId);
                return new FileAssocEntry { Extension = ext, ProgId = progId, DefaultApp = appName, UserOverridden = userOverridden };
            }
            catch
            {
                return new FileAssocEntry { Extension = ext, ProgId = "(读取失败)", DefaultApp = "—" };
            }
        }

        /// <summary>把 ProgId 解析为可读程序名：优先取 ProgId 默认值的 FriendlyTypeName，否则直接用 ProgId。</summary>
        private static string ResolveAppName(string progId)
        {
            if (string.IsNullOrEmpty(progId)) return "—";
            try
            {
                using var key = Helpers.OpenRegKey("HKCR\\" + progId, false);
                if (key != null)
                {
                    var ft = key.GetValue("FriendlyTypeName") as string;
                    if (!string.IsNullOrEmpty(ft))
                    {
                        // 形如 @C:\...\shell32.dll,-12345 的资源字符串，直接展示原值即可
                        return ft;
                    }
                    var def = key.GetValue("") as string;
                    if (!string.IsNullOrEmpty(def)) return def;
                }
            }
            catch { }
            return progId;
        }
    }
}
