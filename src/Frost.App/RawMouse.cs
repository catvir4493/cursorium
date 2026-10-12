using System.Runtime.InteropServices;
using System.Windows.Interop;
using Frost.Core;

namespace Frost.App;
public sealed class RawMouse:IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Device{public ushort Page,Usage;public uint Flags;public IntPtr Target;}
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool RegisterRawInputDevices(Device[] devices,uint count,uint size);
    [DllImport("user32.dll")]private static extern uint GetRawInputData(IntPtr input,uint command,IntPtr data,ref uint size,uint headerSize);
    private readonly HwndSource source;
    private readonly IntPtr buffer=Marshal.AllocHGlobal(256);
    private readonly Action<bool> observe;
    public RawMouse(IntPtr hwnd,Action<bool> observe)
    {
        this.observe=observe;source=HwndSource.FromHwnd(hwnd);source.AddHook(Hook);
        Native.Check(RegisterRawInputDevices(new[]{new Device{Page=1,Usage=2,Flags=0x100,Target=hwnd}},1,(uint)Marshal.SizeOf<Device>()),"Could not register raw mouse input");
    }
    private IntPtr Hook(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled)
    {
        if(msg==0xFF)
        {
            uint size=256;uint header=(uint)(8+2*IntPtr.Size);
            uint read=GetRawInputData(l,0x10000003,buffer,ref size,header);
            if(read!=uint.MaxValue&&read>=header+24&&Marshal.ReadInt32(buffer)==0)
            {
                ushort buttons=unchecked((ushort)Marshal.ReadInt16(buffer,(int)header+4));
                observe((buttons&(1|4|16))!=0);
            }
        }
        return IntPtr.Zero; // Let WPF/DefWindowProc clean up WM_INPUT; never consume legacy mouse messages.
    }
    public void Dispose(){source.RemoveHook(Hook);RegisterRawInputDevices(new[]{new Device{Page=1,Usage=2,Flags=1,Target=IntPtr.Zero}},1,(uint)Marshal.SizeOf<Device>());Marshal.FreeHGlobal(buffer);}
}
