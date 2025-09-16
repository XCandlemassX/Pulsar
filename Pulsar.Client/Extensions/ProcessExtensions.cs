using Pulsar.Client.Utilities;
using System.Diagnostics;
using System.Text;

namespace Pulsar.Client.Extensions
{
    public static class ProcessExtensions
    {
        public static string GetMainModuleFileName(this Process proc)
        {
            uint nChars = 260;
            StringBuilder buffer = new StringBuilder((int)nChars);

            var success = NativeMethods.QueryFullProcessImageName(proc.Handle, 0, buffer, ref nChars);

            return success ? buffer.ToString() : null;
        }
    }
}
