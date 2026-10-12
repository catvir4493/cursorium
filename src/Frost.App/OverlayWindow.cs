using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Frost.Core;

namespace Frost.App;
public sealed class OverlayWindow:Window
{
    public TrailPainter Painter{get;}
    public IntPtr Handle{get;private set;}
    public Native.MonitorInfo Monitor{get;}
    public OverlayWindow(Native.MonitorInfo monitor,TrailModel model,Settings settings)
    {
        Monitor=monitor;Painter=new(model,settings){OriginX=monitor.Monitor.Left,OriginY=monitor.Monitor.Top};
        Title="FROST Overlay "+monitor.Device;AllowsTransparency=true;WindowStyle=WindowStyle.None;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=true;ResizeMode=ResizeMode.NoResize;Focusable=false;IsHitTestVisible=false;Content=Painter;
        Left=monitor.Monitor.Left;Top=monitor.Monitor.Top;Width=1;Height=1;
        SourceInitialized+=(_,_)=>
        {
            Handle=new WindowInteropHelper(this).Handle;var style=Native.GetWindowLongPtr(Handle,Native.GwlExStyle).ToInt64();
            Native.SetWindowLongPtr(Handle,Native.GwlExStyle,(IntPtr)(style|Native.Transparent|Native.ToolWindow|Native.NoActivate|Native.Layered));
            HwndSource.FromHwnd(Handle).AddHook(Hook);Place();
        };
        Loaded+=(_,_)=>Place();
    }
    public void Place()
    {
        var r=Monitor.Monitor;Native.Check(Native.SetWindowPos(Handle,(IntPtr)(-1),r.Left,r.Top,r.Width,r.Height,0x10|0x40),"Could not position the transparent overlay");
    }
    private IntPtr Hook(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled)
    {
        if(msg==0x84){handled=true;return (IntPtr)(-1);} // HTTRANSPARENT, with layered WS_EX_TRANSPARENT for cross-process pass-through
        if(msg==0x21){handled=true;return (IntPtr)3;} // MA_NOACTIVATE
        return IntPtr.Zero;
    }
}
