using System.Security.Cryptography;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frost.Core;

// Separate signed document: catalog v1 remains byte/schema compatible with 1.3 clients.
public sealed class RemoteCollections
{
    public int SchemaVersion {get;set;}=1;
    public string Version {get;set;}="";
    public string MinimumCatalogVersion {get;set;}="";
    public List<FeaturedCollection> Collections {get;set;}=new();
    public void Validate(RemoteCatalog catalog)
    {
        if(SchemaVersion!=1||!System.Version.TryParse(Version,out _)||!System.Version.TryParse(MinimumCatalogVersion,out var minimum)||minimum>System.Version.Parse(catalog.CatalogVersion)||Collections==null||Collections.Count>24)throw new InvalidDataException("Unsupported collections document or catalog dependency.");
        var ids=new HashSet<string>(StringComparer.Ordinal);var members=catalog.Resources.Select(r=>r.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var collection in Collections)
        {
            if(collection==null||!RemoteResource.ValidId(collection.Id)||!ids.Add(collection.Id)||string.IsNullOrWhiteSpace(collection.Name)||collection.Name.Length>80||collection.Description==null||collection.Description.Length>400||collection.ResourceIds==null||collection.ResourceIds.Count is <1 or >2000||collection.ResourceIds.Distinct(StringComparer.Ordinal).Count()!=collection.ResourceIds.Count||collection.ResourceIds.Any(id=>!members.Contains(id)))throw new InvalidDataException("Invalid collection identity, description or members.");
            OfficialHttp.ValidateUrl(new Uri(collection.CoverUrl),false);
        }
    }
}
public sealed class FeaturedCollection
{
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public string Description {get;set;}="";
    public string CoverUrl {get;set;}="";
    public List<string> ResourceIds {get;set;}=new();
}
public sealed class RemoteCollectionsService
{
    public const string Url="https://catvir4493.github.io/cursorium-library/catalog/v1/collections/index.json";
    private readonly OfficialHttp http;
    private readonly string cache=Path.Combine(Storage.Root,"Online","collections-cache.json");
    private readonly object sync=new();private Task? refreshing;
    public RemoteCollections? Current {get;private set;}
    public string Status {get;private set;}="Collections not loaded";
    public RemoteCollectionsService(OfficialHttp http){this.http=http;}
    public static RemoteCollections Verify(byte[] bytes,byte[] signature,RemoteCatalog catalog)
    {
        RemoteCatalogService.VerifySignature(bytes,signature);
        var result=JsonSerializer.Deserialize<RemoteCollections>(bytes,new JsonSerializerOptions(Storage.Json){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow})??throw new InvalidDataException("Empty collections document.");result.Validate(catalog);return result;
    }
    public void LoadCache(RemoteCatalog catalog)
    {
        if(!File.Exists(cache))return;
        try{if(new FileInfo(cache).Length>512*1024)throw new InvalidDataException("Oversized collections cache; preserved.");var record=JsonSerializer.Deserialize<Cache>(File.ReadAllBytes(cache),Storage.Json)??throw new InvalidDataException("Empty collections cache.");if(record.SchemaVersion!=1)throw new InvalidDataException("Unsupported collections cache; preserved.");var verified=Verify(Convert.FromBase64String(record.Document),Convert.FromBase64String(record.Signature),catalog);if(Current!=null&&Version.Parse(verified.Version)<Version.Parse(Current.Version))throw new InvalidDataException("Cached collections rollback rejected.");Current=verified;Status="Cached collections · signature verified";}
        catch(Exception ex)when(IsExpected(ex)){Status="Collections cache unavailable: "+ex.Message;}
    }
    public Task RefreshAsync(RemoteCatalog catalog,CancellationToken cancellation=default){lock(sync)return refreshing is {IsCompleted:false}?refreshing:refreshing=RefreshCore(catalog,cancellation);}
    private async Task RefreshCore(RemoteCatalog catalog,CancellationToken cancellation)
    {
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var bytes=await http.BytesAsync(Url,256*1024,timeout.Token).ConfigureAwait(false);var signature=await http.BytesAsync(Url+".sig",512,timeout.Token).ConfigureAwait(false);var document=Verify(bytes,signature,catalog);
            if(Current!=null&&Version.Parse(document.Version)<Version.Parse(Current.Version))throw new InvalidDataException("Collections rollback rejected.");
            if(File.Exists(cache)&&(new FileInfo(cache).Length>512*1024||JsonSerializer.Deserialize<Cache>(File.ReadAllBytes(cache),Storage.Json)?.SchemaVersion!=1))throw new InvalidDataException("Unsupported collections cache; preserved.");
            Storage.Write(cache,new Cache{Document=Convert.ToBase64String(bytes),Signature=Convert.ToBase64String(signature)});Current=document;Status="Collections · signature verified";
        }
        catch(Exception ex)when(IsExpected(ex)){Status="Collections offline: "+ex.Message;}
    }
    private static bool IsExpected(Exception ex)=>ex is IOException or InvalidDataException or JsonException or CryptographicException or HttpRequestException or OperationCanceledException or ArgumentException or FormatException;
    private sealed class Cache{public int SchemaVersion {get;set;}=1;public string Document {get;set;}="";public string Signature {get;set;}="";}
}
