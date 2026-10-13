using System.ComponentModel;
using System.Windows.Media.Imaging;
using Frost.Core;

namespace Frost.App.Presentation;

public sealed class ThemeCardView:INotifyPropertyChanged
{
    public CursorTheme Theme {get;}
    public BitmapSource Thumbnail {get;}
    public string AnimationLabel=>Theme.SourceFiles.Count>0&&!Theme.SourceFiles.Any(f=>f.Path.EndsWith(".ani",StringComparison.OrdinalIgnoreCase))?(Theme.IsAnimated?"Static / fallback ANI":"Static"):Theme.IsAnimated?"Animated":"Static";
    public bool IsFavorite {get;}
    public string FavoriteGlyph=>IsFavorite?"★":"☆";
    public string FavoriteAction=>IsFavorite?"Remove favorite":"Add favorite";
    private string status="";
    public string Status {get=>status;set{if(status==value)return;status=value;PropertyChanged?.Invoke(this,new(nameof(Status)));}}
    public event PropertyChangedEventHandler? PropertyChanged;
    public ThemeCardView(CursorTheme theme,BitmapSource thumbnail,bool favorite){Theme=theme;Thumbnail=thumbnail;IsFavorite=favorite;}
}
