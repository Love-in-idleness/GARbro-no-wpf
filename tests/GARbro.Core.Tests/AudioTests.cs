using System;
using System.IO;
using System.Threading;
using GARbro.Application;
using GameRes;
using Xunit;

[Collection("Decoder state")]
public sealed class AudioTests
{
    [Fact]
    public void WaveConversionRetainsPcmSamples()
    {
        string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tone.wav");
        string root = Path.Combine(Path.GetTempPath(), "garbro-audio-" + Guid.NewGuid());
        try
        {
            string output = Path.Combine(root, "tone.wav");
            ArchiveService.ConvertAudio(source, output);
            using var original = new WaveInput(File.OpenRead(source));
            using var converted = new WaveInput(File.OpenRead(output));
            Assert.Equal(original.Format.SamplesPerSecond, converted.Format.SamplesPerSecond);
            using var a = new MemoryStream();
            using var b = new MemoryStream();
            original.CopyTo(a);
            converted.CopyTo(b);
            Assert.Equal(a.ToArray(), b.ToArray());
            Assert.Throws<IOException>(() => ArchiveService.ConvertAudio(source, output));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
