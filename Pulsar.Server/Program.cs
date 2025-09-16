using Pulsar.Server.DiscordRPC;
using Pulsar.Server.Forms;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Pulsar.Server
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ComWrappers.RegisterForMarshalling(global::WinFormsComInterop.WinFormsComWrappers.Instance);

            using (FrmMain mainForm = new FrmMain())
            {
                DiscordRPCManager.Initialize(mainForm);

                Application.Run(mainForm);
                
                DiscordRPCManager.Shutdown();
            }
        }
    }
}