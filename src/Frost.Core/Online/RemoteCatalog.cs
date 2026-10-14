using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Frost.Core;

public sealed class RemoteCatalog
{
    public int SchemaVersion {get;set;}=1;
    public string CatalogVersion {get;set;}="";
    public List<RemoteResource> Resources {get;set;}=new();
    public void Validate()
    {
        if(SchemaVersion!=1||!Version.TryParse(CatalogVersion,out _)||Resources==null||Resources.Count>2000)throw new InvalidDataException("Unsupported online catalog version or size.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var item in Resources){item.Validate();if(!ids.Add(item.Id))throw new InvalidDataException("Duplicate online resource ID.");}
    }
}
public sealed class RemoteResource
{
    public string Id {get;set;}="";
    public string Type {get;set;}="";
    public string Version {get;set;}="";
    public string Name {get;set;}="";
    public string Author {get;set;}="";
    public string Description {get;set;}="";
    public List<string> Tags {get;set;}=new();
    public string CoverUrl {get;set;}="";
    public List<string> PreviewMedia {get;set;}=new();
    public string PackageUrl {get;set;}="";
    public long PackageSize {get;set;}
    public string SHA256 {get;set;}="";
    public string License {get;set;}="";
    public string SourceUrl {get;set;}="";
    public List<string> SupportedRoles {get;set;}=new();
    public List<string> NativeRoles {get;set;}=new();
    public int AnimatedCursorCount {get;set;}
    public string RequiredEngine {get;set;}="cursorium-1";
    public string MinimumClientVersion {get;set;}="1.3.0";
    public string? LastUpdated {get;set;}
    public string? PublishedAt {get;set;}
    public DateTimeOffset? AddedAt {get;set;}
    public bool Featured {get;set;}
    public void Validate()
    {
        if(!ValidId(Id)||Type is not ("cursor" or "trail")||!System.Version.TryParse(Version,out _)||string.IsNullOrWhiteSpace(Name)||Name.Length>100||Author==null||Author.Length>150||Description==null||Description.Length>4000||License==null||License.Length>1000||string.IsNullOrWhiteSpace(License)||!Regex.IsMatch(SHA256??"","^[a-fA-F0-9]{64}$")||PackageSize is <1 or >CursorPack.MaxTotalBytes||RequiredEngine!="cursorium-1"||!System.Version.TryParse(MinimumClientVersion,out _))throw new InvalidDataException("Invalid online resource metadata.");
        if(Tags==null||Tags.Count>16||Tags.Any(t=>t==null||t.Length is <1 or >40)||PreviewMedia==null||PreviewMedia.Count>6||SupportedRoles==null||NativeRoles==null||AnimatedCursorCount is <0 or >15)throw new InvalidDataException("Invalid online resource collection.");
        OfficialHttp.ValidateUrl(new Uri(CoverUrl),false);OfficialHttp.ValidateUrl(new Uri(PackageUrl),false);foreach(var media in PreviewMedia)OfficialHttp.ValidateUrl(new Uri(media),false);
        if(!Uri.TryCreate(SourceUrl,UriKind.Absolute,out var source)||source.Scheme!="https"||!string.IsNullOrEmpty(source.UserInfo))throw new InvalidDataException("Invalid source link.");
        var known=CursorRoles.All.Select(r=>r.Name).ToHashSet();
        if(SupportedRoles.Distinct().Count()!=SupportedRoles.Count||NativeRoles.Distinct().Count()!=NativeRoles.Count||SupportedRoles.Any(r=>!known.Contains(r))||NativeRoles.Any(r=>!SupportedRoles.Contains(r))||(Type=="cursor"&&SupportedRoles.Count!=15)||(Type=="trail"&&(SupportedRoles.Count!=0||NativeRoles.Count!=0||AnimatedCursorCount!=0)))throw new InvalidDataException("Invalid role coverage.");
    }
    public void CheckCompatibility(){Validate();if(System.Version.Parse(MinimumClientVersion)>new System.Version(1,4,0))throw new InvalidDataException("This resource requires a newer CURSORIUM version.");}
    public static bool ValidId(string? id)=>id!=null&&Regex.IsMatch(id,"^[a-z0-9][a-z0-9-]{0,79}$")&&id!="system";
}

// No credentials. Every redirect is checked before it is followed.
public sealed class OfficialHttp:IDisposable
{
    public const string CatalogUrl="https://catvir4493.github.io/cursorium-library/catalog/v1/index.json";
    private readonly HttpClient client;
    private readonly TimeSpan requestTimeout;
    public OfficialHttp(HttpMessageHandler? handler=null,TimeSpan? timeout=null){requestTimeout=timeout??TimeSpan.FromMinutes(2);if(requestTimeout<=TimeSpan.Zero||requestTimeout>TimeSpan.FromMinutes(2))throw new ArgumentOutOfRangeException(nameof(timeout));client=new(handler??new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.None});client.Timeout=Timeout.InfiniteTimeSpan;client.DefaultRequestHeaders.UserAgent.ParseAdd("CURSORIUM/1.4.0");}
    public static void ValidateUrl(Uri url,bool releaseRedirect)
    {
        if(url.Scheme!="https"||url.Port!=443||!string.IsNullOrEmpty(url.UserInfo)||!string.IsNullOrEmpty(url.Fragment))throw new InvalidDataException("Only official HTTPS resources are allowed.");
        bool pages=url.Host.Equals("catvir4493.github.io",StringComparison.OrdinalIgnoreCase)&&url.AbsolutePath.StartsWith("/cursorium-library/catalog/v1/",StringComparison.Ordinal);
        bool release=url.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)&&url.AbsolutePath.StartsWith("/catvir4493/cursorium-library/releases/download/",StringComparison.Ordinal);
        bool asset=releaseRedirect&&url.Host.Equals("release-assets.githubusercontent.com",StringComparison.OrdinalIgnoreCase)&&url.AbsolutePath.StartsWith("/github-production-release-asset/",StringComparison.Ordinal);
        if(!pages&&!release&&!asset||Uri.UnescapeDataString(url.AbsolutePath).Split('/').Any(p=>p is "." or ".."||p.Contains('\\')))throw new InvalidDataException("Resource URL is outside the official library.");
    }
    public async Task FetchAsync(string url,Stream destination,long limit,CancellationToken cancellation,IProgress<DownloadProgress>? progress=null,long? expectedSize=null)
    {
        var uri=new Uri(url);ValidateUrl(uri,false);bool release=uri.Host=="github.com";
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(requestTimeout);
        for(int redirect=0;redirect<=3;redirect++)
        {
            using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
            if((int)response.StatusCode is >=300 and <=399){if(redirect==3||response.Headers.Location==null)throw new IOException("Too many download redirects.");uri=new Uri(uri,response.Headers.Location);ValidateUrl(uri,release);continue;}
            response.EnsureSuccessStatusCode();var length=response.Content.Headers.ContentLength;
            if(length>limit||(expectedSize.HasValue&&length.HasValue&&length!=expectedSize))throw new InvalidDataException("Download size does not match the approved resource.");
            using var input=await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);var buffer=new byte[65536];long total=0;int count;
            while((count=await input.ReadAsync(buffer,timeout.Token).ConfigureAwait(false))>0)
            {total+=count;if(total>limit||(expectedSize.HasValue&&total>expectedSize))throw new InvalidDataException("Download exceeds its size limit.");await destination.WriteAsync(buffer.AsMemory(0,count),timeout.Token).ConfigureAwait(false);progress?.Report(new("Downloading",total,expectedSize??length??0));}
            if(expectedSize.HasValue&&total!=expectedSize||length.HasValue&&total!=length)throw new InvalidDataException("The download was interrupted or truncated.");return;
        }
    }
    public async Task<byte[]> BytesAsync(string url,int limit,CancellationToken cancellation){using var stream=new MemoryStream();await FetchAsync(url,stream,limit,cancellation).ConfigureAwait(false);return stream.ToArray();}
    public void Dispose()=>client.Dispose();
}
public sealed record DownloadProgress(string Phase,long Bytes,long Total){public double Percent=>Total>0?100.0*Bytes/Total:0;}

public sealed class RemoteCatalogService
{
    private readonly OfficialHttp http;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object refreshSync=new();
    private Task? refreshTask;
    private readonly string cache=Path.Combine(Storage.Root,"Online","catalog-cache.json");
    public RemoteCatalog? Current {get;private set;}
    public bool Offline {get;private set;}
    public string Status {get;private set;}="Not loaded";
    public RemoteCatalogService(OfficialHttp http){this.http=http;LoadCache();}
    public static RemoteCatalog Verify(byte[] bytes,byte[] signature)
    {
        VerifySignature(bytes,signature);
        var options=new JsonSerializerOptions(Storage.Json){UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow};var result=JsonSerializer.Deserialize<RemoteCatalog>(bytes,options)??throw new InvalidDataException("Empty online catalog.");result.Validate();return result;
    }
    public static void VerifySignature(byte[] bytes,byte[] signature)
    {
        using var rsa=RSA.Create();using var stream=typeof(RemoteCatalogService).Assembly.GetManifestResourceStream("Frost.Core.Online.catalog-public-key.pem")!;using var reader=new StreamReader(stream);rsa.ImportFromPem(reader.ReadToEnd());
        if(!rsa.VerifyData(bytes,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))throw new InvalidDataException("The online catalog signature is invalid.");
    }
    private void LoadCache()
    {
        if(!File.Exists(cache))return;
        try{var c=JsonSerializer.Deserialize<CatalogCache>(File.ReadAllBytes(cache),Storage.Json)!;if(c.SchemaVersion!=1)throw new InvalidDataException("Unsupported cache schema; preserved.");Current=Verify(Convert.FromBase64String(c.Catalog),Convert.FromBase64String(c.Signature));Offline=true;Status="Cached catalog · refresh when online";}
        catch(Exception ex)when(ex is IOException or JsonException or InvalidDataException or FormatException or CryptographicException){Status="Cached catalog unavailable: "+ex.Message;}
    }
    public Task RefreshAsync(CancellationToken cancellation=default)
    {
        lock(refreshSync)return refreshTask is {IsCompleted:false}?refreshTask:refreshTask=RefreshCoreAsync(cancellation);
    }
    private async Task RefreshCoreAsync(CancellationToken cancellation)
    {
        if(!await gate.WaitAsync(0,cancellation).ConfigureAwait(false))return;
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var bytes=await http.BytesAsync(OfficialHttp.CatalogUrl,2*1024*1024,timeout.Token).ConfigureAwait(false);
            var signature=await http.BytesAsync(OfficialHttp.CatalogUrl+".sig",512,timeout.Token).ConfigureAwait(false);
            var catalog=Verify(bytes,signature);
            if(Current!=null&&Version.Parse(catalog.CatalogVersion)<Version.Parse(Current.CatalogVersion))throw new InvalidDataException("The remote catalog is older than the verified cache.");
            // Preserve a newer cache schema rather than silently overwriting it.
            if(File.Exists(cache)){var existing=JsonSerializer.Deserialize<CatalogCache>(File.ReadAllBytes(cache),Storage.Json);if(existing?.SchemaVersion!=1)throw new InvalidDataException("Unsupported existing cache schema; preserved.");}
            Storage.Write(cache,new CatalogCache{Catalog=Convert.ToBase64String(bytes),Signature=Convert.ToBase64String(signature)});Current=catalog;Offline=false;Status=catalog.Resources.Count==0?"The official catalog is empty.":"Official catalog · signature verified";
        }
        catch(Exception ex)when(ex is HttpRequestException or IOException or InvalidDataException or JsonException or CryptographicException or OperationCanceledException or FormatException or ArgumentException){Offline=true;Status=(Current==null?"Online catalog unavailable: ":"Offline · using the verified cache: ")+ex.Message;}
        finally{gate.Release();}
    }
    private sealed class CatalogCache{public int SchemaVersion {get;set;}=1;public string Catalog {get;set;}="";public string Signature {get;set;}="";}
}
