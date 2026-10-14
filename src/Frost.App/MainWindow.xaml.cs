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
using Frost.App.Presentation;

namespace Frost.App;

public partial class MainWindow:Window
{
    private readonly StudioService studio;
    private readonly TrailEngine engine;
    private readonly ThemeService theme;
    private readonly List<Cursor> cursors=new();
    private readonly List<Cursor> ownedCursors=new();
    private readonly Dictionary<string,BitmapSource> thumbnails=new();
    private readonly DispatcherTimer statusTimer;
    private readonly AnimatedCursorPreview animatedPreview;
    public DiscoverView Discover {get;}
    private IReadOnlyList<TrailPreset> AllTrails=>studio.Effects.Trails.Concat(Discover?.Downloads.LoadTrails()??Array.Empty<TrailPreset>()).ToArray();
    internal AnimatedCursorPreview AnimatedPreview=>animatedPreview;
    private bool ready,disposed;
    private UiPreferences preferences=new();
    private ThemeLibraryPreferences library=new();
    public StudioViewState ViewState {get;}=new();
    private string? profileId;
    public StudioConfiguration Draft {get;}
    public bool IsDirty=>profileId!=studio.State.ProfileId||(ColorInput!=null&&!Settings.ValidColor(ColorInput.Text))||JsonSerializer.Serialize(Draft,Storage.Json)!=JsonSerializer.Serialize(studio.State.Applied,Storage.Json);
    public MainWindow(StudioService studio,TrailEngine engine,ThemeService theme)
    {
        this.studio=studio;this.engine=engine;this.theme=theme;Draft=studio.State.Applied.Clone();profileId=studio.State.ProfileId;InitializeComponent();DataContext=ViewState;animatedPreview=new(HeroImage);
        Discover=new(studio.Catalog);DiscoverHost.Content=Discover;
        Discover.LibraryChanged+=RefreshOwnedLibrary;
        Discover.FavoritesChanged+=()=>{library=ThemeLibraryPreferences.Load();BindThemeCards();};
        Discover.OpenOwned+=item=>{studio.Catalog.Reload();BindCatalog();if(item.Type=="cursor"){Navigation.SelectedIndex=1;ThemeBox.SelectedItem=studio.Catalog.Find(item.Id);Draft.CursorThemeId=item.Id;Draft.CursorPackageSha256=studio.Catalog.Find(item.Id).ManagedDownload?studio.Catalog.Find(item.Id).PackageSha256:null;UpdateThemePreview();Changed();}else{Navigation.SelectedIndex=2;TrailBox.SelectedItem=TrailBox.Items.Cast<TrailPreset>().First(p=>p.Id==item.Id);}};
        Discover.PreviewBoundsChanged+=bounds=>{if(Navigation.SelectedIndex==0)engine.PreviewBounds=bounds is Rect r?new Native.Rect{Left=(int)r.Left,Top=(int)r.Top,Right=(int)r.Right,Bottom=(int)r.Bottom}:null;};
        NavVersionLabel.Text="v1.5.1 · Stability preview";
        try{preferences=UiPreferences.Load();}catch(Exception ex){Storage.Log(ex.ToString());Notify(ex.Message,true);}
        try{library=ThemeLibraryPreferences.Load();}catch(Exception ex){Storage.Log(ex.ToString());Notify(ex.Message,true);}
        LibraryFilter.SelectedItem=LibraryFilter.Items.Cast<ComboBoxItem>().FirstOrDefault(i=>(string)i.Tag==library.Filter)??LibraryFilter.Items[0];
        AnimationsCheck.IsChecked=preferences.EnableAnimations;SystemTitleCheck.IsChecked=preferences.UseSystemTitleBar;UiMotion.SetAllowed(preferences.EnableAnimations);
        foreach(var role in CursorRoles.All)PreviewRole.Items.Add(role.Name);PreviewRole.SelectedIndex=0;
        BindCatalog();LoadControls();BindProfiles();Navigation.SelectedIndex=0;ready=true;
        Preview.Update(Draft.Effects);UpdateDraftStatus();if(string.IsNullOrEmpty(ViewState.Notification))Notify(studio.LoadWarning??string.Join("; ",studio.Catalog.Warnings));
        statusTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};statusTimer.Tick+=(_,_)=>{EngineStatus.Text=engine.Status;NavRuntimeLabel.Text=engine.Status;RuntimeIndicator.Fill=(Brush)FindResource(studio.LiveEffects.Enabled&&!engine.PausedByUser?"SuccessBrush":"QuietBrush");RestoreButton.IsEnabled=theme.HasBackup;};
        IsVisibleChanged+=(_,_)=>{if(IsVisible)statusTimer.Start();else statusTimer.Stop();UpdatePreviewBounds();};
        Preview.LayoutUpdated+=PreviewLayoutUpdated;
        ContentScroll.ScrollChanged+=PreviewScrollChanged;
        Preview.MouseEnter+=(_,_)=>PreviewHint.Visibility=Visibility.Collapsed;
        Preview.MouseLeave+=(_,_)=>PreviewHint.Visibility=Visibility.Visible;
        Closing+=(_,e)=>{if(!((App)System.Windows.Application.Current).Exiting){e.Cancel=true;Hide();}};
        SourceInitialized+=(_,_)=>ApplyWindowAppearance();
        PreviewKeyDown+=HandleKeyboard;SystemParameters.StaticPropertyChanged+=SystemMotionChanged;
        Loaded+=(_,_)=>{FitWorkArea();FitLayout();};SizeChanged+=(_,_)=>FitLayout();
        StateChanged+=(_,_)=>{MaximizeGlyph.Data=(Geometry)FindResource(WindowState==WindowState.Maximized?"RestoreIcon":"MaximizeIcon");Dispatcher.BeginInvoke(new Action(UpdateChromeInset),DispatcherPriority.Loaded);};
        DpiChanged+=(_,_)=>Dispatcher.BeginInvoke(new Action(FitWorkArea));
    }
    private void FitWorkArea()
    {
        var dpi=VisualTreeHelper.GetDpi(this);var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var info=new Native.MonitorInfo{Size=System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>(),Device=""};
        if(!Native.GetMonitorInfo(Native.MonitorFromWindow(handle,2),ref info))return;
        double width=info.Work.Width/dpi.DpiScaleX-24,height=info.Work.Height/dpi.DpiScaleY-24;
        MinWidth=Math.Min(780,width);MinHeight=Math.Min(480,height);Width=Math.Min(Width,width);Height=Math.Min(Height,height);
        Left=Math.Max(info.Work.Left/dpi.DpiScaleX,Math.Min(Left,info.Work.Right/dpi.DpiScaleX-Width));Top=Math.Max(info.Work.Top/dpi.DpiScaleY,Math.Min(Top,info.Work.Bottom/dpi.DpiScaleY-Height));
    }
    private void FitLayout()
    {
        bool compact=ActualWidth<1000;
        NavColumn.Width=new GridLength(compact?64:ActualWidth<1180?148:184);ParameterColumn.Width=new GridLength(ActualWidth<1000?280:320);
        foreach(var label in new[]{NavExploreLabel,NavCursorLabel,NavTrailLabel,NavMixLabel,NavSettingsLabel,WorkspaceLabel,NavRuntimeLabel,NavVersionLabel})label.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
        // The entire preview panel stays around 260–320 DIP at normal sizes,
        // leaving the library close to the current theme rather than below a banner.
        TestArea.Height=Math.Clamp((ActualHeight-170)*.28,150,240);EngineStatus.Visibility=compact?Visibility.Visible:Visibility.Collapsed;
        RoleGrid.Columns=ContentScroll.ActualWidth<500?3:5;
        ResizeLibrary(ThemeCards);ResizeLibrary(TrailCards);
        Dispatcher.BeginInvoke(new Action(UpdateChromeInset),DispatcherPriority.Loaded);
    }
    private void UpdateChromeInset()
    {
        if(disposed)return;
        var chrome=System.Windows.Shell.WindowChrome.GetWindowChrome(this);
        if(WindowState!=WindowState.Maximized||preferences.UseSystemTitleBar||!IsLoaded||!IsVisible){RootFrame.Padding=new Thickness();if(chrome!=null)chrome.CaptionHeight=48;return;}
        var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var monitor=new Native.MonitorInfo{Size=System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>(),Device=""};
        if(!Native.GetMonitorInfo(Native.MonitorFromWindow(handle,2),ref monitor))return;
        var start=RootFrame.PointToScreen(new Point());var end=RootFrame.PointToScreen(new Point(RootFrame.ActualWidth,RootFrame.ActualHeight));var dpi=VisualTreeHelper.GetDpi(this);
        RootFrame.Padding=new Thickness(Math.Max(0,(monitor.Work.Left-start.X)/dpi.DpiScaleX),Math.Max(0,(monitor.Work.Top-start.Y)/dpi.DpiScaleY),Math.Max(0,(end.X-monitor.Work.Right)/dpi.DpiScaleX),Math.Max(0,(end.Y-monitor.Work.Bottom)/dpi.DpiScaleY));
        if(chrome!=null)chrome.CaptionHeight=48+RootFrame.Padding.Top;
    }
    private void MinimizeWindow(object sender,RoutedEventArgs e)=>SystemCommands.MinimizeWindow(this);
    private void MaximizeWindow(object sender,RoutedEventArgs e){if(WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(this);else SystemCommands.MaximizeWindow(this);}
    private void CloseWindow(object sender,RoutedEventArgs e)=>Close();
    private void ApplyWindowAppearance()
    {
        WindowAppearance.Apply(this,preferences.UseSystemTitleBar);CustomTitleBar.Visibility=preferences.UseSystemTitleBar?Visibility.Collapsed:Visibility.Visible;TitleRow.Height=new GridLength(preferences.UseSystemTitleBar?0:48);
        Dispatcher.BeginInvoke(new Action(UpdateChromeInset),DispatcherPriority.Loaded);
    }
    private void AppearanceChanged(object sender,RoutedEventArgs e)
    {
        if(!ready)return;
        var previous=(preferences.EnableAnimations,preferences.UseSystemTitleBar);
        try{preferences.EnableAnimations=AnimationsCheck.IsChecked==true;preferences.UseSystemTitleBar=SystemTitleCheck.IsChecked==true;preferences.Save();if(preferences.UseSystemTitleBar!=previous.Item2)ApplyWindowAppearance();UpdateMotion();UpdatePreviewBounds();Notify("Appearance saved.");}
        catch(Exception ex){preferences.EnableAnimations=previous.Item1;preferences.UseSystemTitleBar=previous.Item2;ready=false;AnimationsCheck.IsChecked=previous.Item1;SystemTitleCheck.IsChecked=previous.Item2;ready=true;ApplyWindowAppearance();UpdateMotion();Notify("Could not save appearance: "+ex.Message,true);}
    }
    private void UpdateMotion(){UiMotion.SetAllowed(preferences.EnableAnimations);if(!UiMotion.Allowed){ContentScroll.BeginAnimation(OpacityProperty,null);ContentScroll.Opacity=1;}}
    private void SystemMotionChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e){if(e.PropertyName==nameof(SystemParameters.ClientAreaAnimation))Dispatcher.BeginInvoke(new Action(UpdateMotion));}
    private void HandleKeyboard(object sender,System.Windows.Input.KeyEventArgs e)
    {
        var dropdown=new[]{ThemeBox,TrailBox,ClickBox,PreviewRole,ProfileBox,FpsBox,LibraryFilter}.FirstOrDefault(box=>box.IsDropDownOpen);
        if(dropdown!=null){if(e.Key==Key.Escape){dropdown.IsDropDownOpen=false;dropdown.Focus();e.Handled=true;}return;}
        if(Navigation.SelectedIndex==0){if(e.Key==Key.Escape&&Discover.Selected!=null){Discover.Back();e.Handled=true;}else if(e.Key==Key.F6){if(Navigation.IsKeyboardFocusWithin)Discover.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));else Navigation.Focus();e.Handled=true;}return;}
        if(e.Key==Key.Enter&&Keyboard.Modifiers==ModifierKeys.Control&&ViewState.CanApply){ApplyDraft();e.Handled=true;}
        else if(e.Key==Key.Escape&&ViewState.CanCancel){CancelChanges(this,new RoutedEventArgs());e.Handled=true;}
        else if(e.Key==Key.F6){if(Navigation.IsKeyboardFocusWithin){if(Preview.IsVisible)Preview.Focus();else ContentScroll.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));}else if(ContentScroll.IsKeyboardFocusWithin)ConfigTabs.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));else Navigation.Focus();e.Handled=true;}
    }
    private void Notify(string message,bool error=false){ViewState.Notification=message;ViewState.NotificationError=error;Feedback.Text=message;Feedback.Foreground=(Brush)FindResource(error?"ErrorBrush":"SuccessBrush");}
    private void PreviewLayoutUpdated(object? sender,EventArgs e)=>UpdatePreviewBounds();
    private void PreviewScrollChanged(object sender,ScrollChangedEventArgs e)=>UpdatePreviewBounds();
    private void UpdatePreviewBounds()
    {
        if(disposed)return;
        if(Navigation.SelectedIndex==0){animatedPreview?.SetVisible(false);return;}
        if(!IsVisible||!Preview.IsLoaded||!Preview.IsVisible){engine.PreviewBounds=null;animatedPreview?.SetVisible(false);return;}
        var top=Preview.PointToScreen(new Point(0,0));var bottom=Preview.PointToScreen(new Point(Preview.ActualWidth,Preview.ActualHeight));
        var viewport=ContentScroll.PointToScreen(new Point(0,0));var viewportEnd=ContentScroll.PointToScreen(new Point(ContentScroll.ActualWidth,ContentScroll.ActualHeight));
        engine.PreviewBounds=new Native.Rect{Left=(int)Math.Max(top.X,viewport.X),Top=(int)Math.Max(top.Y,viewport.Y),Right=(int)Math.Min(bottom.X,viewportEnd.X),Bottom=(int)Math.Min(bottom.Y,viewportEnd.Y)};
        animatedPreview.SetVisible(Math.Min(bottom.Y,viewportEnd.Y)>Math.Max(top.Y,viewport.Y)&&Math.Min(bottom.X,viewportEnd.X)>Math.Max(top.X,viewport.X));
    }
    private BitmapSource Thumbnail(CursorTheme selected,string role="Normal")
    {
        var key=selected.Id+":"+selected.Version+":"+selected.PackageSha256+":"+selected.DirectoryPath+":"+role;if(thumbnails.TryGetValue(key,out var cached))return cached;
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
        BindThemeCards();var trails=AllTrails;TrailBox.ItemsSource=trails;TrailCards.ItemsSource=trails;ready=wasReady;
    }
    private void BindThemeCards()
    {
        bool previous=ready;ready=false;
        ThemeCards.ItemsSource=studio.Catalog.Themes.Where(library.Includes).Select(t=>new ThemeCardView(t,Thumbnail(t),library.Favorites.Contains(t.Id),ThemeCover(t))).ToArray();
        ThemeCards.SelectedItem=ThemeCards.Items.Cast<ThemeCardView>().FirstOrDefault(t=>t.Theme.Id==Draft.CursorThemeId);
        LibraryCount.Text=$"{ThemeCards.Items.Count} themes";UpdateCardStates();ready=previous;
    }
    private void UpdateCardStates()
    {
        if(ThemeCards==null)return;
        foreach(var card in ThemeCards.Items.Cast<ThemeCardView>())
        {
            bool selected=card.Theme.Id==Draft.CursorThemeId,applied=card.Theme.Id==studio.State.Applied.CursorThemeId;
            card.Status=selected&&applied?(IsDirty?"Selected · Applied · Modified":"Selected · Applied"):applied?"Applied":selected?"Selected · Modified":"";
            if(applied&&card.Theme.ManagedDownload&&studio.State.Applied.CursorPackageSha256!=null&&!card.Theme.PackageSha256.Equals(studio.State.Applied.CursorPackageSha256,StringComparison.OrdinalIgnoreCase))card.Status=(selected?"Selected · ":"")+"Older version applied";
        }
        if(TrailCards!=null)foreach(var p in TrailCards.Items.Cast<TrailPreset>())
        {
            var applied=studio.State.Applied.Effects;bool matches=applied.Enabled&&applied.TrailEnabled&&p.Renderer==applied.TrailEffect&&p.Length==applied.Length&&p.Duration==applied.Duration&&p.Opacity==applied.Opacity&&p.Particles==applied.Particles&&p.Color.Equals(applied.Color,StringComparison.OrdinalIgnoreCase);bool selected=ReferenceEquals(TrailCards.SelectedItem,p);
            p.StatusLabel=(selected?"Selected"+(IsDirty?" · Modified":""):"")+(matches?(selected?" · Applied":"Applied"):"");
        }
    }
    private BitmapSource ThemeCover(CursorTheme selected)
    {
        var key="cover:"+selected.Id+":"+selected.PackageSha256;if(!thumbnails.TryGetValue(key,out var cover)){cover=ThemeCoverRenderer.Cursor(selected,selected.IsSystem?role=>Thumbnail(selected,role):null);thumbnails[key]=cover;}return cover;
    }
    private void LibraryResized(object sender,SizeChangedEventArgs e)=>ResizeLibrary((ListBox)sender);
    private void ResizeLibrary(ListBox libraryBox)
    {
        if(libraryBox==null||libraryBox.ActualWidth<=0)return;
        double available=Math.Max(160,libraryBox.ActualWidth-6);int columns=Math.Clamp((int)(available/190),1,5);double width=Math.Floor(available/columns)-10;
        foreach(var item in libraryBox.Items)if(libraryBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem cell)cell.Width=width;
    }
    private void FilterChanged(object sender,SelectionChangedEventArgs e)
    {
        if(!ready||LibraryFilter.SelectedItem is not ComboBoxItem filter)return;
        var before=library.Filter;try{library.Filter=(string)filter.Tag;library.Save();BindThemeCards();}catch(Exception ex){library.Filter=before;Notify("Filter could not be saved: "+ex.Message,true);}
    }
    private void FavoriteClicked(object sender,RoutedEventArgs e)
    {
        e.Handled=true;if(((Button)sender).DataContext is not ThemeCardView card)return;
        bool existed=library.Favorites.Contains(card.Theme.Id);
        try{library=ThemeLibraryPreferences.Load();if(existed)library.Favorites.Remove(card.Theme.Id);else library.Favorites.Add(card.Theme.Id);library.Save();BindThemeCards();Discover.Render();}
        catch(Exception ex){if(existed)library.Favorites.Add(card.Theme.Id);else library.Favorites.Remove(card.Theme.Id);Notify("Favorite could not be saved: "+ex.Message,true);}
    }
    private void ShowThemeDetails(CursorTheme selected)
    {
        RemoveThemeButton.IsEnabled=!selected.IsSystem&&(selected.ManagedDownload||selected.LibraryCollection=="imported");
        ThemeDetailsTitle.Text=selected.Name+" · "+selected.Author;
        int cur=selected.SourceFiles.Count(f=>f.Path.EndsWith(".cur",StringComparison.OrdinalIgnoreCase)),ani=selected.SourceFiles.Count-cur;
        ThemeDetailsText.Text=selected.IsSystem?selected.Description:$"{selected.RoleSummary}\n{(selected.SourceFiles.Count>0?$"Source: {cur} CUR + {ani} ANI ({selected.SourceFiles.Count} files)":$"{selected.Roles.Count} role resources")} · {selected.AnimatedRoleCount} animated roles\nNative roles: {string.Join(", ",selected.Roles.Keys.Except(selected.InheritedRoles.Keys))}\nMissing from source: {(selected.InheritedRoles.Count==0?"None":string.Join(", ",selected.InheritedRoles.Keys))}\nFallback: {(selected.InheritedRoles.Count==0?"None":string.Join(", ",selected.InheritedRoles.Select(r=>r.Key+" ← "+r.Value)))}\nLicense: {(selected.License.Length>0?selected.License:"Local/built-in resources; see source terms")}\n{(selected.PublishedAt.Length>0?"Published: "+selected.PublishedAt:"")}";
        SourceButton.Content="View original source";SourceButton.Tag=selected.SourceUrl;SourceButton.ToolTip=selected.SourceUrl;SourceButton.Visibility=Uri.TryCreate(selected.SourceUrl,UriKind.Absolute,out var uri)&&uri.Scheme=="https"?Visibility.Visible:Visibility.Collapsed;
    }
    private void OpenThemeSource(object sender,RoutedEventArgs e)
    {
        if(Uri.TryCreate((string)SourceButton.Tag,UriKind.Absolute,out var uri)&&uri.Scheme=="https")try{Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});}catch(Exception ex){Notify(ex.Message,true);}
    }
    private void BindProfiles()
    {
        bool wasReady=ready;ready=false;ProfileBox.ItemsSource=studio.Profiles.Profiles.ToArray();ProfileBox.SelectedItem=studio.Profiles.Profiles.FirstOrDefault(p=>p.Id==profileId);
        if(ProfileBox.SelectedItem is StudioProfile selected)ProfileName.Text=selected.Name;
        SaveProfileButton.IsEnabled=RenameProfileButton.IsEnabled=DeleteProfileButton.IsEnabled=ProfileBox.SelectedItem!=null;ready=wasReady;UpdateDraftStatus();
    }
    private void LoadControls()
    {
        bool wasReady=ready;ready=false;var s=Draft.Effects;
        ThemeBox.SelectedItem=studio.Catalog.Themes.FirstOrDefault(t=>t.Id==Draft.CursorThemeId);ThemeCards.SelectedItem=ThemeCards.Items.Cast<ThemeCardView>().FirstOrDefault(t=>t.Theme.Id==Draft.CursorThemeId);
        TrailBox.SelectedItem=TrailBox.Items.Cast<TrailPreset>().FirstOrDefault(p=>p.Renderer==s.TrailEffect&&p.Length==s.Length&&p.Duration==s.Duration&&p.Opacity==s.Opacity&&p.Particles==s.Particles&&p.Color.Equals(s.Color,StringComparison.OrdinalIgnoreCase))??TrailBox.Items.Cast<TrailPreset>().FirstOrDefault(p=>p.Id==s.TrailEffect);TrailCards.SelectedItem=TrailBox.SelectedItem;
        RemoveTrailButton.IsEnabled=TrailBox.SelectedItem is TrailPreset p&&p.ManagedDownload;
        EnabledCheck.IsChecked=s.Enabled;TrailCheck.IsChecked=s.TrailEnabled;LengthSlider.Value=s.Length;DurationSlider.Value=s.Duration;OpacitySlider.Value=s.Opacity*100;ParticleSlider.Value=s.Particles;ColorInput.Text=s.Color;
        ClickBox.SelectedIndex=s.ClickEffect=="off"||!s.ClickHalo?0:s.ClickEffect=="ring"?2:1;ClickDurationSlider.Value=s.HaloDuration;ClickRadiusSlider.Value=s.ClickRadius;ClickOpacitySlider.Value=s.ClickOpacity*100;
        UpdateParameterAvailability();
        FullscreenCheck.IsChecked=s.PauseFullscreen;PowerCheck.IsChecked=s.PowerSaving;FpsBox.SelectedIndex=s.FrameRate>=120?1:0;StartupCheck.IsChecked=StartupRegistration.Enabled;
        RestoreButton.IsEnabled=theme.HasBackup;ready=wasReady;UpdateThemePreview();Preview.Update(s);UpdateDraftStatus();
    }
    private void UpdateThemePreview()
    {
        if(ThemeBox.SelectedItem is not CursorTheme selected){ThemeTitle.Text="Unavailable cursor theme";ThemeDescription.Text=Draft.CursorThemeId;return;}
        try{selected=studio.ResolveTheme(Draft);}catch(Exception ex)when(ex is IOException or InvalidDataException){Notify("Retained cursor version unavailable: "+ex.Message,true);return;}
        UpdatePageHeader();HeroImage.Source=Thumbnail(selected);ShowThemeDetails(selected);
        Preview.Cursor=null;RoleGrid.Children.Clear();Mouse.UpdateCursor();foreach(var cursor in ownedCursors)cursor.Dispose();ownedCursors.Clear();cursors.Clear();
        foreach(var role in CursorRoles.All)
        {
            Cursor? cursor=null;var resource=selected.IsSystem?theme.OriginalResource(role.Name):selected.Resource(role.Name);
            if(resource!=null){cursor=CursorResource.OpenPreviewCursor(resource);ownedCursors.Add(cursor);}else cursor=SystemCursor(role.Name);cursors.Add(cursor);
            var box=new StackPanel();var roleImage=new Image{Source=Thumbnail(selected,role.Name),Width=36,Height=36};RenderOptions.SetBitmapScalingMode(roleImage,BitmapScalingMode.NearestNeighbor);box.Children.Add(roleImage);box.Children.Add(new TextBlock{Text=role.Label,FontSize=11,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,6,0,3)});box.Children.Add(new TextBlock{Text=selected.InheritedRoles.TryGetValue(role.Name,out var fallback)?"Inherited · "+fallback:selected.IsSystem?"System":"Native",FontSize=10,TextAlignment=TextAlignment.Center,Foreground=(Brush)FindResource("MutedBrush")});var button=new Button{Content=box,Tag=role.Name,Margin=new Thickness(3,6,3,6),Padding=new Thickness(4,8,4,8),Cursor=cursor,ToolTip=role.Name};System.Windows.Automation.AutomationProperties.SetName(button,"Preview role "+role.Name);button.Click+=(_,_)=>PreviewRole.SelectedItem=role.Name;RoleGrid.Children.Add(button);
        }
        SetPreviewCursor();
    }
    private void SetPreviewCursor()
    {
        int role=PreviewRole.SelectedIndex;Preview.Cursor=cursors.Count>role&&role>=0?cursors[role]:null;
        for(int i=0;i<RoleGrid.Children.Count;i++)if(RoleGrid.Children[i] is Button button){button.Background=(Brush)FindResource(i==role?"SelectedBrush":"CardBrush");button.BorderBrush=(Brush)FindResource(i==role?"AccentBrush":"BorderBrush");}
        if(ThemeBox.SelectedItem is CursorTheme selected&&role>=0)
        {
            try{selected=studio.ResolveTheme(Draft);}catch(Exception ex)when(ex is IOException or InvalidDataException){Notify(ex.Message,true);return;}
            var name=CursorRoles.All[role].Name;animatedPreview.SetResource(selected.IsSystem?theme.OriginalResource(name):selected.Resource(name),Thumbnail(selected,name));UpdatePreviewBounds();
        }
    }
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
        if(DraftStatus==null)return;bool dirty=IsDirty,valid=Settings.ValidColor(ColorInput.Text);
        ViewState.CanApply=dirty&&valid&&ThemeBox.SelectedItem!=null&&!ViewState.IsBusy;ViewState.CanCancel=dirty&&!ViewState.IsBusy;ViewState.InvalidColor=!valid;
        ViewState.StateLabel=dirty?"Draft":"Applied";ViewState.DraftMessage=!valid?"Enter a valid color to apply.":dirty?"Unapplied changes · preview only":"Matches your applied combination";
        if(PreviewLatestButton!=null)PreviewLatestButton.Visibility=ThemeBox.SelectedItem is CursorTheme installed&&installed.ManagedDownload&&Draft.CursorPackageSha256!=null&&!installed.PackageSha256.Equals(Draft.CursorPackageSha256,StringComparison.OrdinalIgnoreCase)?Visibility.Visible:Visibility.Collapsed;
        ViewState.ClickName=Draft.Effects.ClickHalo?Draft.Effects.ClickEffect:"off";ViewState.CursorName=(ThemeBox.SelectedItem as CursorTheme)?.Name??"Unavailable cursor";ViewState.TrailName=$"{(TrailBox.SelectedItem as TrailPreset)?.Name??Draft.Effects.TrailEffect} · {ViewState.ClickName} clicks";
        var saved=studio.Profiles.Profiles.FirstOrDefault(p=>p.Id==profileId);ViewState.HasProfile=saved!=null;
        ViewState.ProfileCaption=saved==null?"No saved profile selected":"Saved profile · "+saved.Name+(JsonSerializer.Serialize(saved.Configuration,Storage.Json)!=JsonSerializer.Serialize(Draft,Storage.Json)?" · modified":"");
        ViewState.AppliedSummary=$"Applied: {studio.State.Applied.CursorThemeId} / {studio.State.Applied.Effects.TrailEffect}";
        CombinationLabel.Text=$"{(Draft.Effects.TrailEnabled?Draft.Effects.TrailEffect:"no trail")} · {ViewState.ClickName} clicks · {Draft.Effects.Color}";
        ColorValidation.Text=valid?"Hex color · #RRGGBB":"Enter six hex digits after #.";ColorValidation.Foreground=(Brush)FindResource(valid?"MutedBrush":"ErrorBrush");ColorInput.BorderBrush=(Brush)FindResource(valid?"BorderBrush":"ErrorBrush");
        foreach(var swatch in new[]{IceColor,PurpleColor,AmberColor})swatch.BorderBrush=(Brush)FindResource(valid&&string.Equals(ColorInput.Text,(string)swatch.Tag,StringComparison.OrdinalIgnoreCase)?"AccentBrush":"BorderBrush");
        UpdateCardStates();
    }
    private void UpdatePageHeader()
    {
        int page=Navigation.SelectedIndex;var selected=ThemeBox.SelectedItem as CursorTheme;
        ThemeTitle.Text=page==2?"My Motion Trails":page==3?"Mix Studio":page==4?"Settings":selected?.Name??"Cursor library";
        ThemeDescription.Text=page==2?"Your built-in effects and downloaded motion presets. Preview, adjust, then Apply.":page==3?"Your reusable cursor, motion and click combinations.":page==4?"Desktop behavior, appearance and local data.":selected?.Description??"Choose a local theme to preview.";
    }
    public void Refresh(){RestoreButton.IsEnabled=theme.HasBackup;((App)System.Windows.Application.Current).RefreshTray();}
    private void FindMore(object sender,RoutedEventArgs e)=>Navigation.SelectedIndex=0;
    private void TrailImageLoaded(object sender,RoutedEventArgs e)
    {
        if(sender is not System.Windows.Controls.Image image||image.DataContext is not TrailPreset p)return;
        var key="trail:"+p.Id+":"+p.PackageSha256;if(!thumbnails.TryGetValue(key,out var bitmap))
        {
            bitmap=ThemeCoverRenderer.Trail(p);thumbnails[key]=bitmap;
        }
        image.Source=bitmap;
    }
    private void RemoveSelectedTheme(object sender,RoutedEventArgs e)
    {
        if(ThemeBox.SelectedItem is not CursorTheme selected)return;
        if(MessageBox.Show(this,"Remove "+selected.Name+" from your local library? Applied themes and Profile dependencies are protected. Stable Windows cursor files remain available.","Remove from library",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        try{Discover.Downloads.RemoveCursor(selected,studio,theme);Draft.CursorThemeId=studio.State.Applied.CursorThemeId;BindCatalog();LoadControls();Discover.Render();Notify("Theme removed from your library. Windows pointers were not changed.");}catch(Exception ex){Notify(ex.Message,true);}
    }
    private void RemoveSelectedTrail(object sender,RoutedEventArgs e)
    {
        if(TrailBox.SelectedItem is not TrailPreset selected)return;
        if(MessageBox.Show(this,"Remove "+selected.Name+" from your motion library? Applied settings and saved Profile parameters stay available.","Remove motion preset",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        try{Discover.Downloads.RemoveTrail(selected,studio);BindCatalog();LoadControls();Discover.Render();Notify("Preset removed; applied parameters and Profiles are preserved.");}catch(Exception ex){Notify(ex.Message,true);}
    }
    internal void RefreshOwnedLibrary(){studio.Catalog.Reload();BindCatalog();LoadControls();}
    public void RefreshAppliedToggle(){Draft.Effects.Enabled=studio.LiveEffects.Enabled;LoadControls();}
    public void RefreshRestoredCursor(){Draft.CursorThemeId=studio.State.Applied.CursorThemeId;Draft.CursorPackageSha256=studio.State.Applied.CursorPackageSha256;profileId=null;LoadControls();BindProfiles();}
    private void Navigate(object s,SelectionChangedEventArgs e)
    {
        if(CursorPage==null)return;int index=Navigation.SelectedIndex;CursorPage.Visibility=index==1?Visibility.Visible:Visibility.Collapsed;TrailPage.Visibility=index==2?Visibility.Visible:Visibility.Collapsed;MixPage.Visibility=index==3?Visibility.Visible:Visibility.Collapsed;SettingsPage.Visibility=index==4?Visibility.Visible:Visibility.Collapsed;
        DiscoverHost.Visibility=index==0?Visibility.Visible:Visibility.Collapsed;StudioContent.Visibility=ConfigurationPanel.Visibility=index==0?Visibility.Collapsed:Visibility.Visible;
        if(index==0)engine.PreviewBounds=null;
        ThemeLibrary.Visibility=index==1?Visibility.Visible:Visibility.Collapsed;PreviewContainer.Visibility=index is 1 or 2?Visibility.Visible:Visibility.Collapsed;PageLabel.Text=new[]{"EXPLORE / DISCOVER","CURSORS / MY CURSOR THEMES","TRAILS / MY MOTION TRAILS","MIX STUDIO / SAVED COMBINATIONS","SETTINGS / DESKTOP BEHAVIOR"}[Math.Clamp(index,0,4)];ContentScroll.ScrollToTop();UiMotion.Reveal(ContentScroll);
        if(index==1)ConfigTabs.SelectedIndex=1;else if(index==2)ConfigTabs.SelectedIndex=0;
        UpdatePageHeader();
        UpdatePreviewBounds();
    }
    private void ThemeChanged(object s,SelectionChangedEventArgs e){if(!ready||ThemeBox.SelectedItem is not CursorTheme selected)return;Draft.CursorThemeId=selected.Id;Draft.CursorPackageSha256=selected.ManagedDownload?selected.PackageSha256:null;bool was=ready;ready=false;ThemeCards.SelectedItem=ThemeCards.Items.Cast<ThemeCardView>().FirstOrDefault(t=>t.Theme.Id==selected.Id);ready=was;UpdateThemePreview();Changed();}
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
        ViewState.IsBusy=true;UpdateDraftStatus();
        try
        {
            if(!Settings.ValidColor(ColorInput.Text))throw new ArgumentException("Enter a #RRGGBB color.");if(IsDirty){studio.Apply(Draft,profileId);Draft.CursorPackageSha256=studio.State.Applied.CursorPackageSha256;}Notify("Applied to desktop. Original cursors remain recoverable.");Refresh();return true;
        }
        catch(Exception ex){Notify("Apply failed: "+ex.Message,true);Storage.Log(ex.ToString());return false;}
        finally{ViewState.IsBusy=false;UpdateDraftStatus();}
    }
    private void CancelChanges(object s,RoutedEventArgs e){profileId=studio.State.ProfileId;CopyDraft(studio.State.Applied);BindProfiles();Notify("Preview reset to your applied combination.");}
    private void CopyDraft(StudioConfiguration value){Draft.CursorThemeId=value.CursorThemeId;Draft.CursorPackageSha256=value.CursorPackageSha256;Draft.Effects.CopyFrom(value.Effects);LoadControls();}
    private void PreviewLatestCursor(object sender,RoutedEventArgs e)
    {
        if(ThemeBox.SelectedItem is not CursorTheme selected||!selected.ManagedDownload)return;
        Draft.CursorPackageSha256=selected.PackageSha256;UpdateThemePreview();Changed();Notify("Previewing the latest installed cursor version. Apply Changes to activate it.");
    }
    private void ProfileSelected(object s,SelectionChangedEventArgs e){if(!ready||ProfileBox.SelectedItem is not StudioProfile selected)return;profileId=selected.Id;ProfileName.Text=selected.Name;CopyDraft(selected.Configuration);BindProfiles();Notify("Profile loaded into preview. Press Apply to activate it.");}
    private void CreateProfile(object s,RoutedEventArgs e)=>WriteProfile(null);
    private void SaveProfile(object s,RoutedEventArgs e)=>WriteProfile(profileId);
    private void WriteProfile(string? id)
    {
        try{studio.Validate(Draft);if(!Settings.ValidColor(ColorInput.Text))throw new ArgumentException("Enter a valid color before saving.");var saved=studio.Profiles.Save(id,ProfileName.Text,Draft);profileId=saved.Id;BindProfiles();Notify("Profile saved locally.");}catch(Exception ex){Notify(ex.Message,true);}
    }
    private void RenameProfile(object s,RoutedEventArgs e){try{if(profileId==null)return;studio.Profiles.Rename(profileId,ProfileName.Text);BindProfiles();Notify("Profile renamed.");}catch(Exception ex){Notify(ex.Message,true);}}
    private void DeleteProfile(object s,RoutedEventArgs e)
    {
        if(profileId==null)return;if(MessageBox.Show(this,"Delete the selected saved profile? Your applied combination is kept.","Delete profile",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        try{studio.Profiles.Delete(profileId);profileId=null;BindProfiles();Notify("Profile deleted.");}catch(Exception ex){Notify(ex.Message,true);}
    }
    private void ImportFiles(object s,RoutedEventArgs e)
    {
        var dialog=new OpenFileDialog{Filter="Windows cursors (*.cur;*.ani)|*.cur;*.ani",Multiselect=true,Title="Import cursor resources"};if(dialog.ShowDialog(this)!=true)return;
        try
        {
            var files=new Dictionary<string,string>();
            if(dialog.FileNames.Length==1)files.Add(CursorRoles.All[PreviewRole.SelectedIndex].Name,dialog.FileName);
            else foreach(var path in dialog.FileNames){var role=CursorRoleRecognition.Match(path)??throw new ArgumentException("Unrecognized role filename: "+Path.GetFileName(path)+". Use Import Cursor Pack to review mappings.");if(!files.TryAdd(role,path))throw new ArgumentException("Choose one file per role: "+role);}
            ImportResources(ImportName.Text,files);
        }
        catch(Exception ex){Notify("Import failed: "+ex.Message,true);Storage.Log(ex.ToString());}
    }
    public void ImportResources(string name,IReadOnlyDictionary<string,string> resources)
    {
        var selected=studio.Catalog.Find(Draft.CursorThemeId);if(selected.IsSystem)selected=studio.Catalog.Find("frost");
        var imported=studio.Catalog.Import(name,selected,resources);BindCatalog();Draft.CursorThemeId=imported.Id;LoadControls();Notify("Imported into your local library. Preview it and press Apply.");
    }
    private void ImportPack(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFileDialog{Filter="Cursor pack (*.zip)|*.zip",Title="Import Cursor Pack"};if(dialog.ShowDialog(this)!=true)return;ReviewPack(dialog.FileName);
    }
    private void ImportDirectory(object sender,RoutedEventArgs e)
    {
        using var dialog=new System.Windows.Forms.FolderBrowserDialog{Description="Choose a folder containing cursor resources",UseDescriptionForTitle=true};if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;ReviewPack(dialog.SelectedPath);
    }
    private void ReviewPack(string path)
    {
        try
        {
            using var pack=CursorPack.Open(path);var selected=(CursorTheme)ThemeBox.SelectedItem;
            var review=new PackImportDialog(this,pack,studio.Catalog.Themes,selected,ImportName.Text);
            if(review.ShowDialog()!=true)return;
            var imported=studio.Catalog.Import(review.ThemeName,review.Fallback,review.Replacements,pack.Files);
            library.Filter="all";LibraryFilter.SelectedIndex=0;library.Save();BindCatalog();Draft.CursorThemeId=imported.Id;LoadControls();Notify("Pack imported locally. Selection is a draft; press Apply to activate it.");
        }
        catch(Exception ex){Notify("Import failed: "+ex.Message,true);Storage.Log(ex.ToString());}
    }
    private void RestoreTheme(object s,RoutedEventArgs e)=>RestoreWithConfirmation();
    public void RestoreWithConfirmation()
    {
        if(!theme.HasBackup)return;
        if(MessageBox.Show(this,"Restore the cursor configuration saved before the first installation? This replaces the current Windows cursor scheme.","Restore original cursors",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        try{studio.RestoreOriginal();RefreshRestoredCursor();Notify("Original Windows cursor configuration restored.");}catch(Exception ex){Notify("Restore failed: "+ex.Message,true);Storage.Log(ex.ToString());}
    }
    private void StartupChanged(object s,RoutedEventArgs e)
    {
        if(!ready)return;try{StartupRegistration.Set(StartupCheck.IsChecked==true,Environment.ProcessPath!);Notify("Sign-in startup updated.");}catch(Exception ex){Notify(ex.Message,true);ready=false;StartupCheck.IsChecked=StartupRegistration.Enabled;ready=true;}
    }
    private void OpenData(object s,RoutedEventArgs e){try{Directory.CreateDirectory(Storage.Root);Process.Start(new ProcessStartInfo("explorer.exe",Storage.Root){UseShellExecute=true});}catch(Exception ex){Notify(ex.Message,true);}}
    public void DisposeResources(){if(disposed)return;disposed=true;statusTimer.Stop();ContentScroll.ScrollChanged-=PreviewScrollChanged;Discover.Dispose();animatedPreview.Dispose();SystemParameters.StaticPropertyChanged-=SystemMotionChanged;Preview.LayoutUpdated-=PreviewLayoutUpdated;engine.PreviewBounds=null;Preview.Dispose();Preview.Cursor=null;RoleGrid.Children.Clear();Mouse.UpdateCursor();foreach(var cursor in ownedCursors)cursor.Dispose();ownedCursors.Clear();cursors.Clear();thumbnails.Clear();}
}
