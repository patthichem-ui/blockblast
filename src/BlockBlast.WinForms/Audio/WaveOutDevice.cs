using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BlockBlast.WinForms.Audio;

/// <summary>
/// A single WinMM PCM output, owned exclusively by the audio worker. Headers and sample
/// storage stay alive until WinMM has released them. No native callback enters managed code.
/// </summary>
internal sealed class WaveOutDevice : IDisposable
{
    private const int BufferCount = 4;
    private const int SamplesPerBuffer = PcmComposer.SampleRate / 50; // 20 ms; at most 80 ms queued.
    private const uint WaveMapper = uint.MaxValue;
    private const uint HeaderDone = 1;
    private static readonly uint HeaderSize = (uint)Marshal.SizeOf<WaveHeader>();
    private static readonly int FlagsOffset = (int)Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.Flags));
    private readonly List<Buffer> _buffers = new(BufferCount);
    private IntPtr _handle;
    private bool _disposed;

    public WaveOutDevice()
    {
        var format = new WaveFormat
        {
            FormatTag = 1, // WAVE_FORMAT_PCM
            Channels = 1,
            SamplesPerSecond = PcmComposer.SampleRate,
            AverageBytesPerSecond = PcmComposer.SampleRate * sizeof(short),
            BlockAlign = sizeof(short),
            BitsPerSample = 16,
            ExtraSize = 0
        };
        Check(Native.waveOutOpen(out _handle, WaveMapper, ref format, UIntPtr.Zero, UIntPtr.Zero, 0), "open");
        try
        {
            for (var i = 0; i < BufferCount; i++)
            {
                var buffer = new Buffer(SamplesPerBuffer);
                _buffers.Add(buffer);
                Check(Native.waveOutPrepareHeader(_handle, buffer.Header, HeaderSize), "prepare buffer");
                buffer.Prepared = true;
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void SubmitAvailableBuffers(Action<short[]> render)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (var buffer in _buffers)
        {
            if (buffer.Queued && ((uint)Marshal.ReadInt32(buffer.Header, FlagsOffset) & HeaderDone) == 0)
                continue;
            render(buffer.Samples);
            Check(Native.waveOutWrite(_handle, buffer.Header, HeaderSize), "write buffer");
            buffer.Queued = true;
        }
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Check(Native.waveOutReset(_handle), "stop playback");
        foreach (var buffer in _buffers) buffer.Queued = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle == IntPtr.Zero) return;

        Native.waveOutReset(_handle);
        // Some drivers return STILLPLAYING briefly after reset. Cleanup happens on the worker,
        // never the UI thread. In a broken-driver case, retaining native-owned memory is safer
        // than freeing memory the driver may still access.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var allReleased = true;
            foreach (var buffer in _buffers)
            {
                if (buffer.Prepared)
                {
                    var result = Native.waveOutUnprepareHeader(_handle, buffer.Header, HeaderSize);
                    if (result == 0) buffer.Prepared = false;
                    else allReleased = false;
                }
            }
            if (allReleased) break;
            Thread.Sleep(10);
        }

        foreach (var buffer in _buffers)
            if (!buffer.Prepared) buffer.Release();

        if (_buffers.All(buffer => !buffer.Prepared))
        {
            var result = Native.waveOutClose(_handle);
            if (result != 0) Trace.WriteLine($"BlockBlast audio device close returned {result}.");
            _handle = IntPtr.Zero;
        }
        else
        {
            Trace.WriteLine("BlockBlast audio driver did not release its buffers. Native-owned storage was retained until process exit.");
        }
    }

    private static void Check(uint result, string operation)
    {
        if (result != 0) throw new InvalidOperationException($"Audio could not {operation} (WinMM error {result}).");
    }

    private sealed class Buffer
    {
        private GCHandle _samplesHandle;
        public short[] Samples { get; }
        public IntPtr Header { get; private set; }
        public bool Prepared { get; set; }
        public bool Queued { get; set; }

        public Buffer(int sampleCount)
        {
            Samples = new short[sampleCount];
            _samplesHandle = GCHandle.Alloc(Samples, GCHandleType.Pinned);
            try
            {
                Header = Marshal.AllocHGlobal((int)HeaderSize);
                Marshal.StructureToPtr(new WaveHeader
                {
                    Data = _samplesHandle.AddrOfPinnedObject(),
                    BufferLength = (uint)(sampleCount * sizeof(short))
                }, Header, false);
            }
            catch
            {
                Release();
                throw;
            }
        }

        public void Release()
        {
            if (Header != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Header);
                Header = IntPtr.Zero;
            }
            if (_samplesHandle.IsAllocated) _samplesHandle.Free();
        }
    }

    // WAVEFORMATEX has WORD packing. WAVEHDR uses native pointer alignment, including
    // both DWORD_PTR members; using 32-bit integers here would corrupt memory on x64.
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSecond;
        public uint AverageBytesPerSecond;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort ExtraSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public uint BufferLength;
        public uint BytesRecorded;
        public UIntPtr User;
        public uint Flags;
        public uint Loops;
        public IntPtr Next;
        public UIntPtr Reserved;
    }

    private static class Native
    {
        [DllImport("winmm.dll", ExactSpelling = true)]
        internal static extern uint waveOutOpen(out IntPtr handle, uint deviceId, ref WaveFormat format,
            UIntPtr callback, UIntPtr instance, uint flags);

        [DllImport("winmm.dll", ExactSpelling = true)]
        internal static extern uint waveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);

        [DllImport("winmm.dll", ExactSpelling = true)]
        internal static extern uint waveOutWrite(IntPtr handle, IntPtr header, uint size);

        [DllImport("winmm.dll", ExactSpelling = true)]
        internal static extern uint waveOutReset(IntPtr handle);

        [DllImport("winmm.dll", ExactSpelling = true)]
        internal static extern uint waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);

        [DllImport("winmm.dll", ExactSpelling = true)]
        internal static extern uint waveOutClose(IntPtr handle);
    }
}
