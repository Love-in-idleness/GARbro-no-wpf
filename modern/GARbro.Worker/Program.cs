using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GARbro.Application;

namespace GARbro.Worker
{
    public static class WorkerMarker
    {
        public static string AssemblyPath=>typeof(WorkerMarker).Assembly.Location;
    }
    static class Program
    {
        sealed class ImmediateProgress<T>(Action<T> callback) : IProgress<T>
        { public void Report(T value)=>callback(value); }
        static TextWriter replies;
        static void Reply(WorkerReply reply)=>replies.WriteLine(WorkerProtocol.Serialize(reply));

        static async Task<int> Main()
        {
            Console.InputEncoding=Encoding.UTF8;Console.OutputEncoding=new UTF8Encoding(false);
            replies=Console.Out;
            // Provider diagnostics (including generated scanners) must not be
            // interpreted as protocol messages by the supervising application.
            Console.SetOut(Console.Error);
            try
            {
                WorkerTemporaryFiles.InitializeFromEnvironment();
                string message=null;
                await WorkerSupervisor.ReadLinesAsync(Console.In,line=>
                {
                    if(message!=null) throw new InvalidDataException("Expected one worker request.");
                    message=line;
                },32<<20);
                var request=WorkerProtocol.Deserialize<WorkerRequest>(message)
                    ?? throw new InvalidDataException("Worker request is missing.");
                if(request.Operation.StartsWith("resource-",StringComparison.Ordinal))
                {
                    (request.Context ?? new DecoderContext()).Apply();
                    var provider=new ScopedOptionsProvider(request);
                    var updates=new ImmediateProgress<ExtractionProgress>(p=>Reply(new WorkerReply("progress",Progress:p)));
                    object value;
                    switch(request.Operation)
                    {
                        case "resource-catalog": value=ResourceService.Catalog();break;
                        case "resource-validate-schemes":
                            using(var file=File.OpenRead(request.Path))
                            using(var gzip=new System.IO.Compression.GZipStream(file,System.IO.Compression.CompressionMode.Decompress))
                            using(var bounded=new MemoryStream())
                            {
                                byte[] buffer=new byte[81920];int n;
                                while((n=gzip.Read(buffer))>0)
                                {if(bounded.Length>(64L<<20)-n)throw new InvalidDataException("Format database exceeds the size limit.");bounded.Write(buffer,0,n);}
                                bounded.Position=0;
                                var report=GameRes.FormatCatalog.Instance.DeserializePortableSchemeJson(bounded);
                                if(report.Failed.Count>0)throw new InvalidDataException("Format database contains incompatible schemes.");
                            }
                            value=true;break;
                        case "resource-list":value=ResourceService.List(request.Location,provider);break;
                        case "resource-preview":value=ResourceService.Preview(request.Location,request.Entry,request.Context??new(),request.ForcedType,request.Offset,provider);break;
                        case "resource-audio":value=ResourceService.PrepareAudio(request.Location,request.Entry,request.Output,provider);break;
                        case "resource-extract":value=ResourceService.Extract(request.Location,request.Output,request.ResourceOptions,request.Extraction,
                            request.Overwrite,request.Selection,provider,updates,request.CompletedPaths);break;
                        case "resource-create":
                            ResourceService.CreateArchive(request.Path,request.Inputs,request.Output,request.FormatId,request.OptionsJson,request.Scheme,request.Overwrite,updates);
                            value=request.Output;break;
                        case "resource-convert":
                            using(var resource=new ResourceSession(request.Location,provider))
                                value=ResourceService.ConvertFile(resource,request.Entry,request.Output,request.FormatId,request.Overwrite);
                            break;
                        default:throw new ArgumentException("Unknown resource operation.");
                    }
                    Reply(WorkerProtocol.Result(value));return 0;
                }
                var fingerprint=ArchiveFingerprint.Read(request.Path);
                if(request.Fingerprint!=null && request.Fingerprint!=fingerprint)
                    throw new IOException("Archive changed; reopen it before continuing.");
                var options=new JsonOptionsProvider(request.OptionsJson,request.Scheme);
                WorkerReply result;
                using(var archive=ArchiveService.Open(request.Path,options:options))
                {
                  switch(request.Operation)
                  {
                    case "open":
                        result=WorkerProtocol.Result(new WorkerArchiveData(archive.Path,archive.Tag,archive.Entries.ToArray(),fingerprint));
                        break;
                    case "preview":result=WorkerProtocol.Result(archive.Preview(request.Entry));break;
                    case "extract":
                        var progress=new ImmediateProgress<ExtractionProgress>(p=>Reply(new WorkerReply("progress",Progress:p)));
                        var files=await archive.ExtractAsync(request.Output,request.Overwrite,request.Extraction,progress,
                            selection:request.Selection==null?null:new HashSet<string>(request.Selection));
                        result=WorkerProtocol.Result(files);
                        break;
                    default:throw new ArgumentException("Unknown worker operation.");
                  }
                }
                Reply(result);
                return 0;
            }
            catch(Exception error)
            {
                var missing=error as MissingOptionsException;
                Reply(new WorkerReply("error",Error:new WorkerError(error.GetType().Name,error.Message,missing?.FormatId,missing?.OptionsJson,missing?.CompletedPaths)));
                return 1;
            }
        }
    }
}
