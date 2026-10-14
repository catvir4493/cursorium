using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Frost.Core;

internal static class CurationVerification
{
    private sealed class Handler:HttpMessageHandler
    {
        public Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> Respond=null!;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)=>Respond(request,cancellation);
    }
    public static async Task Run(string application,string stage,string previous,string report)
    {
        Directory.CreateDirectory(report);Storage.UseTestingRoot(Path.Combine(report,"isolated-data"));var checks=new List<object>();var baseline=JsonSerializer.Serialize(new ThemeService().Snapshot().Cursors);
        void Check(bool condition,string name){checks.Add(new{name,passed=condition});if(!condition)throw new Exception(name);}
        async Task Reject(Func<Task> work,string name){bool rejected=false;try{await work();}catch(Exception ex)when(ex is IOException or InvalidDataException or InvalidOperationException or ArgumentException or JsonException or CryptographicException or OperationCanceledException){rejected=true;}Check(rejected,name);}
        T Clone<T>(T item)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(item,Storage.Json),Storage.Json)!;
        var index=File.ReadAllBytes(Path.Combine(stage,"catalog/v1/index.json"));var signature=File.ReadAllBytes(Path.Combine(stage,"catalog/v1/index.json.sig"));var catalog=RemoteCatalogService.Verify(index,signature);
        var old=RemoteCatalogService.Verify(File.ReadAllBytes(previous),File.ReadAllBytes(previous+".sig"));Check(catalog.Resources.Count==29&&catalog.Resources.Count-old.Resources.Count==19,"29 actual resources, including 19 reviewed additions");
        foreach(var item in old.Resources){var current=catalog.Resources.Single(r=>r.Id==item.Id);Check(current.SHA256==item.SHA256&&current.PackageUrl==item.PackageUrl&&current.Version==item.Version&&current.Author==item.Author&&current.SourceUrl==item.SourceUrl,"Published identity and package immutable: "+item.Id);}
        var collectionBytes=File.ReadAllBytes(Path.Combine(stage,"catalog/v1/collections/index.json"));var collectionSignature=File.ReadAllBytes(Path.Combine(stage,"catalog/v1/collections/index.json.sig"));var collections=RemoteCollectionsService.Verify(collectionBytes,collectionSignature,catalog);Check(collections.Collections.Count==6,"Six independently signed collections with verified members");
        foreach(var c in collections.Collections)Check(c.ResourceIds.Count>0&&c.ResourceIds.All(id=>catalog.Resources.Any(r=>r.Id==id)),"Collection references only actual resources: "+c.Id);
        await Reject(()=>Task.Run(()=>RemoteCollectionsService.Verify(collectionBytes,new byte[384],catalog)),"Forged collection signature rejected");
        var altered=(byte[])collectionBytes.Clone();altered[20]^=1;await Reject(()=>Task.Run(()=>RemoteCollectionsService.Verify(altered,collectionSignature,catalog)),"Changed collection bytes rejected before parsing");
        foreach(var mutation in new Action<RemoteCollections>[] {d=>d.SchemaVersion=99,d=>d.Collections.Add(d.Collections[0]),d=>d.Collections[0].ResourceIds.Add("missing-resource"),d=>d.Collections[0].ResourceIds.Add(d.Collections[0].ResourceIds[0]),d=>d.Collections[0].CoverUrl="http://example.com/evil.png",d=>d.MinimumCatalogVersion="99.0.0"}){var bad=Clone(collections);mutation(bad);await Reject(()=>Task.Run(()=>bad.Validate(catalog)),"Invalid collections schema/member/URL/dependency rejected");}
        var handler=new Handler();int requests=0;handler.Respond=async(request,token)=>{Interlocked.Increment(ref requests);await Task.Delay(15,token);string url=request.RequestUri!.AbsoluteUri;return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(url==RemoteCollectionsService.Url?collectionBytes:url==RemoteCollectionsService.Url+".sig"?collectionSignature:File.ReadAllBytes(Path.Combine(stage,"packages",Path.GetFileName(request.RequestUri.AbsolutePath))))};};
        using var http=new OfficialHttp(handler);var service=new RemoteCollectionsService(http);await Task.WhenAll(service.RefreshAsync(catalog),service.RefreshAsync(catalog));Check(requests==2&&service.Current?.Collections.Count==6,"Concurrent collection refresh coalesces into one document/signature request pair");
        handler.Respond=(_,_)=>throw new HttpRequestException("offline test");var restarted=new RemoteCollectionsService(http);restarted.LoadCache(catalog);await restarted.RefreshAsync(catalog);Check(restarted.Current?.Version==collections.Version&&restarted.Status.Contains("offline"),"Restart and offline retain the last verified collections");
        var cache=Path.Combine(Storage.Root,"Online/collections-cache.json");File.WriteAllText(cache,"{\"SchemaVersion\":99}");var cacheBefore=File.ReadAllBytes(cache);handler.Respond=(request,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(request.RequestUri!.AbsoluteUri.EndsWith(".sig")?collectionSignature:collectionBytes)});await restarted.RefreshAsync(catalog);Check(cacheBefore.SequenceEqual(File.ReadAllBytes(cache)),"Unknown future collections cache is preserved without overwrite");
        foreach(var resource in catalog.Resources.Where(r=>!old.Resources.Any(o=>o.Id==r.Id)))
        {
            var archive=Path.Combine(stage,"packages",resource.Id+".zip");Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))).Equals(resource.SHA256,StringComparison.OrdinalIgnoreCase),"Published package SHA256: "+resource.Id);
            using var pack=new VerifiedResourcePack(archive,resource,Path.Combine(report,"verified-packs"));
            Check(pack.Cursor!=null?pack.Cursor.NativeRoleCount==15&&pack.Cursor.InheritedRoles.Count==0:pack.Trail!=null,"Actual native 15-role cursor or bounded trusted preset: "+resource.Id);
            using var zip=CursorPack.OpenValidatedArchive(archive);Check(zip.GetEntry("NOTICE.txt")!=null,"Attribution NOTICE included: "+resource.Id);
        }
        Check(catalog.Resources.Select(r=>r.SHA256).Distinct(StringComparer.OrdinalIgnoreCase).Count()==catalog.Resources.Count,"Duplicate package hashes refused by publication preflight");
        var themes=new ThemeCatalog(application);var manager=new DownloadManager(http,themes);var cursor=catalog.Resources.Single(r=>r.Id=="brown-cow");var package=File.ReadAllBytes(Path.Combine(stage,"packages",cursor.Id+".zip"));byte[] response=package;
        handler.Respond=(_,token)=>{token.ThrowIfCancellationRequested();return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(response)});};
        await manager.InstallAsync(cursor,null,CancellationToken.None);var installed=themes.Find(cursor.Id);var files=Directory.GetFiles(installed.DirectoryPath,"*",SearchOption.AllDirectories).ToDictionary(f=>f,f=>SHA256.HashData(File.ReadAllBytes(f)));var rootRecord=File.ReadAllBytes(Path.Combine(installed.DirectoryPath,"theme.json"));
        var update=Clone(cursor);update.Version="1.0.1";var badUpdate=Clone(update);badUpdate.SHA256=new string('0',64);Check(manager.State(update)=="Update available","Stable ID and package version project Update available");await Reject(()=>manager.InstallAsync(badUpdate,null,CancellationToken.None),"Failed update hash preserves existing installation");Check(rootRecord.SequenceEqual(File.ReadAllBytes(Path.Combine(installed.DirectoryPath,"theme.json"))),"Update failure leaves atomic installation record byte-identical");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();await Reject(()=>manager.InstallAsync(update,null,cancelled.Token),"Cancelled update does not commit");}
        var work=Path.Combine(report,"update-source");Directory.CreateDirectory(work);using(var verified=new VerifiedResourcePack(Path.Combine(stage,"packages",cursor.Id+".zip"),cursor,Path.Combine(report,"update-staging"))){foreach(var file in Directory.GetFiles(verified.DirectoryPath,"*",SearchOption.AllDirectories)){var target=Path.Combine(work,Path.GetRelativePath(verified.DirectoryPath,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}var manifest=ThemeCatalog.ReadTheme(Path.Combine(work,"theme.json"));manifest.Version=update.Version;Storage.Write(Path.Combine(work,"theme.json"),manifest);}
        var updatedZip=Path.Combine(report,"update-fixture.zip");ZipFile.CreateFromDirectory(work,updatedZip);response=File.ReadAllBytes(updatedZip);update.PackageSize=response.Length;update.SHA256=Convert.ToHexString(SHA256.HashData(response));await manager.InstallAsync(update,null,CancellationToken.None);Check(themes.Find(cursor.Id).Version=="1.0.1"&&manager.State(update)=="Installed","Verified update atomically installs new immutable version");
        Check(files.Where(p=>p.Key!=Path.Combine(installed.DirectoryPath,"theme.json")).All(p=>File.Exists(p.Key)&&p.Value.SequenceEqual(SHA256.HashData(File.ReadAllBytes(p.Key)))),"Every previously installed immutable resource remains byte-identical");
        var mediaOnly=Clone(update);mediaOnly.CoverUrl="https://catvir4493.github.io/cursorium-library/catalog/v1/previews/alternate-cover.png";Check(manager.State(mediaOnly)=="Installed","Cover-only catalog change does not request package update");await Reject(()=>manager.InstallAsync(cursor,null,CancellationToken.None),"Resource downgrade refused without deleting newer version");
        var sameVersion=Clone(update);sameVersion.SHA256=new string('1',64);await Reject(()=>manager.InstallAsync(sameVersion,null,CancellationToken.None),"Same resource version with different package bytes rejected");var imposter=Clone(update);imposter.Author="Different author";Check(manager.State(imposter)=="ID conflict","Author conflict is distinct from installed state");await Reject(()=>manager.InstallAsync(imposter,null,CancellationToken.None),"Different author cannot overwrite an owned stable ID");
        var studio=new StudioService(application,new ThemeService());var previousConfiguration=new StudioConfiguration{CursorThemeId=cursor.Id,CursorPackageSha256=cursor.SHA256};var latestConfiguration=new StudioConfiguration{CursorThemeId=cursor.Id,CursorPackageSha256=update.SHA256};
        Check(studio.ResolveTheme(previousConfiguration).Version=="1.0.0"&&studio.ResolveTheme(latestConfiguration).Version=="1.0.1","Applied and latest resource versions resolve independently through retained manifests");
        var profile=studio.Profiles.Save(null,"Retained version fixture",previousConfiguration);var profilesBefore=File.ReadAllBytes(Path.Combine(Storage.Root,"profiles.json"));previousConfiguration.Effects.Color="#ABCDEF";
        Check(studio.ResolveTheme(previousConfiguration).Version=="1.0.0","Editing motion parameters preserves the previously applied cursor version");
        Check(previousConfiguration.Clone().CursorPackageSha256==cursor.SHA256&&new ProfileManager().Profiles.Single(p=>p.Id==profile.Id).Configuration.CursorPackageSha256==cursor.SHA256,"Cloning and saved Profiles retain explicit immutable cursor version");
        await manager.InstallAsync(update,null,CancellationToken.None);Check(profilesBefore.SequenceEqual(File.ReadAllBytes(Path.Combine(Storage.Root,"profiles.json"))),"Repeated resource installation never rewrites an existing Profile");
        var escaping=previousConfiguration.Clone();escaping.CursorPackageSha256="../outside";await Reject(()=>Task.Run(()=>studio.ResolveTheme(escaping)),"A retained-version pin cannot escape the managed theme directory");
        Check(JsonSerializer.Serialize(new ThemeService().Snapshot().Cursors)==baseline,"All curation/download/update checks preserve actual Windows cursor snapshot");
        Storage.Write(Path.Combine(report,"curation-report.json"),new{passed=true,checks,resources=catalog.Resources.Count,collections=collections.Collections.Count,notes="Update 1.0.1 is an isolated explicit test fixture, not an invented public resource. No system apply was performed."});Console.WriteLine("Curation verification passed: "+checks.Count+" checks.");
    }
}
