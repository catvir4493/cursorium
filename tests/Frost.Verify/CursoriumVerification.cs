using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Win32;
using Frost.Core;

internal static class CursoriumVerification
{
    private static readonly List<object> checks=new();
    private static void Check(bool condition,string name){checks.Add(new{name,passed=condition});if(!condition)throw new Exception(name);}
    private static void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception ex)when(ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException){rejected=true;}Check(rejected,name);}
    private static string SnapshotValues(ThemeBackup backup)=>JsonSerializer.Serialize(new{backup.Cursors,backup.Scheme,backup.Schemes},Storage.Json);
    public static void Run(string appDirectory,string output,bool native)
    {
        Directory.CreateDirectory(output);Storage.UseTestingRoot(Path.Combine(output,"core-data-"+Guid.NewGuid().ToString("N")));
        var theme=new ThemeService();var baseline=theme.Snapshot("CURSORIUM / frost","CURSORIUM / obsidian");bool success=false;string? error=null;
        try
        {
            var studio=new StudioService(appDirectory,theme);var catalog=studio.Catalog;
            Check(catalog.Themes.Count==3&&catalog.Warnings.Count==0,"Catalog loads original system, FROST and Obsidian");
            Check(studio.Effects.Trails.Select(t=>t.Id).SequenceEqual(new[]{"comet","minimal","neon","stardust"}),"Four independent presets come from JSON");
            var frost=catalog.Find("frost");var dark=catalog.Find("obsidian");
            foreach(var t in new[]{frost,dark})foreach(var role in CursorRoles.All){CursorResource.Validate(t.Resource(role.Name));Check(true,t.Id+" native CUR/ANI validated: "+role.Name);}
            Check(!File.ReadAllBytes(frost.Resource("Normal")).SequenceEqual(File.ReadAllBytes(dark.Resource("Normal"))),"Built-in native themes have different actual pixels");
            Reject(()=>ThemeCatalog.SafeResource(frost.DirectoryPath,"../frost.ico"),"Theme paths reject traversal");
            Reject(()=>ThemeCatalog.SafeResource(frost.DirectoryPath,frost.Resource("Normal")),"Theme paths reject absolute resources");
            var bad=Path.Combine(Storage.Root,"invalid.cur");Directory.CreateDirectory(Storage.Root);File.WriteAllBytes(bad,new byte[32]);Reject(()=>CursorResource.Validate(bad),"Invalid CUR bytes rejected");
            var spoof=Path.Combine(Storage.Root,"cursor.exe");File.Copy(frost.Resource("Normal"),spoof,true);Reject(()=>CursorResource.Validate(spoof),"Executable extensions rejected even with valid cursor bytes");
            var brokenAni=Path.Combine(Storage.Root,"broken.ani");var ani=File.ReadAllBytes(frost.Resource("Busy"));ani[4]=0;File.WriteAllBytes(brokenAni,ani);Reject(()=>CursorResource.Validate(brokenAni),"Malformed ANI RIFF rejected");
            var imported=catalog.Import("Imported animation",dark,new Dictionary<string,string>{{"Normal",frost.Resource("Normal")},{"Busy",frost.Resource("Busy")}});
            Check(imported.Roles["Busy"].EndsWith(".ani")&&File.ReadAllBytes(imported.Resource("Normal")).SequenceEqual(File.ReadAllBytes(frost.Resource("Normal"))),"Import stores CUR and ANI with correct role mapping");
            Check(new ThemeCatalog(appDirectory).Find(imported.Id).Roles.Count==15,"Imported complete theme survives catalog reload");
            var combination=new StudioConfiguration{CursorThemeId="frost"};studio.Effects.Trails.Single(t=>t.Id=="neon").Apply(combination.Effects);combination.Effects.Color="#BA79FF";combination.Effects.ClickEffect="ring";
            var p=studio.Profiles.Save(null,"Purple Frost",combination);combination.Effects.Color="#91DEFF";
            Check(p.Configuration.Effects.Color=="#BA79FF","Profile has an independent configuration snapshot");
            studio.Profiles.Rename(p.Id,"Renamed Frost");var reload=new ProfileManager();Check(reload.Profiles.Single().Name=="Renamed Frost"&&reload.Profiles.Single().Configuration.Effects.ClickEffect=="ring","Profile name and click parameters persist");
            var updated=p.Configuration.Clone();updated.Effects.Length=177;studio.Profiles.Save(p.Id,"Updated",updated);Check(new ProfileManager().Profiles.Single().Configuration.Effects.Length==177,"Saving an existing profile persists edits");
            studio.Profiles.Delete(p.Id);Check(new ProfileManager().Profiles.Count==0,"Profile deletion persists");
            var model=new TrailModel();var clicks=new ClickEffectManager(model);var effects=new Settings{TrailEnabled=false};
            Check(ClickEffectManager.DecodeRawButtons(1)==ClickButton.Left&&ClickEffectManager.DecodeRawButtons(4)==ClickButton.Right&&ClickEffectManager.DecodeRawButtons(2|8)==ClickButton.None,"Raw Input distinguishes button-down identity and ignores releases");
            clicks.Observe(ClickButton.Left|ClickButton.Right,1,2,0,effects);Check(model.Halos.Select(h=>h.Button).SequenceEqual(new[]{ClickButton.Left,ClickButton.Right}),"Click effects work independently with trails disabled");
            effects.ClickEffect="off";clicks.Observe(ClickButton.Left,1,2,1,effects);model.Expire(2,effects);Check(model.Halos.Count==0,"Off clears click effects");
            foreach(var preset in studio.Effects.Trails)
            {
                preset.Apply(effects);model.Clear();for(int i=0;i<400;i++)model.Add(Math.Cos(i*.02)*100,Math.Sin(i*.02)*100,i*5,effects);Check(model.Points.Count<=256,"Bounded history: "+preset.Id);model.Expire(5000,effects);Check(!model.HasContent,"Time-based expiry: "+preset.Id);
            }
            if(native)
            {
                theme.ApplyTheme(frost);Check(theme.IsActive(frost),"Actual Windows switch to FROST");var original=JsonSerializer.Deserialize<ThemeBackup>(File.ReadAllText(theme.BackupPath),Storage.Json)!;
                Check(JsonSerializer.Serialize(original.Cursors)==JsonSerializer.Serialize(baseline.Cursors),"First original configuration preserved");
                theme.ApplyTheme(dark);Check(theme.IsActive(dark),"Actual Windows switch to distinct Obsidian theme");
                var before=SnapshotValues(theme.Snapshot("CURSORIUM / frost","CURSORIUM / obsidian"));var backupText=File.ReadAllText(theme.BackupPath);
                theme.AfterRoleWrite=index=>{if(index==4)throw new IOException("Injected failure after four native role writes.");};
                Reject(()=>theme.ApplyTheme(frost),"Injected partial native failure is reported");theme.AfterRoleWrite=null;
                Check(before==SnapshotValues(theme.Snapshot("CURSORIUM / frost","CURSORIUM / obsidian"))&&backupText==File.ReadAllText(theme.BackupPath),"Partial failure rolls back registry types, values and original backup");
                Reject(()=>theme.ApplyTheme(frost,()=>throw new IOException("Applied-state persistence failed.")),"Persistence failure after native apply is reported");
                Check(before==SnapshotValues(theme.Snapshot("CURSORIUM / frost","CURSORIUM / obsidian")),"Persistence failure rolls back the active Windows theme");
                var config=new StudioConfiguration{CursorThemeId=imported.Id,Effects=p.Configuration.Effects.Clone()};studio.Apply(config);
                Check(theme.IsActive(imported)&&studio.LiveEffects.TrailEffect=="neon"&&studio.LiveEffects.ClickEffect=="ring","Imported theme + Neon + Ring actually apply independently");
                var restarted=new StudioService(appDirectory,new ThemeService());Check(JsonSerializer.Serialize(restarted.State.Applied)==JsonSerializer.Serialize(config),"Last successful combination restored by new service instance");
                var snapshot=theme.Snapshot();var stateText=File.ReadAllText(StudioService.StatePath);var backupBefore=File.ReadAllText(theme.BackupPath);
                Storage.Write(theme.JournalPath,new ThemeTransaction(snapshot,backupBefore,stateText));
                using(var key=Registry.CurrentUser.CreateSubKey(ThemeService.KeyPath))key.SetValue("Arrow","interrupted-apply.cur");Storage.WriteText(StudioService.StatePath,"broken");
                theme.Recover();Check(SnapshotValues(snapshot)==SnapshotValues(theme.Snapshot())&&stateText==File.ReadAllText(StudioService.StatePath),"Restart journal recovery restores both registry and applied JSON");
                studio.RestoreOriginal();Check(!theme.HasBackup&&SnapshotValues(baseline)==SnapshotValues(theme.Snapshot("CURSORIUM / frost","CURSORIUM / obsidian")),"Restore returns exact pre-test Windows configuration and scheme entries");
            }
            success=true;
        }
        catch(Exception ex){error=ex.ToString();throw;}
        finally
        {
            theme.AfterRoleWrite=null;if(theme.HasBackup)theme.Restore();
            File.WriteAllText(Path.Combine(output,"core-report.json"),JsonSerializer.Serialize(new{passed=success,error,nativeRoundtrip=native,checks},Storage.Json));
        }
        Console.WriteLine("CURSORIUM PASS: "+checks.Count+" checks; native roundtrip="+native);
    }
}
