using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Frost.Core;
using Frost.App.Presentation;

namespace Frost.App;
internal static class DiscoverVerification
{
    [DllImport("user32.dll")]private static extern int GetGuiResources(IntPtr process,int flags);
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr handle);
    public static async Task Run(App app,string[] args)
    {
        var directory=Path.GetFullPath(args[Array.IndexOf(args,"--report")+1]);Directory.CreateDirectory(directory);var checks=new List<object>();var performance=new List<object>();var panel=app.Panel;var native=new ThemeService();var baseline=native.Snapshot();var baselineJson=JsonSerializer.Serialize(baseline.Cursors);Native.GetPhysicalCursorPos(out var pointer);var foreground=Native.GetForegroundWindow();bool passed=false;string? error=null;
        void Check(bool value,string name){checks.Add(new{name,passed=value});if(!value)throw new InvalidOperationException(name);}
        T Main<T>(string name)=>(T)panel.FindName(name);
        T Discover<T>(string name)=>(T)panel.Discover.FindName(name);
        async Task Capture(string name){await Task.Delay(180);InterfaceVerification.CaptureWindow(panel,Path.Combine(directory,name+".png"));}
        try
        {
            panel.Topmost=true;panel.Show();panel.Width=1280;panel.Height=830;Native.SetWindowPos(new WindowInteropHelper(panel).Handle,IntPtr.Zero,40,40,1280,830,0x10);panel.Activate();SetForegroundWindow(new WindowInteropHelper(panel).Handle);await Task.Delay(250);
            await panel.Discover.RefreshCatalog();for(int i=0;i<50&&panel.Discover.Catalog.Current==null;i++)await Task.Delay(100);
            var catalog=panel.Discover.Catalog.Current;if(catalog==null)throw new IOException(panel.Discover.Catalog.Status);Check(!panel.Discover.Catalog.Offline,"Public official HTTPS catalog fetched and RSA-PSS signature verified");Check(catalog.Resources.Count==10,"Official catalog contains ten real published packages");
            Check(!Main<Button>("ApplyButton").IsVisible&&!Main<LivePreview>("Preview").IsVisible,"Explore discovers resources without workspace preview or Customize");await Task.Delay(1200);await Capture("explore");
            foreach(var item in catalog.Resources.Where(r=>r.Id.StartsWith("cute-")))Check(panel.Discover.Downloads.State(item)=="Bundled owned","Existing bundled theme recognized without copying: "+item.Id);
            Discover<TextBox>("Search").Text="Superstar";Check(panel.Discover.Filtered().Count==1,"Search filters actual title, author and metadata");await Capture("search");Discover<TextBox>("Search").Text="";
            Discover<ComboBox>("Category").SelectedItem="Motion Trails";Check(panel.Discover.Filtered().Count==4,"Motion category reflects four data-only presets");Discover<ComboBox>("Category").SelectedItem="All";
            var resource=catalog.Resources.Single(r=>r.Id=="cool-cat-superstar");panel.Discover.ShowDetails(resource);await Task.Delay(500);await Capture("details");
            Check(Discover<TextBlock>("Information").Text.Contains(resource.SHA256)&&Discover<TextBlock>("Information").Text.Contains("15 native"),"Details show signed package hash and actual role provenance");
            var stateBefore=JsonSerializer.Serialize(app.Studio.State,Storage.Json);await panel.Discover.TryLivePreview();Check(Discover<LivePreview>("DetailPreview").Cursor!=null,"Online ANI pack fetched, validated and loaded into real temporary WPF cursor preview");
            var preview=Discover<LivePreview>("DetailPreview");preview.BringIntoView();await Task.Delay(100);var origin=preview.PointToScreen(new Point(60,70));for(int i=0;i<16;i++){Native.SetPhysicalCursorPos((int)origin.X+i*12,(int)origin.Y+(int)(Math.Sin(i*.3)*20));await Task.Delay(15);}await Capture("ani-preview");
            foreach(var role in CursorRoles.All){Discover<ComboBox>("Roles").SelectedItem=role.Name;Check(preview.Cursor!=null,"Temporary native preview role: "+role.Name);}Discover<ComboBox>("Roles").SelectedItem="Normal";
            Check(!app.Studio.Catalog.Themes.Any(t=>t.Id==resource.Id)&&JsonSerializer.Serialize(native.Snapshot().Cursors)==baselineJson&&JsonSerializer.Serialize(app.Studio.State,Storage.Json)==stateBefore,"Try Live Preview neither installs nor applies nor changes desktop state");
            panel.Discover.Back();Check(preview.Cursor==null&&!Directory.EnumerateDirectories(panel.Discover.Downloads.StagingRoot,"resource-*").Any(),"Leaving details releases native preview and its staged resources");
            panel.Discover.ShowDetails(resource);Discover<Button>("DetailFavorite").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(ThemeLibraryPreferences.Load().Favorites.Contains(resource.Id)&&!app.Studio.Catalog.Themes.Any(t=>t.Id==resource.Id),"Online-only favorite is persisted without installation");
            var cancelled=panel.Discover.DownloadSelected();Discover<Button>("CancelDownload").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await cancelled;Check(!panel.Discover.IsDownloading&&!app.Studio.Catalog.Themes.Any(t=>t.Id==resource.Id)&&!Directory.GetFiles(panel.Discover.Downloads.StagingRoot,"download-*").Any(),"Actual WPF Cancel button interrupts download and leaves no partial install");
            var downloading=panel.Discover.DownloadSelected();for(int i=0;i<100&&!panel.Discover.IsDownloading&&!downloading.IsCompleted;i++)await Task.Delay(10);await Capture("download-progress");await downloading;await Capture("download-installed");
            Check(app.Studio.Catalog.Themes.Any(t=>t.Id==resource.Id)&&panel.Discover.Downloads.State(resource)=="Installed","Real network download atomically enters local cursor library");Check(JsonSerializer.Serialize(native.Snapshot().Cursors)==baselineJson,"Download never calls native Apply");
            int beforeThemes=app.Studio.Catalog.Themes.Count;await panel.Discover.Downloads.InstallAsync(resource,null,CancellationToken.None);Check(app.Studio.Catalog.Themes.Count==beforeThemes,"Duplicate download creates no duplicate theme");
            Discover<Button>("Download").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Main<ListBox>("Navigation").SelectedIndex==1&&panel.Draft.CursorThemeId==resource.Id,"Open in Cursors selects downloaded theme as Draft");await Capture("cursors");
            Check(panel.IsDirty&&Main<Button>("ApplyButton").IsEnabled&&JsonSerializer.Serialize(native.Snapshot().Cursors)==baselineJson,"Local selection does not apply Windows pointers");Check(panel.ApplyDraft()&&native.IsActive(app.Studio.Catalog.Find(resource.Id)),"Downloaded cursor truly applies through original StudioService transaction");
            var saved=app.Studio.Profiles.Save(null,"Online verification combination",panel.Draft);Check(new ProfileManager().Profiles.Any(p=>p.Id==saved.Id&&p.Configuration.CursorThemeId==resource.Id),"Downloaded cursor can be saved in Profile");
            bool protectedRemoval=false;try{panel.Discover.Downloads.RemoveCursor(app.Studio.Catalog.Find(resource.Id),app.Studio,native);}catch(InvalidOperationException){protectedRemoval=true;}Check(protectedRemoval,"Applied/Profile-referenced theme cannot be removed");
            Main<ComboBox>("ThemeBox").SelectedItem=app.Studio.Catalog.Find("cute-bunny-cute-kawaii");Main<Button>("CancelButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(panel.Draft.CursorThemeId==resource.Id&&!panel.IsDirty,"Cancel restores applied downloaded cursor and parameters");
            Main<ListBox>("Navigation").SelectedIndex=0;var trail=catalog.Resources.Single(r=>r.Id=="sakura-pink");panel.Discover.ShowDetails(trail);await panel.Discover.TryLivePreview();Check(Discover<LivePreview>("DetailPreview").IsVisible,"Data-only motion preset uses the trusted live preview engine");await panel.Discover.DownloadSelected();Discover<Button>("Download").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(Main<ListBox>("Navigation").SelectedIndex==2&&panel.Draft.Effects.TrailEffect=="neon"&&panel.Draft.Effects.Color=="#FFB7D8","Downloaded motion preset opens in Trails and updates only Draft");await Capture("trails");
            Check(panel.ApplyDraft()&&app.Studio.LiveEffects.Color=="#FFB7D8"&&native.IsActive(app.Studio.Catalog.Find(resource.Id)),"Motion preset applies independently while preserving cursor theme");
            Check(Main<ListBox>("TrailCards").Items.Cast<TrailPreset>().Single(p=>p.Id==trail.Id).StatusLabel.Contains("Applied"),"Downloaded motion card projects actual Applied parameters separately from ownership");
            var restart=new ThemeCatalog(AppContext.BaseDirectory);var manager=new DownloadManager(new OfficialHttp(),restart);Check(restart.Find(resource.Id).Roles.Values.All(p=>File.Exists(restart.Find(resource.Id).Resource(restart.Find(resource.Id).Roles.Single(r=>r.Value==p).Key)))&&manager.LoadTrails().Any(p=>p.Id==trail.Id),"Cursor and motion installation records survive service restart with stable valid paths");
            Main<ListBox>("Navigation").SelectedIndex=0;panel.Discover.Back();
            foreach(int count in new[]{100,300,500})
            {
                var fixtures=Enumerable.Range(0,count).Select(i=>new RemoteResource{Id="fixture-"+i,Name="Fixture "+i,Author="Performance fixture",Type="trail",CoverUrl=trail.CoverUrl,Version="1.0.0",Description="Synthetic catalog for control-count measurement only."}).ToArray();var process=Process.GetCurrentProcess();int userBefore=GetGuiResources(process.Handle,1);long memory=GC.GetTotalMemory(true);var timer=Stopwatch.StartNew();panel.Discover.SetResourcesForVerification(fixtures);panel.UpdateLayout();timer.Stop();int elements=Discover<WrapPanel>("Cards").Children.Count;Check(elements==24,"Bounded page creates 24 cards for "+count+" resources");performance.Add(new{count,elements,layoutMilliseconds=timer.Elapsed.TotalMilliseconds,managedBytesDelta=GC.GetTotalMemory(false)-memory,userHandlesDelta=GetGuiResources(process.Handle,1)-userBefore});
            }
            panel.Discover.SetResourcesForVerification(catalog.Resources);panel.Width=850;panel.Height=520;await Capture("compact-explore");panel.Discover.ShowDetails(resource);await Capture("compact-details");Check(Discover<Button>("Download").IsVisible&&Discover<Button>("BackButton").IsVisible,"Compact details retain fixed download and Back actions");
            native.Restore();Check(JsonSerializer.Serialize(native.Snapshot().Cursors)==baselineJson,"Original pointer configuration restored after real downloaded-theme application");passed=true;
        }
        catch(Exception ex){error=ex.ToString();Storage.Log(error);}
        finally
        {
            try{if(native.HasBackup)native.Restore();Check(JsonSerializer.Serialize(native.Snapshot().Cursors)==baselineJson,"Exact pre-test native pointer snapshot restored in finally");}catch(Exception ex){passed=false;error=(error??"")+ex;}
            Native.SetPhysicalCursorPos(pointer.X,pointer.Y);if(foreground!=IntPtr.Zero)SetForegroundWindow(foreground);
            Storage.Write(Path.Combine(directory,"discover-report.json"),new{passed,error,checks,performance,monitors=Native.Monitors().Select(m=>new{m.Device,width=m.Monitor.Width,height=m.Monitor.Height}).ToArray(),physicalDpi=Native.GetDpiForWindow(new WindowInteropHelper(panel).Handle),notes="Actual official network, running WPF and native Apply under isolated data. Performance fixtures are explicitly synthetic and never published as online content."});app.ExitApp(passed?0:1);
        }
    }
}
