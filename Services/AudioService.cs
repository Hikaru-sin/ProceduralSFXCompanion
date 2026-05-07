using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MiniAudioEx.Core.StandardAPI;

namespace ProceduralSFXCompanion.Services;

public class AudioService : IDisposable
{
    private CancellationTokenSource? _debounceTokenSource;
    private static readonly uint SampleRate = 48000;
    private static readonly uint Channels = 2;

    private AudioSource? _source;
    private AudioClip? _clip;
    
    private bool _bIsDisposed;
    
    public AudioService()
    {
        AudioContext.Initialize(SampleRate, Channels);
    }
    
    public async void Play(string filePath)
    {
        try
        {
            if (_debounceTokenSource is not null)
                await _debounceTokenSource.CancelAsync();
            _debounceTokenSource = new CancellationTokenSource();
            Stop();

            _clip = new AudioClip(filePath);
            _source = new AudioSource();
            _source.Play(_clip);
            var delay = _source.Length;
            
            TimeSpan duration = TimeSpan.FromSeconds((float)delay / SampleRate);
            await Task.Delay(duration, _debounceTokenSource.Token);
            Stop();
            _debounceTokenSource.Dispose();
            _debounceTokenSource = null;
        }
        catch (Exception)
        {
            //Ignore
        }
    }

    private void Stop()
    {
        if (_source is not null)
        {
            _source.Stop();
            _source.Dispose();
            _source = null;
        }

        if (_clip is not null)
        {
            _clip.Dispose();
            _clip = null;
        }
    }
    
    public void Dispose()
    {
        if(_bIsDisposed)
            return;

        if (_debounceTokenSource is not null)
        {
            _debounceTokenSource.Cancel();
            _debounceTokenSource.Dispose();
        }

        _bIsDisposed = true;
        _clip?.Dispose();
        _source?.Dispose();
        AudioContext.Deinitialize();
    }
}