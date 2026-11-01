using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using GARbro.Application;
namespace GARbro.Desktop;

public sealed partial class MainWindow
{
    void BuildPlayback()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(ActionButton("Play", PlayAudio));
        row.Children.Add(ActionButton("Pause", () => { player?.Pause(); return Task.CompletedTask; }));
        row.Children.Add(ActionButton("Stop", () => { player?.Stop(); return Task.CompletedTask; }));
        audioControls.Children.Add(row); audioControls.Children.Add(playbackInfo); audioControls.Children.Add(position); audioControls.Children.Add(Label("Volume")); audioControls.Children.Add(volume);
        volume.Value = Settings.Volume; volume.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { Settings.Volume = volume.Value; if (player != null) player.Volume = volume.Value; } };
        position.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty && !changingPosition && player != null) player.Position = position.Value; };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        timer.Tick += (_, _) =>
        {
            if (player == null) return;
            changingPosition = true; position.Maximum = Math.Max(1, player.Duration); position.Value = player.Position; changingPosition = false;
            playbackInfo.Text = TimeSpan.FromSeconds(player.Position).ToString(@"mm\:ss") + " / " + TimeSpan.FromSeconds(player.Duration).ToString(@"mm\:ss");
        };
        timer.Start(); Closed += (_, _) => timer.Stop();
    }
    async Task PlayAudio()
    {
        if (player != null) { player.Play(); return; }
        if (previewEntry == null || listing == null) return;
        audioDirectory = Path.Combine(DesktopServices.CacheDirectory, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(audioDirectory);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(audioDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string output = Path.Combine(audioDirectory, "preview.wav");
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); active = operation; SetBusy(true); status.Text = Text["Preparing"];
        try
        {
            var artifact = await Request<AudioArtifact>(new("resource-audio", null, Location: listing.Location, Entry: previewEntry.Name, Output: output), operation.Token);
            if (artifact.Path != output || new FileInfo(output).Length != artifact.Bytes) throw new InvalidDataException("Invalid preview artifact.");
            player = UiPlatform.Audio(output); player.Volume = volume.Value; player.Play();
        }
        catch { StopAudio(); throw; }
        finally { active = null; SetBusy(false); }
    }
    void StopAudio()
    {
        player?.Dispose(); player = null;
        if (audioDirectory != null)
        {
            try { if (Directory.Exists(audioDirectory)) Directory.Delete(audioDirectory, true); } catch (IOException) { }
            audioDirectory = null;
        }
    }
}
