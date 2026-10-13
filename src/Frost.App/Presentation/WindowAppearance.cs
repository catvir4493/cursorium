using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shell;

namespace Frost.App.Presentation;

public static class WindowAppearance
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    public static bool Apply(Window window,bool systemCaption)
    {
        WindowChrome.SetWindowChrome(window,systemCaption?null:new WindowChrome{CaptionHeight=48,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false});
        window.WindowStyle=systemCaption?WindowStyle.SingleBorderWindow:WindowStyle.None;
        var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return false;
        int dark=1,corner=2,caption=0x0022140D,text=0x00FBF6F3;
        bool success=DwmSetWindowAttribute(handle,20,ref dark,sizeof(int))>=0;
        DwmSetWindowAttribute(handle,33,ref corner,sizeof(int));
        if(systemCaption){DwmSetWindowAttribute(handle,35,ref caption,sizeof(int));DwmSetWindowAttribute(handle,36,ref text,sizeof(int));}
        return success;
    }
}
