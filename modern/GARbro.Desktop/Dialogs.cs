using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using GARbro.Application;

namespace GARbro.Desktop;

public sealed record ExtractionChoice(string Output, ResourceExtractionOptions Options, OverwritePolicy Overwrite);
public sealed record CreationChoice(string Output, string FormatId, string OptionsJson, OverwritePolicy Overwrite);
public sealed record ConversionChoice(string Output, string FormatId, OverwritePolicy Overwrite);

public static class Dialogs
{
    public static Window Window(DesktopText text, string title, double width = 620, double height = 500)
    {
        var window = new Window { Title = text[title], Width = width, Height = height, WindowStartupLocation = WindowStartupLocation.CenterOwner, MinWidth = 400, MinHeight = 220 };
        WaylandTextInput.Attach(window); return window;
    }
    public static TextBlock Label(DesktopText text, string key) => new() { Text = text[key], TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    public static Button Button(DesktopText text, string key, Action clicked)
    { var b = new Button { Content = text[key] }; b.Click += (_, _) => clicked(); return b; }
    public static StackPanel Panel() => new() { Margin = new Avalonia.Thickness(18), Spacing = 10 };
    public static Control Frame(Window window, StackPanel body, params Button[] buttons)
    {
        var root = new DockPanel(); var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Avalonia.Thickness(18) };
        foreach (var button in buttons) bar.Children.Add(button); DockPanel.SetDock(bar, Dock.Bottom); root.Children.Add(bar);
        root.Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }); return root;
    }
    public static async Task<string> Input(Window owner, DesktopText text, string title, string initial)
    {
        var window = Window(text, title, 520, 240); var body = Panel(); var input = new TextBox { Text = initial }; body.Children.Add(input);
        window.Content = Frame(window, body, Button(text, "Save", () => window.Close(input.Text)), Button(text, "Cancel", () => window.Close()));
        return await window.ShowDialog<string>(owner);
    }
    public static async Task<bool> Confirm(Window owner, DesktopText text, string question)
    {
        var window = Window(text, "Confirm", 520, 240); var body = Panel(); body.Children.Add(Label(text, question));
        window.Content = Frame(window, body, Button(text, "Confirm", () => window.Close(true)), Button(text, "Cancel", () => window.Close(false)));
        return await window.ShowDialog<bool>(owner);
    }
    public static async Task<string> Parameters(Window owner, DesktopText text, string json, string notice, string[] schemes = null)
    {
        var window = Window(text, "MissingOptions"); var body = Panel(); body.Children.Add(new TextBlock { Text = notice, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var form = new ParameterForm(text, json, schemes); body.Children.Add(form); var error = new TextBlock(); body.Children.Add(error);
        window.Content = Frame(window, body, Button(text, "Save", () => { try { window.Close(form.Value()); } catch (Exception e) { error.Text = e.Message; } }), Button(text, "Cancel", () => window.Close()));
        return await window.ShowDialog<string>(owner);
    }
    static StackPanel Output(Window owner, DesktopText text, string initial, out TextBox path)
    {
        var row = new StackPanel { Spacing = 6 }; row.Children.Add(Label(text, "Output"));
        var input = new TextBox { Text = initial }; path = input; row.Children.Add(input);
        var browse = new Button { Content = text["Browse"] }; browse.Click += async (_, _) =>
        { var selected = await owner.StorageProvider.OpenFolderPickerAsync(new() { Title = text["SelectOutput"] }); if (selected.FirstOrDefault()?.TryGetLocalPath() is string full) input.Text = full; };
        row.Children.Add(browse); return row;
    }
    static ComboBox Overwrite(DesktopText text) => new() { ItemsSource = new[] { text["Fail"], text["Skip"], text["Replace"] }, SelectedIndex = 0 };
    public static async Task<ExtractionChoice> Extraction(Window owner, DesktopText text, string initial, bool recursive)
    {
        var window = Window(text, recursive ? "Recursive" : "ExtractAll", 620, 600); var body = Panel();
        body.Children.Add(Output(owner, text, initial, out var path)); body.Children.Add(Label(text, "Overwrite")); var overwrite = Overwrite(text); body.Children.Add(overwrite);
        var recurse = new CheckBox { Content = text["Recursive"], IsChecked = recursive }; body.Children.Add(recurse);
        body.Children.Add(Label(text, "MaxDepth")); var depth = new NumericUpDown { Minimum = 0, Maximum = 64, Value = 8 }; body.Children.Add(depth);
        var types = new[] { ("Images", "image"), ("Audio", "audio"), ("Scripts", "script"), ("Archives", "archive"), ("Other", "") };
        var checks = new List<CheckBox>(); var row = new WrapPanel(); foreach (var t in types) { var check = new CheckBox { Content = text[t.Item1], IsChecked = true, Margin = new Avalonia.Thickness(0, 0, 14, 0) }; checks.Add(check); row.Children.Add(check); } body.Children.Add(row);
        body.Children.Add(Label(text, "ImageFormat")); var image = new ComboBox { ItemsSource = new[] { "", "PNG", "BMP", "JPEG", "TIFF", "TGA" }, SelectedIndex = 0 }; body.Children.Add(image);
        var audio = new CheckBox { Content = text["AudioConvert"], IsChecked = false }; body.Children.Add(audio);
        var offset = new CheckBox { Content = text["AdjustOffset"], IsChecked = false }; body.Children.Add(offset);
        var error = new TextBlock(); body.Children.Add(error);
        window.Content = Frame(window, body, Button(text, "Start", () =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path.Text) || !Path.IsPathFullyQualified(path.Text)) throw new ArgumentException(text["SelectOutput"]);
                string[] selected = types.Where((t, i) => checks[i].IsChecked == true).Select(t => t.Item2).ToArray();
                if (selected.Length == 0) throw new ArgumentException(text["SelectFiles"]);
                window.Close(new ExtractionChoice(Path.GetFullPath(path.Text), new(recurse.IsChecked == true, (int)(depth.Value ?? 8), selected,
                    image.SelectedIndex == 0 ? null : image.SelectedItem as string, audio.IsChecked == true, offset.IsChecked == true), (OverwritePolicy)overwrite.SelectedIndex));
            }
            catch (Exception e) { error.Text = e.Message; }
        }), Button(text, "Cancel", () => window.Close()));
        return await window.ShowDialog<ExtractionChoice>(owner);
    }
    static ComboBox FormatPicker(ResourceFormatInfo[] formats) => new() { ItemsSource = formats, DisplayMemberBinding = new Binding("Tag"), SelectedIndex = 0 };
    public static async Task<CreationChoice> Creation(Window owner, DesktopText text, ResourceFormatInfo[] formats)
    {
        var window = Window(text, "Create", 650, 650); var body = Panel();
        body.Children.Add(Label(text, "Format")); var format = FormatPicker(formats); body.Children.Add(format);
        body.Children.Add(Label(text, "Filename")); var output = new TextBox(); body.Children.Add(output);
        var browse = new Button { Content = text["Browse"] }; browse.Click += async (_, _) =>
        { var f = (ResourceFormatInfo)format.SelectedItem; var selected = await owner.StorageProvider.SaveFilePickerAsync(new() { Title = text["Create"], SuggestedFileName = "archive." + f.Extensions.FirstOrDefault() }); if (selected?.TryGetLocalPath() is string full) output.Text = full; }; body.Children.Add(browse);
        var host = new ContentControl(); body.Children.Add(host); ParameterForm form = null;
        void Change() { var selected = format.SelectedItem as ResourceFormatInfo; form = new(text, selected?.OptionsJson, selected?.Schemes); host.Content = form; }
        format.SelectionChanged += (_, _) => Change(); Change();
        body.Children.Add(Label(text, "Overwrite")); var overwrite = Overwrite(text); body.Children.Add(overwrite); var error = new TextBlock(); body.Children.Add(error);
        window.Content = Frame(window, body, Button(text, "Create", () =>
        { try { if (!Path.IsPathFullyQualified(output.Text ?? "")) throw new ArgumentException(text["Filename"]); window.Close(new CreationChoice(output.Text, ((ResourceFormatInfo)format.SelectedItem).Id, form.Value(), (OverwritePolicy)overwrite.SelectedIndex)); } catch (Exception e) { error.Text = e.Message; } }), Button(text, "Cancel", () => window.Close()));
        return await window.ShowDialog<CreationChoice>(owner);
    }
    public static async Task<ConversionChoice> Conversion(Window owner, DesktopText text, ResourceFormatInfo[] formats, string initial)
    {
        var window = Window(text, "Convert"); var body = Panel(); body.Children.Add(Output(owner, text, initial, out var path));
        body.Children.Add(Label(text, "Format")); var format = FormatPicker(formats); body.Children.Add(format);
        body.Children.Add(Label(text, "Overwrite")); var overwrite = Overwrite(text); body.Children.Add(overwrite); var error = new TextBlock(); body.Children.Add(error);
        window.Content = Frame(window, body, Button(text, "Start", () =>
        { if (!Path.IsPathFullyQualified(path.Text ?? "")) { error.Text = text["SelectOutput"]; return; } window.Close(new ConversionChoice(path.Text, ((ResourceFormatInfo)format.SelectedItem).Id, (OverwritePolicy)overwrite.SelectedIndex)); }), Button(text, "Cancel", () => window.Close()));
        return await window.ShowDialog<ConversionChoice>(owner);
    }
}

/// <summary>Typed primitive options with a JSON editor for nested cipher/scheme objects.</summary>
public sealed class ParameterForm : StackPanel
{
    readonly JsonObject data;
    readonly List<Action> apply = new();
    public ParameterForm(DesktopText text, string json, string[] schemes = null)
    {
        Spacing = 8;
        data = JsonNode.Parse(string.IsNullOrWhiteSpace(json) || json == "null" ? "{}" : json) as JsonObject ?? new();
        Children.Add(Dialogs.Label(text, "Options"));
        if (schemes is { Length: > 0 })
        {
            Children.Add(Dialogs.Label(text, "Scheme"));
            var selected = new ComboBox { ItemsSource = new[] { "" }.Concat(schemes).ToArray(), SelectedIndex = 0 };
            Children.Add(selected);
            apply.Add(() => { if (selected.SelectedItem is string name && name.Length > 0) data["$scheme"] = name; });
        }
        foreach (var property in data.ToArray())
        {
            string name = property.Key; var value = property.Value;
            if (value is JsonValue scalar && scalar.TryGetValue<bool>(out var boolean))
            {
                var check = new CheckBox { Content = name, IsChecked = boolean }; Children.Add(check); apply.Add(() => data[name] = check.IsChecked == true);
            }
            else
            {
                Children.Add(new TextBlock { Text = name });
                bool complex = value is JsonObject or JsonArray;
                bool password = name.Contains("Password", StringComparison.OrdinalIgnoreCase) || name.Contains("PassPhrase", StringComparison.OrdinalIgnoreCase) || name.Contains("Secret", StringComparison.OrdinalIgnoreCase);
                bool isString = value is JsonValue primitive && primitive.TryGetValue<string>(out _) || value == null && password;
                var box = new TextBox { Text = value == null ? isString ? "" : "null" : isString ? value.GetValue<string>() : value.ToJsonString(), AcceptsReturn = complex, MinHeight = complex ? 70 : 0 };
                if (name.Contains("Password", StringComparison.OrdinalIgnoreCase) || name.Contains("Secret", StringComparison.OrdinalIgnoreCase)) box.PasswordChar = '●';
                Children.Add(box);
                apply.Add(() => data[name] = isString ? JsonValue.Create(box.Text ?? "") : JsonNode.Parse(box.Text ?? "null"));
            }
        }
        if (data.Count == 0)
        {
            var box = new TextBox { Text = "{}", AcceptsReturn = true, MinHeight = 100 }; Children.Add(box);
            apply.Add(() => { var parsed = JsonNode.Parse(box.Text) as JsonObject ?? throw new ArgumentException("Expected a JSON object."); data.Clear(); foreach (var p in parsed) data[p.Key] = p.Value?.DeepClone(); });
        }
    }
    public string Value()
    {
        foreach (var action in apply) action();
        string json = data.ToJsonString();
        using var document = JsonDocument.Parse(json);
        if (json.Contains("\"$type\"", StringComparison.Ordinal) || json.Contains("\"$ref\"", StringComparison.Ordinal)) throw new ArgumentException("Type metadata is not allowed.");
        return json;
    }
}
