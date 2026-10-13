using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Frost.Core;

public sealed record PackCursor(string OriginalName,string Path,string? SuggestedRole)
{
    public override string ToString()=>OriginalName;
}

// Only stages cursor bytes. Caller reviews roles, then commits through ThemeCatalog.Import.
public sealed class CursorPack:IDisposable
{
    public const int MaxEntries=256,MaxFiles=128;
    public const long MaxTotalBytes=64*1024*1024;
    private readonly string staging=System.IO.Path.Combine(Storage.Root,"ImportStaging","pack-"+Guid.NewGuid().ToString("N"));
    public List<PackCursor> Files {get;}=new();
    private long total;
    private CursorPack(){Directory.CreateDirectory(staging);}
    public static CursorPack Open(string path)
    {
        var pack=new CursorPack();
        try
        {
            if(Directory.Exists(path))pack.ReadDirectory(path,path,0,new int[1]);
            else
            {
                if(!System.IO.Path.GetExtension(path).Equals(".zip",StringComparison.OrdinalIgnoreCase)||new FileInfo(path).Length>MaxTotalBytes)throw new InvalidDataException("Choose a ZIP up to 64 MiB.");
                ValidateDirectoryHeader(path);using var zip=ZipFile.OpenRead(path);if(zip.Entries.Count>MaxEntries)throw new InvalidDataException("Too many ZIP entries.");
                foreach(var entry in zip.Entries)
                {
                    var name=entry.FullName.Replace('\\','/');var parts=name.Split('/');
                    if(name.Length>512||name.StartsWith('/')||name.Contains(':')||parts.Any(p=>p is "." or "..")||parts.Any(p=>p.IndexOfAny(new[]{'\0','<','>','|','?','*'})>=0)||(entry.ExternalAttributes&0x400)!=0||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("Unsafe or linked ZIP path: "+name);
                    if(!IsCursor(name))continue;
                    if(entry.Length>CursorResource.MaxBytes||entry.Length<22||entry.Length>Math.Max(1,entry.CompressedLength)*200)throw new InvalidDataException("Unsafe cursor size or compression ratio.");
                    using var input=entry.Open();pack.Stage(name,input,entry.Length);
                }
            }
            if(pack.Files.Count==0)throw new InvalidDataException("No cursor resources found.");return pack;
        }
        catch{pack.Dispose();throw;}
    }
    private static bool IsCursor(string name)=>System.IO.Path.GetExtension(name).ToLowerInvariant() is ".cur" or ".ani";
    private static void ValidateDirectoryHeader(string path)
    {
        // Bound entry count before ZipArchive allocates its central-directory objects.
        using var file=File.OpenRead(path);var tail=new byte[(int)Math.Min(file.Length,65557)];file.Position=file.Length-tail.Length;file.ReadExactly(tail);
        for(int i=tail.Length-22;i>=0;i--)
        {
            if(BitConverter.ToUInt32(tail,i)!=0x06054B50||i+22+BitConverter.ToUInt16(tail,i+20)!=tail.Length)continue;
            int entries=BitConverter.ToUInt16(tail,i+10);
            if(BitConverter.ToUInt16(tail,i+4)!=0||BitConverter.ToUInt16(tail,i+6)!=0||BitConverter.ToUInt16(tail,i+8)!=entries||entries>MaxEntries||BitConverter.ToUInt32(tail,i+12)==uint.MaxValue||BitConverter.ToUInt32(tail,i+16)==uint.MaxValue)throw new InvalidDataException("Multipart, ZIP64 or oversized directories are unsupported.");
            return;
        }
        throw new InvalidDataException("Invalid ZIP directory header.");
    }
    private void ReadDirectory(string root,string directory,int depth,int[] count)
    {
        if(depth>8||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked or deeply nested folders are unsupported.");
        foreach(var item in Directory.EnumerateFileSystemEntries(directory))
        {
            if(++count[0]>MaxEntries)throw new InvalidDataException("Too many folder entries.");
            var attributes=File.GetAttributes(item);if((attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked resources are unsupported.");
            if((attributes&FileAttributes.Directory)!=0)ReadDirectory(root,item,depth+1,count);
            else if(IsCursor(item)){using var input=File.OpenRead(item);Stage(System.IO.Path.GetRelativePath(root,item),input,input.Length);}
        }
    }
    private void Stage(string original,Stream input,long length)
    {
        if(length is <22 or >CursorResource.MaxBytes||Files.Count>=MaxFiles||(total+=length)>MaxTotalBytes)throw new InvalidDataException("Cursor pack resource limits exceeded.");
        var path=System.IO.Path.Combine(staging,Files.Count+System.IO.Path.GetExtension(original).ToLowerInvariant());
        using(var output=File.Create(path))
        {
            var buffer=new byte[81920];long written=0;int read;
            while((read=input.Read(buffer))>0){written+=read;if(written>length)throw new InvalidDataException("Unexpected expanded ZIP size.");output.Write(buffer,0,read);}
            if(written!=length)throw new InvalidDataException("Truncated ZIP resource.");
        }
        CursorResource.Validate(path);Files.Add(new(original,path,CursorRoleRecognition.Match(original)));
    }
    public Dictionary<string,string[]> Candidates()=>CursorRoles.All.ToDictionary(r=>r.Name,r=>Files.Where(f=>f.SuggestedRole==r.Name).Select(f=>f.Path).ToArray());
    public void Dispose(){if(Directory.Exists(staging))Directory.Delete(staging,true);}
}

public static class CursorRoleRecognition
{
    public static string? Match(string filename)
    {
        var leaf=filename.Replace('\\','/').Split('/').Last();
        var name=Regex.Replace(leaf.ToLowerInvariant(),@"\.(cur|ani)$","");
        var tokens=Regex.Replace(name,@"[^a-z0-9]+"," ").Trim();
        foreach(var role in CursorRoles.All)if(tokens.Equals(role.Name,StringComparison.OrdinalIgnoreCase))return role.Name;
        bool Has(string pattern)=>Regex.IsMatch(tokens,@"(^| )("+pattern+@")( |$)");
        if(Has("diagonal"))return Has("nwse|1")?"DiagonalNWSE":Has("nesw|2")?"DiagonalNESW":null;
        var matches=new List<string>();
        foreach(var (pattern,role) in new[]{("normal|arrow|default","Normal"),("help","Help"),("working|background|appstarting","Working"),("busy|wait","Busy"),("precision|crosshair|cross","Precision"),("text|ibeam","Text"),("handwriting|pen|nwpen","Handwriting"),("unavailable|unavailble|unavailiable|no|forbidden","Unavailable"),("vertical|sizens","Vertical"),("horizontal|sizewe","Horizontal"),("move|sizeall","Move"),("alternate|alt|uparrow","Alternate"),("link|hand","Link")})if(Has(pattern))matches.Add(role);
        return matches.Count==1?matches[0]:null;
    }
}
