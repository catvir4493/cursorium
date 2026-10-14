using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Frost.Core;
using Button=System.Windows.Controls.Button;
using Image=System.Windows.Controls.Image;
using UserControl=System.Windows.Controls.UserControl;

namespace Frost.App.Presentation;

public partial class DiscoverView:UserControl,IDisposable
{
    public const int PageSize=24;
    private readonly OfficialHttp http=new();
    public RemoteCatalogService Catalog {get;}
    public RemoteCollectionsService Collections {get;}
    public string? SelectedCollectionId {get;private set;}
    public DownloadManager Downloads {get;}
    private readonly ThemeCatalog themes;
    private readonly CancellationTokenSource lifetime=new();
    private CancellationTokenSource? operation,previewOperation,mediaOperation,detailMedia;
    private readonly SemaphoreSlim images=new(4,4);
    private readonly Dictionary<string,BitmapSource> decodedCovers=new(StringComparer.Ordinal);
    private long decodedCoverBytes;
    private VerifiedResourcePack? previewPack;
    private System.Windows.Input.Cursor? cursor;
    private readonly AnimatedCursorPreview animation;
    private RemoteResource? selected;
    private string technicalSummary="";
    private int page;
    private bool ready,disposed;
    private bool compactCollections;
    private readonly HashSet<string> failed=new();
    private IReadOnlyList<RemoteResource> resources=Array.Empty<RemoteResource>();
    public event Action<RemoteResource>? OpenOwned;
    public event Action? LibraryChanged;
    public event Action? FavoritesChanged;
    public event Action<Rect?>? PreviewBoundsChanged;
    public RemoteResource? Selected=>selected;
    public bool IsDownloading=>operation!=null;
    internal AnimatedCursorPreview AnimatedPreview=>animation;
    internal bool StartedWithCachedCatalog {get;}
    public DiscoverView(ThemeCatalog themes)
    {
        this.themes=themes;InitializeComponent();animation=new(AnimatedImage);Catalog=new(http);Collections=new(http);if(Catalog.Current!=null)Collections.LoadCache(Catalog.Current);Downloads=new(http,themes);
        Sort.SelectedIndex=0;foreach(var role in CursorRoles.All)Roles.Items.Add(role.Name);Roles.SelectedIndex=0;ObservationZoom.SelectedIndex=0;
        DetailPreview.Update(new Settings{Enabled=false});ready=true;LoadCatalog();StartedWithCachedCatalog=Catalog.Current!=null;
        Loaded+=async(_,_)=>{Render();if(Catalog.Current==null)await RefreshCatalog();else _=RefreshCatalog();};
        SizeChanged+=(_,_)=>{if(IsLoaded&&ready&&compactCollections!=(ActualHeight<650||ActualWidth<900))Render();};
        IsVisibleChanged+=(_,_)=>{if(!IsVisible){ReleasePreview();mediaOperation?.Cancel();detailMedia?.Cancel();if(operation==null)Status.Text=Catalog.Status;}else{Render();if(selected!=null)LoadDetailCover(selected);}};
        DetailPreview.LayoutUpdated+=(_,_)=>UpdatePreviewBounds();DetailScroll.ScrollChanged+=(_,_)=>UpdatePreviewBounds();
    }
    private void UpdatePreviewBounds()
    {
        if(!IsVisible||!DetailPreview.IsVisible||!IsLoaded){animation.SetVisible(false);PreviewBoundsChanged?.Invoke(null);return;}
        var top=DetailPreview.PointToScreen(new Point());var bottom=DetailPreview.PointToScreen(new Point(DetailPreview.ActualWidth,DetailPreview.ActualHeight));var start=DetailScroll.PointToScreen(new Point());var end=DetailScroll.PointToScreen(new Point(DetailScroll.ActualWidth,DetailScroll.ActualHeight));
        var bounds=Rect.Intersect(new Rect(top,bottom),new Rect(start,end));bool visible=!bounds.IsEmpty&&bounds.Height>0&&bounds.Width>0;animation.SetVisible(visible);PreviewBoundsChanged?.Invoke(visible?bounds:null);
    }
    public async Task RefreshCatalog(){RefreshButton.IsEnabled=false;Status.Text="Loading the official catalog…";await Catalog.RefreshAsync(lifetime.Token);if(disposed)return;LoadCatalog();if(Catalog.Current!=null){Collections.LoadCache(Catalog.Current);await Collections.RefreshAsync(Catalog.Current,lifetime.Token);}if(disposed)return;Render();RefreshButton.IsEnabled=true;}
    private async void RefreshClicked(object sender,RoutedEventArgs e)=>await RefreshCatalog();
    private void LoadCatalog()
    {
        resources=Catalog.Current?.Resources??new();var current=Category.SelectedItem as string;ready=false;Category.ItemsSource=new[]{"All","Cursor Themes","Motion Trails","Featured","Favorites","Animated","Installed","Not Installed"}.Concat(resources.SelectMany(r=>r.Tags).Distinct().Order()).Distinct().ToArray();Category.SelectedItem=current;if(Category.SelectedIndex<0)Category.SelectedIndex=0;ready=true;Status.Text=Catalog.Status;Render();
    }
    public void SetResourcesForVerification(IReadOnlyList<RemoteResource> values){resources=values;Render();}
    public IReadOnlyList<RemoteResource> Filtered()
    {
        string category=Category.SelectedItem as string??"All",search=Search.Text.Trim();HashSet<string> favorites=ThemeLibraryPreferences.Load().Favorites;
        var collections=Collections.Current?.Collections??new();var collection=collections.FirstOrDefault(c=>c.Id==SelectedCollectionId);
        var installed=themes.Themes.Select(t=>t.Id).Concat(Downloads.LoadTrails().Select(t=>t.Id)).ToHashSet(StringComparer.Ordinal);
        var query=resources.Where(r=>collection==null||collection.ResourceIds.Contains(r.Id,StringComparer.Ordinal)).Where(r=>search.Length==0||(r.Name+" "+r.Author+" "+r.Description+" "+string.Join(' ',r.Tags)+" "+string.Join(' ',collections.Where(c=>c.ResourceIds.Contains(r.Id,StringComparer.Ordinal)).Select(c=>c.Name))).Contains(search,StringComparison.OrdinalIgnoreCase)).Where(r=>category switch{"All"=>true,"Cursor Themes"=>r.Type=="cursor","Motion Trails"=>r.Type=="trail","Featured"=>r.Featured,"Favorites"=>favorites.Contains(r.Id),"Animated"=>r.AnimatedCursorCount>0,"Installed"=>installed.Contains(r.Id),"Not Installed"=>!installed.Contains(r.Id),_=>r.Tags.Contains(category,StringComparer.OrdinalIgnoreCase)});
        return (Sort.SelectedIndex switch{1=>query.OrderByDescending(r=>r.AddedAt),2=>query.OrderBy(r=>r.Name,StringComparer.OrdinalIgnoreCase),_=>query.OrderByDescending(r=>r.Featured).ThenBy(r=>r.Name)}).ToArray();
    }
    public void SelectCollection(string? id)
    {
        if(id!=null&&Collections.Current?.Collections.All(c=>c.Id!=id)!=false)throw new ArgumentException("Unknown collection.");
        SelectedCollectionId=id;page=0;Render();BrowseScroll.ScrollToTop();
    }
    private void ClearCollectionClicked(object sender,RoutedEventArgs e)=>SelectCollection(null);
    private void RenderCollections()
    {
        compactCollections=ActualHeight<650||ActualWidth<900;
        CollectionCards.Children.Clear();var collections=Collections.Current?.Collections;
        CollectionsHost.Visibility=collections is {Count:>0}?Visibility.Visible:Visibility.Collapsed;
        ClearCollection.Visibility=SelectedCollectionId==null?Visibility.Collapsed:Visibility.Visible;
        CollectionHeading.Text=collections?.FirstOrDefault(c=>c.Id==SelectedCollectionId)?.Name??"Featured collections";
        if(collections==null)return;
        foreach(var collection in collections)
        {
            var content=new StackPanel();var image=new Image{Width=156,Height=78,Stretch=Stretch.UniformToFill};if(!compactCollections)content.Children.Add(image);
            content.Children.Add(new TextBlock{Text=collection.Name,Margin=new Thickness(8),FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis});
            var button=new Button{Content=content,Width=158,Padding=new Thickness(),Margin=new Thickness(0,0,10,8),Style=(Style)FindResource("ResourceCardButton"),ToolTip=collection.Description,Tag=collection.Id};
            System.Windows.Automation.AutomationProperties.SetName(button,"Collection "+collection.Name);button.Click+=(_,_)=>SelectCollection(collection.Id);CollectionCards.Children.Add(button);
            if(!compactCollections&&IsVisible&&Browse.IsVisible)_=LoadCover(image,collection.CoverUrl,mediaOperation!.Token);
        }
    }
    private void FilterChanged(object sender,RoutedEventArgs e){if(!ready)return;page=0;Render();BrowseScroll.ScrollToTop();}
    public void Render()
    {
        // Cached catalogs are available during construction, before the host's
        // shared design resources can be resolved. Render after joining the tree.
        if(!ready||disposed||!IsLoaded)return;mediaOperation?.Cancel();mediaOperation?.Dispose();mediaOperation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        try
        {
            RenderCollections();var filtered=Filtered();int pages=Math.Max(1,(filtered.Count+PageSize-1)/PageSize);page=Math.Min(page,pages-1);Cards.Children.Clear();
            EmptyMessage.Text=resources.Count==0?(Catalog.Current==null?Catalog.Status:"The online catalog is empty."):filtered.Count==0?"No resources match your search.":"";EmptyMessage.Visibility=filtered.Count==0?Visibility.Visible:Visibility.Collapsed;
            foreach(var item in filtered.Skip(page*PageSize).Take(PageSize))
            {
                var image=new Image{Height=(CardWidth()-2)*.75,Stretch=Stretch.UniformToFill};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.NearestNeighbor);
                var name=new TextBlock{Text=item.Name,FontSize=14,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=item.Name};
                var content=new StackPanel();content.Children.Add(new Border{Child=image,ClipToBounds=true,CornerRadius=new CornerRadius(6,6,0,0),Background=(Brush)FindResource("PreviewBrush")});
                var metadata=new StackPanel{Margin=new Thickness(12,10,12,12)};metadata.Children.Add(name);metadata.Children.Add(Caption(item.Author));metadata.Children.Add(Caption(item.Type=="cursor"?$"Cursor · {(item.AnimatedCursorCount>0?"Animated":"Static")} · {item.NativeRoles.Count}/15 native":"Motion · trusted renderer"));metadata.Children.Add(new TextBlock{Text=Downloads.State(item),Foreground=(Brush)FindResource("AccentBrush"),FontSize=11,Margin=new Thickness(0,5,0,0)});content.Children.Add(metadata);
                var open=new Button{Content=content,Style=(Style)FindResource("ResourceCardButton")};System.Windows.Automation.AutomationProperties.SetName(open,"View "+item.Name);open.Click+=(_,_)=>ShowDetails(item);
                var cell=new Grid{Margin=new Thickness(0,0,12,12),Width=CardWidth()};cell.Children.Add(open);
                bool favorite=ThemeLibraryPreferences.Load().Favorites.Contains(item.Id);var star=new Button{Content=favorite?"★":"☆",Width=30,Height=30,MinHeight=30,Padding=new Thickness(),Background=(Brush)FindResource("SurfaceBrush"),Foreground=(Brush)FindResource(favorite?"AccentBrush":"TextBrush"),VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,8,8,0),ToolTip=favorite?"Remove favorite":"Favorite"};System.Windows.Automation.AutomationProperties.SetName(star,"Favorite "+item.Name);star.Click+=(_,e)=>{e.Handled=true;ToggleFavorite(item.Id);};cell.Children.Add(star);Cards.Children.Add(cell);if(IsVisible&&Browse.IsVisible)_=LoadCover(image,item.CoverUrl,mediaOperation.Token);
            }
            PageCount.Text=$"{filtered.Count} resources · {page+1}/{pages}";PreviousPage.IsEnabled=page>0;NextPage.IsEnabled=page+1<pages;if(selected!=null)UpdateDetailsState();
        }
        catch(Exception ex){Status.Text="Library could not be read: "+ex.Message;}
    }
    private TextBlock Caption(string text)=>new(){Text=text,Style=(Style)FindResource("Caption"),TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,3,0,0)};
    private double CardWidth(){double available=Math.Max(180,(Cards.ActualWidth>0?Cards.ActualWidth:BrowseScroll.ActualWidth)-2);int columns=Math.Clamp((int)(available/245),1,5);return Math.Max(170,available/columns-12);}
    private void CardsResized(object sender,SizeChangedEventArgs e){foreach(Grid cell in Cards.Children){cell.Width=CardWidth();if(cell.Children[0] is Button button&&button.Content is StackPanel content&&content.Children[0] is Border cover&&cover.Child is Image image)image.Height=(cell.Width-2)*.75;}}
    private void NextClicked(object sender,RoutedEventArgs e){page++;Render();BrowseScroll.ScrollToTop();}
    private void PreviousClicked(object sender,RoutedEventArgs e){page--;Render();BrowseScroll.ScrollToTop();}
    public void ShowDetails(RemoteResource item)
    {
        ReleasePreview();selected=item;Browse.Visibility=Visibility.Collapsed;Details.Visibility=Visibility.Visible;DetailName.Text=item.Name;DetailAuthor.Text=$"by {item.Author} · {item.Type} · v{item.Version}";Description.Text=item.Description;Cover.Source=null;
        if(operation==null)Status.Text=Catalog.Status;TechnicalDetails.IsExpanded=false;DetailTags.Text=(item.Type=="cursor"?"Cursor Theme":"Motion Trail")+" · "+string.Join(" · ",item.Tags)+(item.AnimatedCursorCount>0?" · Animated":"");
        Overview.Text=(item.Type=="cursor"?$"{item.NativeRoles.Count}/15 native · {item.SupportedRoles.Count-item.NativeRoles.Count} inherited\n{item.AnimatedCursorCount} animated roles\n":"Data-only preset · trusted renderer\n")+$"License  {item.License}\nPackage  {item.PackageSize/1024d:N1} KiB";
        Information.Text=$"Author: {item.Author}\nVersion: {item.Version}\nCategories: {string.Join(", ",item.Tags)}\nLicense: {item.License}\nPackage size: {item.PackageSize:N0} bytes\nSHA256: {item.SHA256}\n"+(item.Type=="cursor"?$"Coverage: {item.SupportedRoles.Count}/15 · {item.NativeRoles.Count} native\nAnimated roles: {item.AnimatedCursorCount}\nFallback roles: {string.Join(", ",item.SupportedRoles.Except(item.NativeRoles))}\n":"Renderer: existing CURSORIUM engine; data-only preset\n")+(item.PublishedAt!=null?$"Published (source): {item.PublishedAt}\n":"")+(item.LastUpdated!=null?$"Last updated (source): {item.LastUpdated}\n":"")+"Source: "+item.SourceUrl;
        Information.Text+=$"\nRequired engine: {item.RequiredEngine}\nMinimum client: {item.MinimumClientVersion}";technicalSummary=Information.Text;
        var owned=themes.Themes.FirstOrDefault(t=>t.Id==item.Id);FillRoleGallery(owned);if(owned!=null)ShowFileInformation(owned);UpdateDetailsState();DetailScroll.ScrollToTop();LoadDetailCover(item);UiMotion.Reveal(Details);
    }
    public void Back(){ReleasePreview();detailMedia?.Cancel();selected=null;Browse.Visibility=Visibility.Visible;Details.Visibility=Visibility.Collapsed;if(operation==null)Status.Text=Catalog.Status;Render();}
    private void LoadDetailCover(RemoteResource item){detailMedia?.Cancel();detailMedia?.Dispose();detailMedia=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);_=LoadCover(Cover,item.CoverUrl,detailMedia.Token);}
    private void DetailsResized(object sender,SizeChangedEventArgs e){bool compact=Details.ActualWidth<740;HeroCoverColumn.Width=new GridLength(compact?1:3,GridUnitType.Star);Cover.Height=compact?220:300;DetailName.FontSize=compact?21:25;HeroRoles.Rows=compact?2:1;HeroRoles.Columns=compact?3:6;}
    private void FillRoleGallery(CursorTheme? theme)
    {
        HeroRoles.Children.Clear();if(theme==null)return;
        foreach(var role in new[]{"Normal","Help","Working","Busy","Text","Link"})
        {
            var image=new Image{Source=ThemeCoverRenderer.Frame(theme.Resource(role)),Width=32,Height=32};RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.NearestNeighbor);
            var button=new Button{Content=image,ToolTip=role+(theme.InheritedRoles.ContainsKey(role)?" · inherited":" · native"),Margin=new Thickness(0,0,6,0),Padding=new Thickness(5),MinHeight=44};System.Windows.Automation.AutomationProperties.SetName(button,"Preview "+role);button.Click+=(_,_)=>Roles.SelectedItem=role;HeroRoles.Children.Add(button);
        }
    }
    private void ShowFileInformation(CursorTheme theme)
    {
        Information.Text=technicalSummary+"\n\nVerified file mapping:\n"+string.Join("\n",theme.Roles.Select(r=>r.Key+" → "+r.Value+(theme.InheritedRoles.TryGetValue(r.Key,out var inherited)?" (inherited: "+inherited+")":" (native)")))+"\n\nOriginal source files:\n"+string.Join("\n",theme.SourceFiles.Select(f=>f.OriginalName+" · "+f.Path+"\nSHA256: "+f.Sha256));
    }
    private void BackClicked(object sender,RoutedEventArgs e)=>Back();
    private void UpdateDetailsState()
    {
        if(selected==null)return;string state=Downloads.State(selected);DetailStatus.Text=state+(state is "Installed" or "Bundled owned"?" · in your library":" · Download adds to library");Download.Content=state is "Installed" or "Bundled owned"?(selected.Type=="cursor"?"Open in Cursors":"Open in Trails"):state=="Update available"?"Download update":failed.Contains(selected.Id)?"Retry":"Download";Download.IsEnabled=operation==null&&previewOperation==null;DetailFavorite.Content=ThemeLibraryPreferences.Load().Favorites.Contains(selected.Id)?"★":"☆";
    }
    private void ToggleFavorite(string id)
    {
        try{var prefs=ThemeLibraryPreferences.Load();if(!prefs.Favorites.Remove(id))prefs.Favorites.Add(id);prefs.Save();FavoritesChanged?.Invoke();Render();}catch(Exception ex){Status.Text="Favorite could not be saved: "+ex.Message;}
    }
    private void DetailFavoriteClicked(object sender,RoutedEventArgs e){if(selected!=null)ToggleFavorite(selected.Id);}
    private IProgress<DownloadProgress> ProgressReporter(RemoteResource item)=>new Progress<DownloadProgress>(value=>{if(disposed||(operation==null&&(previewOperation==null||previewOperation.IsCancellationRequested||selected?.Id!=item.Id||!IsVisible)))return;Status.Text=$"{item.Name} · {value.Phase} · {value.Bytes:N0} / {value.Total:N0} bytes";Progress.IsIndeterminate=false;Progress.Value=value.Percent;if(selected?.Id==item.Id){DetailStatus.Text=value.Phase;if(operation!=null)Download.Content=value.Phase;}});
    private async void DownloadClicked(object sender,RoutedEventArgs e)=>await DownloadSelected();
    public async Task DownloadSelected()
    {
        if(selected==null||operation!=null)return;var item=selected;var state=Downloads.State(item);
        if(state is "Installed" or "Bundled owned"){OpenOwned?.Invoke(item);return;}
        operation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);Download.IsEnabled=false;Download.Content="Downloading…";TryPreview.IsEnabled=false;Status.Text=item.Name+" · Downloading…";DetailStatus.Text="Downloading";Progress.IsIndeterminate=true;Progress.Value=0;CancelDownload.Visibility=Visibility.Visible;Progress.Visibility=Visibility.Visible;
        try{await Downloads.InstallAsync(item,ProgressReporter(item),operation.Token);failed.Remove(item.Id);LibraryChanged?.Invoke();Status.Text=item.Name+(state=="Update available"?" updated in your library. Existing Windows pointers remain unchanged.":" installed in your library. Preview and Apply there when ready.");}
        catch(OperationCanceledException){Status.Text="Download cancelled. Nothing was applied to Windows.";}
        catch(Exception ex){failed.Add(item.Id);Status.Text="Download failed: "+ex.Message+" · Retry is available.";}
        finally{operation.Dispose();operation=null;TryPreview.IsEnabled=true;CancelDownload.Visibility=Visibility.Collapsed;Progress.Visibility=Visibility.Collapsed;if(!disposed)Render();}
    }
    private void CancelClicked(object sender,RoutedEventArgs e){operation?.Cancel();previewOperation?.Cancel();}
    private async void TryClicked(object sender,RoutedEventArgs e)=>await TryLivePreview();
    public async Task TryLivePreview()
    {
        if(selected==null||previewOperation!=null||operation!=null)return;var item=selected;TryPreview.IsEnabled=false;previewOperation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);UpdateDetailsState();CancelDownload.Visibility=Visibility.Visible;Progress.Visibility=Visibility.Visible;
        try
        {
            var pack=await Downloads.FetchPreviewAsync(item,ProgressReporter(item),previewOperation.Token);if(disposed||selected?.Id!=item.Id||!IsVisible){pack.Dispose();return;}previewPack=pack;PreviewHost.Visibility=Visibility.Visible;Roles.Visibility=item.Type=="cursor"?Visibility.Visible:Visibility.Collapsed;
            ObservationZoom.Visibility=pack.Trail==null?Visibility.Visible:Visibility.Collapsed;
            if(pack.Trail!=null){var settings=new Settings();pack.Trail.Apply(settings);DetailPreview.Update(settings);}else{DetailPreview.Update(new Settings{Enabled=false});FillRoleGallery(pack.Cursor);ShowFileInformation(pack.Cursor!);ChangeRole();}Status.Text="Verified temporary preview · not installed or applied.";UpdatePreviewBounds();
        }
        catch(OperationCanceledException){if(selected?.Id==item.Id&&IsVisible)Status.Text="Preview cancelled.";}catch(Exception ex){if(selected?.Id==item.Id&&IsVisible)Status.Text="Preview unavailable: "+ex.Message;}
        finally{previewOperation?.Dispose();previewOperation=null;TryPreview.IsEnabled=true;if(operation==null){CancelDownload.Visibility=Visibility.Collapsed;Progress.Visibility=Visibility.Collapsed;}if(!disposed)UpdateDetailsState();}
    }
    private void RoleChanged(object sender,SelectionChangedEventArgs e){if(ready)ChangeRole();}
    private void ChangeRole()
    {
        if(previewPack?.Cursor is not CursorTheme theme||Roles.SelectedItem is not string role)return;DetailPreview.Cursor=null;Mouse.UpdateCursor();cursor?.Dispose();cursor=new(theme.Resource(role));DetailPreview.Cursor=cursor;var handle=Native.LoadOwnedCursor(theme.Resource(role));if(handle==IntPtr.Zero)throw new IOException("Cannot load preview cursor.");
        try{var image=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();animation.SetResource(theme.Resource(role),image);SetObservationSize();}finally{Native.DestroyCursor(handle);}UpdatePreviewBounds();
    }
    private void ZoomChanged(object sender,SelectionChangedEventArgs e){if(ready)SetObservationSize();}
    private void SetObservationSize(){if(AnimatedImage.Source is not BitmapSource source)return;int scale=ObservationZoom.SelectedIndex switch{1=>2,2=>4,_=>1};AnimatedImage.Width=source.PixelWidth*scale;AnimatedImage.Height=source.PixelHeight*scale;}
    private void ReleasePreview(){previewOperation?.Cancel();animation.Clear();DetailPreview.Cursor=null;Mouse.UpdateCursor();cursor?.Dispose();cursor=null;previewPack?.Dispose();previewPack=null;PreviewHost.Visibility=Visibility.Collapsed;TryPreview.IsEnabled=true;PreviewBoundsChanged?.Invoke(null);}
    private async Task LoadCover(Image target,string url,CancellationToken cancellation)
    {
        bool entered=false;
        try
        {
            await images.WaitAsync(cancellation);entered=true;if(decodedCovers.TryGetValue(url,out var cachedCover)){target.Source=cachedCover;UiMotion.Reveal(target);return;}var folder=Path.Combine(Storage.Root,"Online","Media");DownloadManager.GuardRoot(folder);Directory.CreateDirectory(folder);var file=Path.Combine(folder,Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url)))+".png");byte[] bytes;
            if(File.Exists(file)&&new FileInfo(file).Length<=1024*1024)bytes=await File.ReadAllBytesAsync(file,cancellation);else{bytes=await http.BytesAsync(url,1024*1024,cancellation);cancellation.ThrowIfCancellationRequested();await File.WriteAllBytesAsync(file,bytes,cancellation);}
            if(bytes.Length<24||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4))>2048||System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4))>2048)throw new InvalidDataException("Unsupported cover image size or type.");
            cancellation.ThrowIfCancellationRequested();using var input=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=(int)Math.Min(720,System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)));bitmap.StreamSource=input;bitmap.EndInit();bitmap.Freeze();if(!disposed){long size=(long)bitmap.PixelWidth*bitmap.PixelHeight*4;if(decodedCovers.Remove(url,out var previous))decodedCoverBytes-=(long)previous.PixelWidth*previous.PixelHeight*4;while(decodedCovers.Count>0&&(decodedCovers.Count>=24||decodedCoverBytes+size>40*1024*1024)){string key=decodedCovers.Keys.First();var old=decodedCovers[key];decodedCoverBytes-=(long)old.PixelWidth*old.PixelHeight*4;decodedCovers.Remove(key);}decodedCovers[url]=bitmap;decodedCoverBytes+=size;target.Source=bitmap;UiMotion.Reveal(target);}
            var cached=new DirectoryInfo(folder).GetFiles("*.png").OrderByDescending(f=>f.LastWriteTimeUtc).ToArray();long retained=0;foreach(var old in cached){retained+=old.Length;if(retained>24*1024*1024)try{old.Delete();}catch(IOException){}}
        }
        catch(Exception ex)when(ex is OperationCanceledException or IOException or System.Net.Http.HttpRequestException or InvalidDataException or NotSupportedException or ArgumentException){if(ex is not OperationCanceledException)Storage.Log("Cover: "+ex.Message);}
        finally{if(entered)images.Release();}
    }
    private void SourceClicked(object sender,RoutedEventArgs e){if(selected!=null)try{Process.Start(new ProcessStartInfo(selected.SourceUrl){UseShellExecute=true});}catch(Exception ex){Status.Text=ex.Message;}}
    public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();operation?.Cancel();mediaOperation?.Cancel();detailMedia?.Cancel();ReleasePreview();animation.Dispose();DetailPreview.Dispose();decodedCovers.Clear();http.Dispose();}
}
