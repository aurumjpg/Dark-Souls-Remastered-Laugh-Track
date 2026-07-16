using DSLaughTrack.Audio;
using NAudio.Wave;
using Xunit;

namespace DSLaughTrack.Tests;

public class AudioFormatSniffTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dslt-sniff-" + Guid.NewGuid());
    public AudioFormatSniffTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// Minimal valid 16-bit mono PCM WAV: 44-byte RIFF header + 4 silent samples.
    private static byte[] MinimalWav()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        const int dataSize = 8;
        w.Write("RIFF"u8.ToArray()); w.Write(36 + dataSize);
        w.Write("WAVEfmt "u8.ToArray()); w.Write(16);
        w.Write((short)1); w.Write((short)1); w.Write(22050); w.Write(44100);
        w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(dataSize);
        for (var i = 0; i < 4; i++) w.Write((short)0);
        w.Flush();
        return ms.ToArray();
    }

    [Fact]
    public void Detect_RiffHeader_IsWav() =>
        Assert.Equal(SniffedFormat.Wav, LaughPlayer.DetectFormat(Write("a.mp3", MinimalWav())));

    [Fact]
    public void Detect_Id3Header_IsMp3() =>
        Assert.Equal(SniffedFormat.Mp3, LaughPlayer.DetectFormat(Write("a.wav", new byte[] { 0x49, 0x44, 0x33, 0x04, 0, 0, 0, 0 })));

    [Fact]
    public void Detect_MpegFrameSync_IsMp3() =>
        Assert.Equal(SniffedFormat.Mp3, LaughPlayer.DetectFormat(Write("b.wav", new byte[] { 0xFF, 0xFB, 0xD0, 0x64, 0, 0, 0, 0 })));

    [Fact]
    public void Detect_Garbage_IsUnknown() =>
        Assert.Equal(SniffedFormat.Unknown, LaughPlayer.DetectFormat(Write("c.wav", new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 })));

    [Fact]
    public void Detect_TooShort_IsUnknown() =>
        Assert.Equal(SniffedFormat.Unknown, LaughPlayer.DetectFormat(Write("d.wav", new byte[] { 0x52 })));

    [Fact]
    public void OpenAudioFile_WavContentWithMp3Extension_DecodesAsWav()
    {
        var path = Write("mislabeled.mp3", MinimalWav());
        using var reader = LaughPlayer.OpenAudioFile(path);
        Assert.IsType<WaveFileReader>(reader);
    }
}
