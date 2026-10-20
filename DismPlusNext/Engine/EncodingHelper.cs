using System.Text;

namespace DismPlusNext.Engine
{
    /// <summary>确保 GBK(936) 等代码页可用（DISM/注册表命令常输出中文，需 CodePages 提供程序）。</summary>
    public static class EncodingHelper
    {
        private static bool _registered;
        public static void Ensure()
        {
            if (_registered) return;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _registered = true;
        }
    }
}
