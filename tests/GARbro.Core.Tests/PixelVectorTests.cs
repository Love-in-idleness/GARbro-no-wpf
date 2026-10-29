using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GARbro.Imaging;
using Xunit;

public sealed class PixelVectorTests
{
    static JsonDocument Load() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pixel-vectors.json")));
    public static IEnumerable<object[]> Cases()
    {
        using var doc = Load();
        foreach (var item in doc.RootElement.EnumerateArray())
            yield return new object[]
            {
                item.GetProperty("name").GetString()
            };
    }

    [Theory, MemberData(nameof(Cases))]
    public void PixelModelMatchesFrozenVectors(string name)
    {
        using var doc = Load();
        var item = doc.RootElement.EnumerateArray().Single(e => e.GetProperty("name").GetString() == name);
        var format = (PixelFormat)typeof(PixelFormats).GetProperty(item.GetProperty("format").GetString()).GetValue(null);
        BitmapPalette palette = null;
        if (item.GetProperty("palette").ValueKind != JsonValueKind.Null)
            palette = new BitmapPalette(item.GetProperty("palette").EnumerateArray().Select(c => Color.FromArgb(c.GetProperty("a").GetByte(), c.GetProperty("r").GetByte(), c.GetProperty("g").GetByte(), c.GetProperty("b").GetByte())));
        var image = PixelBuffer.Create(item.GetProperty("width").GetInt32(), item.GetProperty("height").GetInt32(), 96, 96, format, palette, Convert.FromBase64String(item.GetProperty("pixels").GetString()), item.GetProperty("stride").GetInt32());
        Assert.Equal(Convert.FromBase64String(item.GetProperty("bgra32").GetString()), image.ToBgra32());
    }
}
