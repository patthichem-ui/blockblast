using System.Diagnostics;

namespace BlockBlast.WinForms.Audio;

/// <summary>
/// Nonblocking UI-facing audio service. One background worker owns synthesis, mixing,
/// the native output and cleanup. A missing or removed sound device only disables audio.
/// </summary>
public sealed class GameAudioService : IGameAudio
{
    private const int MaximumPendingEffects = 32;
    private readonly object _gate = new();
    private readonly Queue<PlayRequest> _pending = new(MaximumPendingEffects);
    private readonly AutoResetEvent _wake = new(false);
    // Stay silent until the window applies the user's saved preferences and pause state.
    private bool _musicEnabled;
    private bool _soundEnabled;
    private int _volume = 45;
    private bool _paused = true;
    private bool _disposed;
    private bool _workerStopped;
    private int _available;

    public GameAudioService()
    {
        var worker = new Thread(Run) { IsBackground = true, Name = "BlockBlast audio" };
        worker.Start();
    }

    public bool IsAvailable => Volatile.Read(ref _available) != 0;

    public void Configure(bool musicEnabled, bool soundEnabled, int volume)
    {
        lock (_gate)
        {
            if (_disposed || _workerStopped) return;
            _musicEnabled = musicEnabled;
            _soundEnabled = soundEnabled;
            _volume = Math.Clamp(volume, 0, 100);
            if (!soundEnabled) _pending.Clear();
            _wake.Set();
        }
    }

    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            if (_disposed || _workerStopped) return;
            _paused = paused;
            if (paused) _pending.Clear();
            _wake.Set();
        }
    }

    public void Play(AudioCue cue, int intensity = 1)
    {
        lock (_gate)
        {
            if (_disposed || _workerStopped || _paused || !_soundEnabled || !Enum.IsDefined(cue)) return;
            if (_pending.Count == MaximumPendingEffects) _pending.Dequeue();
            _pending.Enqueue(new PlayRequest(cue, Math.Clamp(intensity, 1, 4)));
            _wake.Set();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _pending.Clear();
            Volatile.Write(ref _available, 0);
            if (!_workerStopped) _wake.Set();
        }
        // The worker releases the native buffers after WinMM returns them. Waiting here
        // would make window-close depend on the speed or health of the audio driver.
    }

    private void Run()
    {
        WaveOutDevice? device = null;
        try
        {
            if (!OperatingSystem.IsWindows()) return;
            lock (_gate)
                if (_disposed) return;

            device = new WaveOutDevice();
            var mixer = new AudioMixer(new PcmComposer());
            var wasPaused = false;
            lock (_gate)
            {
                if (_disposed) return;
                Volatile.Write(ref _available, 1);
            }

            while (true)
            {
                bool paused;
                lock (_gate)
                {
                    if (_disposed) break;
                    paused = _paused;
                    mixer.Configure(_musicEnabled, _soundEnabled, _volume);
                    mixer.SetPaused(paused);
                    while (_pending.TryDequeue(out var request)) mixer.Play(request.Cue, request.Intensity);
                }
                if (paused && !wasPaused) device.Reset();
                wasPaused = paused;
                if (!paused) device.SubmitAvailableBuffers(mixer.Render);
                _wake.WaitOne(paused ? 100 : 8);
            }
        }
        catch (Exception exception)
        {
            // Audio is optional; device loss must never terminate a game or its save operation.
            Trace.WriteLine($"BlockBlast audio disabled: {exception.Message}");
        }
        finally
        {
            Volatile.Write(ref _available, 0);
            try { device?.Dispose(); }
            catch (Exception exception) { Trace.WriteLine($"BlockBlast audio cleanup: {exception.Message}"); }
            lock (_gate)
            {
                _workerStopped = true;
                _pending.Clear();
                _wake.Dispose();
            }
        }
    }

    private readonly record struct PlayRequest(AudioCue Cue, int Intensity);
}
