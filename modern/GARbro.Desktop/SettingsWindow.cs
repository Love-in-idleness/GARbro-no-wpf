using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Platform.Storage;
using GARbro.Application;

namespace GARbro.Desktop;

public sealed class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings, DesktopText text, ResourceFormatInfo[] formats)
    {
        Title = text["Settings"]; Width = 720; Height = 680; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WaylandTextInput.Attach(this);
        var working = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings));
        working.SessionFormatSettings = new(settings.SessionFormatSettings);
        var body = Dialogs.Panel(); var tabs = new TabControl(); body.Children.Add(tabs);
        var general = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };
        ComboBox Choice(string key, string[] values, string selected)
        { general.Children.Add(Dialogs.Label(text, key)); var combo = new ComboBox { ItemsSource = values, SelectedItem = selected }; general.Children.Add(combo); return combo; }
        var language = Choice("Language", new[] { "auto", "en-US", "zh-CN" }, working.Language);
        var theme = Choice("Theme", new[] { "system", "light", "dark" }, working.Theme);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Choice("Encoding", new[] { "auto", "gbk", "shift_jis", "utf-8", "utf-16", "utf-16BE", "gb18030", "big5" }.Concat(Encoding.GetEncodings().Select(e => e.Name)).Distinct().ToArray(), working.TextEncoding); encoding.IsEditable = true;
        general.Children.Add(Dialogs.Label(text, "Time")); var time = new NumericUpDown { Minimum = 1, Maximum = 86400, Value = (decimal)working.TimeoutSeconds }; general.Children.Add(time);
        general.Children.Add(Dialogs.Label(text, "Memory")); var memory = new NumericUpDown { Minimum = 64, Maximum = 1048576, Value = working.MemoryMiB }; general.Children.Add(memory);
        general.Children.Add(Dialogs.Label(text, "DefaultOutput")); var output = new TextBox { Text = working.OutputDirectory }; general.Children.Add(output);
        var browse = new Button { Content = text["Browse"] }; browse.Click += async (_, _) =>
        { var folders = await StorageProvider.OpenFolderPickerAsync(new() { Title = text["SelectOutput"] }); if (folders.FirstOrDefault()?.TryGetLocalPath() is string path) output.Text = path; }; general.Children.Add(browse);
        var fit = new CheckBox { Content = text["FitImage"], IsChecked = working.FitImage }; var wrap = new CheckBox { Content = text["Wrap"], IsChecked = working.WrapText };
        general.Children.Add(fit); general.Children.Add(wrap); tabs.Items.Add(new TabItem { Header = text["General"], Content = general });
        var formatPanel = new Grid { ColumnDefinitions = new("180,*"), ColumnSpacing = 12, Height = 450 };
        var list = new ListBox { ItemsSource = formats.Where(f => f.Settings.Length > 0).ToArray(), DisplayMemberBinding = new Binding("Tag") };
        var fields = new StackPanel { Spacing = 8 }; var fieldsScroll = new ScrollViewer { Content = fields }; Grid.SetColumn(fieldsScroll, 1);
        formatPanel.Children.Add(list); formatPanel.Children.Add(fieldsScroll);
        list.SelectionChanged += (_, _) =>
        {
            fields.Children.Clear();
            if (list.SelectedItem is not ResourceFormatInfo format) return;
            foreach (var setting in format.Settings)
            {
                string key = format.Id + "/" + setting.Name;
                bool secret = setting.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) || setting.Name.Contains("Key", StringComparison.OrdinalIgnoreCase) || setting.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase);
                var destination = secret ? working.SessionFormatSettings : working.FormatSettings;
                JsonElement value = destination.GetValueOrDefault(key, setting.Value);
                fields.Children.Add(new TextBlock { Text = setting.Label ?? setting.Name, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
                if (setting.Kind == "bool")
                {
                    var check = new CheckBox { IsChecked = value.ValueKind == JsonValueKind.True }; fields.Children.Add(check);
                    check.IsCheckedChanged += (_, _) => destination[key] = JsonSerializer.SerializeToElement(check.IsChecked == true);
                }
                else if (setting.Kind == "encoding")
                {
                    var combo = new ComboBox { ItemsSource = new[] { text["Defaults"], "932", "936", "65001", "54936", "950", "1200", "1201" },
                        SelectedItem = working.FormatSettings.ContainsKey(key) ? value.ToString() : text["Defaults"], IsEditable = true };
                    fields.Children.Add(combo);
                    combo.SelectionChanged += (_, _) =>
                    { if (combo.SelectedIndex == 0) destination.Remove(key); else if (int.TryParse(combo.SelectedItem?.ToString(), out int cp)) destination[key] = JsonSerializer.SerializeToElement(cp); };
                }
                else
                {
                    var box = new TextBox { Text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString() }; fields.Children.Add(box);
                    if (secret) box.PasswordChar = '●';
                    box.TextChanged += (_, _) =>
                    {
                        if (setting.Kind == "int") { if (int.TryParse(box.Text, out int n)) destination[key] = JsonSerializer.SerializeToElement(n); }
                        else destination[key] = JsonSerializer.SerializeToElement(box.Text);
                    };
                }
            }
        };
        tabs.Items.Add(new TabItem { Header = text["Formats"], Content = formatPanel });
        var error = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap }; body.Children.Add(error);
        Content = Dialogs.Frame(this, body, Dialogs.Button(text, "Save", () =>
        {
            try
            {
                working.Language = language.SelectedItem as string ?? "auto"; working.Theme = theme.SelectedItem as string ?? "system";
                working.TextEncoding = encoding.Text ?? encoding.SelectedItem as string ?? "auto";
                working.TimeoutSeconds = (double)(time.Value ?? 300); working.MemoryMiB = (long)(memory.Value ?? 1024);
                working.OutputDirectory = output.Text?.Trim() ?? ""; working.FitImage = fit.IsChecked == true; working.WrapText = wrap.IsChecked == true; working.Validate();
                foreach (var property in typeof(AppSettings).GetProperties().Where(p => p.CanWrite)) property.SetValue(settings, property.GetValue(working));
                Close(true);
            }
            catch (Exception e) { error.Text = e.Message; }
        }), Dialogs.Button(text, "Cancel", () => Close(false)));
    }
}
