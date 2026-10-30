using System;
using System.IO;
using System.Threading.Tasks;
using GARbro.Application;

namespace GARbro.Worker.Probe
{
    public static class ProbeMarker {public static string AssemblyPath=>typeof(ProbeMarker).Assembly.Location;}
    static class Program
    {
        static async Task Main(string[] args)
        {
            WorkerTemporaryFiles.InitializeFromEnvironment();
            string temporary=WorkerTemporaryFiles.Create(args[1]);
            File.WriteAllText(temporary,"incomplete output");
            Console.WriteLine(temporary);
            byte[] memory=null;
            if(args[0]=="memory")
            {
                memory=new byte[384<<20];
                for(int i=0;i<memory.Length;i+=4096)memory[i]=1;
            }
            await Task.Delay(30000);
            GC.KeepAlive(memory);
        }
    }
}
