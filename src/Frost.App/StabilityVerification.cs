using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using Frost.Core;

namespace Frost.App;

// Explicit local diagnostics only. No telemetry, registry writes or automatic Apply.
internal static class StabilityVerification
{
    [DllImport("user32.dll")]private static extern int GetGuiResources(IntPtr process,int kind);
    public static async Task Run(App app,TrailEngine engine,string[] args)
    {
        string Option(string name,string fallback){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        var output=Path.GetFullPath(Option("--report","artifacts/stability"));Directory.CreateDirectory(output);
        int seconds=int.Parse(Option("--duration-seconds","60"),CultureInfo.InvariantCulture);
        if(seconds<5||seconds>259200){app.ExitApp(2);return;}
        var started=DateTimeOffset.UtcNow;var clock=Stopwatch.StartNew();using var process=Process.GetCurrentProcess();
        var settings=engine.Windows[0].Painter.Settings;var initial=settings.Clone();
        var before=System.Text.Json.JsonSerializer.Serialize(new ThemeService().Snapshot().Cursors);
        int gdi=GetGuiResources(process.Handle,0),user=GetGuiResources(process.Handle,1),cycles=0;bool passed=false;string? error=null;
        using var csv=new StreamWriter(Path.Combine(output,"samples.csv"));csv.WriteLine("seconds,mode,cpuOneCorePercent,workingSet,privateBytes,gdi,user,threads,totalAllocatedBytes,renderInvalidations,paintCount,meanPaintMs");
        var samples=new List<object>();var previousCpu=process.TotalProcessorTime;double previousTime=0;
        try
        {
            // The actual existing shell, preview and tray are exercised; motion is a
            // deterministic local-preview workload, not a physical input/hardware test.
            var profile=app.Studio.Profiles.Save(null,"Isolated stability fixture",app.Studio.State.Applied);
            var preview=(LivePreview)app.Panel.FindName("Preview");
            ((System.Windows.Controls.ListBox)app.Panel.FindName("Navigation")).SelectedIndex=1;
            using var http=new OfficialHttp();
            while(clock.Elapsed.TotalSeconds<seconds)
            {
                string mode=new[]{"idle","comet","minimal","neon","stardust","clicks","paused","display-rebuild"}[cycles%8];
                settings.CopyFrom(initial);settings.PauseFullscreen=false;settings.Enabled=true;engine.PausedByUser=mode=="paused";
                if(mode=="display-rebuild")engine.RefreshDisplays();
                if(mode is "comet" or "minimal" or "neon" or "stardust"){app.Studio.Effects.Trails.Single(p=>p.Id==mode).Apply(settings);settings.Length=360;settings.Particles=24;settings.Duration=900;}
                app.Panel.Show();app.Panel.WindowState=WindowState.Normal;preview.BringIntoView();await Task.Delay(50);preview.Update(settings);
                _=new RemoteCatalogService(http);
                double t=clock.Elapsed.TotalMilliseconds;
                if(mode is not ("idle" or "paused"))for(int i=0;i<64;i++)preview.Model.Add(100+i*3,100+Math.Sin(i*.2)*30,t+i*.001,settings);
                if(mode=="clicks")for(int i=0;i<32;i++)preview.Model.Click(160,100,t,i%2==0?ClickButton.Left:ClickButton.Right);
                preview.Update(settings);
                foreach(var role in CursorRoles.All)
                {
                    var h=Native.LoadOwnedCursor(app.Studio.Catalog.Find("frost").Resource(role.Name));
                    if(h==IntPtr.Zero)throw new IOException("Native cursor lifetime check failed.");Native.DestroyCursor(h);
                }
                app.Studio.Profiles.Rename(profile.Id,"Isolated cycle "+cycles);_ =new ProfileManager();
                await Task.Delay(700);app.Panel.Hide();await Task.Delay(300);
                process.Refresh();double now=clock.Elapsed.TotalSeconds;
                double cpu=(process.TotalProcessorTime-previousCpu).TotalSeconds/(now-previousTime)*100;previousCpu=process.TotalProcessorTime;previousTime=now;
                long paints=preview.PaintCount,ticks=preview.PaintTicks;
                double mean=paints==0?0:ticks*1000d/Stopwatch.Frequency/paints;
                var row=new{seconds=now,mode,cpuOneCorePercent=cpu,workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,gdi=GetGuiResources(process.Handle,0),user=GetGuiResources(process.Handle,1),threads=process.Threads.Count,totalAllocatedBytes=GC.GetTotalAllocatedBytes(),renderInvalidations=engine.RenderedFrames,paintCount=paints,meanPaintMs=mean};
                csv.WriteLine(FormattableString.Invariant($"{now:F3},{mode},{cpu:F3},{row.workingSet},{row.privateBytes},{row.gdi},{row.user},{row.threads},{row.totalAllocatedBytes},{row.renderInvalidations},{paints},{mean:F4}"));csv.Flush();
                if(samples.Count==720)samples.RemoveAt(0);samples.Add(row);cycles++;
                if(preview.Model.Points.Count>256||preview.Model.Halos.Count>8)throw new InvalidDataException("Unbounded preview history.");
            }
            app.Studio.Profiles.Delete(profile.Id);
            passed=before==System.Text.Json.JsonSerializer.Serialize(new ThemeService().Snapshot().Cursors);
            if(!passed)error="Native state changed during a preview-only diagnostic.";
        }
        catch(Exception ex){error=ex.GetType().Name;Storage.Log("Stability diagnostic failure: "+error);}
        finally
        {
            settings.CopyFrom(initial);engine.PausedByUser=false;app.Panel.Hide();
            Storage.Write(Path.Combine(output,"stability-report.json"),new{passed,error,started,ended=DateTimeOffset.UtcNow,durationSeconds=clock.Elapsed.TotalSeconds,cycles,runtime=RuntimeInformation.FrameworkDescription,os=Environment.OSVersion.VersionString,monitorCount=Native.Monitors().Count,gdiBefore=gdi,gdiAfter=GetGuiResources(process.Handle,0),userBefore=user,userAfter=GetGuiResources(process.Handle,1),samples,notes="Local synthetic preview plus real WPF show/hide, native load/release, isolated Profile and overlay rebuild. No system Apply, input injection, hardware hotplug, lock, sleep or telemetry. CSV retains all samples; JSON retains the latest 720."});
            app.ExitApp(passed?0:1);
        }
    }
}
