using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Frost.Core;

internal static class DownloadFilesystemVerification
{
    public static async Task Run(string application,string report,string isolatedRoot)
    {
        var started=DateTimeOffset.UtcNow;Storage.UseTestingRoot(isolatedRoot);
        if(Directory.Exists(isolatedRoot))throw new InvalidOperationException("A fresh isolated download root is required.");
        Directory.CreateDirectory(isolatedRoot);Directory.CreateDirectory(report);var checks=new List<object>();
        void Check(bool passed,string name){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
        using var http=new OfficialHttp();var bytes=await http.BytesAsync(OfficialHttp.CatalogUrl,2*1024*1024,CancellationToken.None);
        var signature=await http.BytesAsync(OfficialHttp.CatalogUrl+".sig",512,CancellationToken.None);
        var catalog=RemoteCatalogService.Verify(bytes,signature);Check(catalog.Resources.Count>0,"Actual public catalog signature and schema validated");
        var native=new ThemeService();string before=JsonSerializer.Serialize(native.Snapshot().Cursors);
        var themes=new ThemeCatalog(application);var manager=new DownloadManager(http,themes);
        var item=catalog.Resources.Single(r=>r.Id=="simple-dot-star");
        using(var preview=await manager.FetchPreviewAsync(item,null,CancellationToken.None))
        {
            Check(preview.Cursor?.Roles.Count==15,"Reported real theme downloads and validates all 15 roles");
            Check(manager.State(item)=="Online only","Verified preview has not installed or applied the theme");
            var cancelledTarget=Path.Combine(isolatedRoot,"cancelled-copy");using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();bool refused=false;try{CursorPack.CommitThemeDirectory(preview.DirectoryPath,cancelledTarget,cancelled.Token);}catch(OperationCanceledException){refused=true;}
                Check(refused&&!Directory.Exists(cancelledTarget),"Cancelled theme copy never publishes a partial manifest");
            }
            var failedTarget=Path.Combine(isolatedRoot,"failed-copy");bool copyFailed=false;
            using(var locked=new FileStream(Path.Combine(preview.DirectoryPath,"NOTICE.txt"),FileMode.Open,FileAccess.Read,FileShare.None))
            {try{CursorPack.CommitThemeDirectory(preview.DirectoryPath,failedTarget);}catch(IOException){copyFailed=true;}}
            Check(copyFailed&&!Directory.Exists(failedTarget)&&Directory.Exists(preview.DirectoryPath),"Copy I/O failure removes only its partial destination and preserves the verified source");
            var existingTarget=Path.Combine(isolatedRoot,"existing-copy");Directory.CreateDirectory(existingTarget);File.WriteAllText(Path.Combine(existingTarget,"keep.txt"),"preserved");bool existingRefused=false;
            try{CursorPack.CommitThemeDirectory(preview.DirectoryPath,existingTarget);}catch(IOException){existingRefused=true;}
            Check(existingRefused&&File.ReadAllText(Path.Combine(existingTarget,"keep.txt"))=="preserved","Existing destination is rejected without overwriting or deleting it");
            await manager.InstallAsync(item,null,CancellationToken.None);
            Check(Directory.Exists(preview.DirectoryPath),"Separate preview survives installation");
        }
        var installed=themes.Find(item.Id);Check(manager.State(item)=="Installed","Reported theme atomically enters the local library");
        Check(installed.Roles.Count==15&&installed.AnimatedRoleCount==2,"Installed real CUR/ANI resources retain role mapping");
        var hashes=Directory.GetFiles(installed.DirectoryPath,"*",SearchOption.AllDirectories).ToDictionary(p=>p,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
        await manager.InstallAsync(item,null,CancellationToken.None);
        Check(hashes.All(p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p.Key)))==p.Value),"Retry/duplicate install preserves immutable file hashes");
        Check(new ThemeCatalog(application).Find(item.Id).PackageSha256==item.SHA256,"Installation survives catalog restart");
        var frost=themes.Find("frost");var imported=themes.Import("Encrypted folder import regression",frost,new Dictionary<string,string>{{"Normal",installed.Resource("Normal")}});
        Check(imported.Roles.Count==15&&imported.InheritedRoles.Count==14,"Local import retains safe fallback and works in the same filesystem");
        Check(JsonSerializer.Serialize(native.Snapshot().Cursors)==before,"Download, preview and local import leave all system cursor values/types unchanged");
        Check(!Directory.EnumerateDirectories(manager.StagingRoot,"resource-*").Any(),"Preview and installation release owned extraction directories");
        Storage.Write(Path.Combine(report,"download-filesystem-report.json"),new{passed=true,started,ended=DateTimeOffset.UtcNow,encrypted=(File.GetAttributes(isolatedRoot)&FileAttributes.Encrypted)!=0,resource=item.Id,packageSHA256=item.SHA256,checks});
        Console.WriteLine($"Real download filesystem checks passed: {checks.Count}");
    }
}
