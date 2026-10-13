using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;

namespace Frost.Core;

// Uses the same bounded central-directory parser as the existing cursor import.
// The freshly created extraction tree is never exposed as an installed theme.
public sealed class VerifiedResourcePack:IDisposable
{
    public string DirectoryPath {get;}
    public CursorTheme? Cursor {get;private set;}
    public TrailPreset? Trail {get;private set;}
    public VerifiedResourcePack(string archive,RemoteResource resource,string stagingRoot)
    {
        DirectoryPath=Path.Combine(stagingRoot,"resource-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(DirectoryPath);
        try
        {
            using var zip=CursorPack.OpenValidatedArchive(archive);int resources=0;
            foreach(var entry in zip.Entries)
            {
                var name=entry.FullName.Replace('\\','/');if(name.EndsWith('/'))continue;
                bool cursor=Path.GetExtension(name).ToLowerInvariant() is ".cur" or ".ani";
                bool metadata=name is "theme.json" or "preset.json" or "NOTICE.txt";
                if(!cursor&&!metadata)throw new InvalidDataException("Unexpected executable or unsupported package file: "+name);
                if(cursor&&++resources>CursorPack.MaxFiles||metadata&&entry.Length>256*1024)throw new InvalidDataException("Package file limit exceeded.");
                var target=Path.GetFullPath(Path.Combine(DirectoryPath,name));if(!target.StartsWith(DirectoryPath+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Escaping package path.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var input=entry.Open();using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {var buffer=new byte[65536];long written=0;int n;while((n=input.Read(buffer))>0){written+=n;if(written>entry.Length)throw new InvalidDataException("Expanded package size mismatch.");output.Write(buffer,0,n);}if(written!=entry.Length)throw new InvalidDataException("Truncated package entry.");}
                if(cursor)CursorResource.Validate(target);
            }
            if(resource.Type=="cursor")
            {
                Cursor=ThemeCatalog.ReadTheme(Path.Combine(DirectoryPath,"theme.json"));
                if(Cursor.Id!=resource.Id||Cursor.Name!=resource.Name||Cursor.Author!=resource.Author||Cursor.Version!=resource.Version||Cursor.NativeRoleCount!=resource.NativeRoles.Count||Cursor.AnimatedRoleCount!=resource.AnimatedCursorCount||resource.NativeRoles.Any(r=>Cursor.InheritedRoles.ContainsKey(r)))throw new InvalidDataException("Package identity or role provenance differs from the approved catalog.");
                Cursor.ManagedDownload=true;Cursor.Collection="downloaded";Cursor.PackageSha256=resource.SHA256;Cursor.ImportedAt=DateTimeOffset.UtcNow;Storage.Write(Path.Combine(DirectoryPath,"theme.json"),Cursor);
            }
            else
            {
                if(resources!=0||File.Exists(Path.Combine(DirectoryPath,"theme.json")))throw new InvalidDataException("Motion packages contain data only.");
                Trail=JsonSerializer.Deserialize<TrailPreset>(File.ReadAllText(Path.Combine(DirectoryPath,"preset.json")),Storage.Json)??throw new InvalidDataException("Empty motion preset.");Trail.Validate();
                if(Trail.Id!=resource.Id||Trail.Name!=resource.Name||Trail.Author!=resource.Author||Trail.Version!=resource.Version)throw new InvalidDataException("Motion package identity differs from catalog.");
                Trail.ManagedDownload=true;Trail.PackageSha256=resource.SHA256;
            }
        }
        catch{Dispose();throw;}
    }
    public void Dispose(){if(Directory.Exists(DirectoryPath))Directory.Delete(DirectoryPath,true);}
}

public sealed class DownloadManager
{
    private readonly OfficialHttp http;
    private readonly ThemeCatalog themes;
    private readonly SynchronizationContext? ownerContext=SynchronizationContext.Current;
    private readonly object sync=new();
    private readonly HashSet<string> active=new(StringComparer.Ordinal);
    public string StagingRoot {get;}
    public string PresetsRoot {get;}=Path.Combine(Storage.Root,"MotionLibrary");
    public List<string> Warnings {get;}=new();
    public DownloadManager(OfficialHttp http,ThemeCatalog themes)
    {
        this.http=http;this.themes=themes;GuardRoot(Storage.Root);var root=Path.Combine(Storage.Root,"Online","Staging");GuardRoot(root);Directory.CreateDirectory(root);
        using var process=System.Diagnostics.Process.GetCurrentProcess();StagingRoot=Path.Combine(root,$"session-{process.Id}-{process.StartTime.ToUniversalTime().Ticks}");
        foreach(var directory in Directory.EnumerateDirectories(root))
        {
            var parts=Path.GetFileName(directory).Split('-');if(parts.Length!=3||parts[0]!="session"||!int.TryParse(parts[1],out int pid)||!long.TryParse(parts[2],out long started))continue;
            bool alive=false;try{using var previous=System.Diagnostics.Process.GetProcessById(pid);alive=previous.StartTime.ToUniversalTime().Ticks==started;}catch(ArgumentException){}catch(System.ComponentModel.Win32Exception){alive=true;}
            if(alive)continue;
            try{GuardTree(directory,0,new int[1]);Directory.Delete(directory,true);Warnings.Add("Cleaned an interrupted download session.");}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidDataException){Warnings.Add("Interrupted staging was preserved: "+ex.Message);}
        }
    }
    public IReadOnlyList<TrailPreset> LoadTrails()
    {
        if(!Directory.Exists(PresetsRoot))return Array.Empty<TrailPreset>();GuardRoot(PresetsRoot);
        var list=new List<TrailPreset>();foreach(var file in Directory.EnumerateFiles(PresetsRoot,"*.json").Take(2001))
        {
            try{if(list.Count>=2000)throw new InvalidDataException("Motion library limit exceeded.");GuardRoot(file);if(new FileInfo(file).Length>256*1024)throw new InvalidDataException("Oversized motion preset.");var preset=JsonSerializer.Deserialize<TrailPreset>(File.ReadAllText(file),Storage.Json)??throw new InvalidDataException("Empty motion installation record.");preset.Validate();if(!preset.ManagedDownload||Path.GetFileNameWithoutExtension(file)!=preset.Id)throw new InvalidDataException("Invalid motion installation record.");list.Add(preset);}catch(Exception ex)when(ex is IOException or InvalidDataException or JsonException or ArgumentException){var warning=Path.GetFileName(file)+": "+ex.Message;if(!Warnings.Contains(warning))Warnings.Add(warning);}
        }
        return list;
    }
    public string State(RemoteResource resource)
    {
        lock(sync){if(active.Contains(resource.Id))return "Downloading";}
        string? version=resource.Type=="cursor"?themes.Themes.FirstOrDefault(t=>t.Id==resource.Id)?.Version:LoadTrails().FirstOrDefault(p=>p.Id==resource.Id)?.Version;
        bool owned=resource.Type=="cursor"&&themes.Themes.Any(t=>t.Id==resource.Id);
        if(owned&&themes.Themes.Any(t=>t.Id==resource.Id&&t.SourceUrl!=resource.SourceUrl))return "ID conflict";
        if(owned&&string.IsNullOrEmpty(version))return "Bundled owned";
        if(version==null)return "Online only";
        return Version.TryParse(version,out var installed)&&installed<Version.Parse(resource.Version)?"Update available":"Installed";
    }
    public async Task<VerifiedResourcePack> FetchPreviewAsync(RemoteResource resource,IProgress<DownloadProgress>? progress,CancellationToken cancellation)
    {
        resource.CheckCompatibility();GuardRoot(StagingRoot);Directory.CreateDirectory(StagingRoot);var archive=Path.Combine(StagingRoot,"download-"+Guid.NewGuid().ToString("N")+".zip");
        try
        {
            for(int attempt=0;;attempt++)
            {
                try{using var file=new FileStream(archive,FileMode.Create,FileAccess.Write,FileShare.None,65536,FileOptions.Asynchronous);await http.FetchAsync(resource.PackageUrl,file,CursorPack.MaxTotalBytes,cancellation,progress,resource.PackageSize).ConfigureAwait(false);break;}
                catch(System.Net.Http.HttpRequestException)when(attempt<1&&!cancellation.IsCancellationRequested){await Task.Delay(400,cancellation).ConfigureAwait(false);}
            }
            cancellation.ThrowIfCancellationRequested();progress?.Report(new("Verifying",resource.PackageSize,resource.PackageSize));
            using(var file=File.OpenRead(archive)){var hash=Convert.ToHexString(await SHA256.HashDataAsync(file,cancellation).ConfigureAwait(false));if(!hash.Equals(resource.SHA256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("The resource SHA256 checksum does not match. Nothing was installed.");}
            return await Task.Run(()=>{cancellation.ThrowIfCancellationRequested();var pack=new VerifiedResourcePack(archive,resource,StagingRoot);try{cancellation.ThrowIfCancellationRequested();return pack;}catch{pack.Dispose();throw;}},cancellation).ConfigureAwait(false);
        }
        finally{if(File.Exists(archive))File.Delete(archive);}
    }
    public async Task InstallAsync(RemoteResource resource,IProgress<DownloadProgress>? progress,CancellationToken cancellation)
    {
        resource.CheckCompatibility();
        if(State(resource)=="ID conflict")throw new InvalidOperationException("This resource ID belongs to a different local theme. Nothing was overwritten.");
        if(resource.Type=="cursor"&&themes.Themes.Any(t=>t.Id==resource.Id&&t.PackageSha256.Equals(resource.SHA256,StringComparison.OrdinalIgnoreCase)))return;
        if(resource.Type=="trail"&&LoadTrails().Any(p=>p.Id==resource.Id&&p.PackageSha256.Equals(resource.SHA256,StringComparison.OrdinalIgnoreCase)))return;
        lock(sync){if(!active.Add(resource.Id))throw new InvalidOperationException("This resource is already downloading.");}
        try
        {
            if(resource.Type=="cursor"&&themes.Themes.Any(t=>t.Id==resource.Id&&!t.ManagedDownload))return;
            using var pack=await FetchPreviewAsync(resource,progress,cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
            if(pack.Cursor!=null){InstallCursor(pack,resource,cancellation);await ReloadCatalogAsync().ConfigureAwait(false);}
            else{GuardRoot(PresetsRoot);Directory.CreateDirectory(PresetsRoot);var record=Path.Combine(PresetsRoot,resource.Id+".json");if(File.Exists(record)){var existing=JsonSerializer.Deserialize<TrailPreset>(File.ReadAllText(record),Storage.Json)??throw new InvalidDataException("Existing preset record is unreadable; preserved.");existing.Validate();if(!existing.ManagedDownload||existing.Id!=resource.Id)throw new InvalidDataException("Preset ID belongs to another installation; preserved.");}Storage.Write(record,pack.Trail);}
            progress?.Report(new("Installed",resource.PackageSize,resource.PackageSize));
        }
        finally{lock(sync){active.Remove(resource.Id);}}
    }
    private Task ReloadCatalogAsync()
    {
        if(ownerContext==null||ownerContext==SynchronizationContext.Current){themes.Reload();return Task.CompletedTask;}
        var completed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);ownerContext.Post(_=>{try{themes.Reload();completed.SetResult();}catch(Exception ex){completed.SetException(ex);}},null);return completed.Task;
    }
    private void InstallCursor(VerifiedResourcePack pack,RemoteResource resource,CancellationToken cancellation)
    {
        var destination=Path.Combine(Storage.Root,"Themes",resource.Id);GuardRoot(destination);Directory.CreateDirectory(destination);
        var record=Path.Combine(destination,"theme.json");if(File.Exists(record)){var existing=ThemeCatalog.ReadTheme(record);if(!existing.ManagedDownload||existing.Id!=resource.Id)throw new InvalidDataException("Existing theme record belongs to another installation; preserved.");}
        // Hash directories are immutable. The root manifest is the atomic installation record.
        var versionDirectory=Path.Combine(destination,resource.SHA256.ToLowerInvariant());bool created=false;
        try
        {
            if(!Directory.Exists(versionDirectory)){Directory.Move(pack.DirectoryPath,versionDirectory);created=true;}
            var installed=ThemeCatalog.ReadTheme(Path.Combine(versionDirectory,"theme.json"));var prefix=resource.SHA256.ToLowerInvariant()+"/";
            installed.Roles=installed.Roles.ToDictionary(p=>p.Key,p=>prefix+p.Value);foreach(var f in installed.SourceFiles)f.Path=prefix+f.Path;if(installed.Preview.Length>0)installed.Preview=prefix+installed.Preview;
            cancellation.ThrowIfCancellationRequested();Storage.Write(Path.Combine(destination,"theme.json"),installed);
        }
        catch{if(created&&Directory.Exists(versionDirectory)&&!File.Exists(Path.Combine(destination,"theme.json")))Directory.Delete(versionDirectory,true);throw;}
    }
    public void RemoveCursor(CursorTheme resource,StudioService studio,ThemeService native)
    {
        var root=Path.GetFullPath(Path.Combine(Storage.Root,"Themes",resource.Id));
        if(!resource.ManagedDownload&&resource.LibraryCollection!="imported"||!Path.GetFullPath(resource.DirectoryPath).Equals(root,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Bundled themes cannot be removed.");
        if(studio.State.Applied.CursorThemeId==resource.Id||studio.Profiles.Profiles.Any(p=>p.Configuration.CursorThemeId==resource.Id))throw new InvalidOperationException("This theme is applied or referenced by a Profile. Switch themes and update those Profiles first.");
        using var key=Registry.CurrentUser.OpenSubKey(ThemeService.KeyPath);
        var backup=File.Exists(native.BackupPath)?JsonSerializer.Deserialize<ThemeBackup>(File.ReadAllText(native.BackupPath),Storage.Json)??throw new InvalidDataException("Original backup is unreadable; removal refused."):null;
        if(backup!=null&&backup.SchemaVersion!=1)throw new InvalidDataException("Original backup version is unsupported; removal refused.");
        var paths=(key?.GetValueNames().Select(n=>key.GetValue(n) as string)??Enumerable.Empty<string?>()).Concat(backup?.Cursors.Where(c=>c.Value.ValueKind==JsonValueKind.String).Select(c=>c.Value.GetString())??Enumerable.Empty<string?>());
        if(paths.Where(p=>p!=null).Any(p=>Environment.ExpandEnvironmentVariables(p!).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Windows or its original backup still references these files.");
        GuardRoot(root);Directory.Delete(root,true);themes.Reload();
        // Never remove Theme/installed: Windows can still reference an older immutable copy.
    }
    public void RemoveTrail(TrailPreset preset,StudioService studio)
    {
        if(!preset.ManagedDownload)throw new InvalidOperationException("Built-in effects cannot be removed.");
        // Profiles store renderer and numeric settings, not a dependency on the preset file.
        var file=Path.Combine(PresetsRoot,preset.Id+".json");GuardRoot(file);if(File.Exists(file))File.Delete(file);
    }
    public static void GuardRoot(string path)
    {
        for(string? current=Path.GetFullPath(path);current!=null;current=Path.GetDirectoryName(current))if((Directory.Exists(current)||File.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked resource storage is unsupported.");
    }
    private static void GuardTree(string path,int depth,int[] count)
    {
        GuardRoot(path);if(depth>16||++count[0]>1024)throw new InvalidDataException("Staging cleanup exceeded its bounded tree limits.");
        if(Directory.Exists(path))foreach(var child in Directory.EnumerateFileSystemEntries(path))GuardTree(child,depth+1,count);
    }
}
