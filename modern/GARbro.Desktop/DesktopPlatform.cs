using System;
using Avalonia;

namespace GARbro.Desktop
{
    public static class DesktopPlatform
    {
        public const string AppId="org.garbro.GARbro";

        public static AppBuilder Configure(AppBuilder builder)
        {
            builder.UsePlatformDetect().WithInterFont()
                .With(new X11PlatformOptions {UseDBusFilePicker=true});
            if(OperatingSystem.IsLinux())
            {
                builder.With(new WaylandPlatformOptions {AppId=AppId});
                string backend=Environment.GetEnvironmentVariable("GARBRO_LINUX_BACKEND")??"auto";
                switch(backend.ToLowerInvariant())
                {
                    // The native backend binds zwp_text_input_v3 automatically
                    // when the compositor advertises its manager. TextBox supplies
                    // composition, surrounding text, selection and cursor geometry.
                    case "auto":builder.UseWaylandWithFallback();break;
                    case "wayland":builder.UseWayland();break;
                    case "x11":break;
                    default:throw new ArgumentException("GARBRO_LINUX_BACKEND must be auto, wayland or x11.");
                }
            }
            return builder;
        }
    }
}
