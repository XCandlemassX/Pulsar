using Pulsar.Server.DiscordRPC;
using Pulsar.Server.Forms;
using System;
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

            using (FrmMain mainForm = new FrmMain())
            {
                DiscordRPCManager.Initialize(mainForm);

                Application.Run(mainForm);
                
                DiscordRPCManager.Shutdown();
            }
        }
    }
}