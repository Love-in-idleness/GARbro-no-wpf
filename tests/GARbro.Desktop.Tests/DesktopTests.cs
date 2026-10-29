using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using GARbro.Desktop;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(DesktopTests))]
public sealed class DesktopTests
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    [AvaloniaFact]
    public async Task WindowOpensListsAndExtractsThroughApplicationService()
    {
        string root = Path.Combine(Path.GetTempPath(), "garbro-desktop-" + Guid.NewGuid());
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.LoadArchiveAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "standard.xfl"));
            Assert.Equal("XFL", window.ArchiveTag);
            Assert.Equal(3, window.VisibleEntryCount);
            if (Environment.GetEnvironmentVariable("GARBRO_UI_SNAPSHOT")is string snapshot)
            {
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(snapshot, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            }

            await window.ExtractToAsync(root);
            Assert.Equal("Hello, GARbro!\n", File.ReadAllText(Path.Combine(root, "hello.txt")));
            Assert.True(File.Exists(Path.Combine(root, "sub", "日本語.bin")));
        }
        finally
        {
            window.Close();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
