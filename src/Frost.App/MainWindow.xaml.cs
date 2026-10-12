using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Frost.Core;

namespace Frost.App;

public sealed record ThemeCardView(CursorTheme Theme,BitmapSource Thumbnail);

public partial class MainWindow:Window
{
    private readonly StudioService studio;
    private readonly TrailEngine engine;
    private readonly ThemeService theme;
    private readonly List<Cursor> cursors=new();
    private readonly List<Cursor> ownedCursors=new();
    private readonly Dictionary<string,BitmapSource> thumbnails=new();
    private readonly DispatcherTimer statusTimer;
    private bool ready,disposed;
    private string? profileId;
    public StudioConfiguration Draft {get;}
    public bool IsDirty=>profileId!=studio.State.ProfileId||(ColorInput!=null&&!Settings.ValidColor(ColorInput.Text))||JsonSerializer.Serialize(Draft,Storage.Json)!=JsonSerializer.Serialize(studio.State.Applied,Storage.Json);
    public MainWindow(StudioService studio,TrailEngine engine,ThemeService theme)
    {
        this.studio=studio;this.engine=engine;this.theme=theme;Draft=studio.State.Applied.Clone();profileId=studio.State.ProfileId;InitializeComponent();
        foreach(var role in CursorRoles.All)PreviewRole.Items.Add(role.Name);PreviewRole.SelectedIndex=0;
        BindCatalog();LoadControls();BindProfiles();Navigation.SelectedIndex=0;ready=true;
        Preview.Update(Draft.Effects);UpdateDraftStatus();Feedback.Text=studio.LoadWarning??string.Join("; ",studio.Catalog.Warnings);
        statusTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};statusTimer.Tick+=(_,_)=>{EngineStatus.Text=engine.Status;RestoreButton.IsEnabled=theme.HasBackup;};
        IsVisibleChanged+=(_,_)=>{if(IsVisible)statusTimer.Start();else statusTimer.Stop();UpdatePreviewBounds();};
        Preview.LayoutUpdated+=PreviewLayoutUpdated;
        Closing+=(_,e)=>{if(!((App)System.Windows.Application.Current).Exiting){e.Cancel=true;Hide();}};
        Loaded+=(_,_)=>FitWorkArea();SizeChanged+=(_,_)=>{NavColumn.Width=new GridLength(ActualWidth<1100?132:158);ParameterColumn.Width=new GridLength(ActualWidth<1100?264:300);};
        DpiChanged+=(_,_)=>Dispatcher.BeginInvoke(new Action(FitWorkArea));
    }
    private void FitWorkArea()
    {
        var dpi=VisualTreeHelper.GetDpi(this);var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var info=new Native.MonitorInfo{Size=System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>(),Device=""};
        if(!Native.GetMonitorInfo(Native.MonitorFromWindow(handle,2),ref info))return;
        double width=info.Work.Width/dpi.DpiScaleX-24,height=info.Work.Height/dpi.DpiScaleY-24;
        MinWidth=Math.Min(900,width);MinHeight=Math.Min(580,height);Width=Math.Min(Width,width);Height=Math.Min(Height,height);
        Left=Math.Max(info.Work.Left/dpi.DpiScaleX,Math.Min(Left,info.Work.Right/dpi.DpiScaleX-Width));Top=Math.Max(info.Work.Top/dpi.DpiScaleY,Math.Min(Top,info.Work.Bottom/dpi.DpiScaleY-Height));
    }
    private void PreviewLayoutUpdated(object? sender,EventArgs e)=>UpdatePreviewBounds();
    private void UpdatePreviewBounds()
    {
        if(!IsVisible||!Preview.IsLoaded){engine.PreviewBounds=null;return;}
        var top=Preview.PointToScreen(new Point(0,0));var bottom=Preview.PointToScreen(new Point(Preview.ActualWidth,Preview.ActualHeight));
        var viewport=ContentScroll.PointToScreen(new Point(0,0));var viewportEnd=ContentScroll.PointToScreen(new Point(ContentScroll.ActualWidth,ContentScroll.ActualHeight));
        engine.PreviewBounds=new Native.Rect{Left=(int)Math.Max(top.X,viewport.X),Top=(int)Math.Max(top.Y,viewport.Y),Right=(int)Math.Min(bottom.X,viewportEnd.X),Bottom=(int)Math.Min(bottom.Y,viewportEnd.Y)};
    }
    private BitmapSource Thumbnail(CursorTheme selected,string role="Normal")
    {
        var key=selected.Id+":"+role;if(thumbnails.TryGetValue(key,out var cached))return cached;
        BitmapSource image;
        var resource=selected.IsSystem?theme.OriginalResource(role):selected.Resource(role);
        if(resource==null)
        {
            var shared=Native.LoadCursor(IntPtr.Zero,(IntPtr)CursorRoles.All.Single(r=>r.Name==role).SystemId);
            image=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(shared,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();
        }
        else
        {
            var handle=Native.LoadImage(IntPtr.Zero,resource,2,48,48,0x10);
            if(handle==IntPtr.Zero)handle=Native.LoadOwnedCursor(resource);
            if(handle==IntPtr.Zero)throw new IOException("Cannot preview cursor: "+role);
            try{image=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();}finally{Native.DestroyCursor(handle);}
        }
        thumbnails[key]=image;return image;
    }
    private void BindCatalog()
    {
        bool wasReady=ready;ready=false;ThemeBox.ItemsSource=studio.Catalog.Themes;
        ThemeCards.ItemsSource=studio.Catalog.Themes.Select(t=>new ThemeCardView(t,Thumbnail(t))).ToArray();TrailBox.ItemsSource=studio.Effects.Trails;TrailCards.ItemsSource=studio.Effects.Trails;ready=wasReady;
    }
    private void BindProfiles()
    {
        bool wasReady=ready;ready=false;ProfileBox.ItemsSource=studio.Profiles.Profiles.ToArray();ProfileBox.SelectedItem=studio.Profiles.Profiles.FirstOrDefault(p=>p.Id==profileId);
        if(ProfileBox.SelectedItem is StudioProfile selected)ProfileName.Text=selected.Name;
        SaveProfileButton.IsEnabled=RenameProfileButton.IsEnabled=DeleteProfileButton.IsEnabled=ProfileBox.SelectedItem!=null;ready=wasReady;
    }
    private void LoadControls()
    {
        bool wasReady=ready;ready=false;var s=Draft.Effects;
        ThemeBox.SelectedItem=studio.Catalog.Themes.FirstOrDefault(t=>t.Id==Draft.CursorThemeId);ThemeCards.SelectedItem=ThemeCards.Items.Cast<ThemeCardView>().FirstOrDefault(t=>t.Theme.Id==Draft.CursorThemeId);
        TrailBox.SelectedItem=studio.Effects.Trails.FirstOrDefault(p=>p.Id==s.TrailEffect);TrailCards.SelectedItem=TrailBox.SelectedItem;
        EnabledCheck.IsChecked=s.Enabled;TrailCheck.IsChecked=s.TrailEnabled;LengthSlider.Value=s.Length;DurationSlider.Value=s.Duration;OpacitySlider.Value=s.Opacity*100;ParticleSlider.Value=s.Particles;ColorInput.Text=s.Color;
        ClickBox.SelectedIndex=s.ClickEffect=="off"||!s.ClickHalo?0:s.ClickEffect=="ring"?2:1;ClickDurationSlider.Value=s.HaloDuration;ClickRadiusSlider.Value=s.ClickRadius;ClickOpacitySlider.Value=s.ClickOpacity*100;
        UpdateParameterAvailability();
        FullscreenCheck.IsChecked=s.PauseFullscreen;PowerCheck.IsChecked=s.PowerSaving;FpsBox.SelectedIndex=s.FrameRate>=120?1:0;StartupCheck.IsChecked=StartupRegistration.Enabled;
        RestoreButton.IsEnabled=theme.HasBackup;ready=wasReady;UpdateThemePreview();Preview.Update(s);UpdateDraftStatus();
    }
    private void UpdateThemePreview()
    {
        if(ThemeBox.SelectedItem is not CursorTheme selected){ThemeTitle.Text="Unavailable cursor theme";ThemeDescription.Text=Draft.CursorThemeId;return;}
        ThemeTitle.Text=selected.Name;ThemeDescription.Text=selected.Description;HeroImage.Source=Thumbnail(selected);
        Preview.Cursor=null;RoleGrid.Children.Clear();Mouse.UpdateCursor();foreach(var cursor in ownedCursors)cursor.Dispose();ownedCursors.Clear();cursors.Clear();
        foreach(var role in CursorRoles.All)
        {
            Cursor? cursor=null;var resource=selected.IsSystem?theme.OriginalResource(role.Name):selected.Resource(role.Name);
            if(resource!=null){cursor=new Cursor(resource);ownedCursors.Add(cursor);}else cursor=SystemCursor(role.Name);cursors.Add(cursor);
            var box=new StackPanel{Margin=new Thickness(3,8,3,10),Cursor=cursor,ToolTip=role.Name};box.Children.Add(new Image{Source=Thumbnail(selected,role.Name),Width=32,Height=32});box.Children.Add(new TextBlock{Text=role.Label,FontSize=10,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,5,0,0)});RoleGrid.Children.Add(box);
        }
        SetPreviewCursor();
    }
    private void SetPreviewCursor(){Preview.Cursor=cursors.Count>PreviewRole.SelectedIndex&&PreviewRole.SelectedIndex>=0?cursors[PreviewRole.SelectedIndex]:null;}
    private static Cursor SystemCursor(string role)=>role switch{"Text"=>Cursors.IBeam,"Precision"=>Cursors.Cross,"Busy"=>Cursors.Wait,"Working"=>Cursors.AppStarting,"Help"=>Cursors.Help,"Unavailable"=>Cursors.No,"Vertical"=>Cursors.SizeNS,"Horizontal"=>Cursors.SizeWE,"DiagonalNWSE"=>Cursors.SizeNWSE,"DiagonalNESW"=>Cursors.SizeNESW,"Move"=>Cursors.SizeAll,"Link"=>Cursors.Hand,"Alternate"=>Cursors.UpArrow,_=>Cursors.Arrow};
    private void PreviewRoleChanged(object s,SelectionChangedEventArgs e){if(Preview!=null)SetPreviewCursor();}
    private void Changed(){if(!ready)return;Preview.Update(Draft.Effects);UpdateDraftStatus();}
    private void UpdateParameterAvailability()
    {
        ParticleSlider.IsEnabled=Draft.Effects.TrailEffect is "comet" or "stardust";
        ParticleSlider.ToolTip=ParticleSlider.IsEnabled?"Maximum simultaneous glow particles":"This preset draws lines without particles";
        ClickDurationSlider.IsEnabled=ClickRadiusSlider.IsEnabled=ClickOpacitySlider.IsEnabled=Draft.Effects.ClickHalo&&Draft.Effects.ClickEffect!="off";
    }
    private void UpdateDraftStatus()
    {
        if(DraftStatus==null)return;DraftStatus.Text=IsDirty?"Preview has changes · Apply to activate":"Matches your applied combination";CancelButton.IsEnabled=IsDirty;
        ApplyButton.IsEnabled=Settings.ValidColor(ColorInput.Text)&&ThemeBox.SelectedItem!=null;
        CombinationLabel.Text=$"{Draft.Effects.TrailEffect} motion · {(Draft.Effects.ClickHalo?Draft.Effects.ClickEffect:"off")} clicks · {Draft.Effects.Color}";
    }
    public void Refresh(){RestoreButton.IsEnabled=theme.HasBackup;((App)System.Windows.Application.Current).RefreshTray();}
    public void RefreshAppliedToggle(){Draft.Effects.Enabled=studio.LiveEffects.Enabled;LoadControls();}
    public void RefreshRestoredCursor(){Draft.CursorThemeId=studio.State.Applied.CursorThemeId;profileId=null;LoadControls();BindProfiles();}
    private void Navigate(object s,SelectionChangedEventArgs e)
    {
        if(CursorPage==null)return;int index=Navigation.SelectedIndex;CursorPage.Visibility=index==1?Visibility.Visible:Visibility.Collapsed;TrailPage.Visibility=index==2?Visibility.Visible:Visibility.Collapsed;MixPage.Visibility=index==3?Visibility.Visible:Visibility.Collapsed;SettingsPage.Visibility=index==4?Visibility.Visible:Visibility.Collapsed;
        ThemeCards.Visibility=index is 0 or 1?Visibility.Visible:Visibility.Collapsed;PageLabel.Text=new[]{"EXPLORE / LOCAL LIBRARY","CURSORS / NATIVE WINDOWS ROLES","TRAILS / MOTION PRESETS","MIX STUDIO / SAVED COMBINATIONS","SETTINGS / DESKTOP BEHAVIOR"}[Math.Clamp(index,0,4)];
    }
    private void ThemeChanged(object s,SelectionChangedEventArgs e){if(!ready||ThemeBox.SelectedItem is not CursorTheme selected)return;Draft.CursorThemeId=selected.Id;bool was=ready;ready=false;ThemeCards.SelectedItem=ThemeCards.Items.Cast<ThemeCardView>().FirstOrDefault(t=>t.Theme.Id==selected.Id);ready=was;UpdateThemePreview();Changed();}
    private void ThemeCardChanged(object s,SelectionChangedEventArgs e){if(ready&&ThemeCards.SelectedItem is ThemeCardView card)ThemeBox.SelectedItem=card.Theme;}
    private void TrailChanged(object s,SelectionChangedEventArgs e){if(!ready||TrailBox.SelectedItem is not TrailPreset selected)return;selected.Apply(Draft.Effects);LoadControls();Changed();}
    private void TrailCardChanged(object s,SelectionChangedEventArgs e){if(ready&&TrailCards.SelectedItem is TrailPreset selected)TrailBox.SelectedItem=selected;}
    private void ParameterChanged(object s,RoutedPropertyChangedEventArgs<double> e)
    {
        if(!ready)return;var p=Draft.Effects;p.Length=LengthSlider.Value;p.Duration=DurationSlider.Value;p.Opacity=OpacitySlider.Value/100;p.Particles=(int)ParticleSlider.Value;p.HaloDuration=ClickDurationSlider.Value;p.ClickRadius=ClickRadiusSlider.Value;p.ClickOpacity=ClickOpacitySlider.Value/100;p.Preset="Custom";Changed();
    }
    private void ParameterToggle(object s,RoutedEventArgs e){if(!ready)return;var p=Draft.Effects;p.Enabled=EnabledCheck.IsChecked==true;p.TrailEnabled=TrailCheck.IsChecked==true;p.PauseFullscreen=FullscreenCheck.IsChecked==true;p.PowerSaving=PowerCheck.IsChecked==true;Changed();}
    private void FpsChanged(object s,SelectionChangedEventArgs e){if(!ready)return;Draft.Effects.FrameRate=FpsBox.SelectedIndex==1?120:60;Changed();}
    private void ClickChanged(object s,SelectionChangedEventArgs e){if(!ready)return;Draft.Effects.ClickEffect=(string)((ComboBoxItem)ClickBox.SelectedItem).Tag;Draft.Effects.ClickHalo=Draft.Effects.ClickEffect!="off";UpdateParameterAvailability();Changed();}
    private void ColorChanged(object s,TextChangedEventArgs e){if(!ready)return;if(Settings.ValidColor(ColorInput.Text))Draft.Effects.Color=ColorInput.Text.ToUpperInvariant();Changed();}
    private void ColorClicked(object s,RoutedEventArgs e)=>ColorInput.Text=(string)((Button)s).Tag;
    private void ApplyCombination(object s,RoutedEventArgs e)=>ApplyDraft();
    public bool ApplyDraft()
    {
        try
        {
            if(!Settings.ValidColor(ColorInput.Text))throw new ArgumentException("Enter a #RRGGBB color.");studio.Apply(Draft,profileId);Feedback.Text="Applied to desktop. Original cursors remain recoverable.";Refresh();UpdateDraftStatus();return true;
        }
        catch(Exception ex){Feedback.Text="Apply failed: "+ex.Message;Storage.Log(ex.ToString());return false;}
    }
    private void CancelChanges(object s,RoutedEventArgs e){CopyDraft(studio.State.Applied);profileId=studio.State.ProfileId;BindProfiles();Feedback.Text="Preview reset to your applied combination.";}
    private void CopyDraft(StudioConfiguration value){Draft.CursorThemeId=value.CursorThemeId;Draft.Effects.CopyFrom(value.Effects);LoadControls();}
    private void ProfileSelected(object s,SelectionChangedEventArgs e){if(!ready||ProfileBox.SelectedItem is not StudioProfile selected)return;profileId=selected.Id;ProfileName.Text=selected.Name;CopyDraft(selected.Configuration);BindProfiles();Feedback.Text="Profile loaded into preview. Press Apply to activate it.";}
    private void CreateProfile(object s,RoutedEventArgs e)=>WriteProfile(null);
    private void SaveProfile(object s,RoutedEventArgs e)=>WriteProfile(profileId);
    private void WriteProfile(string? id)
    {
        try{studio.Validate(Draft);if(!Settings.ValidColor(ColorInput.Text))throw new ArgumentException("Enter a valid color before saving.");var saved=studio.Profiles.Save(id,ProfileName.Text,Draft);profileId=saved.Id;BindProfiles();Feedback.Text="Profile saved locally.";}catch(Exception ex){Feedback.Text=ex.Message;}
    }
    private void RenameProfile(object s,RoutedEventArgs e){try{if(profileId==null)return;studio.Profiles.Rename(profileId,ProfileName.Text);BindProfiles();Feedback.Text="Profile renamed.";}catch(Exception ex){Feedback.Text=ex.Message;}}
    private void DeleteProfile(object s,RoutedEventArgs e)
    {
        if(profileId==null)return;if(MessageBox.Show(this,"Delete the selected saved profile? Your applied combination is kept.","Delete profile",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        try{studio.Profiles.Delete(profileId);profileId=null;BindProfiles();Feedback.Text="Profile deleted.";}catch(Exception ex){Feedback.Text=ex.Message;}
    }
    private void ImportFiles(object s,RoutedEventArgs e)
    {
        var dialog=new OpenFileDialog{Filter="Windows cursors (*.cur;*.ani)|*.cur;*.ani",Multiselect=true,Title="Import cursor resources"};if(dialog.ShowDialog(this)!=true)return;
        try
        {
            var files=new Dictionary<string,string>();
            if(dialog.FileNames.Length==1)files.Add(CursorRoles.All[PreviewRole.SelectedIndex].Name,dialog.FileName);
            else foreach(var path in dialog.FileNames){var role=CursorRoles.All.SingleOrDefault(r=>r.Name.Equals(Path.GetFileNameWithoutExtension(path),StringComparison.OrdinalIgnoreCase))??throw new ArgumentException("Unrecognized role filename: "+Path.GetFileName(path));if(!files.TryAdd(role.Name,path))throw new ArgumentException("Choose one file per role: "+role.Name);}
            ImportResources(ImportName.Text,files);
        }
        catch(Exception ex){Feedback.Text="Import failed: "+ex.Message;Storage.Log(ex.ToString());}
    }
    public void ImportResources(string name,IReadOnlyDictionary<string,string> resources)
    {
        var selected=studio.Catalog.Find(Draft.CursorThemeId);if(selected.IsSystem)selected=studio.Catalog.Find("frost");
        var imported=studio.Catalog.Import(name,selected,resources);BindCatalog();Draft.CursorThemeId=imported.Id;LoadControls();Feedback.Text="Imported into your local library. Preview it and press Apply.";
    }
    private void RestoreTheme(object s,RoutedEventArgs e)=>RestoreWithConfirmation();
    public void RestoreWithConfirmation()
    {
        if(!theme.HasBackup)return;
        if(MessageBox.Show(this,"Restore the cursor configuration saved before the first installation? This replaces the current Windows cursor scheme.","Restore original cursors",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        try{studio.RestoreOriginal();RefreshRestoredCursor();Feedback.Text="Original Windows cursor configuration restored.";}catch(Exception ex){Feedback.Text="Restore failed: "+ex.Message;Storage.Log(ex.ToString());}
    }
    private void StartupChanged(object s,RoutedEventArgs e)
    {
        if(!ready)return;try{StartupRegistration.Set(StartupCheck.IsChecked==true,Environment.ProcessPath!);Feedback.Text="Sign-in startup updated.";}catch(Exception ex){Feedback.Text=ex.Message;ready=false;StartupCheck.IsChecked=StartupRegistration.Enabled;ready=true;}
    }
    private void OpenData(object s,RoutedEventArgs e){try{Directory.CreateDirectory(Storage.Root);Process.Start(new ProcessStartInfo("explorer.exe",Storage.Root){UseShellExecute=true});}catch(Exception ex){Feedback.Text=ex.Message;}}
    public void DisposeResources(){if(disposed)return;disposed=true;statusTimer.Stop();Preview.LayoutUpdated-=PreviewLayoutUpdated;engine.PreviewBounds=null;Preview.Dispose();Preview.Cursor=null;RoleGrid.Children.Clear();Mouse.UpdateCursor();foreach(var cursor in ownedCursors)cursor.Dispose();ownedCursors.Clear();cursors.Clear();}
}
