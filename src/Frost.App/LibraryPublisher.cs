using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Frost.Core;

namespace Frost.App;

// Publishing tool: actual native cursor images and the production trail painter.
// It reads only project resources and never applies a Windows theme.
internal static class LibraryPublisher
{
    // Media-only offline export from already validated package directories. It does
    // not recreate packages or change their versions, role mappings or checksums.
    public static void ExportCovers(string input,string output)
    {
        foreach(var folder in Directory.EnumerateDirectories(input).Order(StringComparer.Ordinal))
        {
            var themePath=Path.Combine(folder,"theme.json");var presetPath=Path.Combine(folder,"preset.json");
            if(File.Exists(themePath))
            {
                var theme=ThemeCatalog.ReadTheme(themePath);
                Presentation.ThemeCoverRenderer.Save(Presentation.ThemeCoverRenderer.Cursor(theme),Path.Combine(output,theme.Id+".png"));
            }
            else if(File.Exists(presetPath))
            {
                var preset=JsonSerializer.Deserialize<TrailPreset>(File.ReadAllText(presetPath),Storage.Json)!;preset.Validate();
                Presentation.ThemeCoverRenderer.Save(Presentation.ThemeCoverRenderer.Trail(preset),Path.Combine(output,preset.Id+".png"));
            }
        }
    }
    public static void Export(string source,string output,string privateKey)
    {
        Directory.CreateDirectory(output);var catalog=new RemoteCatalog{CatalogVersion="1.0.0"};
        var media=Path.Combine(output,"catalog","v1","previews");var packages=Path.Combine(output,"packages");Directory.CreateDirectory(media);Directory.CreateDirectory(packages);
        foreach(var folder in Directory.EnumerateDirectories(Path.Combine(source,"assets","themes")).Where(d=>Path.GetFileName(d).StartsWith("cute-")))
        {
            ExportCursor(folder,false);if(Path.GetFileName(folder)=="cute-cool-cat")ExportCursor(folder,true);
        }
        foreach(var p in new[]{
            new TrailPreset{Id="neon-blue",Name="Neon Blue",Description="Continuous ice-blue light with a compact, readable trail.",RendererType="neon",Length=180,Duration=320,Opacity=.55,Particles=0,Color="#87CFFF"},
            new TrailPreset{Id="sakura-pink",Name="Sakura Pink",Description="Soft pink neon with a restrained glow.",RendererType="neon",Length=150,Duration=360,Opacity=.45,Particles=0,Color="#FFB7D8"},
            new TrailPreset{Id="aurora-flow",Name="Aurora Flow",Description="A pale violet comet using the original trusted Comet renderer.",RendererType="comet",Length=210,Duration=420,Opacity=.55,Particles=14,Color="#B7A8FF"},
            new TrailPreset{Id="golden-spark",Name="Golden Spark",Description="Fine golden particles dispersing through the trusted Stardust renderer.",RendererType="stardust",Length=180,Duration=480,Opacity=.65,Particles=20,Color="#FFCE87"}})
        {
            p.Version="1.0.0";p.Validate();var staging=Path.Combine(output,"work",p.Id);Directory.CreateDirectory(staging);Storage.Write(Path.Combine(staging,"preset.json"),p);File.WriteAllText(Path.Combine(staging,"NOTICE.txt"),"Original CURSORIUM numeric preset. MIT license. No executable renderer code.\n"+File.ReadAllText(Path.Combine(source,"LICENSE")));
            var settings=new Settings();p.Apply(settings);var model=new TrailModel();for(int i=0;i<90;i++){double x=110+i*4.8,y=175+Math.Sin(i*.065)*65;model.Add(x,y,i*3,settings);}var painter=new TrailPainter(model,settings){Width=640,Height=360,Now=270,PhysicalCoordinates=false};painter.Measure(new Size(640,360));painter.Arrange(new Rect(0,0,640,360));
            SaveCover(p.Name,null,painter,Path.Combine(media,p.Id+".png"));AddPackage(p.Id,p.Name,p.Author,p.Description,"trail",p.Version,"MIT","https://github.com/catvir4493/cursorium",staging,new(),new(),0,new(){"Minimal"},null);
        }
        catalog.Validate();var index=Path.Combine(output,"catalog","v1","index.json");Storage.Write(index,catalog);using var rsa=RSA.Create();rsa.ImportFromPem(File.ReadAllText(privateKey));var bytes=File.ReadAllBytes(index);File.WriteAllBytes(index+".sig",rsa.SignData(bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pss));RemoteCatalogService.Verify(bytes,File.ReadAllBytes(index+".sig"));
        File.Copy(Path.Combine(source,"docs","catalog-v1.schema.json"),Path.Combine(output,"catalog","v1","schema.json"),true);
        File.Copy(Path.Combine(source,"src","Frost.Core","Online","catalog-public-key.pem"),Path.Combine(output,"catalog-public-key.pem"),true);
        void ExportCursor(string folder,bool superstar)
        {
            var original=ThemeCatalog.ReadTheme(Path.Combine(folder,"theme.json"));var id=superstar?"cool-cat-superstar":original.Id;var staging=Path.Combine(output,"work",id);Directory.CreateDirectory(staging);
            foreach(var file in Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories).Where(f=>Path.GetExtension(f).ToLowerInvariant() is ".cur" or ".ani")){var dest=Path.Combine(staging,Path.GetRelativePath(folder,file));Directory.CreateDirectory(Path.GetDirectoryName(dest)!);File.Copy(file,dest,true);}
            var theme=JsonSerializer.Deserialize<CursorTheme>(JsonSerializer.Serialize(original,Storage.Json),Storage.Json)!;theme.Id=id;theme.Version="1.0.0";
            if(superstar)
            {
                var bonus=theme.SourceFiles.Single(f=>f.OriginalName=="Cool Cat i'm a superstar.ani");var normal=theme.SourceFiles.Single(f=>f.Path==theme.Roles["Normal"]);normal.Path="Extras/classic.ani";Directory.CreateDirectory(Path.Combine(staging,"Extras"));File.Copy(Path.Combine(folder,theme.Roles["Normal"]),Path.Combine(staging,normal.Path),true);File.Copy(Path.Combine(folder,bonus.Path),Path.Combine(staging,theme.Roles["Normal"]),true);bonus.Path=theme.Roles["Normal"];theme.Name="Cool Cat — Superstar";theme.Description="Bobcat's original Superstar bonus animation as Normal, with all 15 original roles. CURSORIUM mapping variant; no artwork modified.";
            }
            File.WriteAllText(Path.Combine(staging,"NOTICE.txt"),File.ReadAllText(Path.Combine(folder,"NOTICE.md"))+"\nOnline package version 1.0.0. "+(superstar?"Normal uses the author's Superstar bonus animation. Other roles and original source files are preserved.":"Original v1.2 role mapping preserved.")+"\n"+File.ReadAllText(Path.Combine(source,"LICENSE")));
            Storage.Write(Path.Combine(staging,"theme.json"),theme);var validated=ThemeCatalog.ReadTheme(Path.Combine(staging,"theme.json"));SaveCover(theme.Name,validated,null,Path.Combine(media,id+".png"));
            AddPackage(id,theme.Name,theme.Author,theme.Description,"cursor",theme.Version,theme.License,theme.SourceUrl,staging,theme.Roles.Keys.ToList(),theme.Roles.Keys.Except(theme.InheritedRoles.Keys).ToList(),theme.AnimatedRoleCount,new(){"Cute","Pixel"},theme.PublishedAt);
        }
        void AddPackage(string id,string name,string author,string description,string type,string version,string license,string sourceUrl,string staging,List<string> roles,List<string> nativeRoles,int animated,List<string> tags,string? date)
        {
            var path=Path.Combine(packages,id+".zip");if(File.Exists(path))File.Delete(path);ZipFile.CreateFromDirectory(staging,path,CompressionLevel.Optimal,false);using var checkedZip=CursorPack.OpenValidatedArchive(path);
            catalog.Resources.Add(new(){Id=id,Name=name,Author=author,Description=description,Type=type,Version=version,License=license,SourceUrl=sourceUrl,SupportedRoles=roles,NativeRoles=nativeRoles,AnimatedCursorCount=animated,Tags=tags,Featured=id is "cute-bunny-cute-kawaii" or "cool-cat-superstar" or "neon-blue",LastUpdated=string.IsNullOrWhiteSpace(date)?null:date,AddedAt=DateTimeOffset.UtcNow,CoverUrl=$"https://catvir4493.github.io/cursorium-library/catalog/v1/previews/{id}.png",PreviewMedia=new(){$"https://catvir4493.github.io/cursorium-library/catalog/v1/previews/{id}.png"},PackageUrl=$"https://github.com/catvir4493/cursorium-library/releases/download/library-v1.0.0/{id}.zip",PackageSize=new FileInfo(path).Length,SHA256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()});
        }
    }
    private static void SaveCover(string name,CursorTheme? theme,TrailPainter? painter,string path)
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(19,30,48)),null,new Rect(0,0,640,360));
            dc.DrawText(new FormattedText(name,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),25,new SolidColorBrush(Color.FromRgb(243,246,251)),1),new Point(26,26));
            if(theme!=null)
            {
                int index=0;foreach(var role in new[]{"Normal","Link","Help","Busy"})
                {
                    var handle=Native.LoadOwnedCursor(theme.Resource(role));try{var image=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());dc.DrawImage(image,new Rect(48+index*145,130,105,105));}finally{Native.DestroyCursor(handle);}index++;
                }
            }
        }
        var bitmap=new RenderTargetBitmap(640,360,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);if(painter!=null)bitmap.Render(painter);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
}
