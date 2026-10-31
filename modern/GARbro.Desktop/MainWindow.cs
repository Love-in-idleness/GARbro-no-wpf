using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using GARbro.Application;

namespace GARbro.Desktop;

public sealed partial class MainWindow : Window
{
    readonly SettingsStore store;
    internal AppSettings Settings;
    internal DesktopText Text;
    readonly Dictionary<string, DeskCommand> commands = new();
    readonly Menu menu = new();
    readonly StackPanel toolbar = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    readonly StackPanel footer = new() { Spacing = 6 };
    readonly TextBox search = new() { Name = "Search", Width = 220 };
    readonly TextBox scheme = new() { Name = "Scheme", Text = "none", Width = 250 };
    readonly TextBox options = new() { Name = "Options", AcceptsReturn = true, MinHeight = 55, PlaceholderText = "{\"Password\": \"…\"}" };
    readonly TextBox path = new() { Name = "Location", HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TreeView folders = new() { Name = "Folders" };
    readonly ListBox files = new() { Name = "Files", SelectionMode = SelectionMode.Multiple };
    readonly Image image = new() { Stretch = Stretch.Uniform };
    readonly ScrollViewer imageScroll = new();
    readonly ComboBox previewEncoding = new() { ItemsSource = new[] { "auto", "gbk", "shift_jis", "utf-8", "utf-16", "gb18030", "big5" }, SelectedIndex = 0, MinWidth = 115 };
    readonly Grid browser = new() { ColumnDefinitions = new("180,*,350"), ColumnSpacing = 14 };
    readonly TextBox text = new() { IsReadOnly = true, AcceptsReturn = true, FontFamily = FontFamily.Parse("monospace") };
    readonly TextBlock previewInfo = new(), status = new();
    readonly ProgressBar progress = new() { Minimum = 0, Maximum = 1, Height = 4 };
    readonly StackPanel audioControls = new() { Spacing = 8, IsVisible = false };
    readonly StackPanel pageControls = new() { Orientation = Orientation.Horizontal, Spacing = 6, IsVisible = false };
    readonly Slider position = new() { Minimum = 0, Maximum = 1 }, volume = new() { Minimum = 0, Maximum = 1, Value = 1 };
    readonly TextBlock playbackInfo = new();
    readonly Expander advanced = new();
    readonly CancellationTokenSource lifetime = new();
    readonly List<ResourceLocation> history = new(), roots = new();
    readonly Dictionary<string, string> typeOverrides = new(StringComparer.Ordinal);
    readonly Dictionary<string, Dictionary<string, string>> sessionOptions = new(StringComparer.Ordinal);
    CancellationTokenSource active, previewCancellation;
    BrowserListing listing;
    ResourceFormatInfo[] catalog;
    Bitmap bitmap;
    AudioPlayback player;
    string audioDirectory;
    ResourcePreview preview;
    BrowserEntry previewEntry;
    int historyIndex = -1, generation;
    bool isClosed, busy, changingPosition;
    public int VisibleEntryCount => files.ItemCount;
    public string ArchiveTag => listing?.Tag;
    public ResourceLocation CurrentLocation => listing?.Location;

    public MainWindow() : this(new SettingsStore()) { }
    public MainWindow(SettingsStore settingsStore)
    {
        store = settingsStore; Settings = store.Load(); Text = new(Settings.EffectiveLanguage);
        Width = Math.Clamp(Settings.Width, 850, 2400); Height = Math.Clamp(Settings.Height, 540, 1800);
        MinWidth = 850; MinHeight = 540; Title = "GARbro";
        try { Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://GARbro.Desktop/Assets/app-icon.ico"))); } catch (FileNotFoundException) { }
        CreateCommands();
        var layout = new Grid { RowDefinitions = new("Auto,Auto,Auto,*,Auto"), Margin = new Thickness(18), RowSpacing = 12 };
        var heading = new Grid { ColumnDefinitions = new("*,Auto,Auto"), ColumnSpacing = 8 };
        var brand = new StackPanel();
        brand.Children.Add(new TextBlock { Text = "GARbro", FontSize = 27, FontWeight = FontWeight.SemiBold });
        brand.Children.Add(Label("Subtitle")); heading.Children.Add(brand);
        var settingsButton = Button("Settings"); settingsButton.Content = GearIcon(); settingsButton.Width = 38;
        settingsButton.Bind(ToolTip.TipProperty, Bind("Settings")); Grid.SetColumn(settingsButton, 1); heading.Children.Add(settingsButton);
        var helpButton = new Button { Content = new TextBlock { Text = "?", FontSize = 21, FontWeight = FontWeight.Bold }, Width = 38 };
        helpButton.Bind(ToolTip.TipProperty, Bind("Help")); helpButton.Click += (_, _) => ShowHelpMenu(helpButton);
        Grid.SetColumn(helpButton, 2); heading.Children.Add(helpButton); layout.Children.Add(heading);
        Grid.SetRow(menu, 1); layout.Children.Add(menu); BuildMenus();
        var operations = new StackPanel { Spacing = 8 };
        foreach (string key in new[] { "Back", "Forward", "Parent", "Open", "OpenFolder", "ExtractAll", "Recursive", "Cancel" }) toolbar.Children.Add(Button(key));
        search.Bind(TextBox.PlaceholderTextProperty, Bind("Search")); toolbar.Children.Add(search); operations.Children.Add(toolbar);
        path.KeyDown += async (_, e) => { if (!busy && e.Key == Key.Enter) { e.Handled = true; await Guard(() => LoadInputsAsync(new[] { path.Text })); } };
        operations.Children.Add(path);
        var advancedContent = new StackPanel { Spacing = 6 };
        var keyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        keyRow.Children.Add(Label("Scheme")); keyRow.Children.Add(scheme); advancedContent.Children.Add(keyRow); advancedContent.Children.Add(options);
        advanced.Bind(Expander.HeaderProperty, Bind("Advanced")); advanced.Content = advancedContent; operations.Children.Add(advanced);
        Grid.SetRow(operations, 2); layout.Children.Add(operations);
        var folderPanel = new DockPanel(); var foldersLabel = Label("Folders"); DockPanel.SetDock(foldersLabel, Dock.Top);
        folderPanel.Children.Add(foldersLabel); folderPanel.Children.Add(folders); browser.Children.Add(folderPanel);
        var list = new Grid { RowDefinitions = new("Auto,*"), RowSpacing = 6 };
        var headings = new Grid { ColumnDefinitions = new("*,80,90"), Margin = new Thickness(10, 0) };
        foreach (var item in new[] { ("Name", 0), ("Type", 1), ("Size", 2) })
        { var label = Label(item.Item1); label.FontSize = 12; label.Opacity = .65; Grid.SetColumn(label, item.Item2); headings.Children.Add(label); }
        list.Children.Add(headings);
        files.ItemTemplate = new FuncDataTemplate<BrowserEntry>((entry, _) =>
        {
            if (entry == null) return new Control();
            var row = new Grid { ColumnDefinitions = new("*,80,90"), Margin = new Thickness(8, 6) };
            row.Children.Add(new TextBlock { Text = (entry.IsDirectory ? "▸ " : "") + entry.DisplayName, TextTrimming = TextTrimming.CharacterEllipsis });
            var kind = new TextBlock { Text = entry.IsDirectory ? Text["Folders"] : ResourceType(entry), Opacity = .65, FontSize = 12 };
            Grid.SetColumn(kind, 1); row.Children.Add(kind);
            var size = new TextBlock { Text = entry.IsDirectory ? "" : FormatSize(entry.Size), HorizontalAlignment = HorizontalAlignment.Right, FontSize = 12 };
            Grid.SetColumn(size, 2); row.Children.Add(size); return row;
        });
        files.DoubleTapped += async (_, _) => await Guard(OpenSelected);
        files.SelectionChanged += async (_, _) => { UpdateCommands(); await PreviewSelection(); };
        files.ContextMenu = BuildContextMenu(); Grid.SetRow(files, 1); list.Children.Add(files); Grid.SetColumn(list, 1); browser.Children.Add(list);
        var previewPanel = new Grid { RowDefinitions = new("Auto,Auto,*"), RowSpacing = 8 };
        previewPanel.Children.Add(Label("Preview")); Grid.SetRow(previewInfo, 1); previewPanel.Children.Add(previewInfo);
        var content = new Grid(); imageScroll.Content = image; content.Children.Add(imageScroll); content.Children.Add(text);
        BuildPlayback(); content.Children.Add(audioControls);
        var previewStack = new DockPanel(); DockPanel.SetDock(pageControls, Dock.Bottom); previewStack.Children.Add(pageControls); previewStack.Children.Add(content);
        Grid.SetRow(previewStack, 2); previewPanel.Children.Add(previewStack); Grid.SetColumn(previewPanel, 2); browser.Children.Add(previewPanel);
        Grid.SetRow(browser, 3); layout.Children.Add(browser);
        footer.Children.Add(progress); footer.Children.Add(status); Grid.SetRow(footer, 4); layout.Children.Add(footer); Content = layout;
        pageControls.Children.Add(ActionButton("PreviousPage", () => ShowPreviewPage(Math.Max(0, (preview?.Offset ?? 0) - 65536))));
        pageControls.Children.Add(ActionButton("NextPage", () => ShowPreviewPage((preview?.Offset ?? 0) + 65536)));
        pageControls.Children.Add(ActionButton("Copy", async () => { if (Clipboard != null) await Clipboard.SetTextAsync(text.Text ?? ""); }));
        var encodingControls = new StackPanel { Spacing = 6, IsVisible = false };
        encodingControls.Children.Add(Label("Encoding")); encodingControls.Children.Add(previewEncoding);
        var wrap = new CheckBox { IsChecked = Settings.WrapText }; wrap.Bind(ContentControl.ContentProperty, Bind("Wrap"));
        wrap.IsCheckedChanged += (_, _) => { Settings.WrapText = wrap.IsChecked == true; text.TextWrapping = Settings.WrapText ? TextWrapping.Wrap : TextWrapping.NoWrap; };
        encodingControls.Children.Add(wrap); DockPanel.SetDock(encodingControls, Dock.Bottom); previewStack.Children.Insert(0, encodingControls);
        previewEncoding.SelectionChanged += async (_, _) => { if (previewEntry != null) await ShowPreviewPage(0); };
        text.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) encodingControls.IsVisible = text.IsVisible; };
        search.TextChanged += (_, _) => RefreshFiles();
        folders.SelectionChanged += async (_, _) =>
        {
            if (folders.SelectedItem is TreeViewItem { Tag: ResourceLocation location } && location != CurrentLocation)
                await Guard(() => LoadLocationAsync(location));
        };
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = !busy && e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None);
        AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            if (busy) return;
            var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).Where(p => p != null).ToArray();
            if (paths?.Length > 0) await Guard(() => LoadInputsAsync(paths));
        });
        KeyDown += OnKeyDown; WaylandTextInput.Attach(this); ApplySettings(false);
        status.Text = store.LoadError == null ? Text["Welcome"] : Text["Error"] + ": " + store.LoadError;
        text.IsVisible = imageScroll.IsVisible = false;
        Closed += (_, _) =>
        {
            isClosed = true; lifetime.Cancel(); active?.Cancel(); previewCancellation?.Cancel(); StopAudio(); bitmap?.Dispose();
            Settings.Width = Width; Settings.Height = Height;
            try { store.Save(Settings); } catch (IOException error) { DesktopServices.Log(error); }
        };
        UpdateCommands();
    }

    Binding Bind(string key) => new("[" + key + "]") { Source = Text };
    TextBlock Label(string key) { var label = new TextBlock(); label.Bind(TextBlock.TextProperty, Bind(key)); return label; }
    Button Button(string key) { var button = new Button { Command = commands[key], Name = key == "Open" ? "OpenArchive" : key == "Cancel" ? "CancelOperation" : null }; button.Bind(ContentControl.ContentProperty, Bind(key)); return button; }
    internal Button ActionButton(string key, Func<Task> run) { var b = new Button(); b.Bind(ContentControl.ContentProperty, Bind(key)); b.Click += async (_, _) => await Guard(run); return b; }
    static Control GearIcon() => new PathIcon { Width = 20, Height = 20, Data = Geometry.Parse("M10,0 L14,0 L15,3 L18,4 L21,3 L24,7 L22,10 L22,13 L24,16 L21,20 L18,19 L15,21 L14,24 L10,24 L9,21 L6,19 L3,20 L0,16 L2,13 L2,10 L0,7 L3,3 L6,4 L9,3 Z M12,7 A5,5 0 1 0 12,17 A5,5 0 1 0 12,7") };
    static string FormatSize(long n) => n < 1024 ? n + " B" : n < 1048576 ? (n / 1024d).ToString("F1") + " KiB" : (n / 1048576d).ToString("F1") + " MiB";
    string ResourceType(BrowserEntry e) => typeOverrides.GetValueOrDefault(e.Name, e.Type) switch { "image" => Text["Images"], "audio" => Text["Audio"], "script" => Text["Scripts"], "archive" => Text["Archives"], _ => "" };

    internal async Task<T> Request<T>(WorkerRequest request, CancellationToken cancellation = default, IProgress<ExtractionProgress> updates = null)
    {
        string root = request.Location?.RootPath ?? request.Path ?? "";
        if (!sessionOptions.TryGetValue(root, out var profile)) sessionOptions[root] = profile = new();
        bool catalogOperation = request.Operation is "resource-catalog" or "resource-validate-schemes";
        request = request with { Context = (request.Context ?? Settings.Context) with { OptionsByFormat = new(profile) },
            OptionsJson = catalogOperation ? null : request.OptionsJson ?? (string.IsNullOrWhiteSpace(options.Text) ? null : options.Text),
            Scheme = catalogOperation ? null : request.Scheme ?? (string.IsNullOrWhiteSpace(scheme.Text) ? null : scheme.Text),
            OptionsFormatId = listing?.Location.RootPath == root ? listing.ProviderId : request.OptionsFormatId };
        while (true)
        {
            try { return await WorkerArchiveSession.RequestAsync<T>(GARbro.Worker.WorkerMarker.AssemblyPath, request, Settings.Limits, updates, cancellation); }
            catch (WorkerOperationException e) when (e.RemoteType == nameof(InvalidSchemeDatabaseException) && request.Context.SchemesFile != null)
            {
                DesktopServices.Log(e); Settings.SchemesFile = null;
                request = request with { Context = request.Context with { SchemesFile = null } };
                store.Save(Settings);
            }
            catch (WorkerOperationException e) when (e.FormatId != null && !isClosed)
            {
                catalog ??= await Request<ResourceFormatInfo[]>(new("resource-catalog", null), cancellation);
                var value = await Dialogs.Parameters(this, Text, e.OptionsJson ?? "{}", e.Message, catalog.FirstOrDefault(f => f.Id == e.FormatId)?.Schemes);
                if (value == null) throw new OperationCanceledException();
                profile[e.ContextKey ?? e.FormatId] = value;
                request = request with { Context = request.Context with { OptionsByFormat = new(profile) },
                    CompletedPaths = (request.CompletedPaths ?? Array.Empty<string>()).Concat(e.CompletedPaths ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
            }
        }
    }

    public Task LoadArchiveAsync(string path) => LoadInputsAsync(new[] { path });
    public async Task LoadInputsAsync(IEnumerable<string> inputs)
    {
        ObjectDisposedException.ThrowIf(isClosed, this);
        foreach (string input in inputs)
        {
            string full = Path.GetFullPath(input);
            var location = new ResourceLocation(full);
            try { await LoadLocationAsync(location); }
            catch (WorkerOperationException e) when (e.RemoteType == "UnknownFormatException" && File.Exists(full))
            {
                await LoadLocationAsync(new(Path.GetDirectoryName(full)));
                files.SelectedItem = listing.Entries.FirstOrDefault(f => f.Name == Path.GetFileName(full));
            }
            if (!roots.Any(r => r.RootPath == CurrentLocation.RootPath)) roots.Add(CurrentLocation);
            Settings.Recent.Remove(full); Settings.Recent.Insert(0, full); Settings.Recent = Settings.Recent.Take(20).ToList();
        }
        BuildFolders(); BuildMenus();
    }
    async Task LoadLocationAsync(ResourceLocation location, bool recordHistory = true)
    {
        ObjectDisposedException.ThrowIf(isClosed, this); StopAudio(); previewCancellation?.Cancel();
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); active = operation; SetBusy(true); status.Text = Text["Opening"];
        try
        {
            var opened = await Request<BrowserListing>(new("resource-list", null, Location: location), operation.Token);
            operation.Token.ThrowIfCancellationRequested(); if (isClosed) return;
            listing = opened; path.Text = listing.Location.DisplayPath; Title = "GARbro — " + Path.GetFileName(location.RootPath.TrimEnd(Path.DirectorySeparatorChar));
            ClearPreview(); typeOverrides.Clear(); RefreshFiles(); BuildFolders();
            if (recordHistory)
            {
                if (historyIndex + 1 < history.Count) history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
                history.Add(listing.Location); historyIndex = history.Count - 1;
            }
            status.Text = listing.Tag + " · " + listing.Entries.Length + " " + Text["AllFiles"];
        }
        finally { if (ReferenceEquals(active, operation)) active = null; SetBusy(false); }
    }
    void BuildFolders()
    {
        if (listing == null) return;
        var nodes = new List<TreeViewItem>();
        foreach (var root in roots.Where(r => r.RootPath != listing.Location.RootPath)) nodes.Add(new() { Header = Path.GetFileName(root.RootPath), Tag = root });
        var current = new TreeViewItem { Header = Text["AllFiles"], Tag = listing.Location, IsExpanded = true };
        current.ItemsSource = listing.Entries.Where(e => e.IsDirectory).Select(e => new TreeViewItem
        { Header = e.DisplayName, Tag = listing.Physical ? new ResourceLocation(Path.Combine(listing.Location.RootPath, e.Name)) : listing.Location.EnterDirectory(e.Name) }).ToArray();
        nodes.Add(current); folders.ItemsSource = nodes;
    }
    void RefreshFiles()
    {
        if (listing == null) return;
        IEnumerable<BrowserEntry> entries = listing.Entries.Where(e => e.DisplayName.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase));
        entries = Settings.Sort switch { "name" => entries.OrderBy(e => !e.IsDirectory).ThenBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            "type" => entries.OrderBy(e => !e.IsDirectory).ThenBy(e => e.Type).ThenBy(e => e.DisplayName), "size" => entries.OrderBy(e => !e.IsDirectory).ThenBy(e => e.Size), _ => entries };
        if (Settings.SortDescending && Settings.Sort != "none") entries = entries.Reverse().OrderBy(e => !e.IsDirectory);
        files.ItemsSource = entries.ToArray(); UpdateCommands();
    }
    async Task OpenSelected()
    {
        if (files.SelectedItem is not BrowserEntry entry) return;
        ResourceLocation location = listing.Physical ? new(Path.Combine(listing.Location.RootPath, entry.Name)) :
            entry.IsDirectory ? listing.Location.EnterDirectory(entry.Name) : listing.Location.EnterArchive(entry.Name);
        try { await LoadLocationAsync(location); }
        catch (WorkerOperationException e) when (!entry.IsDirectory && e.RemoteType == "UnknownFormatException") { await PreviewSelection(); }
    }
    async Task PreviewSelection()
    {
        previewCancellation?.Cancel(); StopAudio();
        if (files.SelectedItem is not BrowserEntry { IsDirectory: false } entry || listing == null) { ClearPreview(); return; }
        previewEntry = entry; await ShowPreviewPage(0);
    }
    void ClearPreview()
    {
        ++generation; previewCancellation?.Cancel(); StopAudio();
        previewEntry = null; preview = null; bitmap?.Dispose(); bitmap = null; image.Source = null;
        imageScroll.IsVisible = text.IsVisible = audioControls.IsVisible = pageControls.IsVisible = false;
        text.Text = previewInfo.Text = "";
        UpdateCommands();
    }
    async Task ShowPreviewPage(long offset)
    {
        if (previewEntry == null || listing == null) return;
        previewCancellation?.Cancel(); using var cancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); previewCancellation = cancel;
        int ticket = ++generation;
        try
        {
            var result = await Request<ResourcePreview>(new("resource-preview", null, Location: listing.Location, Entry: previewEntry.Name,
                Context: Settings.Context with { TextEncoding = previewEncoding.SelectedItem as string == "auto" ? Settings.TextEncoding : previewEncoding.SelectedItem as string },
                ForcedType: typeOverrides.GetValueOrDefault(previewEntry.Name), Offset: offset), cancel.Token);
            if (ticket != generation || isClosed) return;
            preview = result; bitmap?.Dispose(); bitmap = null; image.Source = null;
            imageScroll.IsVisible = result.Kind == "image"; audioControls.IsVisible = result.Kind == "audio";
            text.IsVisible = result.Kind is "text" or "binary"; pageControls.IsVisible = text.IsVisible;
            previewInfo.Text = result.Kind is "text" or "binary" ? result.Title : result.Text;
            if (result.Image != null) { using var input = new MemoryStream(result.Image); bitmap = new Bitmap(input); image.Source = bitmap; }
            text.Text = result.Text;
            UpdateCommands();
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        catch (Exception e) { if (ticket == generation) previewInfo.Text = Text["Error"] + ": " + e.Message; }
        finally { if (ReferenceEquals(previewCancellation, cancel)) previewCancellation = null; }
    }
    void ApplySettings(bool save = true)
    {
        Text.Change(Settings.EffectiveLanguage);
        if (Avalonia.Application.Current != null) Avalonia.Application.Current.RequestedThemeVariant = Settings.Theme switch { "dark" => ThemeVariant.Dark, "light" => ThemeVariant.Light, _ => ThemeVariant.Default };
        menu.IsVisible = Settings.MenuVisible; toolbar.IsVisible = Settings.ToolbarVisible; footer.IsVisible = Settings.StatusVisible;
        image.Stretch = Settings.FitImage ? Stretch.Uniform : Stretch.None; text.TextWrapping = Settings.WrapText ? TextWrapping.Wrap : TextWrapping.NoWrap;
        imageScroll.HorizontalScrollBarVisibility = imageScroll.VerticalScrollBarVisibility = Settings.FitImage ?
            Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        if (save) store.Save(Settings); BuildMenus(); RefreshFiles(); BuildFolders();
    }
    void SetBusy(bool value)
    {
        busy = value; progress.IsIndeterminate = value;
        path.IsEnabled = search.IsEnabled = folders.IsEnabled = files.IsEnabled = !value;
        UpdateCommands();
    }
    void UpdateCommands() { foreach (var command in commands.Values) command.Update(); }
    internal async Task Guard(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { status.Text = Text["Cancelled"]; }
        catch (Exception error) { status.Text = Text["Error"] + ": " + error.Message; DesktopServices.Log(error); }
    }
}
