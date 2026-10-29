using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace GARbro.Desktop
{
    internal static class Program
    {
        [STAThread] public static void Main(string[] args)=>BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        public static AppBuilder BuildAvaloniaApp()=>AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont()
            .With(new X11PlatformOptions { UseDBusFilePicker=true }).LogToTrace();
    }
    public sealed class App : Avalonia.Application
    {
        public override void Initialize()=>Styles.Add(new FluentTheme());
        public override void OnFrameworkInitializationCompleted()
        {
            if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)desktop.MainWindow=new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }
}
