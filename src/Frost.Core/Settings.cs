using System.Text.Json;
using Microsoft.Win32;

namespace Frost.Core;
public sealed class Settings
{
    public bool Enabled {get;set;}=true;
    public double Length {get;set;}=110;
    public double Duration {get;set;}=260;
    public double Opacity {get;set;}=.40;
    public int Particles {get;set;}=10;
    public string Color {get;set;}="#91DEFF";
    public bool ClickHalo {get;set;}=true;
    public double HaloDuration {get;set;}=180;
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
            "Minimal" or "Standard" or "Glow" or "Custom"=>Preset, _=>"Custom"
        };
        Length=Clamp(Length,10,360,110);Duration=Clamp(Duration,80,900,260);Opacity=Clamp(Opacity,.05,.85,.4);
        Particles=Math.Clamp(Particles,0,24);HaloDuration=Clamp(HaloDuration,80,500,180);FrameRate=FrameRate>=120?120:60;
        try{CursorArtwork.ColorFrom(Color);}catch{Color="#91DEFF";}
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
        try{var result=File.Exists(PathName)?JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathName)):new();result??=new();result.Normalize();return result;}
        catch(Exception ex)when(ex is IOException or JsonException){Storage.Log("Settings fallback: "+ex.Message);return new();}
    }
}
public static class StartupRegistration
{
    private const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run",Name="FrostWhiteComet";
    public static bool Enabled{get{using var key=Registry.CurrentUser.OpenSubKey(KeyPath);return key?.GetValue(Name)!=null;}}
    public static void Set(bool enabled,string executable)
    {
        using var key=Registry.CurrentUser.CreateSubKey(KeyPath);
        if(enabled)key.SetValue(Name,$"\"{executable}\" --tray",RegistryValueKind.String);else key.DeleteValue(Name,false);
    }
}
