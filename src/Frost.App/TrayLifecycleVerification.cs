using System.Diagnostics;
using System.Runtime.InteropServices;
using Frost.Core;

namespace Frost.App;

internal static class TrayLifecycleVerification
{
    [DllImport("user32.dll")]private static extern int GetGuiResources(IntPtr process,int kind);
    public static async Task Run(App app,TrailEngine engine,string[] args)
    {
        string output=Path.GetFullPath(args[Array.IndexOf(args,"--report")+1]);Directory.CreateDirectory(output);
        var checks=new List<object>();var before=System.Text.Json.JsonSerializer.Serialize(new ThemeService().Snapshot().Cursors);
        var started=DateTimeOffset.UtcNow;bool passed=false;string? error=null;using var process=Process.GetCurrentProcess();
        void Check(bool value,string name){checks.Add(new{name,passed=value});if(!value)throw new InvalidOperationException(name);}
        Process Request(string command)
        {
            var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
            foreach(var value in new[]{"--tray-lifecycle-review","--isolated-test","--test-data-root",Storage.Root,"--report",output})start.ArgumentList.Add(value);
            if(command=="shutdown")start.ArgumentList.Add("--shutdown");
            return Process.Start(start)??throw new IOException("Could not start an isolated IPC client.");
        }
        try
        {
            await Task.Delay(500);process.Refresh();long memory=process.WorkingSet64;int gdi=GetGuiResources(process.Handle,0),user=GetGuiResources(process.Handle,1);
            Check(!app.PanelCreated,"Tray startup defers the actual WPF shell");
            Check(engine.Windows.Count==Native.Monitors().Count,"Deferred shell retains per-monitor desktop overlays");
            Check(app.TrayMenu.Items.Count>=6,"Deferred shell retains real tray operations");
            if(args.Contains("--quit-without-panel"))
            {
                ((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[5]).PerformClick();
                Check(app.Exiting&&!app.PanelCreated&&engine.Windows.Count==0,"Actual tray Quit releases overlays without ever constructing the shell");
                Check(before==System.Text.Json.JsonSerializer.Serialize(new ThemeService().Snapshot().Cursors),"No-window tray exit preserves native cursor state");
                Storage.Write(Path.Combine(output,"tray-lifecycle-report.json"),new{passed=true,error,started,ended=DateTimeOffset.UtcNow,checks,beforeShell=new{workingSet=memory,gdi,user},notes="Actual tray Quit before any UI creation; caller also waits for process exit."});passed=true;return;
            }
            using(var client=Request("show")){await client.WaitForExitAsync();Check(client.ExitCode==0,"Separate process sends real existing IPC show request");}
            var timer=Stopwatch.StartNew();while((!app.PanelCreated||!app.Panel.IsVisible||!app.Panel.IsLoaded)&&timer.ElapsedMilliseconds<5000)await Task.Delay(20);
            Check(app.PanelCreated&&app.Panel.IsVisible&&app.Panel.IsLoaded,"First IPC request constructs and loads the real WPF window");
            await Task.Delay(150);
            if(args.Contains("--require-cached-catalog"))Check(app.Panel.Discover.Catalog.Current!=null,"Deferred first show attaches shared resources and reads the signed startup cache");
            var panel=app.Panel;var length=(System.Windows.Controls.Slider)panel.FindName("LengthSlider");length.Value=length.Value==177?193:177;double draftLength=panel.Draft.Effects.Length;
            panel.Close();Check(!panel.IsVisible&&!app.Exiting,"Actual close hides to the running tray");
            using(var client=Request("show")){await client.WaitForExitAsync();Check(client.ExitCode==0,"Second real IPC show request succeeds");}
            timer.Restart();while(!panel.IsVisible&&timer.ElapsedMilliseconds<3000)await Task.Delay(20);
            Check(ReferenceEquals(panel,app.Panel)&&panel.IsVisible&&panel.Draft.Effects.Length==draftLength&&panel.IsDirty,"Reopen retains the same window and its edited in-memory draft");
            ((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[2]).PerformClick();Check(engine.PausedByUser,"Real tray pause works after deferred construction");
            ((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[2]).PerformClick();Check(!engine.PausedByUser,"Real tray resume works after deferred construction");
            Check(before==System.Text.Json.JsonSerializer.Serialize(new ThemeService().Snapshot().Cursors),"Lazy shell and IPC lifecycle never Apply system cursors");
            // Exit is verified externally by the caller's process completion.
            process.Refresh();Storage.Write(Path.Combine(output,"tray-lifecycle-report.json"),new{passed=true,error,started,ended=DateTimeOffset.UtcNow,checks,beforeShell=new{workingSet=memory,gdi,user},afterShell=new{workingSet=process.WorkingSet64,gdi=GetGuiResources(process.Handle,0),user=GetGuiResources(process.Handle,1)},notes="Actual isolated tray, separate-process IPC, WPF first show, close/reopen and tray actions. User data and system pointers are unchanged."});passed=true;
        }
        catch(Exception ex){error=ex.GetType().Name;Storage.Write(Path.Combine(output,"tray-lifecycle-report.json"),new{passed=false,error,started,ended=DateTimeOffset.UtcNow,checks});}
        finally{app.ExitApp(passed?0:1);}
    }
}
