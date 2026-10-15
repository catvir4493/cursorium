using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Frost.Core;

// No Settings object is serialized here: machine preferences are intentionally absent.
public sealed class PortableEffects
{
    public bool Enabled {get;set;}=true;
    public bool TrailEnabled {get;set;}=true;
    public string Renderer {get;set;}="comet";
    public double Length {get;set;}=110;
    public double FadeDuration {get;set;}=260;
    public double GlowIntensity {get;set;}=.4;
    public int ParticleDensity {get;set;}=10;
    public string Color {get;set;}="#91DEFF";
    public string Click {get;set;}="halo";
    public double ClickDuration {get;set;}=180;
    public double ClickOpacity {get;set;}=.55;
    public double ClickRadius {get;set;}=23;
    public void Validate(){if(Renderer is not ("comet" or "minimal" or "neon" or "stardust")||Click is not ("off" or "halo" or "ring")||!Settings.ValidColor(Color)||!double.IsFinite(Length)||Length is <10 or >360||!double.IsFinite(FadeDuration)||FadeDuration is <80 or >900||!double.IsFinite(GlowIntensity)||GlowIntensity is <.05 or >.85||ParticleDensity is <0 or >24||!double.IsFinite(ClickDuration)||ClickDuration is <80 or >500||!double.IsFinite(ClickOpacity)||ClickOpacity is <.05 or >.85||!double.IsFinite(ClickRadius)||ClickRadius is <8 or >60)throw new InvalidDataException("Invalid portable visual parameters.");}
    public Settings ToSettings(){Validate();return new(){Enabled=Enabled,TrailEnabled=TrailEnabled,TrailEffect=Renderer,Length=Length,Duration=FadeDuration,Opacity=GlowIntensity,Particles=ParticleDensity,Color=Color,ClickEffect=Click,ClickHalo=Click!="off",HaloDuration=ClickDuration,ClickOpacity=ClickOpacity,ClickRadius=ClickRadius};}
    public static PortableEffects From(Settings s)=>new(){Enabled=s.Enabled,TrailEnabled=s.TrailEnabled,Renderer=s.TrailEffect,Length=s.Length,FadeDuration=s.Duration,GlowIntensity=s.Opacity,ParticleDensity=s.Particles,Color=s.Color,Click=s.ClickEffect,ClickDuration=s.HaloDuration,ClickOpacity=s.ClickOpacity,ClickRadius=s.ClickRadius};
}
public sealed class PortableResourceReference
{
    public string StableId {get;set;}="system";
    public string? Version {get;set;}
    public string? PackageSha256 {get;set;}
}
public sealed class PortableProfile
{
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public PortableResourceReference Cursor {get;set;}=new();
    public PortableEffects Effects {get;set;}=new();
}
public sealed class PortableWorkspace
{
    public int SchemaVersion {get;set;}=1;
    public List<PortableProfile> Profiles {get;set;}=new();
    public List<string> Favorites {get;set;}=new();
}
public sealed record PortableExport(PortableWorkspace Document,int ExcludedProfiles,int ExcludedFavorites);
public sealed record PortableResolution(string ProfileId,string Status,StudioConfiguration? Draft);
public static class PortableWorkspaceService
{
    private static readonly JsonSerializerOptions Strict=new(Storage.Json){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,MaxDepth=16};
    private static bool Id(string? s)=>s!=null&&Regex.IsMatch(s,"^[a-zA-Z0-9][a-zA-Z0-9-]{0,79}$")&&!s.StartsWith("import-",StringComparison.Ordinal);
    private static bool Label(string? s)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=80&&!s.Any(c=>char.IsControl(c)||c is '/' or '\\' or ':')&&!Regex.IsMatch(s,"(?i)(token|password|secret|bearer)\\s*[:= ]");
    public static void Validate(PortableWorkspace document)
    {
        if(document.SchemaVersion!=1||document.Profiles==null||document.Profiles.Count>256||document.Favorites==null||document.Favorites.Count>2000||document.Favorites.Any(s=>!Id(s))||document.Favorites.Distinct().Count()!=document.Favorites.Count)throw new InvalidDataException("Unsupported or unsafe portable workspace.");
        var ids=new HashSet<string>();foreach(var p in document.Profiles){if(p==null||!Id(p.Id)||!ids.Add(p.Id)||!Label(p.Name)||p.Cursor==null||!Id(p.Cursor.StableId)||p.Cursor.Version!=null&&!Version.TryParse(p.Cursor.Version,out _)||p.Cursor.PackageSha256!=null&&!ResourceRights.Hash(p.Cursor.PackageSha256)||p.Effects==null)throw new InvalidDataException("Unsafe portable profile or resource reference.");p.Effects.Validate();}
    }
    public static PortableWorkspace Read(byte[] bytes){var doc=FoundationJson.Read<PortableWorkspace>(bytes,1024*1024,16);Validate(doc);return doc;}
    public static PortableExport Create(IEnumerable<StudioProfile> profiles,IEnumerable<string> favorites,ThemeCatalog local,RemoteCatalog? official)
    {
        var allowed=new HashSet<string>(new[]{"system","frost","obsidian"});if(official!=null)foreach(var resource in official.Resources)allowed.Add(resource.Id);
        var result=new PortableWorkspace();int excluded=0;
        foreach(var profile in profiles){var cfg=profile.Configuration;if(!allowed.Contains(cfg.CursorThemeId)||!Id(profile.Id)||!Label(profile.Name)){excluded++;continue;}
            var theme=local.Themes.FirstOrDefault(t=>t.Id==cfg.CursorThemeId);if(theme==null){excluded++;continue;}
            if(cfg.CursorPackageSha256!=null&&!cfg.CursorPackageSha256.Equals(theme.PackageSha256,StringComparison.OrdinalIgnoreCase)){
                try{
                    if(!ResourceRights.Hash(cfg.CursorPackageSha256))throw new InvalidDataException("Invalid pinned digest.");
                    var retained=ThemeCatalog.ReadTheme(ThemeCatalog.SafeResource(theme.DirectoryPath,cfg.CursorPackageSha256.ToLowerInvariant()+"/theme.json"));
                    if(retained.Id!=theme.Id||retained.Author!=theme.Author||retained.SourceUrl!=theme.SourceUrl||!cfg.CursorPackageSha256.Equals(retained.PackageSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Pinned identity mismatch.");theme=retained;
                }catch(Exception ex)when(ex is IOException or InvalidDataException or JsonException or ArgumentException){excluded++;continue;}
            }
            result.Profiles.Add(new(){Id=profile.Id,Name=profile.Name,Cursor=new(){StableId=cfg.CursorThemeId,Version=Version.TryParse(theme.Version,out _)?theme.Version:null,PackageSha256=cfg.CursorPackageSha256??(theme.ManagedDownload?theme.PackageSha256:null)},Effects=PortableEffects.From(cfg.Effects)});
        }
        var all=favorites.Distinct().ToArray();result.Favorites=all.Where(id=>allowed.Contains(id)&&Id(id)).ToList();Validate(result);return new(result,excluded,all.Length-result.Favorites.Count);
    }
    public static IReadOnlyList<PortableResolution> Resolve(PortableWorkspace document,ThemeCatalog local)
    {
        Validate(document);var results=new List<PortableResolution>();foreach(var p in document.Profiles){
            var theme=local.Themes.FirstOrDefault(t=>t.Id==p.Cursor.StableId);
            if(theme==null){results.Add(new(p.Id,"Missing resource",null));continue;}
            if(p.Cursor.PackageSha256!=null&&!p.Cursor.PackageSha256.Equals(theme.PackageSha256,StringComparison.OrdinalIgnoreCase)){
                try{var retained=ThemeCatalog.ReadTheme(ThemeCatalog.SafeResource(theme.DirectoryPath,p.Cursor.PackageSha256.ToLowerInvariant()+"/theme.json"));if(retained.Id!=theme.Id||retained.Author!=theme.Author||retained.SourceUrl!=theme.SourceUrl||!p.Cursor.PackageSha256.Equals(retained.PackageSha256,StringComparison.OrdinalIgnoreCase)||p.Cursor.Version!=null&&retained.Version!=p.Cursor.Version)throw new InvalidDataException("Retained identity mismatch.");}
                catch(Exception ex)when(ex is IOException or InvalidDataException or JsonException or ArgumentException){results.Add(new(p.Id,"Missing pinned version",null));continue;}
            }else if(p.Cursor.Version!=null&&p.Cursor.Version!=theme.Version){results.Add(new(p.Id,"Missing pinned version",null));continue;}
            results.Add(new(p.Id,"Draft available; not applied",new(){CursorThemeId=p.Cursor.StableId,CursorPackageSha256=p.Cursor.PackageSha256,Effects=p.Effects.ToSettings()}));
        }return results;
    }
}
