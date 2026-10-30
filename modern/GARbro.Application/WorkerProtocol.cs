using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GARbro.Application
{
    public sealed record ArchiveFingerprint(long Length,long WrittenUtcTicks)
    {
        public static ArchiveFingerprint Read(string path)
        {
            var file=new FileInfo(path);
            if(!file.Exists) throw new FileNotFoundException("Archive does not exist.",path);
            return new(file.Length,file.LastWriteTimeUtc.Ticks);
        }
    }
    public sealed record WorkerRequest(string Operation,string Path,string OptionsJson=null,string Scheme=null,
        string Output=null,string Entry=null,OverwritePolicy Overwrite=OverwritePolicy.Fail,
        ExtractionLimits Extraction=null,string[] Selection=null,ArchiveFingerprint Fingerprint=null);
    public sealed record WorkerArchiveData(string Path,string Tag,ArchiveEntryInfo[] Entries,ArchiveFingerprint Fingerprint);
    public sealed record WorkerError(string Type,string Message);
    public sealed record WorkerReply(string Kind,JsonElement? Data=null,ExtractionProgress Progress=null,WorkerError Error=null);
    public sealed class WorkerOperationException(string type,string message) : IOException(message)
    {
        public string RemoteType {get;}=type;
    }
    public static class WorkerProtocol
    {
        public static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web) {MaxDepth=64};
        public static string Serialize<T>(T value)=>JsonSerializer.Serialize(value,Json);
        public static T Deserialize<T>(string value)=>JsonSerializer.Deserialize<T>(value,Json);
        public static WorkerReply Result<T>(T value)=>new("result",JsonSerializer.SerializeToElement(value,Json));
    }
}
