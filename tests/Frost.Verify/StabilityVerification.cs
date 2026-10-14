using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using Microsoft.Win32;
using Frost.Core;

internal static class StabilityVerification
{
    private static readonly List<object> checks=new();
    private static void Check(bool value,string name){checks.Add(new{name,passed=value});if(!value)throw new Exception(name);}
    private static void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception e)when(e is IOException or InvalidDataException or JsonException){rejected=true;}Check(rejected,name);}
    private static string Values(ThemeBackup value)=>JsonSerializer.Serialize(new{value.Cursors,value.Schemes});
    public static int Child(string mode,string root,string application)
    {
        Storage.UseTestingRoot(root);
        if(mode=="hold")
        {
            using var gate=new Mutex(false,@"Local\FrostWhiteComet.Theme");gate.WaitOne();
            try{Storage.WriteText(Path.Combine(root,"ready"),"ready");Thread.Sleep(12000);}finally{gate.ReleaseMutex();}return 0;
        }
        var themes=new ThemeService();themes.AfterRoleWrite=i=>{if(i==4)Environment.Exit(77);};themes.ApplyTheme(new ThemeCatalog(application).Find("frost"));return 3;
    }
    private static Process Start(string mode,string root,string application)
    {
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
        if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(StabilityVerification).Assembly.Location);
        foreach(var a in new[]{"--stability-child",mode,root,application})start.ArgumentList.Add(a);
        return Process.Start(start)??throw new IOException("Could not start isolated recovery child.");
    }
    public static void Run(string application,string output)
    {
        Directory.CreateDirectory(output);string root=Path.Combine(Path.GetFullPath(output),"recovery-"+Guid.NewGuid().ToString("N"));Storage.UseTestingRoot(root);
        var theme=new ThemeService();var before=theme.Snapshot("CURSORIUM / frost","CURSORIUM / obsidian");bool passed=false;string? error=null;var started=DateTimeOffset.UtcNow;
        try
        {
            Reject(()=>Storage.UseTestingRoot(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FrostWhiteComet")),"Diagnostics reject production user data as test root");
            Reject(()=>Storage.UseTestingRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),"Diagnostics reject ancestors of production data");
            using(var registration=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet"))if(registration?.GetValue("InstallLocation") is string location)Reject(()=>Storage.UseTestingRoot(location),"Diagnostics reject registered installation directory");
            var catalog=new ThemeCatalog(application);var frost=catalog.Find("frost");var dark=catalog.Find("obsidian");
            _=theme.IsActive(frost);Check(!Directory.Exists(Path.Combine(root,"Theme","installed")),"Status query creates no immutable files");
            var longSource=Path.Combine(root,new string('l',90),new string('s',90));Directory.CreateDirectory(longSource);
            var temporaryBefore=Directory.GetFiles(Path.GetTempPath(),"cursorium-native-*").OrderBy(p=>p).ToArray();
            foreach(var role in new[]{"Normal","Busy"})
            {
                var file=Path.Combine(longSource,role+Path.GetExtension(frost.Resource(role)));File.Copy(frost.Resource(role),file);
                Check(file.Length>=260,"Explicit long-path cursor fixture: "+role);CursorResource.Validate(file);
                using var cursor=CursorResource.OpenPreviewCursor(file);Check(cursor!=null,"Native validation and WPF stream preview support long source: "+role);
                bool released=true;for(int cycle=0;cycle<32;cycle++){var owned=Native.LoadOwnedCursor(file);if(owned==IntPtr.Zero)throw new IOException("Repeated long-path native load failed.");released&=Native.DestroyCursor(owned);}
                Check(released,"Repeated long-source native handles are owned and released: "+role);
            }
            Check(temporaryBefore.SequenceEqual(Directory.GetFiles(Path.GetTempPath(),"cursorium-native-*").OrderBy(p=>p)),"Long-path validation removes bounded temporary copies");
            var longInstall=Path.Combine(root,new string('i',160));Storage.UseTestingRoot(longInstall);
            try{Reject(()=>new ThemeService().ApplyTheme(frost),"Overlong permanent native paths fail before Windows writes");Check(!Directory.Exists(Path.Combine(longInstall,"Theme","installed")),"Unsupported native target creates no half-installed resources");}
            finally{Storage.UseTestingRoot(root);}
            Check(Values(before)==Values(theme.Snapshot("CURSORIUM / frost","CURSORIUM / obsidian")),"Long-path refusal preserves exact Windows values and types");
            using(var child=Start("crash",root,application)){Check(child.WaitForExit(15000)&&child.ExitCode==77,"Actual process terminates after four native writes");}
            Check(File.Exists(theme.JournalPath),"Abrupt exit retains durable journal");theme.Recover();
            Check(Values(before)==Values(theme.Snapshot("CURSORIUM / frost","CURSORIUM / obsidian")),"Crash and abandoned mutex recover exact registry state");
            Check(!theme.HasBackup&&!File.Exists(theme.JournalPath),"Recovery restores prior absent original backup");
            using(var child=Start("crash",root,application)){Check(child.WaitForExit(15000)&&child.ExitCode==77,"Second actual crash creates retry evidence");}
            using(var key=Registry.CurrentUser.CreateSubKey(ThemeService.KeyPath))
            {
                var arrow=key.GetValue("Arrow",null,RegistryValueOptions.DoNotExpandEnvironmentNames);var kind=key.GetValueKind("Arrow");
                key.SetValue("Arrow",dark.Resource("Normal"),RegistryValueKind.String);var journal=File.ReadAllText(theme.JournalPath);
                Reject(theme.Recover,"Recovery refuses external edit");
                Check((string?)key.GetValue("Arrow")==dark.Resource("Normal")&&File.ReadAllText(theme.JournalPath)==journal,"External selection and journal retained");key.SetValue("Arrow",arrow!,kind);
            }
            theme.Recover();Check(Values(before)==Values(theme.Snapshot("CURSORIUM / frost","CURSORIUM / obsidian")),"Recovery retry succeeds after conflict resolution");
            theme.ApplyTheme(frost);string backup=File.ReadAllText(theme.BackupPath);
            using(var key=Registry.CurrentUser.CreateSubKey(ThemeService.KeyPath))
            {
                var arrow=key.GetValue("Arrow")!;key.SetValue("Arrow",dark.Resource("Normal"));Reject(()=>theme.Restore(),"Automatic restore refuses external selection");
                Check(File.ReadAllText(theme.BackupPath)==backup,"Refusal preserves first original backup");key.SetValue("Arrow",arrow);
            }
            var stable=Directory.GetFiles(Path.Combine(root,"Theme","installed","frost"),"Normal.cur",SearchOption.AllDirectories).Single();
            theme.ApplyTheme(dark);var bytes=File.ReadAllBytes(stable);File.Copy(dark.Resource("Normal"),stable,true);
            Reject(()=>theme.ApplyTheme(frost),"Substituted valid immutable CUR rejected");Check(theme.IsActive(dark),"Rejected resource leaves active theme unchanged");File.WriteAllBytes(stable,bytes);theme.Restore();
            Check(Values(before)==Values(theme.Snapshot("CURSORIUM / frost","CURSORIUM / obsidian")),"Ownership-aware restore returns exact original");
            Storage.WriteText(theme.JournalPath,"{\"SchemaVersion\":99}");var future=File.ReadAllText(theme.JournalPath);Reject(theme.Recover,"Unknown journal schema rejected");Check(File.ReadAllText(theme.JournalPath)==future,"Future journal bytes preserved");File.Delete(theme.JournalPath);
            using(var child=Start("hold",root,application))
            {
                var timer=Stopwatch.StartNew();while(!File.Exists(Path.Combine(root,"ready"))&&timer.ElapsedMilliseconds<5000)Thread.Sleep(20);
                Check(File.Exists(Path.Combine(root,"ready")),"Independent process owns native mutex");Reject(theme.Recover,"Concurrent native operation times out safely");Check(child.WaitForExit(5000),"Mutex owner exits normally");
            }
            Check(Values(before)==Values(theme.Snapshot("CURSORIUM / frost","CURSORIUM / obsidian")),"Concurrency refusal performs no native writes");
            var missing=before with{Cursors=before.Cursors.Select(e=>e.Name=="Arrow"?new RegistryEntry(e.Name,true,RegistryValueKind.String,JsonSerializer.SerializeToElement(Path.Combine(root,"unavailable.cur"))):e).ToList()};
            Storage.Write(theme.JournalPath,new ThemeTransaction(missing,null,null,Intended:before));Reject(theme.Recover,"Unavailable original resource stops recovery before writes");
            Check(File.Exists(theme.JournalPath)&&Values(before)==Values(theme.Snapshot("CURSORIUM / frost","CURSORIUM / obsidian")),"Missing-resource recovery retains evidence and exact current native state");Storage.Write(theme.JournalPath,new ThemeTransaction(before,null,null));theme.Recover();
            theme.ApplyTheme(frost);using(var locked=new FileStream(theme.BackupPath,FileMode.Open,FileAccess.Read,FileShare.None)){Reject(()=>theme.Restore(),"Denied backup access stops restore safely");Check(theme.IsActive(frost),"Denied access leaves active Windows theme valid");}theme.Restore();
            var sanitized=Storage.SanitizeDiagnostic("Failure C:\\Users\\Private Name\\secret.json\nBearer topsecret\ntoken=private-token");Check(!sanitized.Contains("Private Name")&&!sanitized.Contains("topsecret")&&!sanitized.Contains("private-token"),"Diagnostic logs redact local paths and credential-shaped values");
            var profiles=new ProfileManager();var profile=profiles.Save(null,"Original fixture",new());var profilePath=Path.Combine(root,"profiles.json");
            Storage.WriteText(profilePath,"{\"SchemaVersion\":99}");Reject(()=>profiles.Rename(profile.Id,"Must not overwrite"),"External future Profile schema blocks stale writes");Check(File.ReadAllText(profilePath)=="{\"SchemaVersion\":99}","Future Profile bytes preserved after stale write refusal");Reject(()=>new ProfileManager(),"Future Profile schema rejected on restart");File.Delete(profilePath);
            var studio=new StudioService(application,theme);Storage.WriteText(StudioService.StatePath,"{\"SchemaVersion\":99}");Reject(()=>studio.SetEffectsEnabled(false),"External studio edit blocks stale tray writes");Check(File.ReadAllText(StudioService.StatePath)=="{\"SchemaVersion\":99}","Future applied-state bytes preserved");File.Delete(StudioService.StatePath);
            var source=new Settings{Enabled=false,TrailEnabled=false,TrailEffect="neon",Length=331,Duration=811,Particles=21,Color="#ABCDEF",ClickEffect="ring",PowerSaving=true,PauseFullscreen=false,FrameRate=120,ClickRadius=47,HaloDuration=411,ClickOpacity=.71,Opacity=.67,Preset="Custom",ClickHalo=false};
            Check(JsonSerializer.Serialize(source)==JsonSerializer.Serialize(source.Clone()),"Allocation-light clone preserves every serialized setting");
            object Measure(Func<Settings> clone){for(int i=0;i<100;i++)clone();long allocated=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();for(int i=0;i<20000;i++)GC.KeepAlive(clone());return new{iterations=20000,milliseconds=timer.Elapsed.TotalMilliseconds,allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocated};}
            var oldClone=Measure(()=>JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(source,Storage.Json),Storage.Json)!);var newClone=Measure(source.Clone);
            Storage.Write(Path.Combine(Path.GetFullPath(output),"clone-performance.json"),new{oldClone,newClone,notes="Same process/runtime, 20,000 warmed operations; old production JSON algorithm versus typed copy. Not an end-to-end CPU or startup benchmark."});passed=true;
        }
        catch(Exception e){error=e.GetType().Name;throw;}
        finally
        {
            // Always restore this suite's known pre-test snapshot, including when
            // an injected conflict assertion fails. This recovery path is test-only.
            if(!passed)Storage.Write(theme.JournalPath,new ThemeTransaction(before,null,null));
            if(File.Exists(theme.JournalPath))theme.Recover();if(theme.HasBackup)theme.Restore();
            Storage.Write(Path.Combine(Path.GetFullPath(output),"recovery-report.json"),new{passed,error,started,ended=DateTimeOffset.UtcNow,checks});
        }
        Console.WriteLine("STABILITY PASS: "+checks.Count+" checks");
    }
}
