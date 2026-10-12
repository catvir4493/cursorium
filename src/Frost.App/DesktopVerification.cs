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
            panel=app.Panel;panel.Show();await Task.Delay(200);
            var length=(Slider)panel.FindName("LengthSlider");length.Value=147;Check(Math.Abs(settings.Length-147)<.1,"Real length slider updates engine settings");
            var halo=(CheckBox)panel.FindName("HaloCheck");halo.IsChecked=false;Check(!settings.ClickHalo,"Real halo switch updates settings");halo.IsChecked=true;
            var preset=(ComboBox)panel.FindName("PresetBox");preset.SelectedIndex=0;Check(settings.Particles==2&&settings.Length==65,"Real preset selector applies minimal parameters");preset.SelectedIndex=1;Check(settings.Particles==10&&settings.Duration==260&&settings.Length==110,"Real standard preset restores defaults");
            ((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[1]).PerformClick();Check(!settings.Enabled,"Actual tray toggle disables engine");((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[1]).PerformClick();Check(settings.Enabled,"Actual tray toggle re-enables engine");
            ((System.Windows.Forms.ToolStripMenuItem)((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[2]).DropDownItems[2]).PerformClick();Check(settings.Preset=="Glow"&&settings.Particles==16,"Actual tray preset menu applies glow");
            ((System.Windows.Forms.ToolStripMenuItem)((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[2]).DropDownItems[1]).PerformClick();
            using(var startupKey=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                bool existed=startupKey.GetValueNames().Contains("FrostWhiteComet");var originalValue=existed?startupKey.GetValue("FrostWhiteComet",null,Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames):null;var originalKind=existed?startupKey.GetValueKind("FrostWhiteComet"):Microsoft.Win32.RegistryValueKind.String;
                try
                {
                    var startup=(CheckBox)panel.FindName("StartupCheck");startup.IsChecked=false;startup.IsChecked=true;Check(StartupRegistration.Enabled,"Actual login-start switch writes only app Run entry");startup.IsChecked=false;Check(!StartupRegistration.Enabled,"Actual login-start switch removes app Run entry");
                }
                finally{if(existed)startupKey.SetValue("FrostWhiteComet",originalValue!,originalKind);else startupKey.DeleteValue("FrostWhiteComet",false);panel.Refresh();}
            }
            panel.Close();Check(!panel.IsVisible,"Close panel keeps tray process running");((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[0]).PerformClick();Check(panel.IsVisible,"Actual tray open-settings action shows panel");
            var color=(TextBox)panel.FindName("ColorInput");color.Text="#B7A8FF";
            foreach(var c in FindButtons(panel).Where(b=>(string?)b.Content=="Apply color"))c.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(settings.Color=="#B7A8FF","Real custom color action updates engine");settings.Color="#91DEFF";panel.Refresh();
            await Task.Delay(150);Capture(panel,Path.Combine(output,"control-panel.png"));
            // Record a live desktop crop with actual composited trail over the test area.
            var area=(Border)panel.FindName("TestArea");var start=area.PointToScreen(new System.Windows.Point(30,50));
            for(int i=0;i<25;i++){Native.SetPhysicalCursorPos((int)start.X+i*7,(int)start.Y+(int)(Math.Sin(i*.19)*25));await Task.Delay(10);}
            await Task.Delay(20);var client=(FrameworkElement)panel.Content;var panelOrigin=client.PointToScreen(new System.Windows.Point(0,0));var dpi=VisualTreeHelper.GetDpi(panel);
            using(var bitmap=new System.Drawing.Bitmap((int)(client.ActualWidth*dpi.DpiScaleX),(int)(client.ActualHeight*dpi.DpiScaleY)))
            {using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen((int)panelOrigin.X,(int)panelOrigin.Y,0,0,bitmap.Size);bitmap.Save(Path.Combine(output,"desktop-trail.png"),System.Drawing.Imaging.ImageFormat.Png);}
            settings.PauseFullscreen=true;settings.Save();Check(File.Exists(Settings.PathName),"Settings persisted");
            panel.Hide();panel=null;success=true;
        }
        catch(Exception ex){error=ex.ToString();Storage.Log(error);}
        finally
        {
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
            foreach(var mode in new[]{"idle","active-60","active-120","power-30"})
            {
                s.FrameRate=mode=="active-120"?120:60;s.PowerSaving=mode=="power-30";
                if(mode=="idle")Native.SetPhysicalCursorPos(primary.Left+primary.Width/2,primary.Top+primary.Height/2);
                await Task.Delay(400);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                var before=p.TotalProcessorTime;long frames=engine.RenderedFrames;long samples=engine.Samples;int gdi=GetGuiResources(p.Handle,0),user=GetGuiResources(p.Handle,1);var timer=Stopwatch.StartNew();
                var positions=new HashSet<string>();int movingObservations=0;
                while(timer.Elapsed.TotalSeconds<4)
                {
                    if(mode!="idle"){double t=timer.Elapsed.TotalSeconds;Native.SetPhysicalCursorPos(primary.Left+primary.Width/2+(int)(Math.Sin(t*7)*150),primary.Top+primary.Height/2+(int)(Math.Cos(t*10)*70));}else Native.SetPhysicalCursorPos(primary.Left+primary.Width/2,primary.Top+primary.Height/2);
                    await Task.Delay(8);
                    if(Native.GetPhysicalCursorPos(out var observed)&&positions.Count<100)positions.Add($"{observed.X},{observed.Y}");if(engine.Model.Points.Count>1)movingObservations++;
                }
                p.Refresh();double cpu=(p.TotalProcessorTime-before).TotalSeconds;measurements.Add(new{mode,seconds=timer.Elapsed.TotalSeconds,renderInvalidationsPerSecond=(engine.RenderedFrames-frames)/timer.Elapsed.TotalSeconds,samplesPerSecond=(engine.Samples-samples)/timer.Elapsed.TotalSeconds,cpuOneCorePercent=cpu/timer.Elapsed.TotalSeconds*100,cpuMachinePercent=cpu/timer.Elapsed.TotalSeconds/Environment.ProcessorCount*100,workingSet=p.WorkingSet64,gdiBefore=gdi,gdiAfter=GetGuiResources(p.Handle,0),userBefore=user,userAfter=GetGuiResources(p.Handle,1),observedDistinctPositions=positions.Count,movingObservations});
            }
        }
        finally
        {
            Native.SetPhysicalCursorPos(original.X,original.Y);File.WriteAllText(Path.Combine(output,"performance-report.json"),JsonSerializer.Serialize(new{measurements,notes="Render rates count WPF invalidations, not GPU presentation. This machine's compositor limits high-refresh output. Memory includes WPF and two layered monitor surfaces."},Storage.Json));app.ExitApp();
            if(originalConfig!=null)File.WriteAllText(Settings.PathName,originalConfig);else if(File.Exists(Settings.PathName))File.Delete(Settings.PathName);
        }
    }
    private static IEnumerable<Button> FindButtons(DependencyObject parent)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is Button b)yield return b;foreach(var sub in FindButtons(child))yield return sub;}
    }
    private static void Capture(Window window,string path)
    {
        var client=(FrameworkElement)window.Content;var dpi=VisualTreeHelper.GetDpi(window);var image=new RenderTargetBitmap((int)Math.Ceiling(client.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(client.ActualHeight*dpi.DpiScaleY),96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);image.Render(client);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(path);encoder.Save(stream);
    }
}
