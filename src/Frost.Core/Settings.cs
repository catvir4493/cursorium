using System.Text.Json;
using Microsoft.Win32;

namespace Frost.Core;
public sealed class Settings
{
    public int SchemaVersion {get;set;}=1;
    private bool enabled=true,trailEnabled=true;
    public event Action? ActivityChanged;
    public bool Enabled {get=>enabled;set{if(enabled==value)return;enabled=value;ActivityChanged?.Invoke();}}
    public bool TrailEnabled {get=>trailEnabled;set{if(trailEnabled==value)return;trailEnabled=value;ActivityChanged?.Invoke();}}
    public string TrailEffect {get;set;}="comet";
    public double Length {get;set;}=110;
    public double Duration {get;set;}=260;
    public double Opacity {get;set;}=.40;
    public int Particles {get;set;}=10;
    public string Color {get;set;}="#91DEFF";
    public bool ClickHalo {get;set;}=true;
    public double HaloDuration {get;set;}=180;
    public string ClickEffect {get;set;}="halo";
    public double ClickOpacity {get;set;}=.55;
    public double ClickRadius {get;set;}=23;
    public string Preset {get;set;}="Standard";
    public int FrameRate {get;set;}=60;
    public bool PowerSaving {get;set;}
    public bool PauseFullscreen {get;set;}=true;
    public void Normalize()
    {
        // Migrate legacy preset names without changing saved numeric parameters.
        Preset=Preset switch
        {
            "\u6781\u7b80"=>"Minimal", "\u6807\u51c6"=>"Standard",
            "\u70ab\u5149"=>"Glow", "\u81ea\u5b9a\u4e49"=>"Custom",
            "Minimal" or "Standard" or "Glow" or "Custom" or "Comet" or "Neon" or "Stardust"=>Preset, _=>"Custom"
        };
        Length=Clamp(Length,10,360,110);Duration=Clamp(Duration,80,900,260);Opacity=Clamp(Opacity,.05,.85,.4);
        Particles=Math.Clamp(Particles,0,24);HaloDuration=Clamp(HaloDuration,80,500,180);FrameRate=FrameRate>=120?120:60;
        ClickOpacity=Clamp(ClickOpacity,.05,.85,.55);ClickRadius=Clamp(ClickRadius,8,60,23);
        if(TrailEffect is not ("comet" or "minimal" or "neon" or "stardust"))TrailEffect="comet";
        if(ClickEffect is not ("off" or "halo" or "ring"))ClickEffect="halo";
        if(!ValidColor(Color))Color="#91DEFF";
    }
    public static bool ValidColor(string? value)=>value!=null&&System.Text.RegularExpressions.Regex.IsMatch(value,"^#[0-9a-fA-F]{6}$");
    public Settings Clone(){var copy=new Settings();copy.CopyFrom(this);return copy;}
    public void CopyFrom(Settings value)
    {
        SchemaVersion=value.SchemaVersion;Enabled=value.Enabled;TrailEnabled=value.TrailEnabled;TrailEffect=value.TrailEffect;Length=value.Length;Duration=value.Duration;Opacity=value.Opacity;Particles=value.Particles;Color=value.Color;
        ClickHalo=value.ClickHalo;HaloDuration=value.HaloDuration;ClickEffect=value.ClickEffect;ClickOpacity=value.ClickOpacity;ClickRadius=value.ClickRadius;
        Preset=value.Preset;FrameRate=value.FrameRate;PowerSaving=value.PowerSaving;PauseFullscreen=value.PauseFullscreen;
    }
    private static double Clamp(double value,double min,double max,double fallback)=>double.IsFinite(value)?Math.Clamp(value,min,max):fallback;
    public void ApplyPreset(string name)
    {
        Preset=name;
        (Length,Duration,Opacity,Particles)=name switch{"Minimal"=>(65,180,.22,2),"Glow"=>(180,360,.55,16),_=>(110,260,.4,10)};
    }
    public static string PathName=>Path.Combine(Storage.Root,"settings.json");
    public void Save(){Normalize();Storage.Write(PathName,this);}
    public static Settings Load()
    {
        try{var result=File.Exists(PathName)?JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathName)):new();result??=new();if(result.SchemaVersion!=1)throw new InvalidDataException("Unsupported legacy settings schema; original file preserved.");result.Normalize();return result;}
        catch(Exception ex)when(ex is IOException or JsonException){Storage.Log("Settings fallback: "+ex.Message);return new();}
    }
}
public static class StartupRegistration
{
    private const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run",Name="FrostWhiteComet";
    public static bool Enabled{get{if(!DistributionProfile.Current.UsesCommunityInstaller)return false;using var key=Registry.CurrentUser.OpenSubKey(KeyPath);return key?.GetValue(Name)!=null;}}
    public static void Set(bool enabled,string executable)
    {
        if(!DistributionProfile.Current.UsesCommunityInstaller)throw new InvalidOperationException("Login startup registration is not enabled for this distribution channel.");
        using var key=Registry.CurrentUser.CreateSubKey(KeyPath);
        if(enabled)key.SetValue(Name,$"\"{executable}\" --tray",RegistryValueKind.String);else key.DeleteValue(Name,false);
    }
}
