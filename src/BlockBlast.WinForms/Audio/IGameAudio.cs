namespace BlockBlast.WinForms.Audio;

/// <summary>Audio controls owned by the game window. All calls are safe from the UI thread.</summary>
public interface IGameAudio : IDisposable
{
    bool IsAvailable { get; }
    void Configure(bool musicEnabled, bool soundEnabled, int volume);
    void SetPaused(bool paused);
    void Play(AudioCue cue, int intensity = 1);
}

public enum AudioCue
{
    Place,
    Clear,
    Combo,
    Invalid,
    Undo,
    GameOver,
    Click,
    NewGame
}
