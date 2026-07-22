using System.IO;
using System.Runtime.InteropServices;

namespace CryoChaos.Services;

public enum ChaosSoundSource
{
    WindowsAlias,
    WaveFile
}

public sealed class SoundEffectService : IDisposable
{
    private const uint SndAsync = 0x0001;
    private const uint SndNodefault = 0x0002;
    private const uint SndLoop = 0x0008;
    private const uint SndFilename = 0x00020000;
    private const uint SndAlias = 0x00010000;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task PlayAsync(
        string sound,
        ChaosSoundSource source,
        int repeats,
        TimeSpan delayBetweenPlays,
        bool loop,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sound))
        {
            throw new ArgumentException("A sound alias or .wav path is required.", nameof(sound));
        }

        await _gate.WaitAsync(cancellationToken);
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        try
        {
            string value = source == ChaosSoundSource.WaveFile
                ? ResolveWavePath(sound)
                : sound;
            uint flags = SndAsync | SndNodefault |
                (source == ChaosSoundSource.WaveFile ? SndFilename : SndAlias) |
                (loop ? SndLoop : 0);

            if (loop)
            {
                PlaySound(value, IntPtr.Zero, flags);
                await Task.Delay(duration, cancellationToken);
                return;
            }

            for (int index = 0; index < Math.Max(1, repeats); index++)
            {
                PlaySound(value, IntPtr.Zero, flags);
                if (index < repeats - 1)
                {
                    await Task.Delay(delayBetweenPlays, cancellationToken);
                }
            }

            TimeSpan remaining = duration - (DateTimeOffset.UtcNow - startedAt);
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }
        }
        finally
        {
            PlaySound(null, IntPtr.Zero, 0);
            _gate.Release();
        }
    }

    private static string ResolveWavePath(string path)
    {
        string fullPath = Path.GetFullPath(
            Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The chaos sound file was not found.", fullPath);
        }

        return fullPath;
    }

    public void Dispose()
    {
        PlaySound(null, IntPtr.Zero, 0);
        _gate.Dispose();
    }

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(string? sound, IntPtr module, uint flags);
}
