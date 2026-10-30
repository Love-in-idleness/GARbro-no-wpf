using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GARbro.Application;
using GARbro.Worker;
using GARbro.Worker.Probe;
using Xunit;

[Collection("Decoder state")]
public sealed class WorkerTests
{
    static string Fixture(string name)=>Path.Combine(AppContext.BaseDirectory,"Fixtures",name);

    [Fact]
    public async Task WorkerListsPreviewsAndExtractsWithoutKeepingAnArchiveInTheCaller()
    {
        string root=Path.Combine(Path.GetTempPath(),"garbro-worker-test-"+Guid.NewGuid());
        try
        {
            using var archive=await WorkerArchiveSession.OpenAsync(WorkerMarker.AssemblyPath,Fixture("standard.xfl"));
            Assert.Equal("XFL",archive.Tag);Assert.Equal(3,archive.Entries.Count);
            var preview=await archive.PreviewAsync("hello.txt");
            Assert.Contains("Hello, GARbro!",preview.Text);
            var files=await archive.ExtractAsync(root);
            Assert.Equal(3,files.Count);
            Assert.Equal("Hello, GARbro!\n",File.ReadAllText(Path.Combine(root,"hello.txt")));
        }
        finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Theory]
    [InlineData("sleep",1.5,512)]
    [InlineData("memory",15,128)]
    public async Task LimitsKillUnresponsiveWorkersAndRemoveOnlyTheirTemporaryOutputs(string mode,double timeout,int memoryMiB)
    {
        string root=Path.Combine(Path.GetTempPath(),"garbro-worker-limit-"+Guid.NewGuid());
        Directory.CreateDirectory(root);
        string keep=Path.Combine(root,"keep.txt");File.WriteAllText(keep,"existing");
        string temporary=null;
        try
        {
            await Assert.ThrowsAsync<WorkerLimitException>(()=>WorkerSupervisor.RunManagedAsync(
                ProbeMarker.AssemblyPath,new[]{mode,root},new WorkerLimits(timeout,(long)memoryMiB<<20),
                output:line=>temporary=line));
            Assert.NotNull(temporary);
            Assert.False(File.Exists(temporary));
            Assert.Equal("existing",File.ReadAllText(keep));
            Assert.Single(Directory.EnumerateFiles(root));
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public async Task CancellationTerminatesTheWorkerAndCleansItsPendingOutput()
    {
        string root=Path.Combine(Path.GetTempPath(),"garbro-worker-cancel-"+Guid.NewGuid());
        Directory.CreateDirectory(root);
        using var cancel=new CancellationTokenSource();
        string temporary=null;
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>WorkerSupervisor.RunManagedAsync(
                ProbeMarker.AssemblyPath,new[]{"sleep",root},cancellation:cancel.Token,output:line=>
                {temporary=line;cancel.Cancel();}));
            Assert.NotNull(temporary);Assert.False(File.Exists(temporary));
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public async Task AChangedArchiveCannotBeExtractedUsingStaleWorkerMetadata()
    {
        string root=Path.Combine(Path.GetTempPath(),"garbro-worker-changed-"+Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string input=Path.Combine(root,"input.xfl");File.Copy(Fixture("standard.xfl"),input);
            using var archive=await WorkerArchiveSession.OpenAsync(WorkerMarker.AssemblyPath,input);
            using(var append=new FileStream(input,FileMode.Append))append.WriteByte(0);
            var error=await Assert.ThrowsAsync<WorkerOperationException>(()=>archive.ExtractAsync(Path.Combine(root,"output")));
            Assert.Contains("changed",error.Message);
            Assert.False(Directory.Exists(Path.Combine(root,"output")));
        }
        finally {Directory.Delete(root,true);}
    }
}
