using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Frost.Core;

namespace Frost.App;

internal static partial class LibraryPublisher
{
    internal sealed class ReviewBatch
    {
        public int SchemaVersion {get;set;}=1;
        public string Version {get;set;}="";
        public bool MetadataOnly {get;set;}
        public Dictionary<string,List<string>> ResourceTags {get;set;}=new();
        public List<ReviewedCursor> Cursors {get;set;}=new();
        public List<TrailPreset> Trails {get;set;}=new();
        public List<FeaturedCollection> Collections {get;set;}=new();
    }
    internal sealed class ReviewedCursor
    {
        public string Slug {get;set;}="";public string Id {get;set;}="";public string Name {get;set;}="";public string Author {get;set;}="";
        public string Description {get;set;}="";public string SourceUrl {get;set;}="";public string License {get;set;}="";public string LicenseUrl {get;set;}="";
        public string ArchiveSHA256 {get;set;}="";public string PageSHA256 {get;set;}="";public string PublishedAt {get;set;}="";
        public string Review {get;set;}="";public bool Approved {get;set;}
        public string Version {get;set;}="1.0.0";
        public List<string> Tags {get;set;}=new();public Dictionary<string,string> Roles {get;set;}=new();
    }
    // All preparation is local and isolated. A fresh output tree and exact source
    // hashes make a review reproducible; no downloaded instructions are executed.
    public static void Prepare(string reviewPath,string sources,string previous,string output,string privateKey)
    {
        reviewPath=Path.GetFullPath(reviewPath);sources=Path.GetFullPath(sources);previous=Path.GetFullPath(previous);output=Path.GetFullPath(output);
        var batch=JsonSerializer.Deserialize<ReviewBatch>(File.ReadAllBytes(reviewPath),new JsonSerializerOptions(Storage.Json){UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow})??throw new InvalidDataException("Missing review batch.");
        if(batch.SchemaVersion!=1||!Version.TryParse(batch.Version,out _)||Directory.Exists(output))throw new InvalidDataException("Use a supported review and fresh output directory.");
        var catalog=RemoteCatalogService.Verify(File.ReadAllBytes(previous),File.ReadAllBytes(previous+".sig"));
        if(Version.Parse(batch.Version)<=Version.Parse(catalog.CatalogVersion))throw new InvalidDataException("Catalog version must advance.");
        using var rsa=RSA.Create();rsa.ImportFromPem(File.ReadAllText(privateKey));var challenge="CURSORIUM publisher key check"u8.ToArray();RemoteCatalogService.VerifySignature(challenge,rsa.SignData(challenge,HashAlgorithmName.SHA256,RSASignaturePadding.Pss));
        if(batch.MetadataOnly&&(batch.Cursors.Count!=0||batch.Trails.Count!=0)||!batch.MetadataOnly&&batch.Cursors.Count+batch.Trails.Count==0)throw new InvalidDataException("Metadata-only review must not contain package changes.");
        foreach(var tags in batch.ResourceTags){var resource=catalog.Resources.SingleOrDefault(r=>r.Id==tags.Key)??throw new InvalidDataException("Unknown metadata resource ID.");resource.Tags=tags.Value;resource.Validate();}
        var ids=catalog.Resources.Select(r=>r.Id).ToHashSet(StringComparer.Ordinal);var hashes=catalog.Resources.Select(r=>r.SHA256).ToHashSet(StringComparer.OrdinalIgnoreCase);var content=new HashSet<string>(StringComparer.Ordinal);
        foreach(var c in batch.Cursors)
        {
            if(!c.Approved||!RemoteResource.ValidId(c.Id)||c.Slug!=Path.GetFileName(c.Slug)||c.Slug.Contains(':')||c.Slug is "." or ".."||string.IsNullOrWhiteSpace(c.Author)||string.IsNullOrWhiteSpace(c.Review)||c.License is not ("Release to Public Domain (RealWorld)" or "Attribution Required (CC by; RealWorld)")||c.LicenseUrl!="https://www.rw-designer.com/licenses"||c.Roles.Count!=15||CursorRoles.All.Any(r=>!c.Roles.ContainsKey(r.Name)))throw new InvalidDataException("Incomplete/duplicate/unapproved cursor review: "+c.Id);
            CheckIdentity(c.Id,c.Version,c.Author,c.SourceUrl,"cursor");
            var folder=Path.Combine(sources,c.Slug);if(Digest(Path.Combine(folder,"original.zip"))!=c.ArchiveSHA256||Digest(Path.Combine(folder,"source.html"))!=c.PageSHA256)throw new InvalidDataException("Reviewed source changed: "+c.Id);
        }
        foreach(var p in batch.Trails){p.Validate();if(p.Author!="CURSORIUM")throw new InvalidDataException("Invalid original preset identity.");CheckIdentity(p.Id,p.Version,p.Author,"https://github.com/catvir4493/cursorium","trail");}
        if(batch.Trails.Select(p=>$"{p.Renderer}|{p.Length}|{p.Duration}|{p.Opacity}|{p.Particles}|{p.Color}").Distinct().Count()!=batch.Trails.Count)throw new InvalidDataException("Duplicate motion parameters.");
        Directory.CreateDirectory(output);var media=Path.Combine(output,"catalog","v1","previews","library-v"+batch.Version);var packages=Path.Combine(output,"packages");Directory.CreateDirectory(packages);
        foreach(var c in batch.Cursors)
        {
            using var pack=CursorPack.Open(Path.Combine(sources,c.Slug,"original.zip"));
            var fingerprint=string.Join('|',pack.Files.Select(f=>Digest(f.Path)).Order(StringComparer.Ordinal));if(!content.Add(fingerprint))throw new InvalidDataException("Duplicate cursor artwork.");
            var staging=Path.Combine(output,"work",c.Id);Directory.CreateDirectory(staging);var theme=new CursorTheme{Id=c.Id,Name=c.Name,Author=c.Author,Description=c.Description,SourceUrl=c.SourceUrl,License=c.License,PublishedAt=c.PublishedAt,Version=c.Version,Collection="downloaded"};
            int index=0;foreach(var f in pack.Files){var name="Sources/"+(index++)+Path.GetExtension(f.Path);Directory.CreateDirectory(Path.Combine(staging,"Sources"));File.Copy(f.Path,Path.Combine(staging,name));theme.SourceFiles.Add(new(){OriginalName=f.OriginalName,Path=name,Sha256=Digest(f.Path)});}
            foreach(var role in c.Roles){var item=theme.SourceFiles.SingleOrDefault(f=>f.OriginalName==role.Value)??throw new InvalidDataException("Role mapping is missing or ambiguous: "+role.Key);theme.Roles.Add(role.Key,item.Path);}
            Storage.Write(Path.Combine(staging,"theme.json"),theme);theme=ThemeCatalog.ReadTheme(Path.Combine(staging,"theme.json"));
            File.WriteAllText(Path.Combine(staging,"NOTICE.txt"),$"{c.Name}\nAuthor: {c.Author}\nSource: {c.SourceUrl}\nLicense: {c.License}\nLicense terms: {c.LicenseUrl}\nPublished: {c.PublishedAt}\nSource ZIP SHA256: {c.ArchiveSHA256}\nReview: {c.Review}\nAll artwork, hotspots and ANI timing remain unchanged. Original/unused variants retained in Sources. No author endorsement is implied. No inherited cursor roles.\n");
            Presentation.ThemeCoverRenderer.Save(Presentation.ThemeCoverRenderer.Cursor(theme),Path.Combine(media,c.Id+".png"));Add(c.Id,c.Version,c.Name,c.Author,c.Description,"cursor",c.License,c.SourceUrl,c.Tags,staging,theme.Roles.Keys.ToList(),theme.AnimatedRoleCount,c.PublishedAt);
        }
        foreach(var p in batch.Trails)
        {
            var staging=Path.Combine(output,"work",p.Id);Directory.CreateDirectory(staging);Storage.Write(Path.Combine(staging,"preset.json"),p);File.WriteAllText(Path.Combine(staging,"NOTICE.txt"),"Original CURSORIUM numeric motion preset. Author: CURSORIUM. License: MIT. Source: https://github.com/catvir4493/cursorium\nNo executable renderer code.\n"+File.ReadAllText(Path.Combine(Environment.CurrentDirectory,"LICENSE")));
            Presentation.ThemeCoverRenderer.Save(Presentation.ThemeCoverRenderer.Trail(p),Path.Combine(media,p.Id+".png"));Add(p.Id,p.Version,p.Name,p.Author,p.Description,"trail","MIT","https://github.com/catvir4493/cursorium",new(){p.Renderer=="minimal"?"Minimal":"Neon","Motion"},staging,new(),0,null);
        }
        catalog.CatalogVersion=batch.Version;catalog.Validate();var indexPath=Path.Combine(output,"catalog","v1","index.json");Sign(indexPath,catalog);RemoteCatalogService.Verify(File.ReadAllBytes(indexPath),File.ReadAllBytes(indexPath+".sig"));
        var collections=new RemoteCollections{Version=batch.Version,MinimumCatalogVersion=batch.Version,Collections=batch.Collections};
        foreach(var c in collections.Collections)
        {
            c.CoverUrl=$"https://catvir4493.github.io/cursorium-library/catalog/v1/collections/{batch.Version}/{c.Id}.png";
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
            {
                int position=0;foreach(var id in c.ResourceIds.Take(4))
                {
                    var item=catalog.Resources.Single(r=>r.Id==id);var relative=new Uri(item.CoverUrl).AbsolutePath.Split("/catalog/v1/")[1];var cover=Path.Combine(output,"catalog","v1",relative);
                    if(!File.Exists(cover))cover=Path.Combine(Path.GetDirectoryName(previous)!,relative);
                    if(!File.Exists(cover))throw new InvalidDataException("Missing collection member cover: "+id);
                    var image=new System.Windows.Media.Imaging.BitmapImage();image.BeginInit();image.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;image.UriSource=new Uri(Path.GetFullPath(cover));image.EndInit();image.Freeze();dc.DrawImage(image,new Rect(position%2*360,position/2*270,360,270));position++;
                }
            }
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(720,540,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);Presentation.ThemeCoverRenderer.Save(bitmap,Path.Combine(output,"catalog","v1","collections",batch.Version,c.Id+".png"));
        }
        collections.Validate(catalog);var collectionPath=Path.Combine(output,"catalog","v1","collections","index.json");Sign(collectionPath,collections);RemoteCollectionsService.Verify(File.ReadAllBytes(collectionPath),File.ReadAllBytes(collectionPath+".sig"),catalog);
        Storage.Write(Path.Combine(output,"review.json"),batch);
        void Sign<T>(string path,T value){Storage.Write(path,value);File.WriteAllBytes(path+".sig",rsa.SignData(File.ReadAllBytes(path),HashAlgorithmName.SHA256,RSASignaturePadding.Pss));}
        void CheckIdentity(string id,string version,string author,string source,string type)
        {
            if(!Version.TryParse(version,out var next))throw new InvalidDataException("Invalid reviewed package version.");
            var existing=catalog.Resources.SingleOrDefault(r=>r.Id==id);
            if(existing!=null&&(existing.Type!=type||existing.Author!=author||existing.SourceUrl!=source||next<=Version.Parse(existing.Version)))throw new InvalidDataException("Existing resource identity or immutable version conflict.");
            if(!ids.Add(id)&&existing==null||batch.Cursors.Count(c=>c.Id==id)+batch.Trails.Count(t=>t.Id==id)>1)throw new InvalidDataException("Duplicate batch resource ID.");
        }
        void Add(string id,string version,string name,string author,string description,string type,string license,string source,List<string> tags,string staging,List<string> roles,int animated,string? date)
        {
            var path=Path.Combine(packages,id+".zip");ZipFile.CreateFromDirectory(staging,path,CompressionLevel.Optimal,false);var hash=Digest(path);if(!hashes.Add(hash))throw new InvalidDataException("Duplicate package bytes.");
            var cover=$"https://catvir4493.github.io/cursorium-library/catalog/v1/previews/library-v{batch.Version}/{id}.png";
            var resource=new RemoteResource{Id=id,Name=name,Author=author,Description=description,Type=type,Version=version,License=license,SourceUrl=source,Tags=tags,CoverUrl=cover,PreviewMedia=new(){cover},PackageUrl=$"https://github.com/catvir4493/cursorium-library/releases/download/library-v{batch.Version}/{id}.zip",PackageSize=new FileInfo(path).Length,SHA256=hash,SupportedRoles=roles,NativeRoles=roles.ToList(),AnimatedCursorCount=animated,PublishedAt=date,AddedAt=catalog.Resources.SingleOrDefault(r=>r.Id==id)?.AddedAt??DateTimeOffset.UtcNow};
            resource.Validate();using var checkedPack=new VerifiedResourcePack(path,resource,Path.Combine(output,"verification"));catalog.Resources.RemoveAll(r=>r.Id==id);catalog.Resources.Add(resource);
        }
    }
    public static void Audit(string sources,string output)
    {
        Directory.CreateDirectory(output);var results=new List<object>();
        foreach(var folder in Directory.EnumerateDirectories(sources).Order())
        {
            var zip=Path.Combine(folder,"original.zip");if(!File.Exists(zip))continue;
            try
            {
                using var pack=CursorPack.Open(zip);var files=new List<object>();var visual=new DrawingVisual();int row=0;
                using(var dc=visual.RenderOpen())foreach(var item in pack.Files)
                {
                    var frame=Presentation.ThemeCoverRenderer.Frame(item.Path);var handle=Native.LoadOwnedCursor(item.Path);Native.IconInfo info=default;
                    try
                    {
                        if(!Native.GetIconInfo(handle,out info))throw new IOException("Cannot inspect native hotspot.");
                        var timeline=item.Path.EndsWith(".ani")?AniTimeline.Read(File.ReadAllBytes(item.Path)):null;
                        files.Add(new{item.OriginalName,item.SuggestedRole,sha256=Digest(item.Path),width=frame.PixelWidth,height=frame.PixelHeight,hotspotX=info.HotX,hotspotY=info.HotY,steps=timeline?.Sequence.Length??1,minJiffies=timeline?.Jiffies.Min(),maxJiffies=timeline?.Jiffies.Max(),milliseconds=timeline?.DurationMilliseconds});
                        double y=row++*84;dc.DrawRectangle(Brushes.White,null,new Rect(0,y,800,84));dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(13,20,34)),null,new Rect(88,y,88,84));
                        double scale=Math.Min(2,64d/Math.Max(frame.PixelWidth,frame.PixelHeight));foreach(int x in new[]{8,96})dc.DrawImage(frame,new Rect(x,y+6,frame.PixelWidth*scale,frame.PixelHeight*scale));
                        dc.DrawText(new FormattedText(item.OriginalName+"\nHotspot "+info.HotX+", "+info.HotY,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,Brushes.Black,1),new Point(190,y+12));
                    }
                    finally{if(info.Color!=IntPtr.Zero)Native.DeleteObject(info.Color);if(info.Mask!=IntPtr.Zero)Native.DeleteObject(info.Mask);Native.DestroyCursor(handle);}
                }
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(800,Math.Max(84,row*84),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);Presentation.ThemeCoverRenderer.Save(bitmap,Path.Combine(output,Path.GetFileName(folder)+".png"));
                results.Add(new{slug=Path.GetFileName(folder),archiveSha256=Digest(zip),passed=true,cur=pack.Files.Count(f=>f.Path.EndsWith(".cur")),ani=pack.Files.Count(f=>f.Path.EndsWith(".ani")),files});
            }
            catch(Exception ex){results.Add(new{slug=Path.GetFileName(folder),passed=false,error=ex.Message});}
        }
        Storage.Write(Path.Combine(output,"audit.json"),results);
    }
    private static string Digest(string path){using var file=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();}
    public static void VerifyStage(string output,string previous)
    {
        output=Path.GetFullPath(output);previous=Path.GetFullPath(previous);var path=Path.Combine(output,"catalog","v1","index.json");
        var old=RemoteCatalogService.Verify(File.ReadAllBytes(previous),File.ReadAllBytes(previous+".sig"));var catalog=RemoteCatalogService.Verify(File.ReadAllBytes(path),File.ReadAllBytes(path+".sig"));
        if(Version.Parse(catalog.CatalogVersion)<=Version.Parse(old.CatalogVersion))throw new InvalidDataException("Catalog version must advance.");
        var collection=Path.Combine(output,"catalog","v1","collections","index.json");var verifiedCollections=RemoteCollectionsService.Verify(File.ReadAllBytes(collection),File.ReadAllBytes(collection+".sig"),catalog);
        foreach(var item in verifiedCollections.Collections){var cover=Path.Combine(output,"catalog","v1",new Uri(item.CoverUrl).AbsolutePath.Split("/catalog/v1/")[1]);if(!File.Exists(cover)||new FileInfo(cover).Length>1024*1024)throw new InvalidDataException("Missing/oversized collection cover.");}
        foreach(var existing in old.Resources)
        {
            var item=catalog.Resources.SingleOrDefault(r=>r.Id==existing.Id)??throw new InvalidDataException("Existing resource removed.");
            if(item.SourceUrl!=existing.SourceUrl||item.Author!=existing.Author||item.Type!=existing.Type||Version.Parse(item.Version)<Version.Parse(existing.Version))throw new InvalidDataException("Existing resource identity/version changed unsafely.");
            if(item.Version==existing.Version&&(item.PackageUrl!=existing.PackageUrl||item.SHA256!=existing.SHA256||item.PackageSize!=existing.PackageSize))throw new InvalidDataException("Immutable published package changed.");
        }
        var packageFiles=Directory.GetFiles(Path.Combine(output,"packages"),"*.zip");var expected=catalog.Resources.Where(r=>r.PackageUrl.Contains("/library-v"+catalog.CatalogVersion+"/",StringComparison.Ordinal)).ToArray();
        if(packageFiles.Length!=expected.Length||expected.Length==0&&catalog.Resources.Count!=old.Resources.Count||catalog.Resources.Any(r=>!old.Resources.Any(o=>o.Id==r.Id&&o.Version==r.Version)&&!expected.Contains(r)))throw new InvalidDataException("Package batch does not match the release.");
        var digests=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in catalog.Resources)if(!digests.Add(item.SHA256))throw new InvalidDataException("Duplicate package content.");
        foreach(var item in expected)
        {
            if(string.IsNullOrWhiteSpace(item.Author)||string.IsNullOrWhiteSpace(item.License)||!item.PackageUrl.EndsWith("/"+item.Id+".zip",StringComparison.Ordinal))throw new InvalidDataException("Incomplete provenance or mismatched package URL.");
            var zip=Path.Combine(output,"packages",item.Id+".zip");if(Digest(zip)!=item.SHA256||new FileInfo(zip).Length!=item.PackageSize)throw new InvalidDataException("Package hash/size mismatch.");
            using var archive=CursorPack.OpenValidatedArchive(zip);if(archive.GetEntry("NOTICE.txt")==null)throw new InvalidDataException("Package attribution NOTICE missing.");
            using var pack=new VerifiedResourcePack(zip,item,Path.Combine(output,"verification"));
            foreach(var url in item.PreviewMedia.Prepend(item.CoverUrl))
            {
                var file=Path.Combine(output,"catalog","v1",new Uri(url).AbsolutePath.Split("/catalog/v1/")[1]);
                if(!File.Exists(file)||new FileInfo(file).Length>1024*1024)throw new InvalidDataException("Resource media missing or oversized.");
            }
        }
        Storage.Write(Path.Combine(output,"preflight.json"),new{passed=true,catalogVersion=catalog.CatalogVersion,resources=catalog.Resources.Count,newPackages=expected.Length,metadataOnly=expected.Length==0,oldResourcesPreserved=old.Resources.Count,collections=RemoteCollectionsService.Verify(File.ReadAllBytes(collection),File.ReadAllBytes(collection+".sig"),catalog).Collections.Count});
    }
    public static async Task VerifyPublic(string output,string expectedVersion)
    {
        output=Path.GetFullPath(output);Directory.CreateDirectory(output);using var http=new OfficialHttp();using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(5));var records=new List<object>();
        var index=await http.BytesAsync(OfficialHttp.CatalogUrl,2*1024*1024,timeout.Token);var signature=await http.BytesAsync(OfficialHttp.CatalogUrl+".sig",512,timeout.Token);var catalog=RemoteCatalogService.Verify(index,signature);if(catalog.CatalogVersion!=expectedVersion)throw new IOException("Pages has not deployed the expected catalog yet.");
        File.WriteAllBytes(Path.Combine(output,"index.json"),index);File.WriteAllBytes(Path.Combine(output,"index.json.sig"),signature);
        var bytes=await http.BytesAsync(RemoteCollectionsService.Url,256*1024,timeout.Token);var sig=await http.BytesAsync(RemoteCollectionsService.Url+".sig",512,timeout.Token);var collections=RemoteCollectionsService.Verify(bytes,sig,catalog);File.WriteAllBytes(Path.Combine(output,"collections.json"),bytes);File.WriteAllBytes(Path.Combine(output,"collections.json.sig"),sig);
        foreach(var item in catalog.Resources)
        {
            var zip=Path.Combine(output,item.Id+".zip");using(var file=File.Create(zip))await http.FetchAsync(item.PackageUrl,file,CursorPack.MaxTotalBytes,timeout.Token,null,item.PackageSize);
            if(!Digest(zip).Equals(item.SHA256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Public package hash mismatch: "+item.Id);
            using var validated=new VerifiedResourcePack(zip,item,Path.Combine(output,"staging"));records.Add(new{id=item.Id,version=item.Version,sha256=Digest(zip),size=new FileInfo(zip).Length,nativeRoles=validated.Cursor?.NativeRoleCount??0,animatedRoles=validated.Cursor?.AnimatedRoleCount??0,passed=true});
        }
        var media=new List<object>();foreach(var url in catalog.Resources.SelectMany(r=>r.PreviewMedia.Prepend(r.CoverUrl)).Concat(collections.Collections.Select(c=>c.CoverUrl)).Distinct(StringComparer.Ordinal))
        {
            var image=await http.BytesAsync(url,1024*1024,timeout.Token);if(image.Length<24||!image.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new InvalidDataException("Public cover is not a PNG.");
            int width=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(16,4)),height=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(20,4));if(width is <1 or >2048||height is <1 or >2048)throw new InvalidDataException("Public cover dimensions exceed client limits.");
            media.Add(new{url,sha256=Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant(),width,height,size=image.Length});
        }
        Storage.Write(Path.Combine(output,"public-verification.json"),new{passed=true,catalogVersion=catalog.CatalogVersion,resources=records,collections=collections.Collections.Count,media,notes="Anonymous public HTTPS downloads, exact RSA-PSS signatures, all package SHA256 and production ZIP/CUR/ANI/native validation. No Windows Apply."});
    }
}
