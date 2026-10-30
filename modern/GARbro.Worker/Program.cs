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
        static void Reply(WorkerReply reply)=>Console.WriteLine(WorkerProtocol.Serialize(reply));

        static async Task<int> Main()
        {
            Console.InputEncoding=Encoding.UTF8;Console.OutputEncoding=new UTF8Encoding(false);
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
                Reply(new WorkerReply("error",Error:new WorkerError(error.GetType().Name,error.Message)));
                return 1;
            }
        }
    }
}
