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
            {
                var window = new MainWindow(); desktop.MainWindow = window;
                window.Opened += async (_, _) =>
                {
                    var inputs = System.Linq.Enumerable.Where(desktop.Args ?? Array.Empty<string>(), p => System.IO.File.Exists(p) || System.IO.Directory.Exists(p));
                    if (System.Linq.Enumerable.Any(inputs)) await window.Guard(() => window.LoadInputsAsync(inputs));
                };
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
