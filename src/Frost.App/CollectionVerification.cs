using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Frost.Core;
using Frost.App.Presentation;

namespace Frost.App;

internal static class CollectionVerification
{
    private static readonly List<object> checks=new();
    private static void Check(bool value,string name){checks.Add(new{name,passed=value});if(!value)throw new Exception(name);}
    public static async Task Run(App app,string[] args)
    {
        int index=Array.IndexOf(args,"--report");var directory=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(directory);
        var panel=app.Panel;var studio=app.Studio;var service=new ThemeService();var baseline=JsonSerializer.Serialize(service.Snapshot().Cursors);
        Native.GetPhysicalCursorPos(out var pointer);var foreground=Native.GetForegroundWindow();var process=Process.GetCurrentProcess();var clock=Stopwatch.StartNew();var cpu=process.TotalProcessorTime;
        bool passed=false;string? error=null;int gdiBefore=0,userBefore=0,gdiAfter=0,userAfter=0;
        T Control<T>(string name)=>(T)panel.FindName(name);
        void Click(string name)=>Control<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        try
        {
            panel.Topmost=true;panel.Show();Control<ListBox>("Navigation").SelectedIndex=1;await Task.Delay(250);Native.SetWindowPos(new WindowInteropHelper(panel).Handle,IntPtr.Zero,50,50,1680,980,0x10);panel.Activate();SetForegroundWindow(new WindowInteropHelper(panel).Handle);DesktopVerification.ClickAt(panel.PointToScreen(new Point(300,24)));await Task.Delay(180);
            Check(Native.GetForegroundWindow()==new WindowInteropHelper(panel).Handle,"Collection captures own actual foreground WPF window");
            var themeBox=Control<ComboBox>("ThemeBox");var filter=Control<ComboBox>("LibraryFilter");var cards=Control<ListBox>("ThemeCards");var scroll=Control<ScrollViewer>("ContentScroll");
            filter.SelectedIndex=2;await Task.Delay(100);Check(cards.Items.Count==5&&cards.Items.Cast<ThemeCardView>().All(c=>c.Theme.Collection=="cute"),"Cute filter presents exactly five real themes");
            Check(ThemeLibraryPreferences.Load().Filter=="cute","Filter choice persists across library reload");
            var cat=studio.Catalog.Find("cute-kawaii-cat-curs0rs");var pink=studio.Catalog.Find("cute-kawaii-pink-heart");var bunny=studio.Catalog.Find("cute-bunny-cute-kawaii");
            themeBox.SelectedItem=cat;await Task.Delay(100);var before=JsonSerializer.Serialize(studio.State,Storage.Json);var effects=JsonSerializer.Serialize(studio.LiveEffects,Storage.Json);
            Check(panel.IsDirty&&Control<Button>("ApplyButton").IsEnabled&&JsonSerializer.Serialize(service.Snapshot().Cursors)==baseline,"Cute card selection changes Draft without changing Windows");
            Check(JsonSerializer.Serialize(studio.State,Storage.Json)==before&&JsonSerializer.Serialize(studio.LiveEffects,Storage.Json)==effects,"Cute preview leaves applied desktop effects and saved state unchanged");
            foreach(var (theme,file) in new[]{(cat,"kawaii-cat-preview.png"),(pink,"pink-heart-preview.png"),(bunny,"bunny-preview.png")})
            {
                themeBox.SelectedItem=theme;scroll.ScrollToTop();await Task.Delay(90);
                var preview=Control<LivePreview>("Preview");var point=preview.PointToScreen(new Point(50,100));for(int i=0;i<16;i++){Native.SetPhysicalCursorPos((int)point.X+i*11,(int)point.Y+(int)(Math.Sin(i*.3)*35));await Task.Delay(12);}
                Check(preview.Cursor!=null&&preview.Model.Points.Count>0,theme.Name+": actual pointer moves and draws inside local preview");
                InterfaceVerification.CaptureWindow(panel,Path.Combine(directory,file));
            }
            scroll.ScrollToTop();Control<FrameworkElement>("ThemeLibrary").BringIntoView();await Task.Delay(140);InterfaceVerification.CaptureWindow(panel,Path.Combine(directory,"cute-collection.png"));
            themeBox.SelectedItem=bunny;Control<Expander>("ThemeDetails").IsExpanded=true;Control<FrameworkElement>("ThemeDetails").BringIntoView();await Task.Delay(120);
            Check(Control<TextBlock>("ThemeDetailsText").Text.Contains("12/15")&&Control<TextBlock>("ThemeDetailsText").Text.Contains("Link ← frost"),"Theme details show real native roles, missing roles and named fallback");InterfaceVerification.CaptureWindow(panel,Path.Combine(directory,"theme-details.png"));Control<Expander>("ThemeDetails").IsExpanded=false;
            var catCard=cards.Items.Cast<ThemeCardView>().Single(c=>c.Theme.Id==cat.Id);var container=(ListBoxItem)cards.ItemContainerGenerator.ContainerFromItem(catCard);container.BringIntoView();await Task.Delay(80);
            var favorite=Buttons(container).Single(b=>Equals(b.Tag,"favorite"));favorite.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(ThemeLibraryPreferences.Load().Favorites.Contains(cat.Id)&&panel.Draft.CursorThemeId==bunny.Id,"Favorite persists without changing selected Draft theme");
            filter.SelectedIndex=4;Check(cards.Items.Count==1&&((ThemeCardView)cards.Items[0]).Theme.Id==cat.Id,"Favorites filter reuses theme ID without copying resources");filter.SelectedIndex=1;Check(cards.Items.Count==3,"Built-in filter retains original system, FROST and Obsidian");filter.SelectedIndex=2;
            themeBox.SelectedItem=cat;scroll.ScrollToTop();await Task.Delay(60);Check(panel.AnimatedPreview.Running,"ANI thumbnail timer runs for visible functional preview");
            var timeline=AniTimeline.Read(File.ReadAllBytes(cat.Resource("Normal")));var frames=AnimatedCursorPreview.DecodeFrames(File.ReadAllBytes(cat.Resource("Normal")));
            Check(frames.Length==timeline.Sequence.Distinct().Count()&&frames.All(f=>f.IsFrozen),"ANI frames decoded once into bounded immutable WPF images");
            var hashes=frames.Select(f=>{var bytes=new byte[f.PixelWidth*f.PixelHeight*4];f.CopyPixels(bytes,f.PixelWidth*4,0);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));}).Distinct().Count();
            Check(hashes>1,"ANI preview uses distinct actual source pixels, not a static first frame");
            var initial=Control<System.Windows.Controls.Image>("HeroImage").Source;bool moved=false;for(int i=0;i<25;i++){await Task.Delay(20);moved|=!ReferenceEquals(initial,Control<System.Windows.Controls.Image>("HeroImage").Source);}
            Check(moved,"Visible ANI thumbnail advances according to source timing");Control<CheckBox>("AnimationsCheck").IsChecked=false;Check(panel.AnimatedPreview.Running,"Disabling optional UI motion preserves functional cursor animation");
            panel.Hide();await Task.Delay(40);Check(!panel.AnimatedPreview.Running,"Hidden window stops ANI preview rendering");panel.Show();Control<ListBox>("Navigation").SelectedIndex=1;await Task.Delay(50);scroll.ScrollToEnd();await Task.Delay(60);Check(scroll.VerticalOffset>Control<LivePreview>("Preview").ActualHeight&&!panel.AnimatedPreview.Running,"Preview outside scrolling viewport stops ANI rendering");Control<ListBox>("Navigation").SelectedIndex=1;scroll.ScrollToTop();await Task.Delay(50);
            gdiBefore=GetGuiResources(process.Handle,0);userBefore=GetGuiResources(process.Handle,1);
            for(int i=0;i<30;i++){themeBox.SelectedItem=studio.Catalog.Themes.Where(t=>t.Collection=="cute").ElementAt(i%5);await Task.Delay(12);}
            GC.Collect();GC.WaitForPendingFinalizers();await Task.Delay(80);gdiAfter=GetGuiResources(process.Handle,0);userAfter=GetGuiResources(process.Handle,1);
            Check(gdiAfter-gdiBefore<12&&userAfter-userBefore<12,"Thirty animated theme switches release native/GDI resources within bounded growth");
            themeBox.SelectedItem=cat;Control<ComboBox>("TrailBox").SelectedItem=studio.Effects.Trails.Single(p=>p.Id=="neon");Control<ComboBox>("ClickBox").SelectedIndex=2;Control<TextBox>("ProfileName").Text="Cute purple studio";Control<TextBox>("ColorInput").Text="#BA79FF";Click("NewProfileButton");Check(panel.ViewState.HasProfile,"Cute combination can be saved as a profile");
            Check(panel.ApplyDraft()&&service.IsActive(cat),"Cute Cat Apply performs actual Windows theme transaction");var appliedProfile=studio.State.ProfileId;var appliedConfig=JsonSerializer.Serialize(studio.State.Applied,Storage.Json);
            themeBox.SelectedItem=pink;Check(cards.Items.Cast<ThemeCardView>().Single(c=>c.Theme.Id==cat.Id).Status=="Applied"&&cards.Items.Cast<ThemeCardView>().Single(c=>c.Theme.Id==pink.Id).Status=="Selected · Modified","Cards distinguish Applied Cat from Selected Pink draft");
            scroll.ScrollToTop();await Task.Delay(120);InterfaceVerification.CaptureWindow(panel,Path.Combine(directory,"applied-vs-selected.png"));
            Click("CancelButton");Check(panel.Draft.CursorThemeId==cat.Id&&!panel.IsDirty&&studio.State.ProfileId==appliedProfile&&JsonSerializer.Serialize(panel.Draft,Storage.Json)==appliedConfig,"Cancel restores applied cute theme, profile and parameters");
            var restart=new StudioService(AppContext.BaseDirectory,new ThemeService());Check(restart.State.ProfileId==appliedProfile&&restart.State.Applied.CursorThemeId==cat.Id&&restart.Catalog.Find(cat.Id).SourceFiles.Count==15,"Restart restores applied cute combination and actual resources");
            using(var pack=CursorPack.Open(cat.DirectoryPath))
            {
                var dialog=new PackImportDialog(panel,pack,studio.Catalog.Themes,cat,"Reviewed Cat");dialog.Show();await Task.Delay(100);Check(dialog.Replacements.Count==15&&dialog.Fallback.Id==cat.Id,"Real pack review maps complete canonical roles and exposes fallback");InterfaceVerification.CaptureWindow(dialog,Path.Combine(directory,"import-pack-review.png"));dialog.Close();
            }
            panel.Width=850;panel.Height=520;await Task.Delay(120);Check(Control<Button>("ApplyButton").IsVisible&&Control<Button>("ApplyButton").ActualWidth>100&&Control<ComboBox>("LibraryFilter").ActualWidth>100,"Cute library retains reachable actions and filter at compact size");InterfaceVerification.CaptureWindow(panel,Path.Combine(directory,"cute-compact.png"));
            studio.RestoreOriginal();Check(JsonSerializer.Serialize(service.Snapshot().Cursors)==baseline,"Collection verification restores exact pre-test Windows pointer values");passed=true;
        }
        catch(Exception ex){error=ex.ToString();Storage.Log(error);}
        finally
        {
            if(service.HasBackup)service.Restore();Native.SetPhysicalCursorPos(pointer.X,pointer.Y);if(foreground!=IntPtr.Zero)SetForegroundWindow(foreground);process.Refresh();
            Storage.Write(Path.Combine(directory,"collection-report.json"),new{passed,error,checks,physicalDpi=Native.GetDpiForWindow(new WindowInteropHelper(panel).Handle),monitors=Native.Monitors().Select(m=>new{m.Monitor.Width,m.Monitor.Height}),seconds=clock.Elapsed.TotalSeconds,cpuSeconds=(process.TotalProcessorTime-cpu).TotalSeconds,workingSet=process.WorkingSet64,gdiBefore,gdiAfter,userBefore,userAfter,notes="Actual WPF window captures and real cursor movement. Isolated data, native roundtrip restored. Handle figures cover 30 actual theme switches after thumbnail warmup."});app.ExitApp(passed?0:1);
        }
    }
    private static IEnumerable<Button> Buttons(DependencyObject root){for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++){var child=System.Windows.Media.VisualTreeHelper.GetChild(root,i);if(child is Button button)yield return button;foreach(var b in Buttons(child))yield return b;}}
    [DllImport("user32.dll")]private static extern int GetGuiResources(IntPtr process,int flags);
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr window);
}
