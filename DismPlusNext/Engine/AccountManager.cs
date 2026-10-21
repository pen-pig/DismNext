using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace DismPlusNext.Engine
{
    /// <summary>单个本地用户账户。</summary>
    public class AccountEntry
    {
        public string Name = "";
        public bool Enabled;
        public string Description = "";
        public string Sid = "";
        public string LastLogon = "";
    }

    /// <summary>
    /// 账户管理：原生 netapi32（NetUserEnum / NetUserSetInfo）+ advapi32（LookupAccountName）实现，
    /// 不调用 powershell / 不起子进程。对应 Dism++ 工具箱"账户管理"。
    /// </summary>
    public static class AccountManager
    {
        const int UF_ACCOUNTDISABLE = 0x2;
        const int FILTER_NORMAL_ACCOUNT = 2;
        const int NERR_SUCCESS = 0;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct USER_INFO_1
        {
            public string usri1_name;
            public string usri1_password;
            public uint usri1_password_age;
            public uint usri1_priv;
            public string usri1_home_dir;
            public string usri1_comment;
            public uint usri1_flags;
            public string usri1_script_path;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct USER_INFO_1008 { public uint usri1008_flags; }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetUserEnum(string servername, int level, int filter, out IntPtr bufptr,
            uint prefmaxlen, out int entriesread, out int totalentries, ref IntPtr resumeHandle);
        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetUserGetInfo(string servername, string username, int level, out IntPtr bufptr);
        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        static extern int NetUserSetInfo(string servername, string username, int level, ref USER_INFO_1008 buf, out int parmErr);
        [DllImport("netapi32.dll")]
        static extern int NetApiBufferFree(IntPtr buffer);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool LookupAccountName(string systemName, string accountName, byte[] sid,
            ref int sidLen, System.Text.StringBuilder domain, ref int domainLen, out int peUse);

        static string SidOf(string name)
        {
            try
            {
                int sidLen = 0, domLen = 0;
                LookupAccountName(null, name, null, ref sidLen, null, ref domLen, out _);
                if (sidLen <= 0) return "";
                byte[] sid = new byte[sidLen];
                var dom = new System.Text.StringBuilder(domLen > 0 ? domLen : 1);
                if (!LookupAccountName(null, name, sid, ref sidLen, dom, ref domLen, out _)) return "";
                return new SecurityIdentifier(sid, 0).ToString();
            }
            catch { return ""; }
        }

        public static List<AccountEntry> List()
        {
            var list = new List<AccountEntry>();
            IntPtr buf = IntPtr.Zero;
            try
            {
                IntPtr resume = IntPtr.Zero;
                int read = 0, total = 0;
                int rc = NetUserEnum(null, 1, FILTER_NORMAL_ACCOUNT, out buf, uint.MaxValue, out read, out total, ref resume);
                if (rc != NERR_SUCCESS) return list;
                int size = Marshal.SizeOf<USER_INFO_1>();
                for (int i = 0; i < read; i++)
                {
                    var u = Marshal.PtrToStructure<USER_INFO_1>(buf + i * size);
                    string name = u.usri1_name ?? "";
                    list.Add(new AccountEntry
                    {
                        Name = name,
                        Enabled = (u.usri1_flags & UF_ACCOUNTDISABLE) == 0,
                        Description = u.usri1_comment ?? "",
                        Sid = SidOf(name),
                        LastLogon = "",
                    });
                }
            }
            catch { }
            finally { if (buf != IntPtr.Zero) NetApiBufferFree(buf); }
            return list.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<ExecResult> Toggle(AccountEntry a, bool enable, Action<string> log)
        {
            log ??= _ => { };
            var res = new List<ExecResult>();
            IntPtr info = IntPtr.Zero;
            try
            {
                int rc = NetUserGetInfo(null, a.Name, 1, out info);
                if (rc != NERR_SUCCESS || info == IntPtr.Zero)
                {
                    string e = new Win32Exception(rc).Message;
                    res.Add(new ExecResult { Success = false, Target = a.Name, Message = e }); return res;
                }
                var u = Marshal.PtrToStructure<USER_INFO_1>(info);
                uint flags = u.usri1_flags;
                if (enable) flags &= ~(uint)UF_ACCOUNTDISABLE; else flags |= (uint)UF_ACCOUNTDISABLE;
                var set = new USER_INFO_1008 { usri1008_flags = flags };
                int rc2 = NetUserSetInfo(null, a.Name, 1008, ref set, out _);
                if (rc2 == NERR_SUCCESS)
                {
                    log($"已{(enable ? "启用" : "禁用")}账户：{a.Name}");
                    res.Add(new ExecResult { Success = true, Target = a.Name });
                }
                else
                {
                    string e = new Win32Exception(rc2).Message;
                    log($"操作失败：{a.Name} - {e}");
                    res.Add(new ExecResult { Success = false, Target = a.Name, Message = e });
                }
            }
            catch (Exception ex)
            {
                res.Add(new ExecResult { Success = false, Target = a.Name, Message = ex.Message });
            }
            finally { if (info != IntPtr.Zero) NetApiBufferFree(info); }
            return res;
        }
    }
}
