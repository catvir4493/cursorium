using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Frost.Core;

namespace Frost.App;
public static class DesktopVerification
{
    [StructLayout(LayoutKind.Sequential)]private struct MouseInput{public int X,Y;public uint Data,Flags,Time;public IntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct KeyInput{public ushort Key,Scan;public uint Flags,Time;public IntPtr Extra;}
    [StructLayout(LayoutKind.Explicit)]private struct Union{[FieldOffset(0)]public MouseInput Mouse;[FieldOffset(0)]public KeyInput Key;}
    [StructLayout(LayoutKind.Sequential)]private struct Input{public uint Type;public Union Data;}
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,Input[] inputs,int size);
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")]private static extern int GetGuiResources(IntPtr process,int flag);
    private static readonly List<object> results=new();
    private static void Check(bool value,string name){results.Add(new{name,passed=value});if(!value)throw new Exception(name);}
    public static async Task Run(App app,TrailEngine engine,string[] args)
    {
        string output=Path.GetFullPath(GetOption(args,"--report")??"artifacts/verification");Directory.CreateDirectory(output);
        string? originalConfig=File.Exists(Settings.PathName)?File.ReadAllText(Settings.PathName):null;
        Native.GetPhysicalCursorPos(out var original);var originalForeground=Native.GetForegroundWindow();Window? probe=null;MainWindow? panel=null;bool success=false;string? error=null;
        var process=Process.GetCurrentProcess();var started=Stopwatch.StartNew();var cpuBefore=process.TotalProcessorTime;int gdiBefore=GetGuiResources(process.Handle,0),userBefore=GetGuiResources(process.Handle,1);long memBefore=process.WorkingSet64;
        try
        {
            var settings=engine.Windows[0].Painter.Settings;settings.Enabled=true;settings.PauseFullscreen=false;settings.PowerSaving=false;settings.ApplyPreset("Standard");settings.ClickHalo=true;
            var primary=Native.Monitors().First(m=>(m.Flags&1)!=0);
            int clicks=0;var stack=new StackPanel{Margin=new Thickness(30)};var button=new Button{Content="Native input pass-through target",Height=60,Margin=new Thickness(0,0,0,20)};button.Click+=(_,_)=>clicks++;stack.Children.Add(button);var input=new TextBox{Height=40};stack.Children.Add(input);
            probe=new Window{Title="FROST Desktop Verification",Width=640,Height=300,Background=new SolidColorBrush(CursorArtwork.ColorFrom("#101D30")),Content=stack,Topmost=true};probe.Show();
            var probeHwnd=new WindowInteropHelper(probe).Handle;Native.SetWindowPos(probeHwnd,(IntPtr)(-1),primary.Monitor.Left+120,primary.Monitor.Top+120,640,300,0x40);probe.Activate();
            await Task.Delay(200);var foreground=Native.GetForegroundWindow();
            foreach(var w in engine.Windows)
            {
                w.Place();long styles=Native.GetWindowLongPtr(w.Handle,Native.GwlExStyle).ToInt64();Check((styles&(Native.Transparent|Native.Layered|Native.NoActivate|Native.ToolWindow))==(Native.Transparent|Native.Layered|Native.NoActivate|Native.ToolWindow),"Overlay native styles: "+w.Monitor.Device);
                Native.GetWindowRect(w.Handle,out var r);Check(r.Left==w.Monitor.Monitor.Left&&r.Top==w.Monitor.Monitor.Top&&r.Width==w.Monitor.Monitor.Width&&r.Height==w.Monitor.Monitor.Height,"Physical monitor bounds: "+w.Monitor.Device);
            }
            Check(Native.GetForegroundWindow()==foreground,"Overlay positioning preserves foreground focus");
            var center=button.PointToScreen(new System.Windows.Point(button.ActualWidth/2,button.ActualHeight/2));Native.SetPhysicalCursorPos((int)center.X,(int)center.Y);await Task.Delay(80);
            var events=new[]{new Input{Data=new Union{Mouse=new MouseInput{Flags=2}}},new Input{Data=new Union{Mouse=new MouseInput{Flags=4}}}};
            Check(SendInput(2,events,Marshal.SizeOf<Input>())==2,"SendInput click delivered");await Task.Delay(45);Check(clicks==1,"Actual button click passes through overlay");Check(engine.Model.Halos.Count>0,"Raw Input created click halo");Check(Native.GetForegroundWindow()==probeHwnd,"Click does not activate overlay");
            input.Focus();var keys=new[]{new Input{Type=1,Data=new Union{Key=new KeyInput{Scan='F',Flags=4}}},new Input{Type=1,Data=new Union{Key=new KeyInput{Scan='F',Flags=6}}}};
            Check(SendInput(2,keys,Marshal.SizeOf<Input>())==2,"Keyboard input submitted");await Task.Delay(60);Check(input.Text=="F","Keyboard focus and input preserved");
            foreach(var w in engine.Windows)
            {
                var r=w.Monitor.Monitor;int cx=r.Left+r.Width/2,cy=r.Top+r.Height/2;
                for(int i=0;i<20;i++){Native.SetPhysicalCursorPos(cx-100+i*10,cy+(int)(Math.Sin(i*.35)*25));await Task.Delay(10);}
                await Task.Delay(35);Check(engine.Model.Points.Count>1,"Physical trail recorded on "+w.Monitor.Device);
                bool aligned=false;
                for(int attempt=0;attempt<10;attempt++)
                {
                    Native.GetPhysicalCursorPos(out var actual);if(engine.Model.Points.Count>0){var tip=engine.Model.Points[^1];if(Math.Abs(tip.X-actual.X)<2&&Math.Abs(tip.Y-actual.Y)<2){aligned=true;break;}}await Task.Delay(15);
                }
                Check(aligned,"Trail sample aligns with actual physical cursor on "+w.Monitor.Device);
            }
            await Task.Delay(450);Check(!engine.Model.HasContent,"Stopped trail and halo naturally expire");
            for(int cycle=0;cycle<3;cycle++)
            {
                var handles=engine.Windows.Select(w=>w.Handle).ToArray();engine.RefreshDisplays();await Task.Delay(50);
                Check(handles.All(h=>!Native.IsWindow(h)),"Display rebuild releases old native windows, cycle "+cycle);
                Check(engine.Windows.Count==Native.Monitors().Count,"Display rebuild recreates current monitor overlays, cycle "+cycle);
            }
            settings.Enabled=false;await Task.Delay(60);Check(engine.Model.Points.Count==0,"Disable clears trail");settings.Enabled=true;
            // Fullscreen pause is a heuristic tested with a borderless native-sized window.
            settings.PauseFullscreen=true;
            using(var fullscreen=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--fullscreen-probe"){UseShellExecute=false}))
            {
                try
                {
                    bool positioned=false;
                    for(int i=0;i<60;i++)
                    {
                        fullscreen!.Refresh();var h=fullscreen.MainWindowHandle;
                        if(h!=IntPtr.Zero&&Native.GetWindowRect(h,out var r)&&r.Width==primary.Monitor.Width&&r.Height==primary.Monitor.Height){positioned=true;break;}await Task.Delay(50);
                    }
                    Check(positioned,"External fullscreen test window has actual monitor bounds");
                    SetForegroundWindow(fullscreen!.MainWindowHandle);Native.SetPhysicalCursorPos(primary.Monitor.Left+60,primary.Monitor.Top+60);SendInput(2,events,Marshal.SizeOf<Input>());await Task.Delay(200);
                    Check(Native.GetForegroundWindow()==fullscreen.MainWindowHandle,"External fullscreen test window is foreground");await Task.Delay(650);
                    Check(engine.Status.Contains("Paused"),"External borderless fullscreen auto-pause");
                }
                finally{if(!fullscreen!.HasExited)fullscreen.CloseMainWindow();}
            }
            settings.PauseFullscreen=false;
            probe.Close();probe=null;await Task.Delay(350);
            panel=app.Panel;panel.Show();await Task.Delay(300);
            var liveBefore=JsonSerializer.Serialize(app.Studio.State.Applied,Storage.Json);
            var nativeBefore=JsonSerializer.Serialize(new ThemeService().Snapshot().Cursors);
            var length=(Slider)panel.FindName("LengthSlider");length.Value=147;
            Check(Math.Abs(panel.Draft.Effects.Length-147)<.1&&settings.Length!=147,"Length slider updates draft without changing desktop");
            var themes=(ComboBox)panel.FindName("ThemeBox");var trails=(ComboBox)panel.FindName("TrailBox");var clicksBox=(ComboBox)panel.FindName("ClickBox");
            themes.SelectedItem=app.Studio.Catalog.Find("frost");trails.SelectedItem=app.Studio.Effects.Trails.Single(p=>p.Id=="neon");clicksBox.SelectedIndex=2;
            Check(panel.Draft.CursorThemeId=="frost"&&panel.Draft.Effects.TrailEffect=="neon"&&panel.Draft.Effects.ClickEffect=="ring","Independent cursor, trail and click selectors");
            themes.SelectedItem=app.Studio.Catalog.Find("obsidian");Check(panel.Draft.Effects.TrailEffect=="neon","Changing cursor preserves selected trail");
            Check(JsonSerializer.Serialize(app.Studio.State.Applied,Storage.Json)==liveBefore&&JsonSerializer.Serialize(new ThemeService().Snapshot().Cursors)==nativeBefore,"Preview changes do not write applied state or native cursors");
            ((Button)panel.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!panel.IsDirty,"Cancel resets preview to the applied configuration");
            themes.SelectedItem=app.Studio.Catalog.Find("frost");trails.SelectedItem=app.Studio.Effects.Trails.Single(p=>p.Id=="neon");clicksBox.SelectedIndex=2;
            var color=(TextBox)panel.FindName("ColorInput");color.Text="#BA79FF";Check(panel.Draft.Effects.Color=="#BA79FF","Color text updates local preview");color.Text="bad";
            Check(!((Button)panel.FindName("ApplyButton")).IsEnabled,"Invalid color disables Apply");color.Text="#BA79FF";
            ((Button)panel.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(app.Studio.LiveEffects.TrailEffect=="neon"&&app.Studio.LiveEffects.ClickEffect=="ring"&&new ThemeService().IsActive(app.Studio.Catalog.Find("frost")),"Real Apply activates FROST, purple Neon and Ring on Windows");
            themes.SelectedItem=app.Studio.Catalog.Find("obsidian");Check(panel.ApplyDraft()&&new ThemeService().IsActive(app.Studio.Catalog.Find("obsidian")),"UI Apply switches to second native theme");
            var navigation=(ListBox)panel.FindName("Navigation");navigation.SelectedIndex=3;
            ((TextBox)panel.FindName("ProfileName")).Text="Desktop verification mix";
            ((Button)panel.FindName("NewProfileButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(app.Studio.Profiles.Profiles.Count==1,"Real Create profile button persists a combination");
            ((TextBox)panel.FindName("ProfileName")).Text="Renamed verification mix";
            ((Button)panel.FindName("RenameProfileButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(app.Studio.Profiles.Profiles.Single().Name=="Renamed verification mix","Real Rename profile button persists name");
            length.Value=155;((Button)panel.FindName("SaveProfileButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(new ProfileManager().Profiles.Single().Configuration.Effects.Length==155,"Real Save profile button persists draft edits");
            trails.SelectedItem=app.Studio.Effects.Trails.Single(p=>p.Id=="minimal");var profiles=(ComboBox)panel.FindName("ProfileBox");profiles.SelectedIndex=-1;profiles.SelectedIndex=0;
            Check(panel.Draft.Effects.TrailEffect=="neon"&&panel.Draft.Effects.Length==155,"Profile selection restores its saved combination to draft");Check(panel.ApplyDraft(),"Saved profile can be applied");
            panel.ImportResources("Imported live theme",new Dictionary<string,string>{{"Normal",app.Studio.Catalog.Find("frost").Resource("Normal")},{"Busy",app.Studio.Catalog.Find("frost").Resource("Busy")}});
            Check(panel.Draft.CursorThemeId.StartsWith("import-")&&panel.ApplyDraft(),"CUR and ANI import through UI service can be applied");
            var renderHashes=new HashSet<string>();
            navigation.SelectedIndex=0;panel.Activate();SetForegroundWindow(new WindowInteropHelper(panel).Handle);await Task.Delay(150);
            foreach(var preset in app.Studio.Effects.Trails)
            {
                trails.SelectedItem=preset;Check(panel.ApplyDraft(),"Apply motion preset: "+preset.Id);
                Check(settings.TrailEffect==preset.Id,"Desktop engine receives selected effect: "+preset.Id);
                var fixture=new TrailModel();for(int i=0;i<24;i++)fixture.Add(20+i*7,100+Math.Sin(i*.18)*25,i*7,settings);
                fixture.Click(175,110,150,ClickButton.Right);var painter=new TrailPainter(fixture,settings){PhysicalCoordinates=false,Now=165,Width=240,Height=180};
                painter.Measure(new Size(240,180));painter.Arrange(new Rect(0,0,240,180));painter.UpdateLayout();
                var image=new RenderTargetBitmap(240,180,96,96,PixelFormats.Pbgra32);image.Render(painter);var pixels=new byte[240*180*4];image.CopyPixels(pixels,240*4,0);
                Check(pixels.Any(b=>b!=0),"Effect renderer draws pixels: "+preset.Id);renderHashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels)));
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(output,preset.Id+"-render.png")))encoder.Save(file);
                var preview=(LivePreview)panel.FindName("Preview");preview.BringIntoView();await Task.Delay(60);var start=preview.PointToScreen(new Point(20,100));
                for(int i=0;i<22;i++){Native.SetPhysicalCursorPos((int)start.X+i*7,(int)start.Y+(int)(Math.Sin(i*.19)*25));await Task.Delay(12);}
                Capture(panel,Path.Combine(output,"preview-"+preset.Id+".png"));
                Check(preview.Model.Points.Count>1,"Real mouse movement reaches local preview: "+preset.Id);
                Check(engine.Model.Points.Count==0,"Desktop overlay stays clear over local draft preview: "+preset.Id);
                for(int i=0;i<18;i++){Native.SetPhysicalCursorPos(primary.Monitor.Left+40+i*7,primary.Monitor.Top+40+(int)(Math.Sin(i*.3)*20));await Task.Delay(12);}
                Check(engine.Model.Points.Count>1,"Real desktop motion sampled for effect: "+preset.Id);
            }
            Check(renderHashes.Count==4,"Four preset renderings produce distinct pixel output");
            ((CheckBox)panel.FindName("TrailCheck")).IsChecked=false;clicksBox.SelectedIndex=2;Check(panel.ApplyDraft(),"Apply independent click-only configuration");
            // Use an owned input target; clicking an arbitrary desktop location can activate an external fullscreen app.
            var clickTarget=new Border{Background=Brushes.MidnightBlue};
            probe=new Window{Title="CURSORIUM click-only input target",Content=clickTarget,Width=320,Height=230,Topmost=true};probe.Show();
            Native.SetWindowPos(new WindowInteropHelper(probe).Handle,(IntPtr)(-1),primary.Monitor.Left+60,primary.Monitor.Top+60,320,230,0x40);probe.Activate();
            await Task.Delay(400);engine.Model.Clear();var clickPoint=clickTarget.PointToScreen(new Point(80,80));Native.SetPhysicalCursorPos((int)clickPoint.X,(int)clickPoint.Y);await Task.Delay(60);
            SendInput(2,events,Marshal.SizeOf<Input>());await Task.Delay(40);
            Check(engine.Model.Points.Count==0&&engine.Model.Halos.Any(h=>h.Button==ClickButton.Left),"Left click renders while trails are disabled ("+engine.Status+")");
            var rightEvents=new[]{new Input{Data=new Union{Mouse=new MouseInput{Flags=8}}},new Input{Data=new Union{Mouse=new MouseInput{Flags=16}}}};
            SendInput(2,rightEvents,Marshal.SizeOf<Input>());await Task.Delay(40);Check(engine.Model.Halos.Any(h=>h.Button==ClickButton.Right),"Raw Input right click retains accurate identity");
            probe.Close();probe=null;
            ((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[1]).PerformClick();Check(!settings.Enabled,"Actual tray toggle disables all effects");
            ((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[1]).PerformClick();Check(settings.Enabled,"Actual tray toggle re-enables all effects");
            ((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[2]).PerformClick();await Task.Delay(600);Check(engine.PausedByUser&&engine.Model.Halos.Count==0,"Actual tray pause clears effects");
            ((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[2]).PerformClick();Check(!engine.PausedByUser,"Actual tray resume works");
            using(var startupKey=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                bool existed=startupKey.GetValueNames().Contains("FrostWhiteComet");var originalValue=existed?startupKey.GetValue("FrostWhiteComet",null,Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames):null;var originalKind=existed?startupKey.GetValueKind("FrostWhiteComet"):Microsoft.Win32.RegistryValueKind.String;
                try
                {
                    navigation.SelectedIndex=4;var startup=(CheckBox)panel.FindName("StartupCheck");startup.IsChecked=false;startup.IsChecked=true;Check(StartupRegistration.Enabled,"Actual sign-in switch writes app Run entry");startup.IsChecked=false;Check(!StartupRegistration.Enabled,"Actual sign-in switch removes app Run entry");
                }
                finally{if(existed)startupKey.SetValue("FrostWhiteComet",originalValue!,originalKind);else startupKey.DeleteValue("FrostWhiteComet",false);panel.Refresh();}
            }
            panel.Close();Check(!panel.IsVisible,"Close panel keeps tray process running");((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[0]).PerformClick();Check(panel.IsVisible,"Actual tray action shows studio");
            navigation.SelectedIndex=0;themes.SelectedItem=app.Studio.Catalog.Find("frost");trails.SelectedItem=app.Studio.Effects.Trails.Single(p=>p.Id=="comet");((CheckBox)panel.FindName("TrailCheck")).IsChecked=true;
            Check(panel.ApplyDraft(),"Apply final preview combination");await Task.Delay(150);Capture(panel,Path.Combine(output,"control-panel.png"));
            panel.Width=960;panel.Height=640;await Task.Delay(150);Check(((Button)panel.FindName("ApplyButton")).ActualWidth>100&&((LivePreview)panel.FindName("Preview")).ActualWidth>150,"Compact 960x640 DIP layout retains preview and Apply");Capture(panel,Path.Combine(output,"compact-panel.png"));
            // A fresh process reads the same data root, testing real process-restart persistence.
            var restartReport=Path.Combine(output,"restart-report.json");
            using(var restart=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,ArgumentList={"--state-probe","--isolated-test","--test-data-root",Storage.Root,"--report",restartReport}}))
            {Check(restart!=null,"Restart state probe launched");try{await restart!.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));}catch{restart!.Kill();throw;}Check(restart.ExitCode==0,"Fresh process starts with persisted configuration");}
            using(var report=JsonDocument.Parse(File.ReadAllText(restartReport)))Check(report.RootElement.GetProperty("state").GetProperty("Applied").GetProperty("Effects").GetProperty("TrailEffect").GetString()=="comet"&&report.RootElement.GetProperty("profiles").GetArrayLength()==1,"Fresh process restores applied combination and saved profile");
            // Repeated native cursor previews must release handles even when the pointer was over the preview.
            themes.SelectedItem=app.Studio.Catalog.Find("frost");await Task.Delay(100);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();int previewGdi=GetGuiResources(process.Handle,0),previewUser=GetGuiResources(process.Handle,1);
            for(int i=0;i<16;i++){themes.SelectedItem=app.Studio.Catalog.Find(i%2==0?"obsidian":"frost");await Task.Delay(15);}
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(100);
            Check(GetGuiResources(process.Handle,0)<=previewGdi+4&&GetGuiResources(process.Handle,1)<=previewUser+4,"Repeated cursor preview switches keep native resource counts bounded");
            foreach(var size in new[]{new Size(960,600),new Size(850,520)})
            {
                panel.MinWidth=800;panel.MinHeight=480;panel.Width=size.Width;panel.Height=size.Height;await Task.Delay(80);
                var apply=(Button)panel.FindName("ApplyButton");var preview=(LivePreview)panel.FindName("Preview");
                Check(apply.ActualWidth>100&&preview.ActualWidth>130&&apply.IsVisible,"Small-work-area layout: "+size.Width+"x"+size.Height+" DIPs");
                Capture(panel,Path.Combine(output,"layout-"+size.Width+"x"+size.Height+".png"));
            }
            var handlesAtExit=engine.Windows.Select(w=>w.Handle).ToArray();engine.Dispose();Check(handlesAtExit.All(h=>!Native.IsWindow(h)),"Engine disposal releases all native overlay windows");
            Check(new ThemeService().IsActive(app.Studio.Catalog.Find("frost")),"Stopping trail engine leaves the native cursor theme installed");
            app.Studio.RestoreOriginal();Check(JsonSerializer.Serialize(new ThemeService().Snapshot().Cursors)==nativeBefore,"Desktop verification restores the exact pre-test Windows cursor configuration");
            panel.Hide();panel=null;success=true;
        }
        catch(Exception ex){error=ex.ToString();Storage.Log(error);}
        finally
        {
            var verificationTheme=new ThemeService();if(verificationTheme.HasBackup)verificationTheme.Restore();
            probe?.Close();panel?.Hide();Native.SetPhysicalCursorPos(original.X,original.Y);if(originalForeground!=IntPtr.Zero)SetForegroundWindow(originalForeground);
            process.Refresh();var report=new{passed=success,error,checks=results,monitors=engine.Windows.Select(w=>new{w.Monitor.Device,bounds=w.Monitor.Monitor,dpi=Native.GetDpiForWindow(w.Handle)}).ToArray(),elapsedSeconds=started.Elapsed.TotalSeconds,cpuSeconds=(process.TotalProcessorTime-cpuBefore).TotalSeconds,workingSetBefore=memBefore,workingSetAfter=process.WorkingSet64,gdiBefore,gdiAfter=GetGuiResources(process.Handle,0),userBefore,userAfter=GetGuiResources(process.Handle,1),notes="Input tests used SendInput on this interactive desktop. FPS settings are targets, not measured throughput. Resource figures include UI creation and screenshots."};
            File.WriteAllText(Path.Combine(output,"desktop-report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));app.ExitApp(success?0:1);
            if(originalConfig!=null)File.WriteAllText(Settings.PathName,originalConfig);else if(File.Exists(Settings.PathName))File.Delete(Settings.PathName);
        }
    }
    private static string? GetOption(string[] args,string name){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}
    public static async Task Benchmark(App app,TrailEngine engine,string[] args)
    {
        string output=Path.GetFullPath(GetOption(args,"--report")??"artifacts/verification");Directory.CreateDirectory(output);
        string? originalConfig=File.Exists(Settings.PathName)?File.ReadAllText(Settings.PathName):null;
        Native.GetPhysicalCursorPos(out var original);var measurements=new List<object>();var s=engine.Windows[0].Painter.Settings;
        var primary=engine.Windows[0].Monitor.Monitor;var p=Process.GetCurrentProcess();
        try
        {
            s.Enabled=true;s.PauseFullscreen=false;s.ApplyPreset("Standard");
            // Allow deferred WPF/native window initialization to finish before steady-state measurement.
            s.Enabled=false;await Task.Delay(2000);
            foreach(var mode in new[]{"disabled","idle","comet-60","minimal-60","neon-60","stardust-60","active-120","power-30"})
            {
                s.Enabled=mode!="disabled";s.TrailEnabled=true;var effect=mode.Split('-')[0];
                app.Studio.Effects.Trails.Single(t=>t.Id==(effect is "minimal" or "neon" or "stardust"?effect:"comet")).Apply(s);
                s.FrameRate=mode=="active-120"?120:60;s.PowerSaving=mode=="power-30";
                if(mode is "idle" or "disabled")Native.SetPhysicalCursorPos(primary.Left+primary.Width/2,primary.Top+primary.Height/2);
                await Task.Delay(400);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                var before=p.TotalProcessorTime;long frames=engine.RenderedFrames;long samples=engine.Samples;int gdi=GetGuiResources(p.Handle,0),user=GetGuiResources(p.Handle,1);var timer=Stopwatch.StartNew();
                var positions=new HashSet<string>();int movingObservations=0;
                while(timer.Elapsed.TotalSeconds<4)
                {
                    if(mode is not ("idle" or "disabled")){double t=timer.Elapsed.TotalSeconds;Native.SetPhysicalCursorPos(primary.Left+primary.Width/2+(int)(Math.Sin(t*7)*150),primary.Top+primary.Height/2+(int)(Math.Cos(t*10)*70));}else Native.SetPhysicalCursorPos(primary.Left+primary.Width/2,primary.Top+primary.Height/2);
                    await Task.Delay(8);
                    if(Native.GetPhysicalCursorPos(out var observed)&&positions.Count<100)positions.Add($"{observed.X},{observed.Y}");if(engine.Model.Points.Count>1)movingObservations++;
                }
                p.Refresh();double cpu=(p.TotalProcessorTime-before).TotalSeconds;measurements.Add(new{mode,seconds=timer.Elapsed.TotalSeconds,renderInvalidationsPerSecond=(engine.RenderedFrames-frames)/timer.Elapsed.TotalSeconds,samplesPerSecond=(engine.Samples-samples)/timer.Elapsed.TotalSeconds,cpuOneCorePercent=cpu/timer.Elapsed.TotalSeconds*100,cpuMachinePercent=cpu/timer.Elapsed.TotalSeconds/Environment.ProcessorCount*100,workingSet=p.WorkingSet64,gdiBefore=gdi,gdiAfter=GetGuiResources(p.Handle,0),userBefore=user,userAfter=GetGuiResources(p.Handle,1),observedDistinctPositions=positions.Count,movingObservations});
            }
        }
        finally
        {
            Native.SetPhysicalCursorPos(original.X,original.Y);File.WriteAllText(Path.Combine(output,"performance-report.json"),JsonSerializer.Serialize(new{measurements,monitorCount=Native.Monitors().Count,logicalProcessors=Environment.ProcessorCount,notes="Render rates count WPF invalidations, not GPU presentation. These short engine-only measurements include WPF and the current per-monitor layered surfaces; deferred initialization can affect early samples."},Storage.Json));app.ExitApp();
            if(originalConfig!=null)File.WriteAllText(Settings.PathName,originalConfig);else if(File.Exists(Settings.PathName))File.Delete(Settings.PathName);
        }
    }
    private static IEnumerable<Button> FindButtons(DependencyObject parent)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is Button b)yield return b;foreach(var sub in FindButtons(child))yield return sub;}
    }
    private static void Capture(Window window,string path)
    {
        var client=(FrameworkElement)window.Content;var dpi=VisualTreeHelper.GetDpi(window);var image=new RenderTargetBitmap((int)Math.Ceiling(client.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(client.ActualHeight*dpi.DpiScaleY),96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawRectangle(new VisualBrush(client),null,new Rect(0,0,client.ActualWidth,client.ActualHeight));image.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(path);encoder.Save(stream);
    }
}
