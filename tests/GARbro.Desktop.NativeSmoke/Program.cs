using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using GARbro.Desktop;

public static class Program
{
    internal static string Fixture, Output;
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length!=2) throw new ArgumentException("Expected fixture path and output directory.");
        Fixture=Path.GetFullPath(args[0]); Output=Path.GetFullPath(args[1]);
        return AppBuilder.Configure<NativeSmokeApp>().UsePlatformDetect().WithInterFont()
            .LogToTrace().StartWithClassicDesktopLifetime(args);
    }
}

public sealed class NativeSmokeApp : Avalonia.Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        var lifetime=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime;
        var window=new MainWindow();
        lifetime.MainWindow=window;
        window.Opened+=async (_,_)=>
        {
            int code=1;
            try
            {
                await window.LoadArchiveAsync(Program.Fixture);
                if (window.ArchiveTag!="XFL" || window.VisibleEntryCount!=3)
                    throw new InvalidDataException("Native window did not list the fixture.");
                // Let the native window complete its layout/render pass.
                await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background);
                await Task.Delay(150);
                Directory.CreateDirectory(Program.Output);
                using (var image=new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height)))
                {
                    image.Render(window);
                    image.Save(Path.Combine(Program.Output,"window.png"),new PngBitmapEncoderOptions());
                }
                string extracted=Path.Combine(Program.Output,"extracted");
                await window.ExtractToAsync(extracted);
                if (File.ReadAllText(Path.Combine(extracted,"hello.txt"))!="Hello, GARbro!\n"
                    || !File.Exists(Path.Combine(extracted,"sub","日本語.bin")))
                    throw new InvalidDataException("Native window extraction differs.");
                Console.WriteLine("Native desktop window rendered, listed and extracted the fixture.");
                code=0;
            }
            catch(Exception error) { Console.Error.WriteLine(error); }
            finally { lifetime.Shutdown(code); }
        };
        base.OnFrameworkInitializationCompleted();
    }
}
