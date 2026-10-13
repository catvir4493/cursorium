using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text.Json;
using System.Security.Cryptography;
using Frost.Core;

internal static class CuteCollectionVerification
{
    public static void Run(StudioService studio,ThemeService service,bool native,Action<bool,string> check,Action<Action,string> reject)
    {
        var catalog=studio.Catalog;var cute=catalog.Themes.Where(t=>t.Collection=="cute").ToArray();
        check(cute.Length==5,"Five actual Cute Collection manifests load without warnings");
        foreach(var theme in cute)
        {
            check(theme.Roles.Count==15&&theme.SourceFiles.Count>0&&theme.SourceUrl.StartsWith("https://www.rw-designer.com/cursor-set/")&&theme.License.Contains("Public Domain"),theme.Name+": real source, license and complete roles");
            check(theme.SourceFiles.All(f=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ThemeCatalog.SafeResource(theme.DirectoryPath,f.Path)))).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase)),theme.Name+": every source cursor SHA256 matches");
            check(theme.NativeRoleCount==(theme.Id.Contains("bunny")||theme.Id=="cute-pastel"?12:15),theme.Name+": original and inherited counts are accurate");
            foreach(var inherited in theme.InheritedRoles)check(File.ReadAllBytes(theme.Resource(inherited.Key)).SequenceEqual(File.ReadAllBytes(catalog.Find(inherited.Value).Resource(inherited.Key))),theme.Name+": explicit inherited bytes "+inherited.Key);
            foreach(var resource in theme.SourceFiles.Where(f=>f.Path.EndsWith(".ani")))
            {
                var timeline=AniTimeline.Read(File.ReadAllBytes(ThemeCatalog.SafeResource(theme.DirectoryPath,resource.Path)));
                check(timeline.Sequence.Length==timeline.Jiffies.Length&&timeline.DurationMilliseconds>0&&timeline.StepAt(timeline.DurationMilliseconds)==0,theme.Name+": validated original ANI timeline "+resource.OriginalName);
            }
        }
        foreach(var (filename,role) in new[]{("normal.cur","Normal"),("busy.ani","Busy"),("pinkheart link select.ani","Link"),("cool cat unavailiable.ani","Unavailable"),("working in background.ani","Working"),("Pastel diagonal resize 1.cur","DiagonalNWSE"),("Diagonal resize 2.ani","DiagonalNESW"),("bunny unavailble.ani","Unavailable")})check(CursorRoleRecognition.Match(filename)==role,"Order-independent role recognition: "+filename);
        check(CursorRoleRecognition.Match("unknown.cur")==null&&CursorRoleRecognition.Match("normal busy.cur")==null&&CursorRoleRecognition.Match("diagonal resize.ani")==null,"Unknown or contradictory roles require manual review");
        check(CursorRoleRecognition.Match("Busy/Normal.cur")=="Normal"&&CursorRoleRecognition.Match("Normal/unknown.cur")==null,"Parent folder names cannot silently assign cursor roles");
        var frost=catalog.Find("frost");var source=File.ReadAllBytes(frost.Resource("Normal"));var scratch=Path.Combine(Storage.Root,"pack-checks");Directory.CreateDirectory(scratch);
        var animated=File.ReadAllBytes(catalog.Find("cute-kawaii-cat-curs0rs").Resource("Normal"));
        int Chunk(byte[] bytes,string id){for(int p=12;p<bytes.Length;){if(System.Text.Encoding.ASCII.GetString(bytes,p,4)==id)return p;p+=8+(int)BitConverter.ToUInt32(bytes,p+4)+((int)BitConverter.ToUInt32(bytes,p+4)&1);}throw new Exception("Missing fixture chunk");}
        var badSequence=(byte[])animated.Clone();int seq=Chunk(badSequence,"seq ");BitConverter.GetBytes(255u).CopyTo(badSequence,seq+8);reject(()=>AniTimeline.Read(badSequence),"ANI out-of-range original frame index rejected");
        var badRate=File.ReadAllBytes(catalog.Find("cute-kawaii-pink-heart").Resource("Busy"));int rate=Chunk(badRate,"rate");BitConverter.GetBytes(0u).CopyTo(badRate,rate+8);reject(()=>AniTimeline.Read(badRate),"ANI zero step timing rejected");
        string Zip(string name,Action<ZipArchive> write)
        {
            var path=Path.Combine(scratch,name+".zip");using(var z=ZipFile.Open(path,ZipArchiveMode.Create))write(z);return path;
        }
        void Entry(ZipArchive z,string name,byte[] data,int attributes=0,CompressionLevel level=CompressionLevel.Optimal)
        {var entry=z.CreateEntry(name,level);entry.ExternalAttributes=attributes;using var stream=entry.Open();stream.Write(data);}
        var good=Zip("good",z=>{Entry(z,"nested/normal.cur",source);Entry(z,"nested/busy.ani",File.ReadAllBytes(frost.Resource("Busy")));Entry(z,"install.inf",new byte[22]);Entry(z,"run.exe",new byte[22]);});
        var oldHash=Convert.ToHexString(SHA256.HashData(source));
        using(var pack=CursorPack.Open(good))
        {
            check(pack.Files.Count==2&&pack.Candidates()["Normal"].Length==1&&pack.Candidates()["Busy"].Length==1,"ZIP stages only validated cursor bytes; nested names recognized");
            var mapping=pack.Files.ToDictionary(f=>f.SuggestedRole!,f=>f.Path);var first=catalog.Import("Reviewed pack",frost,mapping,pack.Files);var second=catalog.Import("Reviewed pack",frost,mapping,pack.Files);
            check(first.Id!=second.Id&&first.SourceFiles.Any(f=>f.OriginalName=="nested/normal.cur")&&first.InheritedRoles.Count==13,"Repeat import creates independent IDs and preserves reviewed provenance");
            check(first.DirectoryPath.StartsWith(Path.Combine(Storage.Root,"Themes"),StringComparison.OrdinalIgnoreCase)&&ThemeCatalog.ReadTheme(Path.Combine(first.DirectoryPath,"theme.json")).Id==first.Id,"Committed import paths are permanent and available");
        }
        check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(frost.Resource("Normal"))))==oldHash,"Import leaves original source files unchanged");
        var conflicting=Zip("conflict",z=>{Entry(z,"busy.cur",source);Entry(z,"Busy 2.cur",source);});using(var pack=CursorPack.Open(conflicting))check(pack.Candidates()["Busy"].Length==2,"Duplicate role names expose conflict rather than silently choosing");
        foreach(var path in new[]{"../normal.cur","C:/normal.cur","/normal.cur","nested/../../normal.cur","folder\\..\\normal.cur"})reject(()=>{using var pack=CursorPack.Open(Zip("bad-"+Guid.NewGuid().ToString("N"),z=>Entry(z,path,source)));},"ZIP rejects traversal/rooted path: "+path);
        reject(()=>{using var pack=CursorPack.Open(Zip("symlink",z=>Entry(z,"normal.cur",source,unchecked((int)0xA1FF0000))));},"ZIP rejects Unix symbolic links");
        reject(()=>{using var pack=CursorPack.Open(Zip("reparse",z=>Entry(z,"normal.cur",source,0x400)));},"ZIP rejects Windows reparse entries");
        reject(()=>{using var pack=CursorPack.Open(Zip("too-many",z=>{for(int i=0;i<=CursorPack.MaxEntries;i++)Entry(z,i+".txt",new byte[22]);}));},"ZIP entry limit enforced");
        var forged=Path.Combine(scratch,"forged-directory.zip");
        using(var memory=new MemoryStream())
        {
            using(var writer=new BinaryWriter(memory,System.Text.Encoding.UTF8,true))
            {
                for(int i=0;i<4096;i++)
                {
                    byte[] name=System.Text.Encoding.ASCII.GetBytes("cursor-"+i+".cur");var header=new byte[46];BitConverter.GetBytes(0x02014B50u).CopyTo(header,0);BitConverter.GetBytes((ushort)20).CopyTo(header,4);BitConverter.GetBytes((ushort)20).CopyTo(header,6);BitConverter.GetBytes((ushort)name.Length).CopyTo(header,28);writer.Write(header);writer.Write(name);
                }
                uint size=(uint)memory.Length;var eocd=new byte[22];BitConverter.GetBytes(0x06054B50u).CopyTo(eocd,0);BitConverter.GetBytes((ushort)1).CopyTo(eocd,8);BitConverter.GetBytes((ushort)1).CopyTo(eocd,10);BitConverter.GetBytes(size).CopyTo(eocd,12);writer.Write(eocd);
            }
            File.WriteAllBytes(forged,memory.ToArray());
        }
        long allocated=GC.GetAllocatedBytesForCurrentThread();reject(()=>{using var pack=CursorPack.Open(forged);},"ZIP rejects forged EOCD count with 4096 actual directory entries");
        check(GC.GetAllocatedBytesForCurrentThread()-allocated<512*1024,"Forged ZIP directory rejected before unbounded entry-object allocation");
        reject(()=>{using var pack=CursorPack.Open(Zip("too-many-cursors",z=>{for(int i=0;i<=CursorPack.MaxFiles;i++)Entry(z,i+".cur",source);}));},"ZIP cursor count limit enforced");
        reject(()=>{using var pack=CursorPack.Open(Zip("large",z=>Entry(z,"normal.cur",new byte[CursorResource.MaxBytes+1],level:CompressionLevel.NoCompression)));},"ZIP per-file size limit enforced");
        reject(()=>{using var pack=CursorPack.Open(Zip("ratio",z=>Entry(z,"normal.cur",new byte[1024*1024])));},"ZIP suspicious compression ratio rejected");
        reject(()=>{using var pack=CursorPack.Open(Zip("invalid",z=>Entry(z,"normal.cur",new byte[64])));},"ZIP malformed cursor rejected");
        check(!Directory.Exists(Path.Combine(Storage.Root,"ImportStaging"))||!Directory.EnumerateFileSystemEntries(Path.Combine(Storage.Root,"ImportStaging")).Any(),"Failed and successful imports clean all uncommitted staging resources");
        using(var pack=CursorPack.Open(frost.DirectoryPath))check(pack.Files.Count>=15,"Directory import stages validated CUR and ANI resources");
        var prefs=new ThemeLibraryPreferences{Filter="cute"};prefs.Favorites.Add(cute[0].Id);prefs.Save();var read=ThemeLibraryPreferences.Load();check(read.Filter=="cute"&&read.Favorites.SetEquals(prefs.Favorites),"Library filter and favorites persist independently of applied state");
        var profile=studio.Profiles.Save(null,"Cute Neon",new(){CursorThemeId=cute[0].Id,Effects=new(){TrailEffect="neon",ClickEffect="ring",Color="#BA79FF"}});check(new ProfileManager().Profiles.Any(p=>p.Id==profile.Id&&p.Configuration.CursorThemeId==cute[0].Id),"Cute themes persist in freely combined profiles");
        if(native)
        {
            string? firstBackup=null;
            foreach(var t in cute)
            {
                studio.Apply(new(){CursorThemeId=t.Id,Effects=profile.Configuration.Effects.Clone()});var backup=JsonSerializer.Deserialize<ThemeBackup>(File.ReadAllText(service.BackupPath),Storage.Json)!;var original=JsonSerializer.Serialize(new{backup.Created,backup.Cursors,backup.Scheme});firstBackup??=original;
                check(service.IsActive(t)&&original==firstBackup&&studio.LiveEffects.TrailEffect=="neon"&&studio.LiveEffects.ClickEffect=="ring",t.Name+": actual native Apply + independent effects + original backup preserved");
            }
            string Snapshot(){var snapshot=service.Snapshot(cute.Select(t=>"CURSORIUM / "+t.Id).ToArray());return JsonSerializer.Serialize(new{snapshot.Cursors,snapshot.Scheme,snapshot.Schemes},Storage.Json);}
            var before=Snapshot();service.AfterRoleWrite=i=>{if(i==4)throw new IOException("Cute transaction injected failure");};
            reject(()=>studio.Apply(new(){CursorThemeId=cute[0].Id}),"Cute theme partial native failure reported");service.AfterRoleWrite=null;
            check(Snapshot()==before,"Cute failed Apply rolls back exact native state");studio.RestoreOriginal();
        }
    }
}
