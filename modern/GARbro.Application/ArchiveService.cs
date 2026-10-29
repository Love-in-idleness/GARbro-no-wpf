using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameRes;
using GameRes.Formats.KiriKiri;
using GameRes.Formats.NitroPlus;
using GameRes.Formats.YuRis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GARbro.Application
{
    public enum OverwritePolicy { Fail,Skip,Replace }
    public sealed record ExtractionLimits(long MaxEntryBytes=2L<<30,long MaxTotalBytes=16L<<30,double MaxExpansionRatio=10000,int MaxEntries=262144);
    public sealed record ExtractionProgress(int Completed,int Total,string Entry,long Bytes);
    public sealed record ExtractedFile(string Path,long Size,string Status);
    public sealed record FormatInfo(string Tag,string Type,string Description,bool CanWrite,string Class);
    public sealed record ArchiveEntryInfo(string Name,long Offset,uint Size,string Type);
    public sealed class MissingOptionsException : Exception { public MissingOptionsException(string text):base(text) { } }
    public interface IOptionsProvider { ResourceOptions GetOptions(IResource format,string notice); }

    public sealed class JsonOptionsProvider : IOptionsProvider
    {
        readonly JObject options; readonly string scheme;
        public JsonOptionsProvider(string json=null,string scheme=null) { options=json==null?null:JObject.Parse(json);this.scheme=scheme; }
        public ResourceOptions GetOptions(IResource format,string notice)
        {
            var result=format.GetDefaultOptions();
            if(result==null) throw new MissingOptionsException(format.Tag+": "+notice);
            if(options!=null)
            {
                var settings=new JsonSerializerSettings { TypeNameHandling=TypeNameHandling.None,MissingMemberHandling=MissingMemberHandling.Error };
                JsonConvert.PopulateObject(options.ToString(),result,settings);
            }
            if(scheme!=null)
            {
                if(result is Xp3Options xp3) xp3.Scheme=scheme=="none"?Xp3Opener.GetScheme(null):
                    Xp3Opener.KnownSchemes.TryGetValue(scheme,out var cipher)?cipher:throw new MissingOptionsException("Unknown XP3 scheme: "+scheme);
                else if(result is NpaOptions npa) npa.Scheme=NpaOpener.KnownSchemes.TryGetValue(scheme,out var cipher)?cipher:throw new MissingOptionsException("Unknown NPA scheme: "+scheme);
                else if(result is YpfOptions ypf) ypf.Scheme=scheme;
                else throw new MissingOptionsException("Named schemes are unavailable for "+format.Tag);
            }
            if(options==null&&scheme==null&&result is not YpfOptions) throw new MissingOptionsException(format.Tag+": "+notice+" Supply --options or --scheme.");
            return result;
        }
    }

    public static class ArchiveService
    {
        internal static readonly object Gate=new();
        public static IReadOnlyList<FormatInfo> Formats()
        {
            // Keep the provider in the dependency graph even when discovered through MEF.
            _=typeof(GameRes.Formats.Liar.XflOpener).Assembly;
            lock(Gate) return FormatCatalog.Instance.Formats.Select(f=>new FormatInfo(f.Tag,f.Type,f.Description,f.CanWrite,f.GetType().FullName)).OrderBy(f=>f.Type).ThenBy(f=>f.Tag).ToArray();
        }
        public static ArchiveSession Open(string path,string formatTag=null,IOptionsProvider options=null)
        {
            Formats();path=Path.GetFullPath(path);
            lock(Gate)
            {
                using var scope=new DecoderScope(Path.GetDirectoryName(path),options);
                ArcFile archive;
                if(formatTag==null)
                {
                    FormatCatalog.Instance.LastError=null;
                    archive=ArcFile.TryOpen(path);
                }
                else
                {
                    var format=FormatCatalog.Instance.ArcFormats.SingleOrDefault(f=>f.Tag.Equals(formatTag,StringComparison.OrdinalIgnoreCase))
                        ??throw new ArgumentException("Unknown archive format: "+formatTag);
                    var view=new ArcView(path);
                    try { archive=format.TryOpen(view);if(archive==null)view.Dispose(); }
                    catch {view.Dispose();throw;}
                }
                if(archive==null)
                {
                    if(scope.MissingOptions!=null) throw scope.MissingOptions;
                    throw new UnknownFormatException(FormatCatalog.Instance.LastError);
                }
                return new ArchiveSession(archive,path,options);
            }
        }
        public static ImageData ReadImage(string path,string formatTag=null)
        {
            Formats();path=Path.GetFullPath(path);
            lock(Gate)
            {
                using var scope=new DecoderScope(Path.GetDirectoryName(path),null);
                using var input=BinaryStream.FromFile(path);
                var format=formatTag==null?null:ImageFormat.FindByTag(formatTag);
                if(formatTag!=null&&format==null) throw new ArgumentException("Unknown image format: "+formatTag);
                if(format==null) return ImageFormat.Read(input)??throw new UnknownFormatException();
                var info=format.ReadMetaData(input)??throw new InvalidFormatException();input.Position=0;return format.Read(input,info);
            }
        }
        public static void WriteImage(ImageData image,string output,string formatTag="PNG",OverwritePolicy overwrite=OverwritePolicy.Fail)
        {
            Formats();
            lock(Gate)
            {
                var encoder=ImageFormat.FindByTag(formatTag)??throw new ArgumentException("Unknown image format: "+formatTag);
                output=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(output));
                if(File.Exists(output)&&overwrite==OverwritePolicy.Skip)return;
                var temporary=output+"."+Guid.NewGuid().ToString("N")+".tmp";
                try { using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write)) encoder.Write(stream,image);File.Move(temporary,output,overwrite==OverwritePolicy.Replace); }
                finally {if(File.Exists(temporary))File.Delete(temporary);}
            }
        }
        internal sealed class DecoderScope : IDisposable
        {
            readonly string previous;readonly IOptionsProvider provider;
            public MissingOptionsException MissingOptions { get;private set; }
            public DecoderScope(string directory,IOptionsProvider provider)
            {
                previous=Directory.GetCurrentDirectory();this.provider=provider??new JsonOptionsProvider();
                Directory.SetCurrentDirectory(directory);FormatCatalog.Instance.ParametersRequest+=Request;
            }
            void Request(object source,ParametersRequestEventArgs args)
            {
                try {args.Options=provider.GetOptions((IResource)source,args.Notice);args.InputResult=true;}
                catch(MissingOptionsException e) {MissingOptions=e;throw;}
            }
            public void Dispose() {FormatCatalog.Instance.ParametersRequest-=Request;Directory.SetCurrentDirectory(previous);}
        }
    }

    public sealed class ArchiveSession : IDisposable
    {
        readonly ArcFile archive;readonly IOptionsProvider options;bool disposed;
        public string Path { get; }
        public string Tag=>archive.Tag;
        public IReadOnlyList<ArchiveEntryInfo> Entries { get; }
        internal ArchiveSession(ArcFile archive,string path,IOptionsProvider options)
        {
            this.archive=archive;Path=path;this.options=options;
            Entries=archive.Dir.Select(e=>new ArchiveEntryInfo(e.Name,e.Offset,e.Size,e.Type)).ToArray();
        }
        public Task<IReadOnlyList<ExtractedFile>> ExtractAsync(string root,OverwritePolicy overwrite=OverwritePolicy.Fail,ExtractionLimits limits=null,
            IProgress<ExtractionProgress> progress=null,CancellationToken cancellation=default,ISet<string> selection=null)
            =>Task.Run(()=>Extract(root,overwrite,limits??new(),progress,cancellation,selection),cancellation);
        IReadOnlyList<ExtractedFile> Extract(string root,OverwritePolicy overwrite,ExtractionLimits limits,IProgress<ExtractionProgress> progress,CancellationToken cancellation,ISet<string> selection)
        {
            lock(ArchiveService.Gate)
            {
                ObjectDisposedException.ThrowIf(disposed,this);
                if(limits.MaxEntryBytes<0||limits.MaxTotalBytes<0||limits.MaxEntries<1||limits.MaxExpansionRatio<=0)throw new ArgumentOutOfRangeException(nameof(limits));
                using var scope=new ArchiveService.DecoderScope(System.IO.Path.GetDirectoryName(Path),options);
                root=System.IO.Path.GetFullPath(root);
                var entries=archive.Dir.Where(e=>selection==null||selection.Contains(e.Name)).ToArray();
                if(entries.Length>limits.MaxEntries)throw new InvalidDataException("Archive has too many entries.");
                var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var plan=entries.Select(e=>(Entry:e,Target:ExtractionPath.Resolve(root,e.Name))).ToArray();
                foreach(var item in plan)if(!seen.Add(item.Target))throw new InvalidDataException("Colliding archive paths: "+item.Entry.Name);
                Directory.CreateDirectory(root);var result=new List<ExtractedFile>();long total=0;int done=0;
                foreach(var item in plan)
                {
                    cancellation.ThrowIfCancellationRequested();ExtractionPath.RejectLinks(root,item.Target);
                    if(File.Exists(item.Target))
                    {
                        if(overwrite==OverwritePolicy.Fail)throw new IOException("Output exists: "+item.Target);
                        if(overwrite==OverwritePolicy.Skip){result.Add(new(item.Entry.Name,0,"skipped"));progress?.Report(new(++done,plan.Length,item.Entry.Name,total));continue;}
                    }
                    if(item.Entry is PackedEntry packed&&packed.UnpackedSize>limits.MaxEntryBytes)throw new InvalidDataException("Entry exceeds the output limit: "+item.Entry.Name);
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(item.Target));ExtractionPath.RejectLinks(root,item.Target);
                    string temporary=item.Target+"."+Guid.NewGuid().ToString("N")+".tmp";long length=0;
                    try
                    {
                        using(var input=archive.OpenEntry(item.Entry))
                        using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                        {
                            byte[] buffer=new byte[81920];int read;
                            while((read=input.Read(buffer,0,buffer.Length))!=0)
                            {
                                cancellation.ThrowIfCancellationRequested();length=checked(length+read);total=checked(total+read);
                                if(length>limits.MaxEntryBytes||total>limits.MaxTotalBytes||length>Math.Max(1L,item.Entry.Size)*limits.MaxExpansionRatio)
                                    throw new InvalidDataException("Extraction limit exceeded: "+item.Entry.Name);
                                output.Write(buffer,0,read);
                            }
                        }
                        ExtractionPath.RejectLinks(root,item.Target);File.Move(temporary,item.Target,overwrite==OverwritePolicy.Replace);
                        result.Add(new(item.Entry.Name,length,"extracted"));progress?.Report(new(++done,plan.Length,item.Entry.Name,total));
                    }
                    finally {if(File.Exists(temporary))File.Delete(temporary);}
                }
                return result;
            }
        }
        public byte[] ReadEntry(string name,long limit=16<<20)
        {
            lock(ArchiveService.Gate)
            {
                ObjectDisposedException.ThrowIf(disposed,this);
                using var scope=new ArchiveService.DecoderScope(System.IO.Path.GetDirectoryName(Path),options);
                var entry=archive.Dir.Single(e=>e.Name==name);using var input=archive.OpenEntry(entry);using var result=new MemoryStream();
                var buffer=new byte[81920];int count;while((count=input.Read(buffer,0,buffer.Length))>0)
                {if(result.Length+count>limit)throw new InvalidDataException("Preview exceeds its size limit.");result.Write(buffer,0,count);}return result.ToArray();
            }
        }
        public void Dispose() {lock(ArchiveService.Gate){if(disposed)return;archive.Dispose();disposed=true;}}
    }
}
