using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using GARbro.Application;

namespace GARbro.Desktop;

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public string Language { get; set; } = "auto";
    public string Theme { get; set; } = "system";
    public string TextEncoding { get; set; } = "auto";
    public double TimeoutSeconds { get; set; } = 300;
    public long MemoryMiB { get; set; } = 1024;
    public string OutputDirectory { get; set; } = "";
    public bool MenuVisible { get; set; } = true;
    public bool ToolbarVisible { get; set; } = true;
    public bool StatusVisible { get; set; } = true;
    public bool FitImage { get; set; } = true;
    public bool WrapText { get; set; } = true;
    public string Sort { get; set; } = "name";
    public bool SortDescending { get; set; }
    public double Width { get; set; } = 1180;
    public double Height { get; set; } = 780;
    public double Volume { get; set; } = 1;
    public List<string> Recent { get; set; } = new();
    public Dictionary<string, JsonElement> FormatSettings { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, JsonElement> SessionFormatSettings { get; set; } = new();
    public string SchemesFile { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public WorkerLimits Limits => new(TimeoutSeconds, checked(MemoryMiB * (1L << 20)));
    [System.Text.Json.Serialization.JsonIgnore]
    public string EffectiveLanguage => Language == "auto" ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en-US" : Language;
    [System.Text.Json.Serialization.JsonIgnore]
    public DecoderContext Context => new(TextEncoding, EffectiveLanguage, FormatSettings.Concat(SessionFormatSettings).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Last().Value), SchemesFile);
    public void Validate()
    {
        if (OutputDirectory == null || Recent == null || FormatSettings == null) throw new ArgumentException("Invalid settings collection.");
        Limits.Validate();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _ = Context.ResolveEncoding();
        if (Theme is not ("system" or "light" or "dark")) throw new ArgumentException("Unknown theme.");
        if (Language is not ("auto" or "en-US" or "zh-CN")) throw new ArgumentException("Unknown language.");
        if (OutputDirectory.Length > 0 && !Path.IsPathFullyQualified(OutputDirectory)) throw new ArgumentException("Choose an absolute output directory.");
    }
}

public sealed class SettingsStore
{
    public string Path { get; }
    public string LoadError { get; private set; }
    public SettingsStore(string path = null) => Path = path ?? Environment.GetEnvironmentVariable("GARBRO_SETTINGS_PATH") ??
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GARbro", "settings.json");
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new();
            if (new FileInfo(Path).Length > 1 << 20) throw new InvalidDataException("Settings are too large.");
            var result = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path)) ?? new();
            if (result.Version != 1) throw new InvalidDataException("Unsupported settings version.");
            result.Validate();
            return result;
        }
        catch (Exception e) when (e is IOException or JsonException or ArgumentException or OverflowException)
        { LoadError = e.Message; return new(); }
    }
    public void Save(AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)));
        string temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            if (LoadError != null && File.Exists(Path))
            {
                string backup = Path + ".invalid-" + Guid.NewGuid().ToString("N");
                File.Copy(Path, backup);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(backup, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            File.Move(temporary, Path, true);
            LoadError = null;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed class DesktopText : INotifyPropertyChanged
{
    readonly Dictionary<string, (string En, string Zh)> strings = new(StringComparer.Ordinal);
    public string Language { get; private set; }
    public event PropertyChangedEventHandler PropertyChanged;
    public DesktopText(string language)
    {
        Language = language;
        foreach (string line in Data.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = line.Trim().Split('|');
            if (pair.Length == 3) strings.Add(pair[0], (pair[1], pair[2]));
        }
    }
    public string this[string key] => strings.TryGetValue(key, out var value) ? Language == "zh-CN" ? value.Zh : value.En : key;
    public void Change(string language) { Language = language; PropertyChanged?.Invoke(this, new("Item[]")); }
    public string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, this[key], args);
    public IEnumerable<string> Keys => strings.Keys;
    const string Data = """
File|File|文件
View|View|视图
Help|Help|帮助
Open|Open archive…|打开归档…
OpenFolder|Open folder…|打开目录…
OpenSelected|Open selected item|打开所选项目
Create|Create archive…|创建归档…
Convert|Convert multimedia…|批量媒体转换…
Recent|Recent files|最近打开
Exit|Exit|退出
ExtractAll|Extract all…|全部提取…
ExtractSelected|Extract selected…|提取所选…
Recursive|Recursive extraction…|递归解包…
Cancel|Cancel|取消
Settings|Settings|设置
FitWindow|Fit window to image|窗口适应图片
FitImage|Fit image to preview|图片适应预览区
Refresh|Refresh|刷新
Toolbar|Show toolbar|显示工具栏
Menubar|Show menu bar|显示菜单栏
Statusbar|Show status bar|显示状态栏
Sort|Sort by|排序方式
Name|Name|名称
Type|Type|类型
Size|Size|大小
Unsorted|Original order|原始顺序
Back|Back|后退
Forward|Forward|前进
Parent|Up one level|上一级
Search|Search files…|搜索文件…
Advanced|Advanced options|高级选项
Scheme|Scheme (none = unencrypted)|方案（none 表示未加密）
Folders|Folders|文件夹
Preview|Preview|预览
AllFiles|All files|全部文件
Welcome|Open or drop files and folders to browse resources.|打开或拖入文件及目录，浏览其中的资源。
Opening|Opening…|正在打开…
Working|Working…|正在处理…
Cancelled|Operation cancelled.|操作已取消。
Error|Operation failed|操作失败
Details|Details|详情
SelectPreview|Select a file to preview.|选择文件以查看预览。
SelectFiles|Select files first.|请先选择文件。
Output|Output directory|输出目录
Browse|Browse…|浏览…
Overwrite|Existing files|已有文件
Fail|Report a conflict|报告冲突
Skip|Skip existing|跳过已有文件
Replace|Replace existing|替换已有文件
Images|Images|图像
Audio|Audio|音频
Scripts|Scripts / text|脚本 / 文本
Other|Other resources|其他资源
ImageFormat|Convert images to (empty = original)|图片转换格式（空表示保留原始格式）
AudioConvert|Convert audio to common formats|音频转换为通用格式
MaxDepth|Maximum recursive depth|最大递归层数
Start|Start|开始
Completed|Completed: {0} files, {1} errors.|已完成：{0} 个文件，{1} 项错误。
General|General|常规
Formats|Format settings|格式设置
Language|Language|语言
Auto|Follow system / automatic|跟随系统 / 自动
English|English|英语
Chinese|简体中文|简体中文
Theme|Theme|主题
System|Follow system|跟随系统
Light|Light|浅色
Dark|Dark|深色
Encoding|Text / filename encoding|文本 / 文件名编码
Time|Time limit (seconds)|时间上限（秒）
Memory|Memory limit (MiB)|内存上限（MiB）
DefaultOutput|Default output directory|默认输出目录
Wrap|Wrap text|文本自动换行
Save|Save|保存
Close|Close|关闭
Defaults|Inherit global defaults|继承全局默认值
Play|Play|播放
Pause|Pause|暂停
Stop|Stop|停止
Volume|Volume|音量
Preparing|Preparing audio…|正在准备音频…
NextPage|Next page|下一页
PreviousPage|Previous page|上一页
Copy|Copy|复制
SelectAll|Select all|全选
Mask|Select by wildcard…|按通配符选择…
CopyNames|Copy filenames|复制文件名
AssignType|Assign resource type|指定资源类型
Automatic|Detect automatically|自动识别
Explore|Show in file manager|在文件管理器中显示
Trash|Move to recycle bin…|移入回收站…
TrashConfirm|Move the selected physical files to the recycle bin?|将所选磁盘文件移入回收站？
Confirm|Confirm|确认
Feedback|Report a problem|问题反馈
Manual|User guide|使用说明
Troubleshoot|Troubleshooting|故障排查
Updates|Check for updates|检查更新
License|Software licenses|软件协议
ExportLogs|Export logs…|日志导出…
About|About|关于
Archives|Archives|归档
Registered|Registered providers|已注册提供者
Verified|Verification status|验证状态
ReadOnly|Read only|只读
ReadWrite|Read / write|可读写
RegisteredOnly|Registered; game variants may be unverified.|已注册；具体游戏变体可能尚未验证。
AdjustOffset|Apply image offsets to the converted canvas|转换时将图像偏移应用到画布
FixtureVerified|Verified on named test fixtures|已在列出的测试样本上验证
CreationVerified|Synthetic archive creation and read-back verified|已验证合成归档的创建与回读
NotVerified|Registered; decoding has not been verified|已注册；解码尚未验证
Verification|Verification details and limits|验证详情与限制
Options|Format parameters (JSON)|格式参数（JSON）
Format|Format|格式
Filename|Output filename|输出文件名
MissingOptions|Password or game parameters required|需要密码或游戏参数
Subtitle|Browse, preview and extract game resources|浏览、预览与提取游戏资源
NoAudio|Audio output is unavailable.|音频输出设备不可用。
UpdateNone|This version is up to date.|当前已是最新版本。
UpdateAvailable|A newer version is available: {0}|发现新版本：{0}
DatabaseUpdate|Update format database|更新格式数据库
DatabaseUpdated|Format database updated. New tasks will use it.|格式数据库已更新，新任务将使用新版本。
SelectOutput|Choose output directory|选择输出目录
Exported|Exported: {0}|已导出：{0}
NoFormats|No additional settings for this format.|该格式没有额外设置。
Binary|Hexadecimal|十六进制
Text|Text / script|文本 / 脚本
Metadata|Resource information|资源信息
""";
}
