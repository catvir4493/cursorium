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
    public DownloadManager Downloads {get;}
    private readonly ThemeCatalog themes;
    private readonly CancellationTokenSource lifetime=new();
    private CancellationTokenSource? operation,previewOperation,mediaOperation;
    private readonly SemaphoreSlim images=new(4,4);
    private VerifiedResourcePack? previewPack;
    private System.Windows.Input.Cursor? cursor;
    private readonly AnimatedCursorPreview animation;
    private RemoteResource? selected;
    private int page;
    private bool ready,disposed;
    private readonly HashSet<string> failed=new();
    private IReadOnlyList<RemoteResource> resources=Array.Empty<RemoteResource>();
    public event Action<RemoteResource>? OpenOwned;
    public event Action? LibraryChanged;
    public event Action? FavoritesChanged;
    public event Action<Rect?>? PreviewBoundsChanged;
    public RemoteResource? Selected=>selected;
    public bool IsDownloading=>operation!=null;
    public DiscoverView(ThemeCatalog themes)
    {
        this.themes=themes;InitializeComponent();animation=new(AnimatedImage);Catalog=new(http);Downloads=new(http,themes);
        Sort.SelectedIndex=0;foreach(var role in CursorRoles.All)Roles.Items.Add(role.Name);Roles.SelectedIndex=0;
        DetailPreview.Update(new Settings{Enabled=false});ready=true;LoadCatalog();
        Loaded+=async(_,_)=>{if(Catalog.Current==null)await RefreshCatalog();else _=RefreshCatalog();};
        IsVisibleChanged+=(_,_)=>{if(!IsVisible){ReleasePreview();mediaOperation?.Cancel();}else{Render();if(selected!=null)_=LoadCover(Cover,selected.CoverUrl,lifetime.Token);}};
        DetailPreview.LayoutUpdated+=(_,_)=>UpdatePreviewBounds();DetailScroll.ScrollChanged+=(_,_)=>UpdatePreviewBounds();
    }
    private void UpdatePreviewBounds()
    {
        if(!IsVisible||!DetailPreview.IsVisible||!IsLoaded){animation.SetVisible(false);PreviewBoundsChanged?.Invoke(null);return;}
        var top=DetailPreview.PointToScreen(new Point());var bottom=DetailPreview.PointToScreen(new Point(DetailPreview.ActualWidth,DetailPreview.ActualHeight));var start=DetailScroll.PointToScreen(new Point());var end=DetailScroll.PointToScreen(new Point(DetailScroll.ActualWidth,DetailScroll.ActualHeight));
        var bounds=Rect.Intersect(new Rect(top,bottom),new Rect(start,end));bool visible=!bounds.IsEmpty&&bounds.Height>0&&bounds.Width>0;animation.SetVisible(visible);PreviewBoundsChanged?.Invoke(visible?bounds:null);
    }
    public async Task RefreshCatalog(){RefreshButton.IsEnabled=false;Status.Text="Loading the official catalog…";await Catalog.RefreshAsync(lifetime.Token);if(disposed)return;LoadCatalog();RefreshButton.IsEnabled=true;}
    private async void RefreshClicked(object sender,RoutedEventArgs e)=>await RefreshCatalog();
    private void LoadCatalog()
    {
        resources=Catalog.Current?.Resources??new();var current=Category.SelectedItem as string;ready=false;Category.ItemsSource=new[]{"All","Cursor Themes","Motion Trails","Featured","Favorites"}.Concat(resources.SelectMany(r=>r.Tags).Distinct().Order()).ToArray();Category.SelectedItem=current;if(Category.SelectedIndex<0)Category.SelectedIndex=0;ready=true;Status.Text=Catalog.Status;Render();
    }
    public void SetResourcesForVerification(IReadOnlyList<RemoteResource> values){resources=values;Render();}
    public IReadOnlyList<RemoteResource> Filtered()
    {
        string category=Category.SelectedItem as string??"All",search=Search.Text.Trim();HashSet<string> favorites=ThemeLibraryPreferences.Load().Favorites;
        var query=resources.Where(r=>search.Length==0||(r.Name+" "+r.Author+" "+r.Description+" "+string.Join(' ',r.Tags)).Contains(search,StringComparison.OrdinalIgnoreCase)).Where(r=>category switch{"All"=>true,"Cursor Themes"=>r.Type=="cursor","Motion Trails"=>r.Type=="trail","Featured"=>r.Featured,"Favorites"=>favorites.Contains(r.Id),_=>r.Tags.Contains(category,StringComparer.OrdinalIgnoreCase)});
        return (Sort.SelectedIndex switch{1=>query.OrderByDescending(r=>r.AddedAt),2=>query.OrderBy(r=>r.Name,StringComparer.OrdinalIgnoreCase),_=>query.OrderByDescending(r=>r.Featured).ThenBy(r=>r.Name)}).ToArray();
    }
    private void FilterChanged(object sender,RoutedEventArgs e){if(!ready)return;page=0;Render();BrowseScroll.ScrollToTop();}
    public void Render()
    {
        if(!ready||disposed)return;mediaOperation?.Cancel();mediaOperation?.Dispose();mediaOperation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        try
        {
            var filtered=Filtered();int pages=Math.Max(1,(filtered.Count+PageSize-1)/PageSize);page=Math.Min(page,pages-1);Cards.Children.Clear();
            EmptyMessage.Text=resources.Count==0?(Catalog.Current==null?Catalog.Status:"The online catalog is empty."):filtered.Count==0?"No resources match your search.":"";EmptyMessage.Visibility=filtered.Count==0?Visibility.Visible:Visibility.Collapsed;
            foreach(var item in filtered.Skip(page*PageSize).Take(PageSize))
            {
                var image=new Image{Height=120,Stretch=Stretch.Uniform};var name=new TextBlock{Text=item.Name,FontSize=14,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,10,0,4),ToolTip=item.Name};
                var content=new StackPanel();content.Children.Add(image);content.Children.Add(name);content.Children.Add(Caption(item.Author));content.Children.Add(Caption(item.Type=="cursor"?$"Cursor · {item.NativeRoles.Count}/15 native · {(item.AnimatedCursorCount>0?"Animated":"Static")}":"Motion preset · trusted renderer"));content.Children.Add(new TextBlock{Text=Downloads.State(item),Foreground=(Brush)FindResource("AccentBrush"),FontSize=11,Margin=new Thickness(0,8,0,0)});
                var open=new Button{Content=content,Padding=new Thickness(12),HorizontalContentAlignment=HorizontalAlignment.Stretch};System.Windows.Automation.AutomationProperties.SetName(open,"View "+item.Name);open.Click+=(_,_)=>ShowDetails(item);
                var cell=new Grid{Margin=new Thickness(0,0,12,12),Width=CardWidth()};cell.Children.Add(open);
                bool favorite=ThemeLibraryPreferences.Load().Favorites.Contains(item.Id);var star=new Button{Content=favorite?"★":"☆",Width=30,Height=30,Style=(Style)FindResource("IconButton"),VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,8,8,0),ToolTip=favorite?"Remove favorite":"Favorite"};System.Windows.Automation.AutomationProperties.SetName(star,"Favorite "+item.Name);star.Click+=(_,e)=>{e.Handled=true;ToggleFavorite(item.Id);};cell.Children.Add(star);Cards.Children.Add(cell);if(IsVisible)_=LoadCover(image,item.CoverUrl,mediaOperation.Token);
            }
            PageCount.Text=$"{filtered.Count} resources · {page+1}/{pages}";PreviousPage.IsEnabled=page>0;NextPage.IsEnabled=page+1<pages;if(selected!=null)UpdateDetailsState();
        }
        catch(Exception ex){Status.Text="Library could not be read: "+ex.Message;}
    }
    private TextBlock Caption(string text)=>new(){Text=text,Style=(Style)FindResource("Caption"),TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,3,0,0)};
    private double CardWidth(){double available=Math.Max(180,BrowseScroll.ActualWidth-18);int columns=Math.Max(1,(int)(available/212));return Math.Max(170,available/columns-12);}
    private void CardsResized(object sender,SizeChangedEventArgs e){foreach(FrameworkElement cell in Cards.Children)cell.Width=CardWidth();}
    private void NextClicked(object sender,RoutedEventArgs e){page++;Render();BrowseScroll.ScrollToTop();}
    private void PreviousClicked(object sender,RoutedEventArgs e){page--;Render();BrowseScroll.ScrollToTop();}
    public void ShowDetails(RemoteResource item)
    {
        ReleasePreview();selected=item;Browse.Visibility=Visibility.Collapsed;Details.Visibility=Visibility.Visible;DetailName.Text=item.Name;DetailAuthor.Text=$"by {item.Author} · {item.Type} · v{item.Version}";Description.Text=item.Description;Cover.Source=null;
        Information.Text=$"Author: {item.Author}\nVersion: {item.Version}\nCategories: {string.Join(", ",item.Tags)}\nLicense: {item.License}\nPackage size: {item.PackageSize:N0} bytes\nSHA256: {item.SHA256}\n"+(item.Type=="cursor"?$"Coverage: {item.SupportedRoles.Count}/15 · {item.NativeRoles.Count} native\nAnimated roles: {item.AnimatedCursorCount}\nFallback roles: {string.Join(", ",item.SupportedRoles.Except(item.NativeRoles))}\n":"Renderer: existing CURSORIUM engine; data-only preset\n")+(item.PublishedAt!=null?$"Published (source): {item.PublishedAt}\n":"")+(item.LastUpdated!=null?$"Last updated (source): {item.LastUpdated}\n":"")+"Source: "+item.SourceUrl;
        UpdateDetailsState();DetailScroll.ScrollToTop();_=LoadCover(Cover,item.CoverUrl,lifetime.Token);
    }
    public void Back(){ReleasePreview();selected=null;Browse.Visibility=Visibility.Visible;Details.Visibility=Visibility.Collapsed;Render();}
    private void BackClicked(object sender,RoutedEventArgs e)=>Back();
    private void UpdateDetailsState()
    {
        if(selected==null)return;string state=Downloads.State(selected);DetailStatus.Text=state+(state is "Installed" or "Bundled owned"?" · available in your local library":" · download does not apply to Windows");Download.Content=state is "Installed" or "Bundled owned"?(selected.Type=="cursor"?"Open in Cursors":"Open in Trails"):state=="Update available"?"Download update":failed.Contains(selected.Id)?"Retry":"Download";Download.IsEnabled=operation==null&&previewOperation==null;DetailFavorite.Content=ThemeLibraryPreferences.Load().Favorites.Contains(selected.Id)?"★ Favorited":"☆ Favorite";
    }
    private void ToggleFavorite(string id)
    {
        try{var prefs=ThemeLibraryPreferences.Load();if(!prefs.Favorites.Remove(id))prefs.Favorites.Add(id);prefs.Save();FavoritesChanged?.Invoke();Render();}catch(Exception ex){Status.Text="Favorite could not be saved: "+ex.Message;}
    }
    private void DetailFavoriteClicked(object sender,RoutedEventArgs e){if(selected!=null)ToggleFavorite(selected.Id);}
    private IProgress<DownloadProgress> ProgressReporter(string name)=>new Progress<DownloadProgress>(value=>{if(disposed)return;Status.Text=$"{name} · {value.Phase} · {value.Bytes:N0} / {value.Total:N0} bytes";Progress.IsIndeterminate=false;Progress.Value=value.Percent;if(selected?.Name==name)DetailStatus.Text=value.Phase;});
    private async void DownloadClicked(object sender,RoutedEventArgs e)=>await DownloadSelected();
    public async Task DownloadSelected()
    {
        if(selected==null||operation!=null)return;var item=selected;var state=Downloads.State(item);
        if(state is "Installed" or "Bundled owned"){OpenOwned?.Invoke(item);return;}
        operation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);Download.IsEnabled=false;TryPreview.IsEnabled=false;Status.Text=item.Name+" · Downloading…";DetailStatus.Text="Downloading";Progress.IsIndeterminate=true;Progress.Value=0;CancelDownload.Visibility=Visibility.Visible;Progress.Visibility=Visibility.Visible;
        try{await Downloads.InstallAsync(item,ProgressReporter(item.Name),operation.Token);failed.Remove(item.Id);LibraryChanged?.Invoke();Status.Text=item.Name+" installed in your library. Preview and Apply there when ready.";}
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
            var pack=await Downloads.FetchPreviewAsync(item,ProgressReporter(item.Name),previewOperation.Token);if(disposed||selected?.Id!=item.Id||!IsVisible){pack.Dispose();return;}previewPack=pack;PreviewHost.Visibility=Visibility.Visible;Roles.Visibility=item.Type=="cursor"?Visibility.Visible:Visibility.Collapsed;
            if(pack.Trail!=null){var settings=new Settings();pack.Trail.Apply(settings);DetailPreview.Update(settings);}else{DetailPreview.Update(new Settings{Enabled=false});ChangeRole();}Status.Text="Verified temporary preview · not installed or applied.";UpdatePreviewBounds();
        }
        catch(OperationCanceledException){Status.Text="Preview cancelled.";}catch(Exception ex){Status.Text="Preview unavailable: "+ex.Message;}
        finally{previewOperation?.Dispose();previewOperation=null;TryPreview.IsEnabled=true;if(operation==null){CancelDownload.Visibility=Visibility.Collapsed;Progress.Visibility=Visibility.Collapsed;}if(!disposed)UpdateDetailsState();}
    }
    private void RoleChanged(object sender,SelectionChangedEventArgs e){if(ready)ChangeRole();}
    private void ChangeRole()
    {
        if(previewPack?.Cursor is not CursorTheme theme||Roles.SelectedItem is not string role)return;DetailPreview.Cursor=null;Mouse.UpdateCursor();cursor?.Dispose();cursor=new(theme.Resource(role));DetailPreview.Cursor=cursor;var handle=Native.LoadOwnedCursor(theme.Resource(role));if(handle==IntPtr.Zero)throw new IOException("Cannot load preview cursor.");
        try{var image=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();animation.SetResource(theme.Resource(role),image);}finally{Native.DestroyCursor(handle);}UpdatePreviewBounds();
    }
    private void ReleasePreview(){previewOperation?.Cancel();animation.Clear();DetailPreview.Cursor=null;Mouse.UpdateCursor();cursor?.Dispose();cursor=null;previewPack?.Dispose();previewPack=null;PreviewHost.Visibility=Visibility.Collapsed;TryPreview.IsEnabled=true;PreviewBoundsChanged?.Invoke(null);}
    private async Task LoadCover(Image target,string url,CancellationToken cancellation)
    {
        bool entered=false;
        try
        {
            await images.WaitAsync(cancellation);entered=true;var folder=Path.Combine(Storage.Root,"Online","Media");DownloadManager.GuardRoot(folder);Directory.CreateDirectory(folder);var file=Path.Combine(folder,Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url)))+".png");byte[] bytes;
            if(File.Exists(file)&&new FileInfo(file).Length<=1024*1024)bytes=await File.ReadAllBytesAsync(file,cancellation);else{bytes=await http.BytesAsync(url,1024*1024,cancellation);cancellation.ThrowIfCancellationRequested();await File.WriteAllBytesAsync(file,bytes,cancellation);}
            if(bytes.Length<24||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4))>2048||System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4))>2048)throw new InvalidDataException("Unsupported cover image size or type.");
            cancellation.ThrowIfCancellationRequested();using var input=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=512;bitmap.StreamSource=input;bitmap.EndInit();bitmap.Freeze();if(!disposed)target.Source=bitmap;
            var cached=new DirectoryInfo(folder).GetFiles("*.png").OrderByDescending(f=>f.LastWriteTimeUtc).ToArray();long retained=0;foreach(var old in cached){retained+=old.Length;if(retained>24*1024*1024)try{old.Delete();}catch(IOException){}}
        }
        catch(Exception ex)when(ex is OperationCanceledException or IOException or System.Net.Http.HttpRequestException or InvalidDataException or NotSupportedException or ArgumentException){if(ex is not OperationCanceledException)Storage.Log("Cover: "+ex.Message);}
        finally{if(entered)images.Release();}
    }
    private void SourceClicked(object sender,RoutedEventArgs e){if(selected!=null)try{Process.Start(new ProcessStartInfo(selected.SourceUrl){UseShellExecute=true});}catch(Exception ex){Status.Text=ex.Message;}}
    public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();operation?.Cancel();mediaOperation?.Cancel();ReleasePreview();animation.Dispose();DetailPreview.Dispose();http.Dispose();}
}
