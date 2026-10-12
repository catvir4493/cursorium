using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Frost.Core;

namespace Frost.App;
public partial class MainWindow:Window
{
    private readonly Settings settings;
    private readonly TrailEngine engine;
    private readonly ThemeService theme;
    private readonly List<Cursor> cursors=new();
    private readonly List<(Image Image,System.Windows.Media.Imaging.BitmapSource[] Frames)> animations=new();
    private readonly DispatcherTimer statusTimer,saveTimer;
    private bool ready;
    private int frame;
    public MainWindow(Settings settings,TrailEngine engine,ThemeService theme)
    {
        this.settings=settings;this.engine=engine;this.theme=theme;InitializeComponent();
        foreach(var role in CursorRoles.All)
        {
            var cursor=new Cursor(System.IO.Path.Combine(AppContext.BaseDirectory,"Theme",role.FileName));cursors.Add(cursor);
            var image=new Image{Source=CursorArtwork.Render(role,48),Width=38,Height=38,Margin=new Thickness(0,0,0,5)};
            if(role.Animated)animations.Add((image,Enumerable.Range(0,24).Select(f=>CursorArtwork.Render(role,48,f)).ToArray()));
            var label=new TextBlock{Text=role.Label,FontSize=9,HorizontalAlignment=HorizontalAlignment.Center,Foreground=new SolidColorBrush(CursorArtwork.ColorFrom("#AABED6"))};
            var box=new StackPanel{Margin=new Thickness(0,0,0,14),Cursor=cursor,ToolTip=$"{role.Name} · hotspot {role.HotX},{role.HotY} @32px"};box.Children.Add(image);box.Children.Add(label);RoleGrid.Children.Add(box);
            PreviewRole.Items.Add(role.Label);
        }
        PreviewRole.SelectedIndex=0;LoadControls();UpdateThemeStatus();ready=true;
        saveTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};saveTimer.Tick+=(_,_)=>{saveTimer.Stop();try{settings.Save();}catch(Exception ex){Feedback.Text="Could not save settings: "+ex.Message;}};
        statusTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};statusTimer.Tick+=(_,_)=>{EngineStatus.Text=engine.Status;if(IsVisible){frame=(frame+2)%24;foreach(var (image,frames) in animations)image.Source=frames[frame];}};
        Closing+=(_,e)=>{if(!((App)System.Windows.Application.Current).Exiting){e.Cancel=true;Hide();}};
        IsVisibleChanged+=(_,_)=>{if(IsVisible)statusTimer.Start();else statusTimer.Stop();};
    }
    public void DisposeResources(){statusTimer.Stop();saveTimer.Stop();foreach(var cursor in cursors)cursor.Dispose();}
    private void LoadControls()
    {
        bool previous=ready;ready=false;EnabledCheck.IsChecked=settings.Enabled;LengthSlider.Value=settings.Length;DurationSlider.Value=settings.Duration;OpacitySlider.Value=settings.Opacity*100;ParticleSlider.Value=settings.Particles;ColorInput.Text=settings.Color;
        HaloCheck.IsChecked=settings.ClickHalo;FullscreenCheck.IsChecked=settings.PauseFullscreen;PowerCheck.IsChecked=settings.PowerSaving;StartupCheck.IsChecked=StartupRegistration.Enabled;
        PresetBox.SelectedIndex=settings.Preset=="Minimal"?0:settings.Preset=="Glow"?2:settings.Preset=="Custom"?3:1;FpsBox.SelectedIndex=settings.FrameRate>=120?1:0;ready=previous;
    }
    public void Refresh(){LoadControls();UpdateThemeStatus();}
    private void Changed(){if(!ready)return;saveTimer.Stop();saveTimer.Start();((App)System.Windows.Application.Current).RefreshTray();}
    private void ParameterChanged(object s,RoutedPropertyChangedEventArgs<double> e)
    {
        if(!ready)return;settings.Length=LengthSlider.Value;settings.Duration=DurationSlider.Value;settings.Opacity=OpacitySlider.Value/100;settings.Particles=(int)ParticleSlider.Value;settings.Preset="Custom";Changed();
    }
    private void ToggleChanged(object s,RoutedEventArgs e){if(!ready)return;settings.Enabled=EnabledCheck.IsChecked==true;settings.ClickHalo=HaloCheck.IsChecked==true;settings.PauseFullscreen=FullscreenCheck.IsChecked==true;settings.PowerSaving=PowerCheck.IsChecked==true;Changed();}
    private void PresetChanged(object s,SelectionChangedEventArgs e){if(!ready)return;string name=((ComboBoxItem)PresetBox.SelectedItem).Content.ToString()!;if(name=="Custom")settings.Preset=name;else settings.ApplyPreset(name);LoadControls();Changed();}
    private void FpsChanged(object s,SelectionChangedEventArgs e){if(!ready)return;settings.FrameRate=FpsBox.SelectedIndex==1?120:60;Changed();}
    private void ColorClicked(object s,RoutedEventArgs e){settings.Color=(string)((Button)s).Tag;ColorInput.Text=settings.Color;Changed();}
    private void ApplyColor(object s,RoutedEventArgs e)
    {
        try{string text=ColorInput.Text.Trim();if(text.Length!=7||text[0]!='#')throw new FormatException();CursorArtwork.ColorFrom(text);settings.Color=text.ToUpperInvariant();ColorInput.Text=settings.Color;Changed();Feedback.Text="Effect color applied.";}catch{Feedback.Text="Enter a #RRGGBB color, such as #91DEFF.";}
    }
    private void StartupChanged(object s,RoutedEventArgs e)
    {
        if(!ready)return;try{StartupRegistration.Set(StartupCheck.IsChecked==true,Environment.ProcessPath!);Feedback.Text=StartupCheck.IsChecked==true?"Startup at sign-in is enabled for the current user.":"Startup at sign-in is disabled.";}catch(Exception ex){Feedback.Text=ex.Message;ready=false;StartupCheck.IsChecked=StartupRegistration.Enabled;ready=true;}
    }
    private void PreviewChanged(object s,SelectionChangedEventArgs e){if(TestArea!=null&&PreviewRole.SelectedIndex>=0&&PreviewRole.SelectedIndex<cursors.Count)TestArea.Cursor=cursors[PreviewRole.SelectedIndex];}
    private void InstallTheme(object s,RoutedEventArgs e)
    {
        if(MessageBox.Show(this,"Your current cursor scheme will be backed up, then all 15 system roles will switch to FROST. The theme remains after you quit and can be restored at any time.\n\nInstall FROST / WHITE COMET now?","Install native cursor theme",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        try{theme.Install(System.IO.Path.Combine(AppContext.BaseDirectory,"Theme"));Feedback.Text="Theme installed. Your original scheme has been backed up.";}catch(Exception ex){Feedback.Text="Installation failed: "+ex.Message;Storage.Log(ex.ToString());}UpdateThemeStatus();
    }
    public void RestoreWithConfirmation()
    {
        if(!theme.HasBackup)return;
        if(MessageBox.Show(this,"Restore the cursor scheme saved before installing FROST? This also replaces any scheme you selected manually afterward.","Restore original theme",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        try{theme.Restore();Feedback.Text="Original scheme restored. The backup has been archived.";}catch(Exception ex){Feedback.Text="Restore failed: "+ex.Message;Storage.Log(ex.ToString());}UpdateThemeStatus();
    }
    private void RestoreTheme(object s,RoutedEventArgs e)=>RestoreWithConfirmation();
    private void UpdateThemeStatus(){ThemeStatus.Text=theme.IsInstalled?"FROST is installed. Your original scheme is backed up.":theme.HasBackup?"The active scheme has changed. Your original backup is still available.":"Theme not installed. Cursors and trails can be used independently.";RestoreButton.IsEnabled=theme.HasBackup;}
}
