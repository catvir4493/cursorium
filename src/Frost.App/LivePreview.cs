using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Frost.Core;

namespace Frost.App;

// Preview owns a local model. It never writes system settings or sends input.
public sealed class LivePreview:Grid,IDisposable
{
    private readonly Settings settings=new();
    private readonly TrailModel model=new();
    private readonly ClickEffectManager clicks;
    private readonly TrailPainter painter;
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly DispatcherTimer timer;
    private bool disposed;
    public long Frames {get;private set;}
    public long PaintCount=>painter.PaintCount;
    public long PaintTicks=>painter.PaintTicks;
    public TrailModel Model=>model;
    public LivePreview()
    {
        ClipToBounds=true;Background=System.Windows.Media.Brushes.Transparent;
        clicks=new(model);painter=new(model,settings);Children.Add(painter);
        timer=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(16)};
        timer.Tick+=Tick;
        MouseMove+=(_,e)=>{if(!settings.Enabled)return;var p=e.GetPosition(this);var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(this);if(settings.TrailEnabled)model.Add(p.X*dpi.DpiScaleX,p.Y*dpi.DpiScaleY,clock.Elapsed.TotalMilliseconds,settings);Wake();};
        MouseDown+=(_,e)=>{if(!settings.Enabled||disposed)return;var p=e.GetPosition(this);var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(this);clicks.Observe(e.ChangedButton==MouseButton.Right?ClickButton.Right:e.ChangedButton==MouseButton.Middle?ClickButton.Middle:ClickButton.Left,p.X*dpi.DpiScaleX,p.Y*dpi.DpiScaleY,clock.Elapsed.TotalMilliseconds,settings);Wake();};
        IsVisibleChanged+=(_,_)=>{if(!IsVisible){timer.Stop();model.Clear();painter.InvalidateVisual();}};
    }
    public void Update(Settings value)
    {
        settings.CopyFrom(value);if(!settings.Enabled)model.Clear();model.Expire(clock.Elapsed.TotalMilliseconds,settings);painter.InvalidateVisual();if(model.HasContent)Wake();
    }
    private void Wake(){if(!disposed&&IsVisible)timer.Start();}
    private void Tick(object? sender,EventArgs e)
    {
        var now=clock.Elapsed.TotalMilliseconds;model.Expire(now,settings);painter.Now=now;painter.InvalidateVisual();Frames++;if(!model.HasContent)timer.Stop();
    }
    public void Dispose(){if(disposed)return;disposed=true;timer.Stop();timer.Tick-=Tick;model.Clear();}
}
