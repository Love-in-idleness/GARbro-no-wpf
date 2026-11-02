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
                return await WorkerExecutor.ExecuteAsync(request,Reply);
            }
            catch(Exception error)
            {
                var missing=error as MissingOptionsException;
                Reply(new WorkerReply("error",Error:new WorkerError(error.GetType().Name,error.Message,missing?.FormatId,missing?.OptionsJson,missing?.CompletedPaths,missing?.ContextKey)));
                return 1;
            }
        }
    }
}
