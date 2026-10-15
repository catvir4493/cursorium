using System.Security.Cryptography;
using System.Text.Json;

namespace Frost.Core;

// An opt-in laboratory copy, not an installed-data migration or a registry writer.
public static class IdentityMigration
{
    private const string Marker="cursorium-migration-sandbox.marker";
    private static void GuardLocation(string root)
    {
        root=Path.GetFullPath(root);DownloadManager.GuardRoot(root);
        foreach(var channel in Enum.GetValues<DistributionChannel>()){
            var live=new DistributionProfile(channel).DataRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            if(root.Equals(live,StringComparison.OrdinalIgnoreCase)||root.StartsWith(live+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||live.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Migration sandbox overlaps live data.");
        }
        using var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ProductIdentity.LegacyUninstallKey);
        if(key?.GetValue("InstallLocation") is string installed){installed=Path.GetFullPath(installed).TrimEnd(Path.DirectorySeparatorChar);if(root.Equals(installed,StringComparison.OrdinalIgnoreCase)||root.StartsWith(installed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||installed.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Migration sandbox overlaps the installation.");}
        if(!Path.GetFileName(root).StartsWith("CursoriumMigration-",StringComparison.Ordinal))throw new InvalidDataException("A named migration sandbox is required.");
    }
    private static void Guard(string root)
    {
        GuardLocation(root);var marker=Path.Combine(root,Marker);
        if(!File.Exists(marker)||new FileInfo(marker).Length>128||File.ReadAllText(marker)!="CURSORIUM isolated migration v1")throw new InvalidDataException("An explicitly marked migration sandbox is required.");
    }
    public static void CreateSandbox(string root)
    {
        root=Path.GetFullPath(root);if(Directory.Exists(root)||Path.GetPathRoot(root)==root||!Path.GetFileName(root).StartsWith("CursoriumMigration-",StringComparison.Ordinal))throw new InvalidDataException("Use a new named migration sandbox.");
        // Validate ancestors and live-root overlap before creating any file.
        GuardLocation(root);
        Directory.CreateDirectory(root);Storage.WriteText(Path.Combine(root,Marker),"CURSORIUM isolated migration v1");Directory.CreateDirectory(Path.Combine(root,"legacy"));
    }
    private static Dictionary<string,string> HashTree(string root)
    {
        var hashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);long total=0;int entries=0;
        void Visit(string directory,int depth){if(depth>16)throw new InvalidDataException("Migration depth exceeded.");DownloadManager.GuardRoot(directory);
            foreach(var path in Directory.EnumerateFileSystemEntries(directory)){
                if(++entries>20000)throw new InvalidDataException("Migration entry limit exceeded.");
                DownloadManager.GuardRoot(path);if(Directory.Exists(path)){Visit(path,depth+1);continue;}
                var file=new FileInfo(path);if(file.Length>64*1024*1024||(total+=file.Length)>1024L*1024*1024||hashes.Count>=10000)throw new InvalidDataException("Migration copy limit exceeded.");
                hashes.Add(Path.GetRelativePath(root,path),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            }}
        Visit(root,0);return hashes;
    }
    public static MigrationReceipt Prepare(string root,CancellationToken cancellation=default)
    {
        root=Path.GetFullPath(root);Guard(root);var source=Path.Combine(root,"legacy");var target=Path.Combine(root,"cursorium");var staging=Path.Combine(root,"staging");
        if(Directory.Exists(target)||Directory.Exists(staging)||File.Exists(Path.Combine(root,"migration.json")))throw new IOException("Existing migration evidence is preserved; use another sandbox.");
        if(File.Exists(Path.Combine(source,"theme-transaction.json")))throw new InvalidDataException("Pending native transaction requires review before migration; nothing recovered automatically.");
        foreach(var name in new[]{"settings.json","studio.json","profiles.json","original-theme.json","theme-ownership.json","interface.json"}){
            var path=Path.Combine(source,name);if(!File.Exists(path))continue;DownloadManager.GuardRoot(path);if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Oversized legacy document.");using var json=JsonDocument.Parse(File.ReadAllBytes(path));
            if(json.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Unknown legacy document; preserved.");
            var schemas=json.RootElement.EnumerateObject().Where(p=>p.Name.Equals("SchemaVersion",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(schemas.Length>1||schemas.Length==1&&schemas[0].Value.GetInt32()!=1)throw new InvalidDataException("Unknown legacy schema; preserved.");
        }
        var before=HashTree(source);bool committed=false;Directory.CreateDirectory(staging);
        try{
            foreach(var entry in before){cancellation.ThrowIfCancellationRequested();var path=Path.Combine(staging,entry.Key);DownloadManager.GuardRoot(path);Directory.CreateDirectory(Path.GetDirectoryName(path)!);DownloadManager.GuardRoot(path);File.Copy(Path.Combine(source,entry.Key),path,false);}
            var copied=HashTree(staging);var after=HashTree(source);if(before.Count!=copied.Count||before.Count!=after.Count||before.Any(e=>!copied.TryGetValue(e.Key,out var copy)||copy!=e.Value||!after.TryGetValue(e.Key,out var original)||original!=e.Value))throw new IOException("Source changed or copy mismatch; migration not committed.");
            cancellation.ThrowIfCancellationRequested();var receipt=new MigrationReceipt{Files=before,Status="CopyPrepared"};Storage.Write(Path.Combine(root,"migration.json"),receipt);Directory.Move(staging,target);committed=true;receipt.Status="Prepared";Storage.Write(Path.Combine(root,"migration.json"),receipt);return receipt;
        }finally{if(!committed&&Directory.Exists(staging)){HashTree(staging);Directory.Delete(staging,true);}}
    }
    public static void Rollback(string root)
    {
        root=Path.GetFullPath(root);Guard(root);var receiptPath=Path.Combine(root,"migration.json");DownloadManager.GuardRoot(receiptPath);if(new FileInfo(receiptPath).Length>4*1024*1024)throw new InvalidDataException("Oversized migration receipt.");var receipt=FoundationJson.Read<MigrationReceipt>(File.ReadAllBytes(receiptPath),4*1024*1024);
        if(receipt.SchemaVersion!=1||receipt.Status is not ("Prepared" or "CopyPrepared"))throw new InvalidDataException("Unknown migration receipt preserved.");var target=Path.Combine(root,"cursorium");
        if(Directory.Exists(target)){var hashes=HashTree(target);if(hashes.Count!=receipt.Files.Count||receipt.Files.Any(e=>!hashes.TryGetValue(e.Key,out var hash)||hash!=e.Value))throw new IOException("Prepared copy changed; rollback refuses dangerous deletion.");Directory.Move(target,Path.Combine(root,"rolled-back-copy"));}
        else if(receipt.Status!="CopyPrepared")throw new InvalidDataException("Migration target is missing; inspect the interrupted copy.");
        receipt.Status="RolledBack";Storage.Write(Path.Combine(root,"migration.json"),receipt);
    }
}
public sealed class MigrationReceipt
{
    public int SchemaVersion {get;set;}=1;
    public string Status {get;set;}="Prepared";
    public string SourceIdentity {get;set;}="FrostWhiteComet";
    public string TargetIdentity {get;set;}="CURSORIUM";
    public Dictionary<string,string> Files {get;set;}=new();
}
