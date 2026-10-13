using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frost.Core;

public sealed class CursorTheme
{
    public int SchemaVersion {get;set;}=1;
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public string Author {get;set;}="";
    public string Description {get;set;}="";
    public string Preview {get;set;}="";
    public Dictionary<string,string> Roles {get;set;}=new();
    public string SourceUrl {get;set;}="";
    public string License {get;set;}="";
    public string Collection {get;set;}="";
    public bool IsAnimated {get;set;}
    public DateTimeOffset? ImportedAt {get;set;}
    public string PublishedAt {get;set;}="";
    public string PackageSha256 {get;set;}="";
    public Dictionary<string,string> InheritedRoles {get;set;}=new();
    public List<ThemeSourceFile> SourceFiles {get;set;}=new();
    [JsonIgnore] public int NativeRoleCount=>IsSystem?0:Roles.Count-InheritedRoles.Count;
    [JsonIgnore] public int AnimatedRoleCount=>Roles.Values.Count(p=>p.EndsWith(".ani",StringComparison.OrdinalIgnoreCase));
    [JsonIgnore] public string LibraryCollection=>Collection.Length>0?Collection:Id.StartsWith("import-")?"imported":"built-in";
    [JsonIgnore] public string RoleSummary=>IsSystem?"Windows original":$"{NativeRoleCount}/15 native · {InheritedRoles.Count} inherited";
    [JsonIgnore] public string DirectoryPath {get;set;}="";
    [JsonIgnore] public bool IsSystem=>Id=="system";
    public string Resource(string role)=>ThemeCatalog.SafeResource(DirectoryPath,Roles[role]);
    public override string ToString()=>Name;
}

public sealed class ThemeSourceFile
{
    public string OriginalName {get;set;}="";
    public string Path {get;set;}="";
    public string Sha256 {get;set;}="";
}

public sealed class TrailPreset
{
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public string Description {get;set;}="";
    public double Length {get;set;}
    public double Duration {get;set;}
    public double Opacity {get;set;}
    public int Particles {get;set;}
    public string Color {get;set;}="#91DEFF";
    public void Apply(Settings settings)
    {
        settings.TrailEffect=Id;settings.Length=Length;settings.Duration=Duration;settings.Opacity=Opacity;settings.Particles=Particles;settings.Color=Color;settings.Preset=Name;
    }
    public override string ToString()=>Name;
}
public sealed class EffectCatalog
{
    public int SchemaVersion {get;set;}=1;
    public List<TrailPreset> Trails {get;set;}=new();
    public static EffectCatalog Load(string path)
    {
        var result=JsonSerializer.Deserialize<EffectCatalog>(File.ReadAllText(path),Storage.Json)??throw new InvalidDataException("Empty effect catalog.");
        if(result.SchemaVersion!=1||result.Trails.Count!=4||result.Trails.Select(p=>p.Id).Distinct().Count()!=4)throw new InvalidDataException("Unsupported effect catalog.");
        foreach(var p in result.Trails)
        {
            if(p.Id is not ("comet" or "minimal" or "neon" or "stardust")||!Settings.ValidColor(p.Color)||!double.IsFinite(p.Length)||p.Length<10||p.Length>360||!double.IsFinite(p.Duration)||p.Duration<80||p.Duration>900||!double.IsFinite(p.Opacity)||p.Opacity<.05||p.Opacity>.85||p.Particles<0||p.Particles>24)
                throw new InvalidDataException("Invalid effect preset: "+p.Id);
        }
        return result;
    }
}

public sealed class StudioConfiguration
{
    public string CursorThemeId {get;set;}="system";
    public Settings Effects {get;set;}=new();
    public StudioConfiguration Clone()=>new(){CursorThemeId=CursorThemeId,Effects=Effects.Clone()};
}
public sealed class StudioState
{
    public int SchemaVersion {get;set;}=1;
    public StudioConfiguration Applied {get;set;}=new();
    public string? ProfileId {get;set;}
    public DateTimeOffset? AppliedAt {get;set;}
}
public sealed class StudioProfile
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Name {get;set;}="";
    public StudioConfiguration Configuration {get;set;}=new();
    public override string ToString()=>Name;
}
public sealed class ProfileDocument
{
    public int SchemaVersion {get;set;}=1;
    public List<StudioProfile> Profiles {get;set;}=new();
}

public sealed class ProfileManager
{
    private readonly string path=Path.Combine(Storage.Root,"profiles.json");
    private ProfileDocument document=new();
    public IReadOnlyList<StudioProfile> Profiles=>document.Profiles;
    public ProfileManager()
    {
        if(!File.Exists(path))return;
        document=JsonSerializer.Deserialize<ProfileDocument>(File.ReadAllText(path),Storage.Json)??throw new InvalidDataException("Invalid profile file.");
        if(document.SchemaVersion!=1||document.Profiles.Any(p=>string.IsNullOrWhiteSpace(p.Id)||p.Configuration?.Effects==null)||document.Profiles.Select(p=>p.Id).Distinct().Count()!=document.Profiles.Count)throw new InvalidDataException("Unsupported profile data. The original file was preserved.");
    }
    public StudioProfile Save(string? id,string name,StudioConfiguration configuration)
    {
        name=name.Trim();if(name.Length is <1 or >80)throw new ArgumentException("Profile name must contain 1–80 characters.");
        var next=JsonSerializer.Deserialize<ProfileDocument>(JsonSerializer.Serialize(document,Storage.Json),Storage.Json)!;
        var profile=id==null?null:next.Profiles.SingleOrDefault(p=>p.Id==id)??throw new ArgumentException("Profile no longer exists.");
        if(profile==null){profile=new();next.Profiles.Add(profile);}
        profile.Name=name;profile.Configuration=configuration.Clone();Storage.Write(path,next);document=next;return profile;
    }
    public void Rename(string id,string name)
    {
        var p=document.Profiles.Single(p=>p.Id==id);Save(id,name,p.Configuration);
    }
    public void Delete(string id)
    {
        var next=new ProfileDocument{Profiles=document.Profiles.Where(p=>p.Id!=id).ToList()};Storage.Write(path,next);document=next;
    }
}
