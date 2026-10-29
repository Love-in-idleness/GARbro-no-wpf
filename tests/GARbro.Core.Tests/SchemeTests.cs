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
    [Fact] public void PolymorphicSchemesRoundTripWithoutBinaryFormatter()
    {
        ArchiveService.Formats();var catalog=FormatCatalog.Instance;
        Xp3Opener.KnownSchemes["synthetic-unit"]=new XorCrypt(0xA7);
        try
        {
            using var stream=new MemoryStream();catalog.SerializePortableSchemeJson(stream);
            Xp3Opener.KnownSchemes.Remove("synthetic-unit");stream.Position=0;
            var report=catalog.DeserializePortableSchemeJson(stream);
            Assert.Empty(report.Failed);Assert.Contains("XP3",report.Applied);
            Assert.Equal(0xA7,Assert.IsType<XorCrypt>(Xp3Opener.KnownSchemes["synthetic-unit"]).Key);
        }
        finally {Xp3Opener.KnownSchemes.Remove("synthetic-unit");}
    }
    [Fact] public void SchemeTypeAllowlistRejectsFrameworkObjects()
    {
        ArchiveService.Formats();
        const string data="{\"Version\":1,\"SchemeMap\":{\"XP3\":{\"$type\":\"System.IO.FileInfo, mscorlib\",\"OriginalPath\":\"unsafe\"}}}";
        using var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data));
        var result=FormatCatalog.Instance.DeserializePortableSchemeJson(stream);
        Assert.Contains("XP3",result.Failed.Keys);Assert.Empty(result.Applied);
    }
}
