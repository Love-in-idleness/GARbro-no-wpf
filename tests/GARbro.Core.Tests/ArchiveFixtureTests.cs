using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GARbro.Application;
using GameRes;
using Xunit;

[CollectionDefinition("Decoder state", DisableParallelization = true)]
public sealed class DecoderCollection
{
}

[Collection("Decoder state")]
public sealed class ArchiveFixtureTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "garbro-tests-" + Guid.NewGuid().ToString("N"));
    static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    [Fact]
    public async Task EveryPositiveFixtureListsAndExtractsExpectedHashes()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Fixture("manifest.json")));
        foreach (var item in manifest.RootElement.GetProperty("archives").EnumerateArray())
        {
            string name = item.GetProperty("file").GetString();
            if (name is "unsafe.xfl" or "collision.xfl")
                continue;
            string expected = item.GetProperty("format").GetString();
            using var archive = ArchiveService.Open(Fixture(name), options: new JsonOptionsProvider(scheme: expected == "XP3" ? "none" : null));
            Assert.Equal(expected, archive.Tag);
            var entries = item.GetProperty("entries").EnumerateArray().ToArray();
            Assert.Equal(entries.Length, archive.Entries.Count);
            await archive.ExtractAsync(Path.Combine(root, name));
            foreach (var entry in entries)
            {
                string relative = entry.GetProperty("path").GetString();
                string output = ExtractionPath.Resolve(Path.Combine(root, name), relative);
                Assert.True(File.Exists(output), name + ": missing " + relative);
                Assert.Equal(entry.GetProperty("size").GetInt64(), new FileInfo(output).Length);
                Assert.Equal(entry.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output))).ToLowerInvariant());
            }
        }
    }

    [Fact]
    public void PortableLIMDecodesKnownPixels()
    {
        var image = ArchiveService.ReadImage(Fixture("raw565.lim"));
        Assert.Equal(2u, image.Width);
        Assert.Equal(1u, image.Height);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 255, 0, 255 }, image.Bitmap.ToBgra32());
    }

    [Fact]
    public async Task TraversalAndCaseCollisionsFailBeforeCreatingFiles()
    {
        foreach (string name in new[]
        {
            "unsafe.xfl",
            "collision.xfl"
        }

        )
        {
            using var archive = ArchiveService.Open(Fixture(name));
            await Assert.ThrowsAsync<InvalidDataException>(() => archive.ExtractAsync(root));
            Assert.False(Directory.Exists(root));
        }
    }

    [Theory]
    [InlineData("../bad")]
    [InlineData("a/../../bad")]
    [InlineData("/absolute")]
    [InlineData("C:\\drive")]
    [InlineData("\\\\server\\share")]
    [InlineData("a\\..\\bad")]
    [InlineData("file:stream")]
    [InlineData("NUL.txt")]
    public void RejectsUnsafePathsOnEveryPlatform(string name) => Assert.Throws<InvalidDataException>(() => ExtractionPath.Resolve(root, name));
    [Fact]
    public async Task LinksCannotRedirectExtractionOutsideRoot()
    {
        if (OperatingSystem.IsWindows())
            return;
        Directory.CreateDirectory(root);
        string outside = Path.Combine(root, "outside");
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(Path.Combine(root, "sub"), outside);
        using var archive = ArchiveService.Open(Fixture("standard.xfl"));
        await Assert.ThrowsAsync<InvalidDataException>(() => archive.ExtractAsync(root));
        Assert.Empty(Directory.EnumerateFiles(outside));
    }

    [Fact]
    public async Task LimitsAndCancellationRemoveIncompleteOutputs()
    {
        using var archive = ArchiveService.Open(Fixture("standard.xfl"));
        await Assert.ThrowsAsync<InvalidDataException>(() => archive.ExtractAsync(root, limits: new ExtractionLimits(MaxEntryBytes: 2)));
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => archive.ExtractAsync(root, cancellation: cancelled.Token));
    }

    [Fact]
    public async Task OverwritePoliciesAreExplicit()
    {
        using var archive = ArchiveService.Open(Fixture("standard.xfl"));
        await archive.ExtractAsync(root);
        File.WriteAllText(Path.Combine(root, "hello.txt"), "existing");
        await Assert.ThrowsAsync<IOException>(() => archive.ExtractAsync(root));
        await archive.ExtractAsync(root, OverwritePolicy.Skip);
        Assert.Equal("existing", File.ReadAllText(Path.Combine(root, "hello.txt")));
        await archive.ExtractAsync(root, OverwritePolicy.Replace);
        Assert.Equal("Hello, GARbro!\n", File.ReadAllText(Path.Combine(root, "hello.txt")));
    }

    [Fact]
    public void TruncatedArchivesFailWithoutCrashing()
    {
        Directory.CreateDirectory(root);
        foreach (string name in new[]
        {
            "standard.xp3",
            "pf8.pfs",
            "standard.npa"
        }

        )
        {
            string truncated = Path.Combine(root, name);
            File.WriteAllBytes(truncated, File.ReadAllBytes(Fixture(name)).Take(12).ToArray());
            Assert.ThrowsAny<Exception>(() => ArchiveService.Open(truncated));
        }
    }

    [Fact]
    public void MissingEncryptionOptionsStopDetectionBeforeFallbackFormats()
    {
        var error = Assert.Throws<MissingOptionsException>(() => ArchiveService.Open(Fixture("standard.xp3")));
        Assert.Contains("XP3", error.Message);
    }

    [Fact]
    public void OptionsAreIndependentOfUiAndNeverAcceptTypeMetadata()
    {
        var provider = new JsonOptionsProvider("{\"Password\":\"synthetic\"}");
        var options = (GameRes.Formats.NScripter.NsaOptions)provider.GetOptions(new GameRes.Formats.NScripter.NsaOpener(), "password");
        Assert.Equal("synthetic", options.Password);
        Assert.Throws<ArgumentException>(() => new JsonOptionsProvider("{\"$type\":\"System.IO.FileInfo, System.Private.CoreLib\"}"));
    }
}
