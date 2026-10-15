using System.Reflection;

namespace Frost.Core;

public enum DistributionChannel { Development,Community,Steam }

// This is build metadata, never a mutable JSON file or environment variable switch.
public sealed record DistributionProfile(DistributionChannel Channel)
{
    public static DistributionProfile Current {get;}=Parse(typeof(DistributionProfile).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="Cursorium.Channel").Value!);
    public static DistributionProfile Parse(string value)=>value switch {
        "Development"=>new(DistributionChannel.Development),"Community"=>new(DistributionChannel.Community),"Steam"=>new(DistributionChannel.Steam),_=>throw new InvalidDataException("Unknown distribution channel.")};
    public string UpdateOwner=>Channel switch {DistributionChannel.Community=>"ManualCommunityInstaller",DistributionChannel.Steam=>"SteamPipePlanned",_=>"Developer"};
    public bool SteamIntegrated=>false;
    public string DataRoot(string localAppData){if(string.IsNullOrWhiteSpace(localAppData)||!Path.IsPathFullyQualified(localAppData))throw new InvalidDataException("A real per-user LocalAppData directory is required; never fall back to program files.");return Channel switch {
        DistributionChannel.Community=>Path.Combine(localAppData,"FrostWhiteComet"),
        DistributionChannel.Steam=>Path.Combine(localAppData,"CURSORIUM","Steam"),
        _=>Path.Combine(localAppData,"CURSORIUM","Development")};}
    public bool UsesCommunityInstaller=>Channel==DistributionChannel.Community;
    public bool AllowsNativeWrites(bool isolated)=>Channel==DistributionChannel.Community||(Channel==DistributionChannel.Development&&isolated);
    public static void DemandNativeWrites()
    {
        if(!Current.AllowsNativeWrites(Storage.IsIsolated))throw new InvalidOperationException("This channel is a foundation preview. Windows cursor changes require the Community build; Steam recovery handover is not validated yet.");
    }
}

public static class ProductIdentity
{
    public const string Product="CURSORIUM";
    public static readonly Version ClientVersion=new(1,6,0);
    public const string LegacyExecutable="FrostWhiteComet.exe";
    // Deliberately shared with all old clients. Renaming these would split ownership.
    public const string InstancePrefix="FrostWhiteComet-";
    public const string ThemeMutex=@"Local\FrostWhiteComet.Theme";
    public const string SetupMutex=@"Local\FrostWhiteComet.Setup";
    public const string LegacyRunName="FrostWhiteComet";
    public const string LegacyUninstallKey=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet";
    public static object Describe()=>new {product=Product,executable=LegacyExecutable,channel=DistributionProfile.Current.Channel.ToString(),updateOwner=DistributionProfile.Current.UpdateOwner,steamIntegrated=false,steamAppId=(string?)null,cloudIntegrated=false,liveNativeWrites=DistributionProfile.Current.AllowsNativeWrites(false),instancePrefix=InstancePrefix,themeMutex=ThemeMutex,dataLayoutVersion=1};
}
