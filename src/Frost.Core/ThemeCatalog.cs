using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Frost.Core;

public sealed class ThemeCatalog
{
    private readonly string applicationDirectory;
    public List<CursorTheme> Themes {get;}=new();
    public List<string> Warnings {get;}=new();
    public ThemeCatalog(string applicationDirectory){this.applicationDirectory=applicationDirectory;Reload();}
    public CursorTheme Find(string id)=>Themes.SingleOrDefault(t=>t.Id==id)??throw new InvalidOperationException("Cursor theme is unavailable: "+id);
    public void Reload()
    {
        Themes.Clear();Warnings.Clear();Themes.Add(new(){Id="system",Name="Original system",Author="Windows",Description="Keep or restore your original Windows pointer configuration."});
        var manifests=new List<string>();
        var frost=Path.Combine(applicationDirectory,"Theme","theme.json");if(File.Exists(frost))manifests.Add(frost);
        foreach(var root in new[]{Path.Combine(applicationDirectory,"Themes"),Path.Combine(Storage.Root,"Themes")})
            if(Directory.Exists(root))manifests.AddRange(Directory.EnumerateDirectories(root).Select(d=>Path.Combine(d,"theme.json")).Where(File.Exists));
        foreach(var path in manifests)
        {
            try
            {
                var theme=ReadTheme(path);if(Themes.Any(t=>t.Id==theme.Id))throw new InvalidDataException("Duplicate theme ID: "+theme.Id);
                Themes.Add(theme);
            }
            catch(Exception ex)when(ex is IOException or InvalidDataException or JsonException or ArgumentException or UnauthorizedAccessException){Warnings.Add(Path.GetFileName(Path.GetDirectoryName(path))+": "+ex.Message);}
        }
    }
    public static CursorTheme ReadTheme(string path)
    {
        var theme=JsonSerializer.Deserialize<CursorTheme>(File.ReadAllText(path),Storage.Json)??throw new InvalidDataException("Empty theme manifest.");
        theme.DirectoryPath=Path.GetDirectoryName(Path.GetFullPath(path))!;
        theme.Author??="";theme.Description??="";theme.Preview??="";theme.SourceUrl??="";theme.License??="";theme.Collection??="";theme.PublishedAt??="";
        if(theme.SchemaVersion!=1||!Regex.IsMatch(theme.Id,"^[a-z0-9][a-z0-9-]{0,79}$")||theme.Id=="system"||string.IsNullOrWhiteSpace(theme.Name)||theme.Name.Length>100||theme.Roles==null||theme.Roles.Count!=CursorRoles.All.Length)throw new InvalidDataException("Invalid theme metadata or schema.");
        foreach(var role in CursorRoles.All){if(!theme.Roles.ContainsKey(role.Name))throw new InvalidDataException("Missing role: "+role.Name);CursorResource.Validate(theme.Resource(role.Name));}
        if(theme.InheritedRoles==null||theme.SourceFiles==null||theme.SourceFiles.Count>CursorPack.MaxFiles||theme.SourceFiles.Any(f=>f==null)||theme.InheritedRoles.Keys.Any(r=>!theme.Roles.ContainsKey(r)))throw new InvalidDataException("Invalid role provenance.");
        foreach(var file in theme.SourceFiles)
        {
            var resource=SafeResource(theme.DirectoryPath,file.Path);CursorResource.Validate(resource);
            if(!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(resource))),file.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Source cursor checksum mismatch: "+file.OriginalName);
        }
        theme.IsAnimated=theme.AnimatedRoleCount>0;
        if(theme.Preview.Length>0)SafeResource(theme.DirectoryPath,theme.Preview);
        return theme;
    }
    public static string SafeResource(string root,string relative)
    {
        if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(':'))throw new InvalidDataException("Theme paths must be relative.");
        var folder=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var path=Path.GetFullPath(Path.Combine(folder,relative));
        if(!path.StartsWith(folder,StringComparison.OrdinalIgnoreCase)||!File.Exists(path))throw new InvalidDataException("Theme resource is missing or outside its directory.");
        for(var current=path;current.Length>=folder.TrimEnd(Path.DirectorySeparatorChar).Length;current=Path.GetDirectoryName(current)!)
        {if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked theme resources are not supported.");if(string.Equals(current,folder.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))break;}
        return path;
    }
    public CursorTheme Import(string name,CursorTheme baseTheme,IReadOnlyDictionary<string,string> replacements,IReadOnlyList<PackCursor>? sourceFiles=null)
    {
        name=name.Trim();if(name.Length is <1 or >100||baseTheme.IsSystem||replacements.Count==0)throw new ArgumentException("Choose a base theme, a name, and at least one cursor file.");
        foreach(var item in replacements){if(!CursorRoles.All.Any(r=>r.Name==item.Key))throw new ArgumentException("Unknown cursor role.");CursorResource.Validate(item.Value);}
        var id="import-"+Guid.NewGuid().ToString("N");var destination=Path.Combine(Storage.Root,"Themes",id);var directory=Path.Combine(Storage.Root,"ImportStaging",id);Directory.CreateDirectory(directory);
        try
        {
            var theme=new CursorTheme{Id=id,Name=name,Author="Local import",Description="Imported cursor resources; other roles inherit "+baseTheme.Name,Collection="imported",ImportedAt=DateTimeOffset.Now,DirectoryPath=directory};
            foreach(var role in CursorRoles.All)
            {
                var source=replacements.TryGetValue(role.Name,out var path)?path:baseTheme.Resource(role.Name);CursorResource.Validate(source);
                var file=role.Name+Path.GetExtension(source).ToLowerInvariant();File.Copy(source,Path.Combine(directory,file));theme.Roles.Add(role.Name,file);
                if(replacements.ContainsKey(role.Name)&&sourceFiles==null)theme.SourceFiles.Add(new(){OriginalName=Path.GetFileName(source),Path=file,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)))});
                else if(!replacements.ContainsKey(role.Name))theme.InheritedRoles.Add(role.Name,baseTheme.Id);
            }
            // Pack provenance includes unused/bonus cursors, without treating them as standard roles.
            if(sourceFiles!=null)
            {
                if(sourceFiles.Count>CursorPack.MaxFiles)throw new InvalidDataException("Too many source resources.");
                theme.InheritedRoles.Clear();foreach(var role in CursorRoles.All.Where(r=>!replacements.ContainsKey(r.Name)))theme.InheritedRoles.Add(role.Name,baseTheme.Id);
                int extra=0;
                foreach(var source in sourceFiles)
                {
                    CursorResource.Validate(source.Path);var mapping=replacements.FirstOrDefault(r=>r.Value==source.Path);
                    var file=mapping.Key!=null?theme.Roles[mapping.Key]:"Extras/"+(extra++)+Path.GetExtension(source.Path).ToLowerInvariant();
                    if(mapping.Key==null){Directory.CreateDirectory(Path.Combine(directory,"Extras"));File.Copy(source.Path,Path.Combine(directory,file));}
                    theme.SourceFiles.Add(new(){OriginalName=source.OriginalName,Path=file,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source.Path)))});
                }
            }
            Storage.Write(Path.Combine(directory,"theme.json"),theme);ReadTheme(Path.Combine(directory,"theme.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);Directory.Move(directory,destination);Reload();return Find(id);
        }
        catch
        {
            // This directory has a freshly generated ID and only files written by this operation.
            if(Directory.Exists(directory))Directory.Delete(directory,true);throw;
        }
    }
}

public static class CursorResource
{
    public static System.Windows.Input.Cursor OpenPreviewCursor(string path)
    {
        if(Path.GetFullPath(path).Length<260)return new System.Windows.Input.Cursor(path);
        using var stream=File.OpenRead(path);return new System.Windows.Input.Cursor(stream);
    }
    public const int MaxBytes=32*1024*1024;
    public static void Validate(string path)
    {
        var extension=Path.GetExtension(path).ToLowerInvariant();if(extension is not (".cur" or ".ani"))throw new InvalidDataException("Only .cur and .ani files can be imported.");
        var info=new FileInfo(path);if(!info.Exists||info.Length<22||info.Length>MaxBytes)throw new InvalidDataException("Invalid cursor file size.");
        var bytes=File.ReadAllBytes(path);
        if(extension==".cur")ValidateCur(bytes,false);else ValidateAni(bytes);
        var handle=Native.LoadOwnedCursor(path);if(handle==IntPtr.Zero)throw new InvalidDataException("Windows could not load cursor: "+Path.GetFileName(path));
        Native.DestroyCursor(handle);
    }
    private static void ValidateCur(ReadOnlySpan<byte> bytes,bool allowIcon)
    {
        if(bytes.Length<6||BitConverter.ToUInt16(bytes)!=0)throw new InvalidDataException("Invalid cursor header.");
        var type=BitConverter.ToUInt16(bytes[2..]);int count=BitConverter.ToUInt16(bytes[4..]);
        if((type!=2&&!(allowIcon&&type==1))||count is <1 or >256||6L+16L*count>bytes.Length)throw new InvalidDataException("Invalid cursor directory.");
        for(int i=0;i<count;i++)
        {
            var entry=bytes.Slice(6+16*i,16);int width=entry[0]==0?256:entry[0],height=entry[1]==0?256:entry[1];
            uint size=BitConverter.ToUInt32(entry[8..]),offset=BitConverter.ToUInt32(entry[12..]);
            if(size<12||offset<6+16*count||(long)offset+size>bytes.Length||(type==2&&(BitConverter.ToUInt16(entry[4..])>=width||BitConverter.ToUInt16(entry[6..])>=height)))throw new InvalidDataException("Invalid image bounds or cursor hotspot.");
            var image=bytes.Slice((int)offset,(int)size);
            bool png=image.Length>=24&&image[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});
            if(!png&&(image.Length<40||BitConverter.ToUInt32(image)<40))throw new InvalidDataException("Unsupported cursor image format.");
        }
    }
    private static void ValidateAni(ReadOnlySpan<byte> bytes)
    {
        AniTimeline.Read(bytes);
        if(bytes.Length<12||!bytes[..4].SequenceEqual("RIFF"u8)||BitConverter.ToUInt32(bytes[4..])!=bytes.Length-8||!bytes.Slice(8,4).SequenceEqual("ACON"u8))throw new InvalidDataException("Invalid ANI RIFF header.");
        int frames=0,declared=0;bool header=false;
        for(int i=12;i<bytes.Length;)
        {
            if(i+8>bytes.Length)throw new InvalidDataException("Truncated ANI chunk.");
            uint size=BitConverter.ToUInt32(bytes[(i+4)..]);long end=(long)i+8+size;if(end>bytes.Length)throw new InvalidDataException("Truncated ANI data.");
            var chunk=bytes.Slice(i+8,(int)size);
            if(bytes.Slice(i,4).SequenceEqual("anih"u8))
            {
                if(size<36||BitConverter.ToUInt32(chunk)!=36)throw new InvalidDataException("Invalid ANI animation header.");
                declared=(int)BitConverter.ToUInt32(chunk[4..]);uint steps=BitConverter.ToUInt32(chunk[8..]);
                if(declared is <1 or >256||steps is <1 or >4096)throw new InvalidDataException("ANI frame limit exceeded.");header=true;
            }
            if(bytes.Slice(i,4).SequenceEqual("LIST"u8)&&chunk.Length>=4&&chunk[..4].SequenceEqual("fram"u8))
            {
                for(int f=4;f<chunk.Length;)
                {
                    if(f+8>chunk.Length)throw new InvalidDataException("Truncated ANI frame.");uint length=BitConverter.ToUInt32(chunk[(f+4)..]);
                    if((long)f+8+length>chunk.Length||!chunk.Slice(f,4).SequenceEqual("icon"u8))throw new InvalidDataException("Invalid ANI frame.");
                    ValidateCur(chunk.Slice(f+8,(int)length),true);frames++;if(frames>256)throw new InvalidDataException("ANI frame limit exceeded.");f=checked(f+8+(int)length+((int)length&1));
                }
            }
            i=checked((int)end+((int)size&1));
        }
        if(!header||frames!=declared)throw new InvalidDataException("ANI frame count mismatch.");
    }
}
