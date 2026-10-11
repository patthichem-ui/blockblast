namespace BlockBlast.WinForms.Audio;

/// <summary>
/// Device-independent, single-owner mixer. Music and short effects share one PCM stream,
/// so playing an effect cannot interrupt the music. The service owns thread synchronization.
/// </summary>
internal sealed class AudioMixer
{
    internal const int MaximumVoices = 12;
    private readonly float[] _music;
    private readonly Dictionary<(AudioCue Cue, int Tier), float[]> _effects = new();
    private readonly List<Voice> _voices = new(MaximumVoices);
    private bool _musicEnabled = true;
    private bool _soundEnabled = true;
    private bool _paused;
    private float _volume = 0.7f;
    private float _currentGain;
    private int _musicPosition;

    public AudioMixer(PcmComposer composer)
    {
        _music = composer.CreateMusicLoop();
        foreach (var cue in Enum.GetValues<AudioCue>())
            for (var tier = 1; tier <= 4; tier++)
                _effects[(cue, tier)] = composer.CreateEffect(cue, tier);
    }

    internal int ActiveVoiceCount => _voices.Count;
    internal int MusicPosition => _musicPosition;

    public void Configure(bool musicEnabled, bool soundEnabled, int volume)
    {
        _musicEnabled = musicEnabled;
        _soundEnabled = soundEnabled;
        _volume = Math.Clamp(volume, 0, 100) / 100f;
        if (!soundEnabled) _voices.Clear();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        if (paused)
        {
            _voices.Clear();
            _currentGain = 0;
        }
    }

    public void Play(AudioCue cue, int intensity = 1)
    {
        if (_paused || !_soundEnabled || !_effects.TryGetValue((cue, Math.Clamp(intensity, 1, 4)), out var effect))
            return;
        var priority = cue switch
        {
            AudioCue.GameOver => 5,
            AudioCue.Combo => 4,
            AudioCue.Clear or AudioCue.NewGame => 3,
            _ => 1
        };
        if (_voices.Count == MaximumVoices)
        {
            var quietest = 0;
            for (var i = 1; i < _voices.Count; i++)
                if (_voices[i].Priority < _voices[quietest].Priority) quietest = i;
            if (_voices[quietest].Priority > priority) return;
            _voices.RemoveAt(quietest);
        }
        _voices.Add(new Voice(effect, priority));
    }

    public void Render(short[] destination)
    {
        if (_paused)
        {
            Array.Clear(destination);
            return;
        }
        var gainStep = 1f / (PcmComposer.SampleRate * 0.012f);
        for (var i = 0; i < destination.Length; i++)
        {
            var sample = 0f;
            if (_musicEnabled)
            {
                sample = _music[_musicPosition];
                _musicPosition = (_musicPosition + 1) % _music.Length;
            }
            for (var voice = _voices.Count - 1; voice >= 0; voice--)
            {
                var active = _voices[voice];
                sample += active.Samples[active.Position++];
                if (active.Position == active.Samples.Length) _voices.RemoveAt(voice);
            }
            // A short gain ramp avoids volume-slider clicks; bounded soft limiting prevents overflow.
            _currentGain += Math.Clamp(_volume - _currentGain, -gainStep, gainStep);
            destination[i] = (short)Math.Round(Math.Tanh(sample * _currentGain) * short.MaxValue);
        }
    }

    private sealed class Voice(float[] samples, int priority)
    {
        public float[] Samples { get; } = samples;
        public int Priority { get; } = priority;
        public int Position { get; set; }
    }
}
