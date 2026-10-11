using BlockBlast.WinForms.Audio;

namespace BlockBlast.Audio.Tests;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Every sound and music loop contains finite audible samples", Composition),
            ("Effects mix over uninterrupted background music", SimultaneousPlayback),
            ("Music and sound switches operate independently", IndependentSwitches),
            ("Pausing silences playback and preserves the music position", PauseAndResume),
            ("Zero volume fades to silence and restores without losing playback", Volume),
            ("Rapid inputs keep voices bounded and effects eventually finish", BoundedVoices)
        };
        var passed = 0;
        foreach (var (name, run) in tests)
        {
            try { run(); passed++; Console.WriteLine($"PASS {name}"); }
            catch (Exception error) { Console.Error.WriteLine($"FAIL {name}: {error}"); }
        }
        Console.WriteLine($"{passed}/{tests.Length} audio tests passed.");
        return passed == tests.Length ? 0 : 1;
    }

    private static void Composition()
    {
        var composer = new PcmComposer();
        CheckSamples(composer.CreateMusicLoop());
        foreach (var cue in Enum.GetValues<AudioCue>())
        for (var tier = 1; tier <= 4; tier++) CheckSamples(composer.CreateEffect(cue, tier));
    }

    private static void SimultaneousPlayback()
    {
        var mixed = CreateMixer();
        var musicOnly = CreateMixer();
        Render(mixed, 2_048);
        Render(musicOnly, 2_048);
        mixed.Play(AudioCue.Combo, 3);
        var withEffect = Render(mixed, PcmComposer.SampleRate);
        var baseline = Render(musicOnly, PcmComposer.SampleRate);
        Check(!withEffect.SequenceEqual(baseline), "Sound effect did not enter the mix.");
        Check(mixed.MusicPosition == musicOnly.MusicPosition, "An effect interrupted or restarted the music.");
        Check(Render(mixed, 512).SequenceEqual(Render(musicOnly, 512)), "Finished effect changed the continuing music.");
    }

    private static void IndependentSwitches()
    {
        var mixer = CreateMixer();
        mixer.Configure(false, true, 70);
        mixer.Play(AudioCue.Clear);
        Check(Render(mixer, 2_048).Any(sample => sample != 0), "Disabling music also muted effects.");
        Check(mixer.MusicPosition == 0, "Disabled music advanced.");
        mixer.Configure(false, false, 70);
        mixer.Play(AudioCue.Combo);
        Check(Render(mixer, 2_048).All(sample => sample == 0), "Disabled sound was audible.");
        mixer.Configure(true, false, 70);
        mixer.Play(AudioCue.Place);
        Check(mixer.ActiveVoiceCount == 0, "Disabled sound created a voice.");
        Check(Render(mixer, 2_048).Any(sample => sample != 0), "Disabling sound also muted enabled music.");
    }

    private static void PauseAndResume()
    {
        var mixer = CreateMixer();
        Render(mixer, 2_048);
        var position = mixer.MusicPosition;
        mixer.SetPaused(true);
        mixer.Play(AudioCue.Combo);
        Check(Render(mixer, 2_048).All(sample => sample == 0), "Paused audio was not silent.");
        Check(mixer.MusicPosition == position && mixer.ActiveVoiceCount == 0, "Pause advanced music or queued sounds.");
        mixer.SetPaused(false);
        Check(Render(mixer, 2_048).Any(sample => sample != 0), "Music did not resume.");
    }

    private static void Volume()
    {
        var mixer = CreateMixer();
        Render(mixer, 2_048);
        mixer.Configure(true, true, 0);
        Render(mixer, 2_048);
        Check(Render(mixer, 512).All(sample => sample == 0), "Zero volume remained audible after the fade.");
        mixer.Configure(true, true, 80);
        Check(Render(mixer, 2_048).Any(sample => sample != 0), "Restored volume stayed silent.");
    }

    private static void BoundedVoices()
    {
        var mixer = CreateMixer();
        mixer.Configure(false, true, 100);
        for (var index = 0; index < 1000; index++) mixer.Play(AudioCue.Combo, 4);
        Check(mixer.ActiveVoiceCount <= AudioMixer.MaximumVoices, "Voice limit was exceeded.");
        Check(Render(mixer, PcmComposer.SampleRate * 2).Any(sample => sample != 0), "Voices produced no audio.");
        Check(mixer.ActiveVoiceCount == 0, "Completed effects remained allocated.");
        Check(Render(mixer, 512).All(sample => sample == 0), "Finished voices left residual audio.");
    }

    private static AudioMixer CreateMixer()
    {
        var mixer = new AudioMixer(new PcmComposer());
        mixer.Configure(true, true, 70);
        return mixer;
    }

    private static short[] Render(AudioMixer mixer, int count)
    {
        var buffer = new short[count];
        mixer.Render(buffer);
        return buffer;
    }

    private static void CheckSamples(float[] samples)
    {
        Check(samples.Length > 0 && samples.All(float.IsFinite), "Invalid PCM samples.");
        Check(samples.Any(sample => Math.Abs(sample) > 0.01f), "Composition is silent.");
        Check(samples.All(sample => Math.Abs(sample) < 1), "Unmixed composition clips.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
