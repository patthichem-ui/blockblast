namespace BlockBlast.WinForms.Audio;

/// <summary>
/// Creates this game's original instrumental loop and effects from oscillators.
/// PCM is mono at 22,050 Hz; no external music, codecs, downloads or asset files are needed.
/// </summary>
internal sealed class PcmComposer
{
    public const int SampleRate = 22_050;
    private const double Tau = Math.PI * 2;

    public float[] CreateMusicLoop()
    {
        const double beatSeconds = 60.0 / 84;
        var samples = new float[(int)Math.Round(SampleRate * beatSeconds * 32)];
        // Eight original four-beat phrases. Sparse melody leaves room for game effects.
        int[][] chords = [[48, 55, 59, 64], [45, 52, 57, 60], [41, 48, 53, 57], [43, 50, 57, 62]];
        int[][] melody = [[76, 79, 83], [81, 76, 72], [77, 81, 79], [74, 79, 81],
                         [83, 79, 76], [81, 84, 83], [81, 77, 76], [74, 71, 74]];
        for (var bar = 0; bar < 8; bar++)
        {
            var start = bar * beatSeconds * 4;
            var chord = chords[bar % chords.Length];
            foreach (var note in chord)
                AddTone(samples, start, beatSeconds * 3.85, note, 0.028, Tone.Pad, wrap: true);
            AddTone(samples, start, beatSeconds * 1.8, chord[0] - 12, 0.065, Tone.Warm, wrap: true);
            AddTone(samples, start + beatSeconds * 2, beatSeconds * 1.8, chord[0] - 12, 0.045, Tone.Warm, wrap: true);
            for (var tick = 0; tick < 8; tick++)
                AddTone(samples, start + tick * beatSeconds / 2, beatSeconds * 0.8,
                    chord[tick % chord.Length] + 12, 0.033, Tone.Bell, wrap: true);
            for (var note = 0; note < 3; note++)
                AddTone(samples, start + (note + 0.25) * beatSeconds, beatSeconds * 1.25,
                    melody[bar][note], 0.062, Tone.Bell, wrap: true);
        }
        return samples;
    }

    public float[] CreateEffect(AudioCue cue, int intensity)
    {
        var tier = Math.Clamp(intensity, 1, 4);
        var duration = cue switch
        {
            AudioCue.Place => 0.14,
            AudioCue.Clear => 0.54,
            AudioCue.Combo => 0.78,
            AudioCue.Invalid => 0.18,
            AudioCue.Undo => 0.35,
            AudioCue.GameOver => 1.2,
            AudioCue.NewGame => 0.75,
            _ => 0.1
        };
        var samples = new float[(int)Math.Ceiling(duration * SampleRate)];
        switch (cue)
        {
            case AudioCue.Place:
                AddTone(samples, 0, 0.12, 72, 0.26, Tone.Bell);
                AddTone(samples, 0.008, 0.07, 60, 0.11, Tone.Warm);
                break;
            case AudioCue.Clear:
                AddPhrase(samples, [72, 76, 79, 84], 0.055, 0.28, 0.2, tier - 1);
                break;
            case AudioCue.Combo:
                AddPhrase(samples, [72, 76, 79, 83, 86, 91], 0.07, 0.32, 0.19, tier - 1);
                AddTone(samples, 0.37, 0.37, 72 + tier - 1, 0.13, Tone.Warm);
                break;
            case AudioCue.Invalid:
                AddTone(samples, 0, 0.15, 43, 0.17, Tone.Warm);
                AddTone(samples, 0.04, 0.1, 42, 0.09, Tone.Warm);
                break;
            case AudioCue.Undo:
                AddPhrase(samples, [79, 76, 72], 0.065, 0.17, 0.19);
                break;
            case AudioCue.GameOver:
                AddPhrase(samples, [72, 69, 65, 60], 0.2, 0.5, 0.17);
                break;
            case AudioCue.NewGame:
                AddPhrase(samples, [60, 67, 72, 76, 79], 0.08, 0.36, 0.19);
                break;
            case AudioCue.Click:
                AddTone(samples, 0, 0.075, 83, 0.15, Tone.Bell);
                break;
        }
        return samples;
    }

    private static void AddPhrase(float[] target, int[] notes, double spacing, double duration,
        double gain, int transpose = 0)
    {
        for (var i = 0; i < notes.Length; i++)
            AddTone(target, i * spacing, duration, notes[i] + transpose, gain, Tone.Bell);
    }

    private static void AddTone(float[] target, double startSeconds, double durationSeconds, int midiNote,
        double gain, Tone tone, bool wrap = false)
    {
        var start = (int)Math.Round(startSeconds * SampleRate);
        var count = Math.Max(2, (int)Math.Round(durationSeconds * SampleRate));
        var frequency = 440 * Math.Pow(2, (midiNote - 69) / 12.0);
        for (var i = 0; i < count; i++)
        {
            var index = start + i;
            if (index >= target.Length)
            {
                if (!wrap) break;
                index %= target.Length;
            }
            var t = i / (double)SampleRate;
            var phase = Tau * frequency * t;
            var progress = i / (double)(count - 1);
            var attack = Math.Min(1, t / (tone == Tone.Pad ? 0.12 : 0.008));
            var release = Math.Min(1, (count - 1 - i) / (SampleRate * (tone == Tone.Pad ? 0.35 : 0.035)));
            var decay = tone == Tone.Pad ? 1 : Math.Exp(-progress * (tone == Tone.Bell ? 4.5 : 2.5));
            var signal = tone switch
            {
                Tone.Bell => Math.Sin(phase) * 0.78 + Math.Sin(phase * 2) * 0.17 + Math.Sin(phase * 3) * 0.05,
                Tone.Pad => Math.Sin(phase) * 0.83 + Math.Sin(phase * 2) * 0.17,
                _ => Math.Sin(phase) * 0.9 + Math.Sin(phase * 2) * 0.1
            };
            target[index] += (float)(signal * gain * attack * release * decay);
        }
    }

    private enum Tone { Bell, Pad, Warm }
}
