using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GARbro.Application;
using GARbro.Worker;
using GameRes;
using Xunit;

[Collection("Decoder state")]
public sealed class ResourceBrowserTests
{
    static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory,"Fixtures",name);
    static Task<T> Request<T>(WorkerRequest request) => WorkerArchiveSession.RequestAsync<T>(WorkerMarker.AssemblyPath,
        request,new WorkerLimits(),null,default);

    [Fact]
    public async Task BrowserEntersDirectoriesAndArchivesWithoutExtractingThem()
    {
        var listing=await Request<BrowserListing>(new("resource-list",null,Location:new ResourceLocation(Fixture("standard.xfl"))));
        Assert.Equal("XFL",listing.Tag);
        Assert.Contains(listing.Entries,e=>e.IsDirectory && e.Name=="sub");
        var sub=await Request<BrowserListing>(new("resource-list",null,Location:listing.Location.EnterDirectory("sub")));
        Assert.Single(sub.Entries);
        Assert.Equal("日本語.bin",sub.Entries[0].DisplayName);
        var text=await Request<ResourcePreview>(new("resource-preview",null,Location:listing.Location,Entry:"hello.txt"));
        Assert.Contains("Hello, GARbro!",text.Text);
    }

    [Fact]
    public async Task NestedVfsBrowsingAndRecursiveExtractionKeepTheChildArchive()
    {
        string root=Path.Combine(Path.GetTempPath(),"garbro-nested-"+Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            // XFL automatically flattens children named .xfl; use another
            // container extension to exercise actual nested VFS navigation.
            File.Copy(Fixture("standard.xfl"),Path.Combine(root,"child.unknown"));
            var catalog=await Request<ResourceFormatInfo[]>(new("resource-catalog",null));
            var xfl=catalog.Single(f=>f.Tag=="XFL" && f.Type=="archive");
            string outer=Path.Combine(root,"outer.xfl");
            await Request<string>(new("resource-create",root,Output:outer,FormatId:xfl.Id,Inputs:new[]{"child.unknown"}));
            var nested=new ResourceLocation(outer).EnterArchive("child.unknown");
            var listing=await Request<BrowserListing>(new("resource-list",null,Location:nested));
            Assert.Equal("XFL",listing.Tag);
            string output=Path.Combine(root,"output");
            var report=await Request<ResourceJobReport>(new("resource-extract",null,Location:new(outer),Output:output,
                ResourceOptions:new(Recursive:true)));
            Assert.Empty(report.Errors);
            Assert.True(File.Exists(Path.Combine(output,"child.unknown")));
            Assert.Equal("Hello, GARbro!\n",File.ReadAllText(Path.Combine(output,"child.unknown.unpacked","hello.txt")));
            string limited=Path.Combine(root,"limited");
            var constrained=await Request<ResourceJobReport>(new("resource-extract",null,Location:new(outer),Output:limited,
                ResourceOptions:new(Recursive:true),Extraction:new(MaxEntries:1)));
            Assert.NotEmpty(constrained.Errors);
            Assert.True(File.Exists(Path.Combine(limited,"child.unknown")));
            Assert.False(File.Exists(Path.Combine(limited,"child.unknown.unpacked","hello.txt")));
            Assert.DoesNotContain(Directory.EnumerateFiles(limited,"*",SearchOption.AllDirectories),p=>p.EndsWith(".tmp"));
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public async Task NestedParameterRetryKeepsCommittedOutputsAndCannotResetTheBudget()
    {
        string root=Path.Combine(Path.GetTempPath(),"garbro-retry-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            File.Copy(Fixture("standard.xp3"),Path.Combine(root,"child.xp3"));
            var catalog=await Request<ResourceFormatInfo[]>(new("resource-catalog",null));
            var writer=catalog.Single(f=>f.Tag=="XFL"&&f.Type=="archive");
            var xp3=catalog.Single(f=>f.Tag=="XP3"&&f.Type=="archive");
            string outer=Path.Combine(root,"outer.xfl"),output=Path.Combine(root,"out");
            await Request<string>(new("resource-create",root,Output:outer,FormatId:writer.Id,Inputs:new[]{"child.xp3"}));
            var missing=await Assert.ThrowsAsync<WorkerOperationException>(()=>Request<ResourceJobReport>(new("resource-extract",null,
                Location:new(outer),Output:output,ResourceOptions:new(Recursive:true))));
            Assert.Equal(xp3.Id,missing.FormatId);Assert.Single(missing.CompletedPaths);
            string retained=missing.CompletedPaths[0];var written=File.GetLastWriteTimeUtc(retained);
            var context=new DecoderContext(OptionsByFormat:new(){{xp3.Id,"{\"$scheme\":\"none\"}"}});
            var resumed=await Request<ResourceJobReport>(new("resource-extract",null,Location:new(outer),Output:output,
                ResourceOptions:new(Recursive:true),Context:context,CompletedPaths:missing.CompletedPaths));
            Assert.Empty(resumed.Errors);Assert.Equal(written,File.GetLastWriteTimeUtc(retained));
            Assert.Equal("Hello, GARbro!\n",File.ReadAllText(Path.Combine(output,"child.xp3.unpacked","hello.txt")));
            await Assert.ThrowsAsync<WorkerOperationException>(()=>Request<ResourceJobReport>(new("resource-extract",null,
                Location:new(outer),Output:output,Context:context,ResourceOptions:new(Recursive:true),CompletedPaths:missing.CompletedPaths,
                Extraction:new(MaxTotalBytes:1))));
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public async Task PreviewDecodesScriptsAndPagesBinaryWithoutReturningTheWholeResource()
    {
        string root=Path.Combine(Path.GetTempPath(),"garbro-script-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            byte[] script=System.Text.Encoding.UTF8.GetBytes("*start\nHello decoded script\n");
            File.WriteAllBytes(Path.Combine(root,"nscript.dat"),script.Select(b=>(byte)(b^0x84)).ToArray());
            var text=await Request<ResourcePreview>(new("resource-preview",null,Location:new(root),Entry:"nscript.dat"));
            Assert.Equal("text",text.Kind);Assert.Contains("Hello decoded script",text.Text);
        }
        finally {Directory.Delete(root,true);}
        var binary=await Request<ResourcePreview>(new("resource-preview",null,Location:new(Path.GetDirectoryName(Fixture("standard.xfl"))),
            Entry:"standard.xfl",ForcedType:"binary"));
        Assert.Equal("binary",binary.Kind);Assert.StartsWith("00000000",binary.Text);
        var catalog=await Request<ResourceFormatInfo[]>(new("resource-catalog",null));
        Assert.Contains(catalog,f=>f.Type=="script"&&f.Tag=="AMP/LEAF");
        Assert.Contains(catalog,f=>f.Tag=="XP3"&&f.Schemes.Contains("none"));
    }

    [Fact]
    public async Task AudioIsPreparedAsABoundedParentOwnedFile()
    {
        string root=Path.Combine(Path.GetTempPath(),"garbro-preview-test-"+Guid.NewGuid());Directory.CreateDirectory(root);
        try
        {
            string output=Path.Combine(root,"audio.wav");
            var audio=await Request<AudioArtifact>(new("resource-audio",null,Location:new(Path.GetDirectoryName(Fixture("tone.mp3"))),
                Entry:"tone.mp3",Output:output));
            Assert.Equal(22050,audio.SampleRate);
            Assert.True(audio.Duration>0);
            Assert.True(File.Exists(output));
            using var decoded=new NAudio.Wave.WaveFileReader(output);
            Assert.Equal(16,decoded.WaveFormat.BitsPerSample);
        }
        finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void ResourceEncodingChangesDefaultNameReadersWithoutChangingTheFixedCodepage()
    {
        var previous=Encodings.ResourceEncodingOverride;
        try
        {
            new DecoderContext("gbk").Apply();
            byte[] name=System.Text.Encoding.GetEncoding(936).GetBytes("中文\0");
            Assert.Equal("中文",GameRes.Utility.Binary.GetCString(name,0));
            Assert.Equal(932,Encodings.cp932.CodePage);
        }
        finally {Encodings.ResourceEncodingOverride=previous;}
    }

    [Fact]
    public async Task EveryDeclaredArchiveWriterCreatesAnArchiveThatCanBeListed()
    {
        string root=Path.Combine(Path.GetTempPath(),"garbro-writers-"+Guid.NewGuid());Directory.CreateDirectory(root);
        var failures=new System.Collections.Generic.List<string>();
        try
        {
            File.WriteAllText(Path.Combine(root,"hello.txt"),"Hello writer!\n");
            File.WriteAllBytes(Path.Combine(root,"00000001.scr"),new byte[]{0x53,0x43,0x52,0,1,2,3,4});
            var catalog=await Request<ResourceFormatInfo[]>(new("resource-catalog",null));
            var writers=catalog.Where(f=>f.Type=="archive"&&f.CanWrite).ToArray();Assert.Equal(22,writers.Length);
            int index=0;
            foreach(var writer in writers)
            {
                string output=Path.Combine(root,"archive-"+index+++"."+writer.Extensions.FirstOrDefault());
                try
                {
                    string parameters=writer.Tag=="NPK"?"{\"Key\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=\"}":writer.Tag=="YPF"?"{\"Key\":90,\"Version\":256,\"Scheme\":\"none\"}":null;
                    string filename=writer.Tag=="AMI"?"00000001.scr":"hello.txt";
                    await Request<string>(new("resource-create",root,Output:output,FormatId:writer.Id,Inputs:new[]{filename},OptionsJson:parameters));
                    var listing=await Request<BrowserListing>(new("resource-list",null,OptionsJson:parameters??(writer.OptionsJson=="null"?null:writer.OptionsJson),Location:new(output)));
                    Assert.Contains(listing.Entries,e=>e.DisplayName==filename);
                    var preview=await Request<ResourcePreview>(new("resource-preview",null,OptionsJson:parameters??(writer.OptionsJson=="null"?null:writer.OptionsJson),Location:new(output),Entry:filename,ForcedType:"script"));
                    if(writer.Tag!="AMI")Assert.Contains("Hello writer!",preview.Text);
                }
                catch(Exception error){failures.Add(writer.Tag+": "+error.Message);}
            }
            Assert.True(failures.Count==0,string.Join("\n",failures));
        }
        finally {Directory.Delete(root,true);}
    }
}
