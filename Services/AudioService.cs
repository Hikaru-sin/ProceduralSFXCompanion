using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Ownaudio.Core;
using Ownaudio.Decoders;
using OwnaudioNET;
using OwnaudioNET.Core;
using OwnaudioNET.Events;
using OwnaudioNET.Mixing;
using OwnaudioNET.Sources;

namespace ProceduralSFXCompanion.Services;

public class AudioService : IDisposable
{
    private bool _disposed = false;
    
    private AudioMixer? _audioMixer;
    private FileSource? _audioSource;
    
    public AudioService()
    {
        OwnaudioNet.Initialize();
        var devices = OwnaudioNet.Engine!.GetOutputDevices();
        AudioDeviceInfo? currentDevice = devices.FirstOrDefault(d => d.IsDefault);;
        if (currentDevice is null)
        {
            OwnaudioNet.Shutdown();
            return;
        }
        
        _audioMixer = new AudioMixer(OwnaudioNet.Engine!.UnderlyingEngine);
        OwnaudioNet.Start();
        _audioMixer.Start();
    }
    
    public void Play(string filePath)
    {
        if(!OwnaudioNet.IsInitialized)
            return;
        
        Stop();
        _audioSource = new FileSource(filePath);
        _audioSource.StateChanged += OnAudioSourceStateChanged;
        _audioMixer?.AddSource(_audioSource);
        _audioSource.Play();
    }

    private void OnAudioSourceStateChanged(object? sender, AudioStateChangedEventArgs e)
    {
        if(e.NewState == AudioState.Stopped || e.NewState == AudioState.EndOfStream)
            Stop();
    }

    public void Stop()
    {
        try
        {
            if (_audioSource is not null)
            {
                _audioMixer?.RemoveSource(_audioSource);
                if (_audioSource.State != AudioState.Stopped && _audioSource.State != AudioState.EndOfStream)
                    _audioSource.Stop();
                _audioSource?.Dispose();
                _audioSource = null;
            }
        }
        catch (Exception)
        {
           // Ignore
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _audioMixer?.Stop();
            _audioMixer?.Dispose();
            OwnaudioNet.Stop();
            OwnaudioNet.Shutdown();
            _disposed = true;
        }
    }
}