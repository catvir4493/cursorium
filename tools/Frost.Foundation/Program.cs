using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Frost.Core;

internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        try{
            if(args.Length<2)throw new ArgumentException("A command and isolated root are required.");
            var command=args[0];var root=Path.GetFullPath(args[1]);Storage.UseTestingRoot(root);
            switch(command){
                case "probe":Storage.Write(Path.Combine(root,"channel.json"),ProductIdentity.Describe());break;
                case "verify-rights":{
                    var dir=Path.GetFullPath(args[2]);var rights=RemoteRightsService.Verify(File.ReadAllBytes(Path.Combine(dir,"rights.json")),File.ReadAllBytes(Path.Combine(dir,"rights.json.sig")),File.ReadAllBytes(Path.Combine(dir,"index.json")),File.ReadAllBytes(Path.Combine(dir,"index.json.sig")));
                    Storage.Write(Path.Combine(root,"rights-report.json"),new{passed=true,rights.Version,total=rights.Resources.Count,online=rights.Resources.Count(r=>r.Scope=="Online"),steamApproved=rights.Resources.Count(r=>r.Scope=="Online"&&r.AllowsSteam)});break;
                }
                case "sign-rights":{
                    var dir=Path.GetFullPath(args[2]);var bytes=File.ReadAllBytes(Path.Combine(dir,"rights.json"));var catalog=File.ReadAllBytes(Path.Combine(dir,"index.json"));var cs=File.ReadAllBytes(Path.Combine(dir,"index.json.sig"));var reviewed=FoundationJson.Read<LibraryRightsDocument>(bytes,2*1024*1024);
                    reviewed.Validate(RemoteCatalogService.Verify(catalog,cs),Convert.ToHexString(SHA256.HashData(catalog)));
                    using var rsa=RSA.Create();rsa.ImportFromPem(File.ReadAllText(args[3]));var signature=rsa.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss);_=RemoteRightsService.Verify(bytes,signature,catalog,cs);
                    using var file=new FileStream(Path.Combine(dir,"rights.json.sig"),FileMode.CreateNew,FileAccess.Write,FileShare.None);file.Write(signature);file.Flush(true);break;
                }
                case "verify-steam-payload":{
                    var dir=args[2];var document=RemoteRightsService.Verify(File.ReadAllBytes(Path.Combine(dir,"rights.json")),File.ReadAllBytes(Path.Combine(dir,"rights.json.sig")),File.ReadAllBytes(Path.Combine(dir,"index.json")),File.ReadAllBytes(Path.Combine(dir,"index.json.sig")));
                    BundledRights.VerifySteamPayload(document,args[3]);Storage.Write(Path.Combine(root,"steam-payload-report.json"),new{passed=true,steamIntegrated=false,nativeDeploymentPerformed=false,approvedBundledRecords=6});break;
                }
                case "verify-base-network":{
                    using var http=new OfficialHttp();var index=http.BytesAsync(OfficialHttp.CatalogUrl,2*1024*1024,default).GetAwaiter().GetResult();var sig=http.BytesAsync(OfficialHttp.CatalogUrl+".sig",512,default).GetAwaiter().GetResult();var catalog=RemoteCatalogService.Verify(index,sig);
                    var collections=RemoteCollectionsService.Verify(http.BytesAsync(RemoteCollectionsService.Url,256*1024,default).GetAwaiter().GetResult(),http.BytesAsync(RemoteCollectionsService.Url+".sig",512,default).GetAwaiter().GetResult(),catalog);
                    var expected=FoundationJson.Read<LibraryRightsDocument>(File.ReadAllBytes(Path.Combine(args[2],"rights.json")),2*1024*1024);if(!Convert.ToHexString(SHA256.HashData(index)).Equals(expected.CatalogSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Public v1 bytes differ from reviewed base.");
                    Storage.Write(Path.Combine(root,"base-network-report.json"),new{passed=true,resources=catalog.Resources.Count,collections=collections.Collections.Count,catalogSha256=expected.CatalogSha256});break;
                }
                case "verify-rights-network":{
                    using var http=new OfficialHttp();var service=new RemoteRightsService(http);service.RefreshAsync().GetAwaiter().GetResult();if(!service.LastRefreshSucceeded||service.Current?.Version!=args[2])throw new InvalidDataException("Expected public signed rights revision unavailable.");
                    Storage.Write(Path.Combine(root,"rights-network-report.json"),new{passed=true,version=service.Current.Version,online=service.Current.Resources.Count(r=>r.Scope=="Online"),steamApproved=service.Current.Resources.Count(r=>r.Scope=="Online"&&r.AllowsSteam),catalogSha256=service.Current.CatalogSha256,sha256=Convert.ToHexString(SHA256.HashData(http.BytesAsync(RemoteRightsService.Url,2*1024*1024,default).GetAwaiter().GetResult()))});break;
                }
                case "migration-create":IdentityMigration.CreateSandbox(Path.Combine(root,"CursoriumMigration-"+Guid.NewGuid().ToString("N")));break;
                case "migration-prepare":IdentityMigration.Prepare(args[2]);break;
                case "migration-rollback":IdentityMigration.Rollback(args[2]);break;
                case "inspect-legacy-data":{
                    var profilePath=Path.Combine(root,"profiles.json");var backupPath=Path.Combine(root,"original-theme.json");
                    var profiles=File.Exists(profilePath)?JsonSerializer.Deserialize<ProfileDocument>(File.ReadAllBytes(profilePath),Storage.Json):new ProfileDocument();
                    if(profiles?.SchemaVersion!=1||profiles.Profiles==null||profiles.Profiles.Any(p=>p.Configuration?.Effects==null)||profiles.Profiles.Select(p=>p.Id).Distinct().Count()!=profiles.Profiles.Count)throw new InvalidDataException("Legacy profile schema is incompatible.");
                    var backup=File.Exists(backupPath)?JsonSerializer.Deserialize<ThemeBackup>(File.ReadAllBytes(backupPath),Storage.Json):null;
                    if(File.Exists(backupPath)&&(backup?.SchemaVersion!=1||backup.Cursors==null||backup.Scheme==null))throw new InvalidDataException("Legacy backup schema is incompatible.");
                    var native=new ThemeService();int available=backup==null?0:CursorRoles.All.Count(r=>native.OriginalResource(r.Name)!=null);
                    Storage.Write(Path.Combine(root,"legacy-inspection-report.json"),new{passed=true,profiles=profiles.Profiles.Count,backupExists=backup!=null,backupSchemaCompatible=backup!=null,availableOriginalRoleFiles=available,nativeRestorePerformed=false,productionMigrationPerformed=false,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription});break;
                }
                case "portable-export":{
                    // Existing data must be an explicitly supplied isolated copy, never production.
                    var app=Path.GetFullPath(args[2]);var catalog=RemoteCatalogService.Verify(File.ReadAllBytes(args[3]),File.ReadAllBytes(args[3]+".sig"));var local=new ThemeCatalog(app);
                    var result=PortableWorkspaceService.Create(new ProfileManager().Profiles,ThemeLibraryPreferences.Load().Favorites,local,catalog);
                    var output=Path.Combine(root,"portable-workspace.json");if(File.Exists(output))throw new IOException("Existing export preserved; choose another isolated copy.");
                    Storage.Write(output,result.Document);Storage.Write(Path.Combine(root,"portable-export-report.json"),new{passed=true,profiles=result.Document.Profiles.Count,result.ExcludedProfiles,result.ExcludedFavorites,cloudUploadPerformed=false});break;
                }
                case "portable-preview":{
                    var document=PortableWorkspaceService.Read(File.ReadAllBytes(args[3]));var drafts=PortableWorkspaceService.Resolve(document,new ThemeCatalog(args[2]));
                    Storage.Write(Path.Combine(root,"portable-preview-report.json"),new{cloudUploadPerformed=false,systemApplyPerformed=false,profiles=drafts.Select(d=>new{d.ProfileId,d.Status})});break;
                }
                default:throw new ArgumentException("Unknown foundation command.");
            }
            Console.WriteLine("Foundation operation completed; no Windows input or cursor application performed.");return 0;
        }catch(Exception ex){if(Storage.IsIsolated)Storage.Log(ex.ToString());Console.Error.WriteLine("Foundation operation failed. No automatic retry or deployment.");return 1;}
    }
}
