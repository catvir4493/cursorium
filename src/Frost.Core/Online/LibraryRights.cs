using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frost.Core;

public sealed class LicenseEvidence
{
    public string Url {get;set;}="";
    public string Sha256 {get;set;}="";
    public string Kind {get;set;}="";
}
public sealed class ResourceRights
{
    public string StableId {get;set;}="";
    public string Version {get;set;}="";
    public string Scope {get;set;}="Online";
    public string PackageSha256 {get;set;}="";
    public string Author {get;set;}="";
    public string SourceUrl {get;set;}="";
    public string License {get;set;}="";
    public List<LicenseEvidence> LicenseEvidence {get;set;}=new();
    public string CommercialClearance {get;set;}="Needs Review";
    public string RedistributionPermission {get;set;}="Needs Review";
    public bool AttributionRequired {get;set;}=true;
    public string ReviewStatus {get;set;}="Needs Review";
    public string MinimumClientVersion {get;set;}="1.6.0";
    public List<string> DistributionChannel {get;set;}=new();
    public string WithdrawalStatus {get;set;}="Active";
    public string ReviewNote {get;set;}="";
    [JsonIgnore] public bool AllowsSteam=>CommercialClearance=="Approved"&&ReviewStatus=="Reviewed"&&(RedistributionPermission is "Allowed" or "AllowedWithNotice")&&DistributionChannel.Contains("Steam")&&WithdrawalStatus=="Active";
    internal static bool Hash(string? value)=>value!=null&&value.Length==64&&value.All(Uri.IsHexDigit);
    internal static bool Https(string? value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Port==443&&uri.UserInfo.Length==0&&value!.Length<=2000;
    public void Validate()
    {
        if(!RemoteResource.ValidId(StableId)||Scope is not ("Online" or "Bundled")||(Scope=="Online"&&!System.Version.TryParse(Version,out _))||(Scope=="Bundled"&&Version!="unversioned")||!Hash(PackageSha256)||string.IsNullOrWhiteSpace(Author)||Author.Length>200||!Https(SourceUrl)||string.IsNullOrWhiteSpace(License)||License.Length>1000||!System.Version.TryParse(MinimumClientVersion,out _)||ReviewNote==null||ReviewNote.Length>2000)throw new InvalidDataException("Invalid rights identity, evidence or version.");
        if(CommercialClearance is not ("Approved" or "Needs Review" or "Denied")||RedistributionPermission is not ("Allowed" or "AllowedWithNotice" or "Needs Review" or "Denied")||ReviewStatus is not ("Reviewed" or "Needs Review" or "Rejected")||WithdrawalStatus is not ("Active" or "Withdrawn")||DistributionChannel==null||DistributionChannel.Count is <1 or >3||DistributionChannel.Distinct().Count()!=DistributionChannel.Count||DistributionChannel.Any(c=>c is not ("Development" or "Community" or "Steam")))throw new InvalidDataException("Invalid commercial review state.");
        if(LicenseEvidence==null||LicenseEvidence.Count is <1 or >8||LicenseEvidence.Any(e=>e==null||!Https(e.Url)||!Hash(e.Sha256)||e.Kind is not ("SourcePage" or "PackageNotice" or "RepositoryLicense" or "RepositorySource")))throw new InvalidDataException("Missing traceable license evidence.");
        if(DistributionChannel.Contains("Steam")&&!AllowsSteam)throw new InvalidDataException("Uncleared material cannot enter the Steam whitelist.");
    }
}
public sealed class LibraryRightsDocument
{
    public int SchemaVersion {get;set;}=2;
    public string Version {get;set;}="";
    public string CatalogSha256 {get;set;}="";
    public List<ResourceRights> Resources {get;set;}=new();
    public void Validate(RemoteCatalog catalog,string catalogHash)
    {
        if(SchemaVersion!=2||!System.Version.TryParse(Version,out _)||!ResourceRights.Hash(CatalogSha256)||!CatalogSha256.Equals(catalogHash,StringComparison.OrdinalIgnoreCase)||Resources==null||Resources.Count>2100)throw new InvalidDataException("Rights document does not bind this signed catalog.");
        var identities=new HashSet<string>(StringComparer.Ordinal);
        foreach(var item in Resources){if(item==null)throw new InvalidDataException("Null rights entry.");item.Validate();if(!identities.Add(item.Scope+":"+item.StableId+":"+item.Version))throw new InvalidDataException("Duplicate rights identity.");}
        var online=Resources.Where(r=>r.Scope=="Online").ToArray();
        if(online.Length!=catalog.Resources.Count)throw new InvalidDataException("Every online resource requires an exact rights record.");
        foreach(var resource in catalog.Resources)
            if(!online.Any(r=>r.StableId==resource.Id&&r.Version==resource.Version&&r.Author==resource.Author&&r.SourceUrl==resource.SourceUrl&&r.PackageSha256.Equals(resource.SHA256,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Rights record/package identity differs from catalog.");
        if(Resources.Any(r=>r.Scope=="Bundled"&&r.StableId is not ("frost" or "obsidian" or "comet" or "minimal" or "neon" or "stardust")))throw new InvalidDataException("Unknown bundled identity.");
    }
}

// A distinct signed sidecar; old v1 catalogs, packages and collections are untouched.
public sealed class RemoteRightsService
{
    public const string Url="https://catvir4493.github.io/cursorium-library/catalog/v2/rights.json";
    private static readonly JsonSerializerOptions Strict=new(Storage.Json){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,MaxDepth=32};
    private readonly OfficialHttp http;
    private readonly string cache=Path.Combine(Storage.Root,"Online","rights-v2-cache.json");
    private readonly SemaphoreSlim gate=new(1,1);
    private string digest="";
    public LibraryRightsDocument? Current {get;private set;}
    public string Status {get;private set;}="Commercial rights not loaded";
    public bool LastRefreshSucceeded {get;private set;}
    public RemoteRightsService(OfficialHttp http){this.http=http;LoadCache();}
    public static LibraryRightsDocument Verify(byte[] rights,byte[] signature,byte[] catalog,byte[] catalogSignature)
    {
        if(rights.Length>2*1024*1024||catalog.Length>2*1024*1024||signature.Length!=384||catalogSignature.Length!=384)throw new InvalidDataException("Rights input is oversized or has an invalid signature size.");
        var verified=RemoteCatalogService.Verify(catalog,catalogSignature);RemoteCatalogService.VerifySignature(rights,signature);
        var result=FoundationJson.Read<LibraryRightsDocument>(rights,2*1024*1024);
        result.Validate(verified,Convert.ToHexString(SHA256.HashData(catalog)));return result;
    }
    private void LoadCache()
    {
        if(!File.Exists(cache))return;
        try{
            var value=ReadCache();if(value.SchemaVersion!=1)throw new InvalidDataException("Newer rights cache preserved.");
            var bytes=Convert.FromBase64String(value.Rights);Current=Verify(bytes,Convert.FromBase64String(value.Signature),Convert.FromBase64String(value.Catalog),Convert.FromBase64String(value.CatalogSignature));digest=Convert.ToHexString(SHA256.HashData(bytes));Status="Cached rights · signature verified";
        }catch(Exception ex)when(Expected(ex)){Status="Rights unavailable; existing cache preserved";}
    }
    private Cache ReadCache(){DownloadManager.GuardRoot(cache);if(new FileInfo(cache).Length>8*1024*1024)throw new InvalidDataException("Oversized rights cache.");return FoundationJson.Read<Cache>(File.ReadAllBytes(cache),8*1024*1024);}
    public async Task RefreshAsync(CancellationToken cancellation=default)
    {
        if(!await gate.WaitAsync(0,cancellation).ConfigureAwait(false))return;
        LastRefreshSucceeded=false;
        try{
            var catalog=await http.BytesAsync(OfficialHttp.CatalogUrl,2*1024*1024,cancellation).ConfigureAwait(false);var cs=await http.BytesAsync(OfficialHttp.CatalogUrl+".sig",512,cancellation).ConfigureAwait(false);
            var rights=await http.BytesAsync(Url,2*1024*1024,cancellation).ConfigureAwait(false);var signature=await http.BytesAsync(Url+".sig",512,cancellation).ConfigureAwait(false);var next=Verify(rights,signature,catalog,cs);var nextDigest=Convert.ToHexString(SHA256.HashData(rights));
            if(Current!=null&&(System.Version.Parse(next.Version)<System.Version.Parse(Current.Version)||(next.Version==Current.Version&&digest!=nextDigest)))throw new InvalidDataException("Rights rollback or same-version replacement rejected.");
            if(File.Exists(cache)&&ReadCache().SchemaVersion!=1)throw new InvalidDataException("Unknown rights cache version preserved.");
            DownloadManager.GuardRoot(cache);Storage.Write(cache,new Cache{Rights=Convert.ToBase64String(rights),Signature=Convert.ToBase64String(signature),Catalog=Convert.ToBase64String(catalog),CatalogSignature=Convert.ToBase64String(cs)});Current=next;digest=nextDigest;LastRefreshSucceeded=true;Status="Commercial rights · signature verified";
        }catch(Exception ex)when(Expected(ex)){Status=Current==null?"Rights unavailable; Steam acquisition blocked":"Rights offline · retained verified cache";}
        finally{gate.Release();}
    }
    private static bool Expected(Exception ex)=>ex is IOException or InvalidDataException or JsonException or HttpRequestException or OperationCanceledException or CryptographicException or ArgumentException or FormatException;
    public static void Demand(LibraryRightsDocument? document,RemoteResource resource,DistributionChannel channel)
    {
        var record=document?.Resources.SingleOrDefault(r=>r.Scope=="Online"&&r.StableId==resource.Id&&r.Version==resource.Version&&r.Author==resource.Author&&r.PackageSha256.Equals(resource.SHA256,StringComparison.OrdinalIgnoreCase));
        if(record?.WithdrawalStatus=="Withdrawn")throw new InvalidDataException("This resource version was withdrawn from new acquisition. Existing installed files are preserved.");
        if(record!=null&&(record.RedistributionPermission=="Denied"||!record.DistributionChannel.Contains(channel.ToString())||System.Version.Parse(record.MinimumClientVersion)>ProductIdentity.ClientVersion))throw new InvalidDataException("This resource version is not authorized for acquisition in this channel/client. Existing installed files are preserved.");
        if(channel==DistributionChannel.Steam&&(record==null||!record.AllowsSteam||System.Version.Parse(record.MinimumClientVersion)>ProductIdentity.ClientVersion))throw new InvalidDataException("Steam commercial clearance is missing or Needs Review; nothing was downloaded or applied.");
    }
    public async Task DemandAsync(RemoteResource resource,CancellationToken cancellation)
    {
        if(DistributionProfile.Current.Channel==DistributionChannel.Steam){await RefreshAsync(cancellation).ConfigureAwait(false);if(!LastRefreshSucceeded)throw new InvalidDataException("Current signed commercial clearance is unavailable. Existing installed resources remain usable; no new Steam acquisition was authorized.");}
        cancellation.ThrowIfCancellationRequested();Demand(Current,resource,DistributionProfile.Current.Channel);
    }
    private sealed class Cache {public int SchemaVersion {get;set;}=1;public string Rights {get;set;}="";public string Signature {get;set;}="";public string Catalog {get;set;}="";public string CatalogSignature {get;set;}="";}
}
