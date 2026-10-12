using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Frost.Core;

namespace Frost.App;
public sealed class TrailEngine:IDisposable
{
    private readonly Settings settings;
    private readonly TrailModel model=new();
    private readonly ClickEffectManager clicks;
    private readonly List<OverlayWindow> windows=new();
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly DispatcherTimer sampler;
    private RawMouse? mouse;
    private double lastRender,lastEnvironmentCheck,lastTopmost;
    private bool paused,dirty,hadContent;
    private bool disposed;
    private bool renderAttached;
    public bool PausedByUser {get;set;}
    public Native.Rect? PreviewBounds {get;set;}
    private bool InsidePreview(Native.Point p)=>PreviewBounds is Native.Rect r&&p.X>=r.Left&&p.X<r.Right&&p.Y>=r.Top&&p.Y<r.Bottom;
    public string Status {get;private set;}="Running";
    public IReadOnlyList<OverlayWindow> Windows=>windows;
    public TrailModel Model=>model;
    public long RenderedFrames{get;private set;}
    public long Samples{get;private set;}
    public TrailEngine(Settings settings)
    {
        this.settings=settings;clicks=new(model);Rebuild();
        sampler=new DispatcherTimer(DispatcherPriority.Input){Interval=TimeSpan.FromMilliseconds(8)};sampler.Tick+=Sample;sampler.Start();
        SystemEvents.DisplaySettingsChanged+=DisplayChanged;SystemEvents.PowerModeChanged+=PowerChanged;
    }
    private void Rebuild()
    {
        mouse?.Dispose();mouse=null;foreach(var w in windows)w.Close();windows.Clear();model.Clear();hadContent=false;
        foreach(var monitor in Native.Monitors()){var w=new OverlayWindow(monitor,model,settings);w.Show();windows.Add(w);}
        if(windows.Count>0)mouse=new RawMouse(windows[0].Handle,ObserveRaw);
    }
    private void ObserveRaw(ClickButton buttons)
    {
        if(!settings.Enabled||paused||!Native.GetPhysicalCursorPos(out var p))return;
        if(InsidePreview(p)){model.Clear();dirty|=hadContent;if(dirty)AttachRender();return;}
        double now=clock.Elapsed.TotalMilliseconds;if(settings.TrailEnabled)model.Add(p.X,p.Y,now,settings);dirty|=model.Points.Count>1;
        clicks.Observe(buttons,p.X,p.Y,now,settings);dirty|=model.Halos.Count>0;if(dirty)AttachRender();
    }
    private void AttachRender(){if(!renderAttached){CompositionTarget.Rendering+=Render;renderAttached=true;}}
    private void DetachRender(){if(renderAttached){CompositionTarget.Rendering-=Render;renderAttached=false;}}
    private void DisplayChanged(object? s,EventArgs e)=>System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(!disposed)Rebuild();}));
    private void PowerChanged(object sender,PowerModeChangedEventArgs e)=>System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(disposed)return;if(e.Mode==PowerModes.Suspend)model.Clear();if(e.Mode==PowerModes.Resume)lastEnvironmentCheck=0;}));
    public void RefreshDisplays()=>Rebuild();
    private bool Fullscreen()
    {
        var h=Native.GetForegroundWindow();if(h==IntPtr.Zero)return false;
        Native.GetWindowThreadProcessId(h,out var pid);if(pid==Environment.ProcessId)return false;
        if(!Native.GetWindowRect(h,out var r))return false;
        var m=new Native.MonitorInfo{Size=System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>(),Device=""};
        if(!Native.GetMonitorInfo(Native.MonitorFromWindow(h,2),ref m))return false;
        // Geometric heuristic covers borderless fullscreen; exclusion of the desktop shell avoids pausing on wallpaper.
        var className=new System.Text.StringBuilder(128);GetClassName(h,className,className.Capacity);if(className.ToString() is "Progman" or "WorkerW")return false;
        return r.Left<=m.Monitor.Left&&r.Top<=m.Monitor.Top&&r.Right>=m.Monitor.Right&&r.Bottom>=m.Monitor.Bottom;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]private static extern int GetClassName(IntPtr hwnd,System.Text.StringBuilder value,int count);
    private void Sample(object? sender,EventArgs e)
    {
        Samples++;
        double now=clock.Elapsed.TotalMilliseconds;
        if(now-lastEnvironmentCheck>300)
        {
            lastEnvironmentCheck=now;paused=PausedByUser||!Native.IsDefaultInputDesktop()||(settings.PauseFullscreen&&Fullscreen());
            Status=!settings.Enabled?"Effects disabled":PausedByUser?"Paused by user":paused?"Paused: fullscreen or secure desktop":!settings.TrailEnabled?"Click effects only":settings.PowerSaving?"Power saving · 30 FPS":"Running · "+settings.FrameRate+" FPS target";
            sampler.Interval=TimeSpan.FromMilliseconds(!settings.Enabled||!settings.TrailEnabled?250:paused?100:settings.PowerSaving?16:8);
            foreach(var w in windows){if(!settings.Enabled||paused){if(w.IsVisible)w.Hide();}else if(!w.IsVisible)w.Show();}
        }
        if(!settings.Enabled||paused){if(model.HasContent||model.Points.Count>0){model.Clear();foreach(var w in windows)w.Painter.InvalidateVisual();}hadContent=false;dirty=false;DetachRender();return;}
        if(Native.GetPhysicalCursorPos(out var p))
        {
            if(InsidePreview(p)){model.Clear();dirty|=hadContent;}
            else if(settings.TrailEnabled){model.Add(p.X,p.Y,now,settings);dirty|=model.Points.Count>1;}
        }
        model.Expire(now,settings);
        if(dirty||model.HasContent||hadContent)AttachRender();
    }
    private void Render(object? sender,EventArgs e)
    {
        double now=clock.Elapsed.TotalMilliseconds;int fps=settings.PowerSaving?30:settings.FrameRate;
        double interval=1000d/fps;if(now-lastRender<interval-.7)return;
        lastRender+=interval;if(now-lastRender>interval)lastRender=now; // preserve the cadence instead of accumulating per-frame timing jitter
        bool content=model.HasContent;if(!content&&!hadContent&&!dirty){DetachRender();return;}
        RenderedFrames++;
        foreach(var w in windows){w.Painter.Now=now;w.Painter.InvalidateVisual();}
        hadContent=content;dirty=false;
        if(!content)DetachRender();
        if(content&&now-lastTopmost>1500){lastTopmost=now;foreach(var w in windows)w.Place();}
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;sampler.Stop();sampler.Tick-=Sample;DetachRender();SystemEvents.DisplaySettingsChanged-=DisplayChanged;SystemEvents.PowerModeChanged-=PowerChanged;mouse?.Dispose();foreach(var w in windows)w.Close();windows.Clear();
    }
}
