using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using GARbro.Application;

namespace GARbro.Desktop
{
    public sealed class MainWindow : Window
    {
        readonly Button open = new(), extract = new(), extractSelected = new(), cancel = new(), language = new();
        readonly TextBox search = new()
        {
            Name = "Search",
            Width = 240
        }, scheme = new()
        {
            Text = "none",
            Width = 220
        };
        readonly TextBox options = new()
        {
            AcceptsReturn = true,
            MinHeight = 60,
            PlaceholderText = "{\"Password\": \"…\"}"
        };
        readonly TextBlock status = new(), subtitle = new(), folderTitle = new(), previewTitle = new(), previewInfo = new(), encryptionLabel = new();
        readonly TreeView folders = new()
        {
            Name = "Folders"
        };
        readonly ListBox files = new()
        {
            Name = "Files",
            SelectionMode = SelectionMode.Multiple
        };
        readonly Image image = new()
        {
            Stretch = Stretch.Uniform
        };
        readonly TextBox text = new()
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = FontFamily.Parse("monospace")
        };
        readonly ProgressBar progress = new()
        {
            MinHeight = 4,
            Minimum = 0,
            Maximum = 1
        };
        readonly Expander advanced = new();
        ArchiveSession session;
        CancellationTokenSource active;
        Bitmap bitmap;
        string folder = "";
        int generation;
        bool chinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
        string T(string en, string zh) => chinese ? zh : en;
        public int VisibleEntryCount => files.ItemCount;
        public string ArchiveTag => session?.Tag;

        public MainWindow()
        {
            Title = "GARbro";
            Width = 1180;
            Height = 760;
            MinWidth = 850;
            MinHeight = 540;
            var main = new Grid
            {
                RowDefinitions = new("Auto,Auto,*,Auto"),
                Margin = new Thickness(24),
                RowSpacing = 16
            };
            var heading = new Grid
            {
                ColumnDefinitions = new("*,Auto")
            };
            var brand = new StackPanel
            {
                Spacing = 4
            };
            brand.Children.Add(new TextBlock { Text = "GARbro", FontSize = 28, FontWeight = FontWeight.SemiBold });
            brand.Children.Add(subtitle);
            heading.Children.Add(brand);
            Grid.SetColumn(language, 1);
            heading.Children.Add(language);
            main.Children.Add(heading);
            var toolbar = new StackPanel
            {
                Spacing = 10
            };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10
            };
            foreach (var control in new Control[]
            {
                open,
                extract,
                extractSelected,
                cancel,
                search
            }

            )
                buttons.Children.Add(control);
            toolbar.Children.Add(buttons);
            var advancedContent = new StackPanel
            {
                Spacing = 8
            };
            var keyRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12
            };
            keyRow.Children.Add(encryptionLabel);
            keyRow.Children.Add(scheme);
            advancedContent.Children.Add(keyRow);
            advancedContent.Children.Add(options);
            advanced.Content = advancedContent;
            toolbar.Children.Add(advanced);
            Grid.SetRow(toolbar, 1);
            main.Children.Add(toolbar);
            var browser = new Grid
            {
                ColumnDefinitions = new("190,*,330"),
                ColumnSpacing = 16
            };
            var tree = new DockPanel
            {
                LastChildFill = true
            };
            DockPanel.SetDock(folderTitle, Dock.Top);
            tree.Children.Add(folderTitle);
            tree.Children.Add(folders);
            browser.Children.Add(tree);
            var list = new Grid
            {
                RowDefinitions = new("Auto,*"),
                RowSpacing = 8
            };
            var columns = new Grid
            {
                ColumnDefinitions = new("*,70,85"),
                Margin = new Thickness(8)
            };
            var nameHeader = new TextBlock
            {
                Text = T("NAME", "名称"),
                FontSize = 11,
                Opacity = .6
            };
            var typeHeader = new TextBlock
            {
                Text = T("TYPE", "类型"),
                FontSize = 11,
                Opacity = .6
            };
            var sizeHeader = new TextBlock
            {
                Text = T("SIZE", "大小"),
                FontSize = 11,
                Opacity = .6,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            columns.Children.Add(nameHeader);
            Grid.SetColumn(typeHeader, 1);
            columns.Children.Add(typeHeader);
            Grid.SetColumn(sizeHeader, 2);
            columns.Children.Add(sizeHeader);
            list.Children.Add(columns);
            files.ItemTemplate = new FuncDataTemplate<ArchiveEntryInfo>((entry, scope) =>
            {
                var row = new Grid
                {
                    ColumnDefinitions = new("*,70,85"),
                    Margin = new Thickness(8, 6)
                };
                var name = new TextBlock
                {
                    Text = entry.Name.Replace('\\', '/').Split('/').Last(),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var type = new TextBlock
                {
                    Text = entry.Type,
                    FontSize = 11,
                    Opacity = .65,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var size = new TextBlock
                {
                    Text = Size(entry.Size),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12
                };
                row.Children.Add(name);
                Grid.SetColumn(type, 1);
                row.Children.Add(type);
                Grid.SetColumn(size, 2);
                row.Children.Add(size);
                return row;
            });
            Grid.SetRow(files, 1);
            list.Children.Add(files);
            Grid.SetColumn(list, 1);
            browser.Children.Add(list);
            var preview = new Grid
            {
                RowDefinitions = new("Auto,Auto,*"),
                RowSpacing = 10
            };
            preview.Children.Add(previewTitle);
            Grid.SetRow(previewInfo, 1);
            preview.Children.Add(previewInfo);
            var content = new Grid();
            content.Children.Add(image);
            content.Children.Add(text);
            Grid.SetRow(content, 2);
            preview.Children.Add(content);
            Grid.SetColumn(preview, 2);
            browser.Children.Add(preview);
            Grid.SetRow(browser, 2);
            main.Children.Add(browser);
            var footer = new StackPanel
            {
                Spacing = 8
            };
            footer.Children.Add(progress);
            footer.Children.Add(status);
            Grid.SetRow(footer, 3);
            main.Children.Add(footer);
            Content = main;
            previewInfo.Text = T("Select a file to preview.", "选择文件以查看预览。");
            text.IsVisible = false;
            extract.IsEnabled = false;
            extractSelected.IsEnabled = false;
            cancel.IsEnabled = false;
            open.Click += async (s, e) => await OpenPicker();
            extract.Click += async (s, e) => await ExtractPicker(false);
            extractSelected.Click += async (s, e) => await ExtractPicker(true);
            cancel.Click += (s, e) => active?.Cancel();
            language.Click += (s, e) =>
            {
                chinese = !chinese;
                Translate();
            };
            search.TextChanged += (s, e) => RefreshFiles();
            folders.SelectionChanged += (s, e) =>
            {
                folder = (folders.SelectedItem as TreeViewItem)?.Tag as string ?? "";
                RefreshFiles();
            };
            files.SelectionChanged += async (s, e) => await PreviewSelection();
            KeyDown += async (s, e) =>
            {
                if (e.Key == Key.O && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    e.Handled = true;
                    await OpenPicker();
                }
            };
            Closed += (s, e) =>
            {
                active?.Cancel();
                bitmap?.Dispose();
                var closing = session;
                session = null;
                if (closing != null)
                    _ = Task.Run(closing.Dispose);
            };
            Translate();
            status.Text = T("Open an archive to browse its resources.", "打开归档，浏览其中的资源。");
        }

        void Translate()
        {
            subtitle.Text = T("Browse, preview and extract game resources", "浏览、预览与提取游戏资源");
            open.Content = T("Open archive", "打开归档");
            extract.Content = T("Extract all", "全部提取");
            extractSelected.Content = T("Extract selected", "提取所选");
            cancel.Content = T("Cancel", "取消");
            language.Content = chinese ? "EN" : "ZH";
            search.PlaceholderText = T("Search files…", "搜索文件…");
            folderTitle.Text = T("FOLDERS", "文件夹");
            previewTitle.Text = T("PREVIEW", "预览");
            advanced.Header = T("Encryption options", "解密选项");
            encryptionLabel.Text = T("Scheme (none = unencrypted)", "方案（none 表示未加密）");
        }

        static string Size(long bytes) => bytes < 1024 ? $"{bytes} B" : bytes < 1024 * 1024 ? $"{bytes / 1024.0:F1} KB" : $"{bytes / (1024.0 * 1024):F1} MB";
        void Busy(bool value)
        {
            open.IsEnabled = !value;
            extract.IsEnabled = !value && session != null;
            extractSelected.IsEnabled = !value && session != null;
            cancel.IsEnabled = value;
            search.IsEnabled = !value;
            folders.IsEnabled = !value;
            files.IsEnabled = !value;
        }

        async Task OpenPicker()
        {
            var selected = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = T("Open archive", "打开归档"), AllowMultiple = false });
            string path = selected.FirstOrDefault()?.TryGetLocalPath();
            if (path == null)
                return;
            try
            {
                await LoadArchiveAsync(path);
            }
            catch (Exception e)
            {
                status.Text = e.Message;
            }
        }

        public async Task LoadArchiveAsync(string path)
        {
            Busy(true);
            status.Text = T("Opening…", "正在打开…");
            var provider = new JsonOptionsProvider(string.IsNullOrWhiteSpace(options.Text) ? null : options.Text, string.IsNullOrWhiteSpace(scheme.Text) ? null : scheme.Text);
            try
            {
                var opened = await Task.Run(() => ArchiveService.Open(path, options: provider));
                var previous = session;
                session = opened;
                previous?.Dispose();
                generation++;
                folder = "";
                search.Text = "";
                Title = "GARbro — " + Path.GetFileName(path);
                BuildFolders();
                RefreshFiles();
                status.Text = $"{session.Tag} · {session.Entries.Count} " + T("files", "个文件");
            }
            finally
            {
                Busy(false);
            }
        }

        void BuildFolders()
        {
            var root = new TreeViewItem
            {
                Header = T("All files", "全部文件"),
                Tag = "",
                IsExpanded = true
            };
            var map = new Dictionary<string, TreeViewItem>
            {
                [""] = root
            };
            var children = new Dictionary<string, List<TreeViewItem>>
            {
                [""] = new()
            };
            foreach (var entry in session.Entries)
            {
                var parts = entry.Name.Replace('\\', '/').Split('/');
                string parent = "";
                foreach (var part in parts.Take(parts.Length - 1))
                {
                    string path = parent + part + "/";
                    if (!map.ContainsKey(path))
                    {
                        var node = new TreeViewItem
                        {
                            Header = part,
                            Tag = path
                        };
                        map[path] = node;
                        children[path] = new();
                        children[parent].Add(node);
                    }

                    parent = path;
                }
            }

            foreach (var pair in map)
                pair.Value.ItemsSource = children[pair.Key].OrderBy(x => x.Header?.ToString());
            folders.ItemsSource = new[]
            {
                root
            };
            folders.SelectedItem = root;
        }

        void RefreshFiles()
        {
            if (session == null)
                return;
            string query = search.Text ?? "";
            files.ItemsSource = session.Entries.Where(e => e.Name.Replace('\\', '/').StartsWith(folder, StringComparison.Ordinal) && e.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        async Task PreviewSelection()
        {
            if (session == null || files.SelectedItem is not ArchiveEntryInfo entry)
                return;
            var current = session;
            int ticket = ++generation;
            try
            {
                var preview = await Task.Run(() => current.Preview(entry.Name));
                if (ticket != generation || current != session)
                    return;
                bitmap?.Dispose();
                bitmap = null;
                image.Source = null;
                previewTitle.Text = Path.GetFileName(entry.Name);
                previewInfo.Text = preview.Kind == "image" ? preview.Text : Size(entry.Size);
                image.IsVisible = preview.Image != null;
                text.IsVisible = preview.Image == null;
                if (preview.Image != null)
                {
                    using var stream = new MemoryStream(preview.Image);
                    bitmap = new Bitmap(stream);
                    image.Source = bitmap;
                }
                else
                    text.Text = preview.Text;
            }
            catch (Exception e)
            {
                if (ticket == generation)
                    previewInfo.Text = e.Message;
            }
        }

        async Task ExtractPicker(bool selectedOnly)
        {
            var chosen = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = T("Choose output folder", "选择输出文件夹"), AllowMultiple = false });
            string path = chosen.FirstOrDefault()?.TryGetLocalPath();
            if (path == null)
                return;
            try
            {
                await ExtractToAsync(path, selectedOnly);
            }
            catch (OperationCanceledException)
            {
                status.Text = T("Extraction cancelled.", "提取已取消。");
            }
            catch (Exception e)
            {
                status.Text = e.Message;
            }
        }

        public async Task ExtractToAsync(string root, bool selectedOnly = false)
        {
            if (session == null)
                throw new InvalidOperationException("Open an archive first.");
            var selected = selectedOnly ? files.SelectedItems.Cast<ArchiveEntryInfo>().Select(e => e.Name).ToHashSet() : null;
            if (selectedOnly && selected.Count == 0)
            {
                status.Text = T("Select files to extract.", "请先选择要提取的文件。");
                return;
            }

            Busy(true);
            active = new();
            var updates = new Progress<ExtractionProgress>(p =>
            {
                progress.Value = p.Total == 0 ? 0 : (double)p.Completed / p.Total;
                status.Text = $"{p.Completed}/{p.Total} · {p.Entry}";
            });
            try
            {
                var result = await session.ExtractAsync(root, progress: updates, cancellation: active.Token, selection: selected);
                status.Text = T($"Extracted {result.Count} files.", $"已提取 {result.Count} 个文件。");
            }
            finally
            {
                active.Dispose();
                active = null;
                Busy(false);
            }
        }
    }
}
