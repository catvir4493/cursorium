using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Frost.Core;

internal static class OnlineVerification
{
    private sealed class Handler:HttpMessageHandler
    {
        public Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Respond=null!;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)=>Respond(request,cancellation);
    }
    public static async Task Run(string application,string export,string report)
    {
        Directory.CreateDirectory(report);Storage.UseTestingRoot(Path.Combine(report,"online-data-"+Guid.NewGuid().ToString("N")));var checks=new List<object>();
        void Check(bool passed,string name){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
        async Task Reject(Func<Task> operation,string name){bool rejected=false;try{await operation();}catch(Exception ex)when(ex is InvalidDataException or InvalidOperationException or HttpRequestException or OperationCanceledException or JsonException){rejected=true;}Check(rejected,name);}
        var fixture=File.Exists(Path.Combine(export,"index.json"));var index=File.ReadAllBytes(Path.Combine(export,fixture?"index.json":"catalog/v1/index.json"));var signature=File.ReadAllBytes(Path.Combine(export,fixture?"index.json.sig":"catalog/v1/index.json.sig"));var signed=RemoteCatalogService.Verify(index,signature);Check(signed.Resources.Count==10,"Signed real catalog validates all ten metadata entries");
        if(fixture)
        {
            export=Path.Combine(report,"generated-fixture-packages");Directory.CreateDirectory(Path.Combine(export,"packages"));
            foreach(var item in signed.Resources.Where(r=>r.Id is "cool-cat-superstar" or "neon-blue"))
            {
                var work=Path.Combine(export,"work",item.Id);Directory.CreateDirectory(work);
                if(item.Type=="cursor")
                {
                    var source=Path.Combine(application,"Themes","cute-cool-cat");foreach(var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories).Where(f=>Path.GetExtension(f).ToLowerInvariant() is ".cur" or ".ani")){var target=Path.Combine(work,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}
                    var t=ThemeCatalog.ReadTheme(Path.Combine(source,"theme.json"));t.Id=item.Id;t.Name=item.Name;t.Version=item.Version;Storage.Write(Path.Combine(work,"theme.json"),t);
                }
                else Storage.Write(Path.Combine(work,"preset.json"),new TrailPreset{Id=item.Id,Name=item.Name,Author=item.Author,Version=item.Version,RendererType="neon",Length=180,Duration=320,Opacity=.55,Particles=0,Color="#87CFFF"});
                var archive=Path.Combine(export,"packages",item.Id+".zip");ZipFile.CreateFromDirectory(work,archive);item.PackageSize=new FileInfo(archive).Length;item.SHA256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive)));
            }
        }
        var modified=(byte[])index.Clone();modified[10]^=1;await Reject(()=>Task.Run(()=>RemoteCatalogService.Verify(modified,signature)),"Altered signed catalog rejected");await Reject(()=>Task.Run(()=>RemoteCatalogService.Verify(index,new byte[384])),"Wrong catalog signature rejected");
        var resource=signed.Resources.Single(r=>r.Id=="cool-cat-superstar");var trail=signed.Resources.Single(r=>r.Id=="neon-blue");var package=File.ReadAllBytes(Path.Combine(export,"packages/"+resource.Id+".zip"));
        var handler=new Handler();handler.Respond=(request,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(request.RequestUri!.AbsoluteUri==OfficialHttp.CatalogUrl?index:request.RequestUri.AbsoluteUri==OfficialHttp.CatalogUrl+".sig"?signature:File.ReadAllBytes(Path.Combine(export,"packages/"+Path.GetFileName(request.RequestUri.AbsolutePath))))});
        using var http=new OfficialHttp(handler);var catalog=new RemoteCatalogService(http);await catalog.RefreshAsync();Check(catalog.Current!=null&&!catalog.Offline,"Catalog cache commits only after signature and schema validation");
        var themes=new ThemeCatalog(application);var manager=new DownloadManager(http,themes);var original=new ThemeService();string nativeBefore=JsonSerializer.Serialize(original.Snapshot().Cursors);
        int progress=0;var reporter=new SyncProgress(p=>{if(p.Bytes>0)progress++;});await manager.InstallAsync(resource,reporter,CancellationToken.None);Check(progress>0&&themes.Find(resource.Id).ManagedDownload,"Actual streamed bytes report progress before managed install");Check(manager.State(resource)=="Installed","Installed state derives from committed theme manifest");
        var files=Directory.GetFiles(themes.Find(resource.Id).DirectoryPath,"*",SearchOption.AllDirectories).ToDictionary(f=>f,f=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));await manager.InstallAsync(resource,null,CancellationToken.None);Check(files.All(p=>File.Exists(p.Key)&&Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p.Key)))==p.Value),"Duplicate download preserves every existing theme file");
        Check(JsonSerializer.Serialize(original.Snapshot().Cursors)==nativeBefore,"Download has zero Windows pointer side effects");
        await manager.InstallAsync(trail,null,CancellationToken.None);var preset=manager.LoadTrails().Single();var settings=new Settings();preset.Apply(settings);Check(settings.TrailEffect=="neon"&&settings.Color=="#87CFFF","Data-only motion preset maps to a trusted renderer and real parameters");
        Check(new ThemeCatalog(application).Find(resource.Id).Roles.Count==15&&new DownloadManager(http,themes).LoadTrails().Single().Id==trail.Id,"Atomic installation records survive manager restart");
        handler.Respond=(_,_)=>throw new HttpRequestException("offline fixture");var cached=new RemoteCatalogService(http);await cached.RefreshAsync();Check(cached.Current?.Resources.Count==10&&cached.Offline,"Offline refresh retains the last signature-verified cache");
        foreach(var url in new[]{"http://catvir4493.github.io/cursorium-library/catalog/v1/index.json","https://example.com/cursorium.zip","https://github.com/other/library/releases/download/v1/x.zip","https://catvir4493.github.io/unrelated/index.json","https://release-assets.githubusercontent.com/github-production-release-asset/x","https://user@catvir4493.github.io/cursorium-library/catalog/v1/index.json"})await Reject(()=>Task.Run(()=>OfficialHttp.ValidateUrl(new Uri(url),false)),"Unauthorized URL rejected: "+url);
        handler.Respond=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect){Headers={Location=new Uri("https://example.com/stolen.zip")}});await Reject(()=>http.BytesAsync(resource.PackageUrl,100,CancellationToken.None),"Cross-domain redirect rejected before request");
        handler.Respond=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[101])});await Reject(()=>http.BytesAsync(resource.PackageUrl,100,CancellationToken.None),"Declared oversized download rejected");
        handler.Respond=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[99])});using(var output=new MemoryStream())await Reject(()=>http.FetchAsync(resource.PackageUrl,output,100,CancellationToken.None,null,100),"Package declared size mismatch rejected");
        handler.Respond=async(_,token)=>{await Task.Delay(10000,token);return new HttpResponseMessage(HttpStatusCode.OK);};using(var cancelled=new CancellationTokenSource(30))await Reject(()=>manager.FetchPreviewAsync(trail,null,cancelled.Token),"Cancellation interrupts pending network and cleans temporary archive");Check(!Directory.GetFiles(manager.StagingRoot,"download-*").Any(),"Cancelled download leaves no incomplete ZIP");
        using(var timed=new OfficialHttp(new Handler{Respond=async(_,token)=>{await Task.Delay(10000,token);return new HttpResponseMessage(HttpStatusCode.OK);}},TimeSpan.FromMilliseconds(30)))await Reject(()=>timed.BytesAsync(resource.PackageUrl,100,CancellationToken.None),"Request timeout interrupts a nonresponding server");
        int attempts=0;handler.Respond=(_,_)=>{if(++attempts==1)throw new HttpRequestException("transient fixture");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(package)});};using(var retry=await manager.FetchPreviewAsync(resource,null,CancellationToken.None))Check(attempts==2&&retry.Cursor!=null,"One bounded retry recovers a transient transport failure");
        attempts=0;handler.Respond=(_,_)=>{attempts++;throw new HttpRequestException("persistent fixture");};await Reject(()=>manager.FetchPreviewAsync(resource,null,CancellationToken.None),"Persistent transport failure is reported");Check(attempts==2,"Retry count is strictly bounded at two attempts");
        themes.Find(resource.Id).PackageSha256="";var blocked=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);handler.Respond=async(_,token)=>{await blocked.Task.WaitAsync(token);return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(package)};};var first=manager.InstallAsync(resource,null,CancellationToken.None);await Reject(()=>manager.InstallAsync(resource,null,CancellationToken.None),"Concurrent double download is refused by stable resource ID");blocked.SetResult();await first;
        handler.Respond=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(package)});var mismatch=Clone(resource);mismatch.SHA256=new string('0',64);await Reject(()=>manager.FetchPreviewAsync(mismatch,null,CancellationToken.None),"SHA256 mismatch refuses extraction and installation");
        var future=Clone(resource);future.MinimumClientVersion="99.0.0";await Reject(()=>manager.FetchPreviewAsync(future,null,CancellationToken.None),"Incompatible client version rejected before download");var collision=Clone(resource);collision.Id="frost";await Reject(()=>manager.InstallAsync(collision,null,CancellationToken.None),"Stable ID collision refuses replacing bundled theme");
        foreach(var invalid in new[]{new RemoteCatalog{SchemaVersion=99,CatalogVersion="1.0.0"},new RemoteCatalog{CatalogVersion="invalid"},new RemoteCatalog{CatalogVersion="1.0.0",Resources=new(){resource,resource}}})await Reject(()=>Task.Run(()=>invalid.Validate()),"Unsupported, malformed or duplicate catalog safely rejected");
        var badRoles=Clone(resource);badRoles.NativeRoles=new(){"Invalid"};await Reject(()=>Task.Run(()=>badRoles.Validate()),"Unknown Windows role rejected");var invalidPreset=new TrailPreset{Id="bad",Name="Bad",RendererType="downloaded-dll",Length=180,Duration=300,Opacity=.5};await Reject(()=>Task.Run(()=>invalidPreset.Validate()),"Remote executable renderer rejected");invalidPreset.RendererType="neon";invalidPreset.Length=double.NaN;await Reject(()=>Task.Run(()=>invalidPreset.Validate()),"Nonfinite motion parameter rejected");
        foreach(string name in new[]{"../outside.cur","/absolute.cur","a/../normal.cur","a\\..\\normal.cur","C:/normal.cur"})await BadZip(name,false,0,"ZIP traversal rejected: "+name);
        await BadZip("Normal.cur",true,0,"Duplicate ZIP names rejected");await BadZip("linked.cur",false,0xA000<<16,"ZIP symbolic link rejected");
        var bomb=Path.Combine(report,"bomb.zip");using(var zip=ZipFile.Open(bomb,ZipArchiveMode.Create)){using var stream=zip.CreateEntry("bomb.cur",CompressionLevel.Optimal).Open();stream.Write(new byte[1024*1024]);}await Reject(()=>Task.Run(()=>CursorPack.OpenValidatedArchive(bomb).Dispose()),"ZIP compression bomb rejected");
        var badCursor=Path.Combine(report,"bad-cursor.zip");using(var zip=ZipFile.Open(badCursor,ZipArchiveMode.Create)){using var stream=zip.CreateEntry("Normal.cur",CompressionLevel.NoCompression).Open();stream.Write(new byte[100]);}await Reject(()=>Task.Run(()=>CursorPack.Open(badCursor).Dispose()),"Invalid cursor bytes rejected by production resource validator");
        var script=Path.Combine(report,"script.zip");using(var zip=ZipFile.Open(script,ZipArchiveMode.Create)){using var writer=new StreamWriter(zip.CreateEntry("run.ps1").Open());writer.Write("not executed");}await Reject(()=>Task.Run(()=>new VerifiedResourcePack(script,trail,manager.StagingRoot).Dispose()),"Executable package entry rejected without execution");
        var preferences=ThemeLibraryPreferences.Load();preferences.Favorites.Add("future-online-theme");preferences.Save();Check(ThemeLibraryPreferences.Load().Favorites.Contains("future-online-theme"),"Online-only favorite persists without requiring download or current catalog");
        Check(!Directory.EnumerateDirectories(manager.StagingRoot,"resource-*").Any(),"Failed preview/install operations clean their owned staging trees");
        var abandoned=Path.Combine(Storage.Root,"Online","Staging","session-2147483647-0");Directory.CreateDirectory(abandoned);File.WriteAllBytes(Path.Combine(abandoned,"download-"+Guid.NewGuid().ToString("N")+".zip"),new byte[7]);var recovered=new DownloadManager(http,themes);Check(!Directory.Exists(abandoned)&&recovered.Warnings.Count>0,"Restart cleans a dead process's incomplete download session without marking it installed");
        var currentSession=manager.StagingRoot;Directory.CreateDirectory(currentSession);var currentMarker=Path.Combine(currentSession,"active-marker");File.WriteAllText(currentMarker,"live process");new DownloadManager(http,themes);Check(File.Exists(currentMarker),"Recovery preserves a live process's preview and download session");
        var futureRecord=Path.Combine(manager.PresetsRoot,"future-preset.json");Storage.Write(futureRecord,new TrailPreset{SchemaVersion=99,Id="future-preset",Name="Future",RendererType="neon"});var futureHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(futureRecord)));Check(manager.LoadTrails().All(p=>p.Id!="future-preset")&&futureHash==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(futureRecord))),"Newer motion schema is preserved and does not block existing local presets");
        Storage.Write(Path.Combine(report,"online-security-report.json"),new{passed=true,checks,notes="Production HTTP/catalog/download/ZIP/Win32 resource validators with injected network failures under isolated data. Real official network and real WPF workflow are verified separately."});Console.WriteLine($"Online security checks passed: {checks.Count}");
        async Task BadZip(string name,bool duplicate,int attributes,string description)
        {
            var file=Path.Combine(report,Guid.NewGuid().ToString("N")+".zip");using(var zip=ZipFile.Open(file,ZipArchiveMode.Create)){for(int i=0;i<(duplicate?2:1);i++){var entry=zip.CreateEntry(name,CompressionLevel.NoCompression);entry.ExternalAttributes=attributes;using var stream=entry.Open();stream.Write(new byte[100]);}}await Reject(()=>Task.Run(()=>CursorPack.OpenValidatedArchive(file).Dispose()),description);
        }
    }
    private static RemoteResource Clone(RemoteResource item)=>JsonSerializer.Deserialize<RemoteResource>(JsonSerializer.Serialize(item,Storage.Json),Storage.Json)!;
    private sealed class SyncProgress: IProgress<DownloadProgress>{private readonly Action<DownloadProgress> update;public SyncProgress(Action<DownloadProgress> update){this.update=update;}public void Report(DownloadProgress value)=>update(value);}
}
