using System.Text.Json;

namespace Frost.Core;

public sealed class StudioService
{
    private readonly ThemeService themes;
    public ThemeCatalog Catalog {get;}
    public EffectCatalog Effects {get;}
    public ProfileManager Profiles {get;}
    public StudioState State {get;private set;}
    public Settings LiveEffects {get;}=new();
    public string? LoadWarning {get;private set;}
    public static string StatePath=>Path.Combine(Storage.Root,"studio.json");
    public StudioService(string applicationDirectory,ThemeService themes)
    {
        this.themes=themes;themes.Recover();Catalog=new(applicationDirectory);Effects=EffectCatalog.Load(Path.Combine(applicationDirectory,"Catalog","effects.json"));Profiles=new();
        if(File.Exists(StatePath))
        {
            State=JsonSerializer.Deserialize<StudioState>(File.ReadAllText(StatePath),Storage.Json)??throw new InvalidDataException("Invalid studio state.");
            if(State.SchemaVersion!=1||State.Applied?.Effects==null)throw new InvalidDataException("Unsupported studio state schema; original file preserved.");
        }
        else State=new(){Applied=new(){CursorThemeId=themes.IsInstalled?"frost":"system",Effects=Settings.Load()}};
        State.Applied.Effects.Normalize();LiveEffects.CopyFrom(State.Applied.Effects);
        if(Catalog.Themes.All(t=>t.Id!=State.Applied.CursorThemeId))LoadWarning="The last cursor theme is unavailable. Import it again or select another theme before Apply.";
    }
    public void Validate(StudioConfiguration configuration)
    {
        Catalog.Find(configuration.CursorThemeId);var s=configuration.Effects;
        if(!Effects.Trails.Any(p=>p.Id==s.TrailEffect)||s.ClickEffect is not ("off" or "halo" or "ring")||!Settings.ValidColor(s.Color))throw new ArgumentException("Select a valid effect and a #RRGGBB color.");
        if(!double.IsFinite(s.Length)||!double.IsFinite(s.Duration)||!double.IsFinite(s.Opacity)||!double.IsFinite(s.HaloDuration)||!double.IsFinite(s.ClickOpacity)||!double.IsFinite(s.ClickRadius))throw new ArgumentException("Effect parameters must be finite.");
        if(s.Length<10||s.Length>360||s.Duration<80||s.Duration>900||s.Opacity<.05||s.Opacity>.85||s.Particles<0||s.Particles>24||s.HaloDuration<80||s.HaloDuration>500||s.ClickOpacity<.05||s.ClickOpacity>.85||s.ClickRadius<8||s.ClickRadius>60)throw new ArgumentException("Effect parameters are outside the supported range.");
    }
    public void Apply(StudioConfiguration configuration,string? profileId=null)
    {
        Validate(configuration);var selected=Catalog.Find(configuration.CursorThemeId);
        var next=new StudioState{Applied=configuration.Clone(),ProfileId=profileId,AppliedAt=DateTimeOffset.Now};
        themes.ApplyTheme(selected,()=>Storage.Write(StatePath,next));
        State=next;LiveEffects.CopyFrom(next.Applied.Effects);LoadWarning=null;
    }
    public void RestoreOriginal()
    {
        var next=new StudioState{Applied=State.Applied.Clone(),AppliedAt=DateTimeOffset.Now};next.Applied.CursorThemeId="system";
        themes.Restore(()=>Storage.Write(StatePath,next));State=next;
    }
    public static void RestoreSavedState(ThemeService themes)
    {
        themes.Recover();
        StudioState? next=null;
        if(File.Exists(StatePath))
        {
            try
            {
                next=JsonSerializer.Deserialize<StudioState>(File.ReadAllText(StatePath),Storage.Json);
                if(next?.SchemaVersion!=1||next.Applied?.Effects==null)next=null;
                if(next!=null){next.Applied.CursorThemeId="system";next.ProfileId=null;next.AppliedAt=DateTimeOffset.Now;}
            }
            catch(JsonException){Storage.Log("Invalid studio state preserved during cursor restoration.");}
        }
        themes.Restore(()=>{if(next!=null)Storage.Write(StatePath,next);});
    }
    public void SetEffectsEnabled(bool enabled)
    {
        var next=new StudioState{Applied=State.Applied.Clone(),ProfileId=State.ProfileId,AppliedAt=State.AppliedAt};next.Applied.Effects.Enabled=enabled;
        Storage.Write(StatePath,next);State=next;LiveEffects.CopyFrom(next.Applied.Effects);
    }
}
