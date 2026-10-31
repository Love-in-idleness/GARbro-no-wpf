using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace GARbro.Desktop
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            App.StartupArguments = (string[])args.Clone();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        public static AppBuilder BuildAvaloniaApp() => DesktopPlatform.Configure(AppBuilder.Configure<App>()).LogToTrace();
    }

    public sealed class App : Avalonia.Application
    {
        internal static string[] StartupArguments;
        public override void Initialize() => Styles.Add(new FluentTheme());
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new MainWindow();
                window.Opened += async (_, _) =>
                {
                    var arguments = StartupArguments ?? desktop.Args ?? Array.Empty<string>();
                    var inputs = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(arguments, p => System.IO.File.Exists(p) || System.IO.Directory.Exists(p)));
                    DesktopServices.Activity("startup", $"arguments={arguments.Length}; resources={inputs.Length}");
                    if (inputs.Length > 0) await window.Guard(() => window.LoadInputsAsync(inputs));
                };
                desktop.MainWindow = window;
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
