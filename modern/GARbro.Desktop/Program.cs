using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace GARbro.Desktop
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        public static AppBuilder BuildAvaloniaApp() => DesktopPlatform.Configure(AppBuilder.Configure<App>()).LogToTrace();
    }

    public sealed class App : Avalonia.Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }
}
