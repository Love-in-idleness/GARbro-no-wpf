using System;
using System.Collections.Generic;
using System.IO;
using GARbro.Application;
using GameRes;
using GameRes.Formats.KiriKiri;
using Xunit;

[Collection("Decoder state")]
public sealed class SchemeTests
{
    [Fact]
    public void BundledHistoricalDatabaseLoadsAllMigratedSchemeGroups()
    {
        var report = ArchiveService.BundledSchemes;
        Assert.NotNull(report);
        Assert.Equal(148, report.Version);
        Assert.Empty(report.Failed);
        Assert.Contains("XP3", report.Applied);
        Assert.Equal(7, report.Applied.Length);
        Assert.True(Xp3Opener.KnownSchemes.Count >= 500);
    }

    [Fact]
    public void PolymorphicSchemesRoundTripWithoutBinaryFormatter()
    {
        ArchiveService.Formats();
        var catalog = FormatCatalog.Instance;
        Xp3Opener.KnownSchemes["synthetic-unit"] = new XorCrypt(0xA7);
        Xp3Opener.KnownSchemes["constructor-only"] = new CxEncryption(new CxScheme { Mask = 7, Offset = 11, ControlBlock = new uint[] { 1, 2, 3 } });
        try
        {
            using var stream = new MemoryStream();
            catalog.SerializePortableSchemeJson(stream);
            Xp3Opener.KnownSchemes.Remove("synthetic-unit");
            stream.Position = 0;
            var report = catalog.DeserializePortableSchemeJson(stream);
            Assert.Empty(report.Failed);
            Assert.Contains("XP3", report.Applied);
            Assert.Equal(0xA7, Assert.IsType<XorCrypt>(Xp3Opener.KnownSchemes["synthetic-unit"]).Key);
            var cx = Assert.IsType<CxEncryption>(Xp3Opener.KnownSchemes["constructor-only"]);
            var cache = typeof(CxEncryption).GetField("m_program_list", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(cx);
            Assert.Equal(128, Assert.IsAssignableFrom<System.Array>(cache).Length);
        }
        finally
        {
            Xp3Opener.KnownSchemes.Remove("synthetic-unit");
            Xp3Opener.KnownSchemes.Remove("constructor-only");
        }
    }

    [Fact]
    public void SchemeTypeAllowlistRejectsFrameworkObjects()
    {
        ArchiveService.Formats();
        const string data = "{\"Version\":1,\"SchemeMap\":{\"XP3\":{\"$type\":\"System.IO.FileInfo, mscorlib\",\"OriginalPath\":\"unsafe\"}}}";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data));
        var result = FormatCatalog.Instance.DeserializePortableSchemeJson(stream);
        Assert.Contains("XP3", result.Failed.Keys);
        Assert.Empty(result.Applied);
    }
}
