using System.Windows;
using System.Windows.Controls;
using Frost.Core;

namespace Frost.App.Presentation;

// A bounded mapping review, using the same tokens and control styles as the studio.
public sealed class PackImportDialog:Window
{
    private readonly Dictionary<string,ComboBox> roleBoxes=new();
    private readonly ComboBox fallback=new();
    private readonly TextBox name=new();
    private readonly CheckBox confirm=new(){Content="Use the chosen fallback for every unmapped role"};
    private readonly TextBlock error=new(){TextWrapping=TextWrapping.Wrap};
    public string ThemeName=>name.Text;
    public CursorTheme Fallback=>(CursorTheme)fallback.SelectedItem;
    public Dictionary<string,string> Replacements=>roleBoxes.Where(p=>p.Value.SelectedItem is PackCursor).ToDictionary(p=>p.Key,p=>((PackCursor)p.Value.SelectedItem).Path);
    public PackImportDialog(Window owner,CursorPack pack,IEnumerable<CursorTheme> themes,CursorTheme selected,string proposedName)
    {
        Owner=owner;Title="Import Cursor Pack · Review roles";Width=610;Height=690;MinWidth=440;MinHeight=440;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Resources=owner.Resources;SetResourceReference(BackgroundProperty,"WindowBrush");SetResourceReference(ForegroundProperty,"TextBrush");FontFamily=owner.FontFamily;
        var root=new Grid{Margin=new Thickness(20)};root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new());root.RowDefinitions.Add(new(){Height=GridLength.Auto});Content=root;
        var top=new StackPanel();top.Children.Add(new TextBlock{Text="Review pointer roles",FontSize=20});top.Children.Add(new TextBlock{Text=$"{pack.Files.Count} validated cursor files. Ambiguous names require your choice.\nNothing changes in Windows until Apply Changes.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,12)});root.Children.Add(top);
        var body=new StackPanel();var scroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,1);root.Children.Add(scroll);
        body.Children.Add(new TextBlock{Text="Theme name"});name.Text=proposedName;body.Children.Add(name);
        body.Children.Add(new TextBlock{Text="Fallback theme",Margin=new Thickness(0,10,0,3)});fallback.ItemsSource=themes.Where(t=>!t.IsSystem).ToArray();fallback.SelectedItem=selected.IsSystem?fallback.Items[0]:selected;body.Children.Add(fallback);
        foreach(var role in CursorRoles.All)
        {
            var row=new Grid{Margin=new Thickness(0,5,8,0)};row.ColumnDefinitions.Add(new(){Width=new GridLength(130)});row.ColumnDefinitions.Add(new());
            var candidates=pack.Files.Where(f=>f.SuggestedRole==role.Name).ToArray();row.Children.Add(new TextBlock{Text=role.Name+(candidates.Length>1?" · conflict":""),VerticalAlignment=VerticalAlignment.Center});
            var box=new ComboBox{ItemsSource=new object[]{"Inherit fallback"}.Concat(pack.Files).ToArray(),SelectedIndex=0,ToolTip="Choose an actual file, or explicitly inherit fallback"};if(candidates.Length==1)box.SelectedItem=candidates[0];Grid.SetColumn(box,1);row.Children.Add(box);body.Children.Add(row);roleBoxes.Add(role.Name,box);
        }
        body.Children.Add(confirm);body.Children.Add(error);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};Grid.SetRow(actions,2);root.Children.Add(actions);
        var cancel=new Button{Content="Cancel",IsCancel=true,Margin=new Thickness(0,0,8,0)};actions.Children.Add(cancel);
        var import=new Button{Content="Import reviewed theme",IsDefault=true};import.SetResourceReference(StyleProperty,"PrimaryButton");actions.Children.Add(import);
        import.Click+=(_,_)=>
        {
            var replacements=Replacements;
            if(name.Text.Trim().Length is <1 or >100||replacements.Count==0){error.Text="Provide a theme name and at least one mapped role.";return;}
            if(replacements.Count<15&&confirm.IsChecked!=true){error.Text="Confirm fallback for the unmapped roles before importing.";return;}
            // A conflicted role cannot be silently inherited without an explicit fallback choice.
            if(replacements.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=replacements.Count){error.Text="The same source file is mapped to multiple roles. Choose distinct files.";return;}
            DialogResult=true;
        };
        SourceInitialized+=(_,_)=>WindowAppearance.Apply(this,true);
        Loaded+=(_,_)=>
        {
            var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var monitor=new Native.MonitorInfo{Size=System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>(),Device=""};
            if(!Native.GetMonitorInfo(Native.MonitorFromWindow(handle,2),ref monitor))return;
            var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(this);
            double width=monitor.Work.Width/dpi.DpiScaleX-24,height=monitor.Work.Height/dpi.DpiScaleY-24;
            MinWidth=Math.Min(MinWidth,width);MinHeight=Math.Min(MinHeight,height);Width=Math.Min(Width,width);Height=Math.Min(Height,height);
            Left=Math.Clamp(Left,monitor.Work.Left/dpi.DpiScaleX,monitor.Work.Right/dpi.DpiScaleX-Width);
            Top=Math.Clamp(Top,monitor.Work.Top/dpi.DpiScaleY,monitor.Work.Bottom/dpi.DpiScaleY-Height);
        };
    }
}
