using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Frost.Core;
public static class Native
{
    public const int GwlExStyle=-20;
    public const long Transparent=0x20, ToolWindow=0x80, Layered=0x80000, NoActivate=0x08000000;
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; public int Width=>Right-Left;public int Height=>Bottom-Top; }
    [StructLayout(LayoutKind.Sequential)] public struct IconInfo { [MarshalAs(UnmanagedType.Bool)]public bool IsIcon;public uint HotX,HotY;public IntPtr Mask,Color; }
    [StructLayout(LayoutKind.Sequential)] public struct Bitmap {public int Type,Width,Height,WidthBytes;public ushort Planes,BitsPixel;public IntPtr Bits;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;}
    public delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,ref Rect rect,IntPtr data);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] public static extern bool GetPhysicalCursorPos(out Point point);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern IntPtr LoadImage(IntPtr instance,string path,uint type,int cx,int cy,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern IntPtr LoadCursorFromFile(string path);
    [DllImport("user32.dll")]public static extern IntPtr LoadCursor(IntPtr instance,IntPtr id);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]public static extern bool DestroyCursor(IntPtr cursor);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]public static extern bool GetIconInfo(IntPtr cursor,out IconInfo info);
    [DllImport("gdi32.dll")]public static extern int GetObject(IntPtr handle,int size,out Bitmap bitmap);
    [DllImport("gdi32.dll")] [return:MarshalAs(UnmanagedType.Bool)]public static extern bool DeleteObject(IntPtr handle);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]public static extern bool SystemParametersInfo(uint action,uint param,IntPtr value,uint flags);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW",SetLastError=true)]public static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]public static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]public static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]public static extern bool IsWindow(IntPtr handle);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]public static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
    [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    [DllImport("user32.dll")]public static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] [return:MarshalAs(UnmanagedType.Bool)]public static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]public static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
    [DllImport("user32.dll")]public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")]public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]public static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]public static extern bool GetUserObjectInformation(IntPtr handle,int index,IntPtr info,uint length,out uint needed);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]public static extern bool SetPhysicalCursorPos(int x,int y);
    public static void Check(bool success,string operation){if(!success)throw new Win32Exception(Marshal.GetLastWin32Error(),operation);}
    public static void ReloadCursors()=>Check(SystemParametersInfo(0x57,0,IntPtr.Zero,2),"Windows could not reload the cursor scheme");
    public static List<MonitorInfo> Monitors()
    {
        var list=new List<MonitorInfo>(); MonitorCallback callback=(IntPtr h,IntPtr d,ref Rect r,IntPtr p)=>{var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>(),Device=""};if(GetMonitorInfo(h,ref info))list.Add(info);return true;};
        Check(EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,callback,IntPtr.Zero),"Could not enumerate displays");GC.KeepAlive(callback);return list;
    }
    public static bool IsDefaultInputDesktop()
    {
        var h=OpenInputDesktop(0,false,1);if(h==IntPtr.Zero)return false;
        try{GetUserObjectInformation(h,2,IntPtr.Zero,0,out uint bytes);if(bytes==0||bytes>4096)return false;var p=Marshal.AllocHGlobal((int)bytes);try{return GetUserObjectInformation(h,2,p,bytes,out _)&&string.Equals(Marshal.PtrToStringUni(p),"Default",StringComparison.OrdinalIgnoreCase);}finally{Marshal.FreeHGlobal(p);}}finally{CloseDesktop(h);}
    }
}
