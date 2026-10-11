namespace BlockBlast.Core;

/// <summary>
/// Portable xorshift64* PRNG with an explicit, nonzero state. The algorithm is part of
/// save schema 1: changing it requires a save migration. It is for gameplay, not security.
/// </summary>
internal sealed class StatefulRandom : Random
{
    public ulong State { get; private set; }

    public StatefulRandom(int seed) => State = MixSeed(seed);

    public StatefulRandom(ulong state)
    {
        if (state == 0) throw new ArgumentOutOfRangeException(nameof(state));
        State = state;
    }

    public override int Next() => (int)Below(int.MaxValue);

    public override int Next(int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return maxValue == 0 ? 0 : (int)Below((ulong)maxValue);
    }

    public override int Next(int minValue, int maxValue)
    {
        if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
        ulong range = (ulong)((long)maxValue - minValue);
        return range == 0 ? minValue : (int)(minValue + (long)Below(range));
    }

    public override long NextInt64() => (long)Below(long.MaxValue);

    public override long NextInt64(long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return maxValue == 0 ? 0 : (long)Below((ulong)maxValue);
    }

    public override long NextInt64(long minValue, long maxValue)
    {
        if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
        ulong range = unchecked((ulong)maxValue - (ulong)minValue);
        return range == 0 ? minValue : unchecked((long)((ulong)minValue + Below(range)));
    }

    public override double NextDouble() => (NextBits() >> 11) * (1.0 / (1UL << 53));
    public override float NextSingle() => (NextBits() >> 40) * (1.0f / (1U << 24));
    protected override double Sample() => NextDouble();

    public override void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        NextBytes(buffer.AsSpan());
    }

    public override void NextBytes(Span<byte> buffer)
    {
        ulong bits = 0;
        for (int index = 0; index < buffer.Length; index++)
        {
            if (index % 8 == 0) bits = NextBits();
            buffer[index] = (byte)bits;
            bits >>= 8;
        }
    }

    private ulong Below(ulong bound)
    {
        // Reject the incomplete first bucket instead of introducing modulo bias.
        ulong threshold = unchecked(0UL - bound) % bound;
        ulong bits;
        do { bits = NextBits(); } while (bits < threshold);
        return bits % bound;
    }

    private ulong NextBits()
    {
        ulong next = State;
        next ^= next >> 12;
        next ^= next << 25;
        next ^= next >> 27;
        State = next;
        return unchecked(next * 2685821657736338717UL);
    }

    private static ulong MixSeed(int seed)
    {
        // SplitMix64 avoids weak xorshift starting states, including integer seed zero.
        ulong value = unchecked((ulong)(long)seed + 0x9E3779B97F4A7C15UL);
        value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL);
        value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EBUL);
        value ^= value >> 31;
        return value == 0 ? 0x9E3779B97F4A7C15UL : value;
    }
}
