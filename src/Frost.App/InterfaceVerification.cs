using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using Frost.App.Presentation;
using Frost.Core;

namespace Frost.App;

public static class InterfaceVerification
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle,uint message,IntPtr wparam,IntPtr lparam);
    private static readonly List<object> checks=new();
    private static void Check(bool passed,string name){checks.Add(new{name,passed});if(!passed)throw new InvalidOperationException(name);}
    public static async Task CapturePages(App app,string[] args)
    {
        int option=Array.IndexOf(args,"--report");var directory=Path.GetFullPath(option>=0?args[option+1]:"artifacts/interface-review");Directory.CreateDirectory(directory);
        var panel=app.Panel;var foreground=Native.GetForegroundWindow();Native.GetPhysicalCursorPos(out var pointer);bool passed=false;string? error=null;
        string nativeBefore=JsonSerializer.Serialize(new ThemeService().Snapshot().Cursors),appliedBefore=JsonSerializer.Serialize(app.Studio.State,Storage.Json);
        async Task WaitForInput(Func<bool> condition){var timer=Stopwatch.StartNew();while(!condition()&&timer.ElapsedMilliseconds<1200)await Task.Delay(20);}
        try
        {
            panel.Topmost=true;panel.Show();((ListBox)panel.FindName("Navigation")).SelectedIndex=1;((TabControl)panel.FindName("ConfigTabs")).SelectedIndex=0;await Task.Delay(150);panel.WindowState=WindowState.Normal;panel.MinWidth=780;panel.MinHeight=480;panel.Width=1280;panel.Height=830;
            Native.SetWindowPos(new WindowInteropHelper(panel).Handle,IntPtr.Zero,40,40,1280,830,0x10);panel.Activate();SetForegroundWindow(new WindowInteropHelper(panel).Handle);
            panel.UpdateLayout();await Task.Delay(80);DesktopVerification.ClickAt(panel.PointToScreen(new Point(300,24)));await Task.Delay(80);
            if(args.Contains("--require-cached-catalog"))Check(panel.Discover.StartedWithCachedCatalog&&((WrapPanel)panel.Discover.FindName("Cards")).Children.Count>0,"Actual WPF process restarts with the verified cached catalog and resolves shared card design resources");
            Check(!panel.IsDirty&&!FindButton(panel,"ApplyButton").IsEnabled&&!FindButton(panel,"CancelButton").IsEnabled,"Unchanged applied state disables Apply and Cancel");
            ((Slider)panel.FindName("LengthSlider")).Value=137;
            Check(panel.IsDirty&&FindButton(panel,"ApplyButton").IsEnabled&&FindButton(panel,"CancelButton").IsEnabled&&panel.ViewState.StateLabel=="Draft","Draft edit enables actions and exposes accurate state");
            var color=(TextBox)panel.FindName("ColorInput");color.Text="invalid";
            Check(!FindButton(panel,"ApplyButton").IsEnabled&&FindButton(panel,"CancelButton").IsEnabled&&panel.ViewState.InvalidColor,"Invalid draft remains cancellable and cannot be applied");
            FindButton(panel,"CancelButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(!panel.IsDirty&&!FindButton(panel,"ApplyButton").IsEnabled&&!FindButton(panel,"CancelButton").IsEnabled,"Cancel restores applied configuration and button state");
            var length=(Slider)panel.FindName("LengthSlider");DesktopVerification.ClickAt(length.PointToScreen(new Point(length.ActualWidth/2,length.ActualHeight/2)));await Task.Delay(80);
            Check(Native.GetForegroundWindow()==new WindowInteropHelper(panel).Handle&&length.Focus(),"Owned window receives foreground and slider keyboard focus after actual click");
            double lengthBefore=length.Value;Check(DesktopVerification.PressKey(0x27),"Native keyboard arrow submitted to custom slider");await Task.Delay(80);
            Check(length.Value>lengthBefore&&panel.Draft.Effects.Length==length.Value,"Custom slider retains actual keyboard value change and binding");
            var toggle=(CheckBox)panel.FindName("EnabledCheck");toggle.Focus();bool enabledBefore=toggle.IsChecked==true;DesktopVerification.PressKey(0x20);await Task.Delay(60);
            Check(toggle.IsChecked!=enabledBefore&&panel.Draft.Effects.Enabled==toggle.IsChecked,"Custom toggle retains actual Space keyboard interaction");DesktopVerification.PressKey(0x20);await Task.Delay(60);
            var preset=(ComboBox)panel.FindName("TrailBox");preset.Focus();DesktopVerification.PressKey(0x73);await WaitForInput(()=>preset.IsDropDownOpen);Check(preset.IsDropDownOpen,"Custom combo opens via actual F4 keyboard input");DesktopVerification.PressKey(0x1B);await WaitForInput(()=>!preset.IsDropDownOpen);Check(!preset.IsDropDownOpen&&panel.IsDirty,"Escape closes dropdown without cancelling the draft");
            FindButton(panel,"CancelButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            ((ComboBox)panel.FindName("ThemeBox")).SelectedItem=app.Studio.Catalog.Find("frost");
            ((ComboBox)panel.FindName("TrailBox")).SelectedItem=app.Studio.Effects.Trails.Single(t=>t.Id=="neon");
            ((ComboBox)panel.FindName("ClickBox")).SelectedIndex=2;
            ((TextBox)panel.FindName("ProfileName")).Text="Daily Focus";
            ((Button)panel.FindName("NewProfileButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(panel.ViewState.HasProfile&&panel.ViewState.ProfileCaption.Contains("Daily Focus")&&panel.IsDirty,"Saved Profile is identified separately from unapplied draft");
            var roles=(ComboBox)panel.FindName("PreviewRole");string draftBeforeRoles=JsonSerializer.Serialize(panel.Draft,Storage.Json);bool rolesVisible=true;
            for(int role=0;role<roles.Items.Count;role++){roles.SelectedIndex=role;rolesVisible&=((LivePreview)panel.FindName("Preview")).Cursor!=null&&((System.Windows.Controls.Image)panel.FindName("HeroImage")).Source!=null;}
            Check(rolesVisible&&roles.Items.Count==15&&JsonSerializer.Serialize(panel.Draft,Storage.Json)==draftBeforeRoles,"All 15 local preview roles retain cursor/thumbnail without changing configuration");roles.SelectedIndex=0;
            var navigation=(ListBox)panel.FindName("Navigation");
            for(int page=0;page<5;page++)
            {
                navigation.SelectedIndex=page;await Task.Delay(250);
                if(page is 1 or 2)await MoveInsidePreview(panel);
                if(page==0)Check(panel.Discover.IsVisible&&!((LivePreview)panel.FindName("Preview")).IsVisible&&!((FrameworkElement)panel.FindName("ConfigurationPanel")).IsVisible,"Explore exposes discovery controls while local preview and Customize remain in the owned library");
                CaptureWindow(panel,Path.Combine(directory,new[]{"explore","cursors","trails","mix-studio","settings"}[page]+".png"));
            }
            navigation.SelectedIndex=1;
            var tabs=(TabControl)panel.FindName("ConfigTabs");
            for(int tab=0;tab<3;tab++){tabs.SelectedIndex=tab;await Task.Delay(180);Check(tabs.SelectedContent is ScrollViewer,"Settings view uses a real scrollable control: "+tab);CaptureWindow(panel,Path.Combine(directory,new[]{"customize-trail","customize-cursor","customize-click"}[tab]+".png"));}
            tabs.SelectedIndex=0;var advanced=(Expander)panel.FindName("TrailAdvanced");advanced.IsExpanded=true;await Task.Delay(100);
            Check(((Slider)panel.FindName("DurationSlider")).IsVisible,"Advanced settings exposes bound fade slider");advanced.BringIntoView();await Task.Delay(80);CaptureWindow(panel,Path.Combine(directory,"advanced-settings.png"));advanced.IsExpanded=false;
            Check(!((Slider)panel.FindName("DurationSlider")).IsVisible,"Collapsed advanced settings hides uncommon controls");
            navigation.SelectedIndex=4;var animation=(CheckBox)panel.FindName("AnimationsCheck");animation.IsChecked=false;
            Check(!UiMotion.Allowed&&!UiPreferences.Load().EnableAnimations,"Disable animations persists outside effect profiles");animation.IsChecked=true;
            Check(UiMotion.Allowed==SystemParameters.ClientAreaAnimation,"System animation preference gates optional UI motion");
            var nativeTitle=(CheckBox)panel.FindName("SystemTitleCheck");nativeTitle.IsChecked=true;await Task.Delay(120);
            Check(panel.WindowStyle==WindowStyle.SingleBorderWindow&&UiPreferences.Load().UseSystemTitleBar&&((FrameworkElement)panel.FindName("CustomTitleBar")).Visibility==Visibility.Collapsed,"Native title bar fallback is functional and persisted");CaptureWindow(panel,Path.Combine(directory,"system-titlebar.png"));nativeTitle.IsChecked=false;await Task.Delay(120);
            Check(panel.WindowStyle==WindowStyle.None&&System.Windows.Shell.WindowChrome.GetWindowChrome(panel)!=null,"Custom caption uses native WindowChrome sizing and hit testing");
            var h=new WindowInteropHelper(panel).Handle;var caption=panel.PointToScreen(new Point(330,24));
            Check(SendMessage(h,0x84,IntPtr.Zero,PointParameter(caption)).ToInt32()==2,"Caption native hit-test returns HTCAPTION for drag and double-click");
            Native.GetWindowRect(h,out var r);
            Check(SendMessage(h,0x84,IntPtr.Zero,PointParameter(new Point(r.Left+2,r.Top+r.Height/2))).ToInt32()==10,"Left frame native hit-test returns HTLEFT for resizing");
            FindButton(panel,"MaximizeButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Task.Delay(120);Check(panel.WindowState==WindowState.Maximized,"Custom maximize button invokes native window management");
            var monitor=new Native.MonitorInfo{Size=Marshal.SizeOf<Native.MonitorInfo>(),Device=""};Native.GetMonitorInfo(Native.MonitorFromWindow(h,2),ref monitor);var close=FindButton(panel,"CloseButton");var closeStart=close.PointToScreen(new Point());var closeEnd=close.PointToScreen(new Point(close.ActualWidth,close.ActualHeight));
            Check(closeStart.Y>=monitor.Work.Top&&closeEnd.X<=monitor.Work.Right&&closeEnd.Y<=monitor.Work.Bottom,"Maximized caption controls stay inside monitor work area");
            FindButton(panel,"MaximizeButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Task.Delay(120);Check(panel.WindowState==WindowState.Normal,"Custom restore button restores normal window");
            FindButton(panel,"MinimizeButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));await Task.Delay(100);Check(panel.WindowState==WindowState.Minimized,"Custom minimize button works");SystemCommands.RestoreWindow(panel);
            FindButton(panel,"CloseButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));Check(!panel.IsVisible,"Custom close preserves tray residency");((System.Windows.Forms.ToolStripMenuItem)app.TrayMenu.Items[0]).PerformClick();Check(panel.IsVisible,"Tray reopens custom chrome window");
            foreach(var screen in new[]{new Size(1366,768),new Size(1920,1080),new Size(2560,1440)})
            foreach(double scale in new[]{1.0,1.25,1.5})
            {
                double width=(screen.Width-32)/scale,height=(screen.Height-64)/scale;panel.Left=0;panel.Top=0;panel.MinWidth=Math.Min(780,width);panel.MinHeight=Math.Min(480,height);panel.Width=width;panel.Height=height;navigation.SelectedIndex=1;await Task.Delay(100);
                var apply=FindButton(panel,"ApplyButton");var cancel=FindButton(panel,"CancelButton");var preview=(LivePreview)panel.FindName("Preview");
                Check(InWindow(panel,apply)&&InWindow(panel,cancel)&&preview.ActualWidth>150,"Actions remain reachable at logical work area for "+screen+" / "+scale);
                Check(preview.TranslatePoint(new Point(preview.ActualWidth,0),panel).X<apply.TranslatePoint(new Point(),panel).X,"Preview and settings remain separate for "+screen+" / "+scale);
                for(int page=0;page<5;page++){navigation.SelectedIndex=page;await Task.Delay(20);Check(page==0?InWindow(panel,(Button)panel.Discover.FindName("RefreshButton"))&&!apply.IsVisible:InWindow(panel,apply)&&InWindow(panel,cancel),"Page-specific actions reachable on page "+page+" at "+screen+" / "+scale);}
                navigation.SelectedIndex=1;await Task.Delay(220);CaptureWindow(panel,Path.Combine(directory,$"layout-{screen.Width}x{screen.Height}-{scale*100:F0}-logical.png"));
            }
            panel.Width=850;panel.Height=520;await Task.Delay(100);Check(InWindow(panel,FindButton(panel,"ApplyButton"))&&InWindow(panel,FindButton(panel,"CancelButton")),"850x520 DIP window preserves reachable actions");CaptureWindow(panel,Path.Combine(directory,"compact-850x520.png"));
            panel.ImportResources("An exceptionally long external cursor theme name for layout review",new Dictionary<string,string>{{"Normal",app.Studio.Catalog.Find("frost").Resource("Normal")}});
            navigation.SelectedIndex=3;((TextBox)panel.FindName("ProfileName")).Text="A long reusable profile name for checking compact window layout";FindButton(panel,"RenameProfileButton").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));navigation.SelectedIndex=1;await Task.Delay(220);
            Check(InWindow(panel,FindButton(panel,"ApplyButton"))&&InWindow(panel,FindButton(panel,"CancelButton")),"Long imported theme and saved profile names preserve pinned actions in compact layout");CaptureWindow(panel,Path.Combine(directory,"compact-long-names.png"));
            Check(JsonSerializer.Serialize(new ThemeService().Snapshot().Cursors)==nativeBefore&&JsonSerializer.Serialize(app.Studio.State,Storage.Json)==appliedBefore,"UI review never applies draft or changes Windows cursor state");
            passed=true;
        }
        catch(Exception ex){error=ex.ToString();Storage.Log(error);}
        finally
        {
            Native.SetPhysicalCursorPos(pointer.X,pointer.Y);if(foreground!=IntPtr.Zero)SetForegroundWindow(foreground);
            Storage.Write(Path.Combine(directory,"capture-report.json"),new{passed,error,checks,width=1280,height=830,physicalDpi=Native.GetDpiForWindow(new WindowInteropHelper(panel).Handle),notes="Real WPF/native window capture and native hit tests at the observed physical DPI. Resolution/DPI matrix uses equivalent logical work areas, not physical monitor DPI changes. Controlled isolated data; no Apply or native cursor change."});
            app.ExitApp(passed?0:1);
        }
    }
    private static Button FindButton(MainWindow panel,string name)=>(Button)panel.FindName(name);
    private static IntPtr PointParameter(Point p)=>new(unchecked((int)((uint)(ushort)(int)p.X|((uint)(ushort)(int)p.Y<<16))));
    private static bool InWindow(Window window,FrameworkElement control){var p=control.TranslatePoint(new Point(),window);return control.IsVisible&&control.ActualWidth>=100&&p.X>=0&&p.Y>=0&&p.X+control.ActualWidth<=window.ActualWidth&&p.Y+control.ActualHeight<=window.ActualHeight;}
    private static async Task MoveInsidePreview(MainWindow panel)
    {
        var preview=(LivePreview)panel.FindName("Preview");preview.BringIntoView();await Task.Delay(40);var origin=preview.PointToScreen(new Point(50,100));
        for(int i=0;i<24;i++){Native.SetPhysicalCursorPos((int)origin.X+i*8,(int)origin.Y+(int)(Math.Sin(i*.17)*35));await Task.Delay(10);}
        Check(preview.Model.Points.Count>1,"Real mouse movement renders the temporary combination in local preview");
    }
    public static void CaptureWindow(Window window,string path)
    {
        if(!Native.GetWindowRect(new WindowInteropHelper(window).Handle,out var rectangle))throw new IOException("Could not read screenshot bounds.");
        using var bitmap=new System.Drawing.Bitmap(rectangle.Width,rectangle.Height);
        using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen(rectangle.Left,rectangle.Top,0,0,bitmap.Size);
        bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
    }
}
