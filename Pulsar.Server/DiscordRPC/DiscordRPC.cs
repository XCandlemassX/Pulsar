using DiscordRPC;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Pulsar.Server.DiscordRPC
{
    internal class DiscordRPC
    {
        private readonly Form _form;
        private bool _enabled;
        private DiscordRpcClient _client;
        private Timer _updateTimer; // Made timer a field for proper cleanup
        private readonly string _applicationId = "1351391347491344445";

        public DiscordRPC(Form form)
        {
            _form = form;
            _enabled = false;
            _client = new DiscordRpcClient(_applicationId);
        }

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                _enabled = value;
                if (_enabled)
                {
                    if (_client == null || _client.IsDisposed)
                    {
                        _client = new DiscordRpcClient(_applicationId);
                        Debug.WriteLine("Discord RPC Client recreated");
                    }
                    if (!_client.IsInitialized)
                    {
                        try
                        {
                            _client.Initialize();
                            Debug.WriteLine("Discord RPC Client Initialized");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Failed to initialize Discord RPC: " + ex.Message);
                            return;
                        }
                    }
                    try
                    {
                        _client.OnReady += (sender, e) =>
                        {
                            Debug.WriteLine("Discord RPC Ready for " + _form.Text);
                        };
                        SetPresence();
                        _updateTimer = new Timer();
                        _updateTimer.Interval = 5000; // 5 seconds
                        _updateTimer.Tick += (s, e) => SetPresence();
                        _updateTimer.Start();
                        Debug.WriteLine("Discord RPC Enabled for " + _form.Text);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Failed to set Discord presence: " + ex.Message);
                    }
                }
                else
                {
                    if (_client != null && _client.IsInitialized)
                    {
                        try
                        {
                            _client.ClearPresence();
                            _client.Deinitialize();
                            if (_updateTimer != null)
                            {
                                _updateTimer.Stop();
                                _updateTimer.Dispose();
                                _updateTimer = null;
                            }
                            Debug.WriteLine("Discord RPC Disabled for " + _form.Text);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Failed to disable Discord RPC: " + ex.Message);
                        }
                    }
                }
            }
        }

        private int GetConnectedClientsCount()
        {
            try
            {
                string title = _form.Text;
                const string marker = "Connected: ";
                int markerIndex = title.IndexOf(marker);
                if (markerIndex == -1)
                    return 0;

                int startIndex = markerIndex + marker.Length;
                int endIndex = title.IndexOf(" ", startIndex);
                if (endIndex == -1)
                    endIndex = title.Length;

                string countStr = title.Substring(startIndex, endIndex - startIndex);
                return int.Parse(countStr);
            }
            catch
            {
                return 0;
            }
        }

        private void SetPresence()
        {
            int connectedClients = GetConnectedClientsCount();
            _client.SetPresence(new RichPresence
            {
                State = $"Connected Clients: {connectedClients}",
                Assets = new Assets
                {
                    LargeImageKey = "default",
                    LargeImageText = "Pulsar RAT"
                },
                Timestamps = new Timestamps { Start = DateTime.UtcNow }
            });
        }
    }
}