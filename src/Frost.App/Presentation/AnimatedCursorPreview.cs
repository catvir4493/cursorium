using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Frost.Core;

namespace Frost.App.Presentation;

// Decodes the validated source frames once. Animation runs only for the visible preview.
// The actual local mouse cursor remains the original WPF native Cursor, with its hotspots.
public sealed class AnimatedCursorPreview:IDisposable
{
    private readonly Image image;
    private readonly DispatcherTimer timer=new(DispatcherPriority.Render);
    private readonly Stopwatch clock=new();
    private BitmapSource[] frames=Array.Empty<BitmapSource>();
    private AniTimeline? timeline;
    private string? resource;
    public bool Running=>timer.IsEnabled;
    public int CurrentStep {get;private set;}
    public AnimatedCursorPreview(Image image){this.image=image;timer.Tick+=Tick;}
    public void SetResource(string? path,BitmapSource still)
    {
        if(resource==path&&timeline!=null){image.Source=frames[timeline.Sequence[CurrentStep]];return;}
        timer.Stop();clock.Reset();frames=Array.Empty<BitmapSource>();timeline=null;resource=path;CurrentStep=0;image.Source=still;
        if(path==null||!path.EndsWith(".ani",StringComparison.OrdinalIgnoreCase))return;
        var bytes=File.ReadAllBytes(path);timeline=AniTimeline.Read(bytes);frames=DecodeFrames(bytes);image.Source=frames[timeline.Sequence[0]];
    }
    public void SetVisible(bool visible)
    {
        if(visible&&timeline!=null&&!timer.IsEnabled){clock.Start();Tick(this,EventArgs.Empty);timer.Start();}
        else if(!visible){timer.Stop();clock.Stop();}
    }
    private void Tick(object? sender,EventArgs e)
    {
        if(timeline==null)return;
        double elapsed=clock.Elapsed.TotalMilliseconds;CurrentStep=timeline.StepAt(elapsed);image.Source=frames[timeline.Sequence[CurrentStep]];
        double ticks=elapsed%timeline.DurationMilliseconds*60/1000;
        for(int i=0;i<CurrentStep;i++)ticks-=timeline.Jiffies[i];
        timer.Interval=TimeSpan.FromMilliseconds(Math.Max(1,(timeline.Jiffies[CurrentStep]-ticks)*1000/60));
    }
    public static BitmapSource[] DecodeFrames(byte[] bytes)
    {
        var result=new List<BitmapSource>();
        for(int i=12;i<bytes.Length;)
        {
            int size=checked((int)BitConverter.ToUInt32(bytes,i+4));
            if(bytes.AsSpan(i,4).SequenceEqual("LIST"u8)&&size>=4&&bytes.AsSpan(i+8,4).SequenceEqual("fram"u8))
                for(int f=i+12;f<i+8+size;)
                {
                    int length=checked((int)BitConverter.ToUInt32(bytes,f+4));result.Add(DecodeFrame(bytes.AsSpan(f+8,length)));f+=8+length+(length&1);
                }
            i+=8+size+(size&1);
        }
        return result.ToArray();
    }
    private static BitmapSource DecodeFrame(ReadOnlySpan<byte> frame)
    {
        var entry=frame.Slice(6,16);int length=checked((int)BitConverter.ToUInt32(entry[8..])),offset=checked((int)BitConverter.ToUInt32(entry[12..]));bool icon=BitConverter.ToUInt16(frame[2..])==1;
        // CreateIconFromResourceEx expects CUR image bits prefixed by the two WORD hotspots.
        var bits=new byte[length+(icon?0:4)];if(!icon)entry.Slice(4,4).CopyTo(bits);frame.Slice(offset,length).CopyTo(bits.AsSpan(icon?0:4));
        var handle=CreateIconFromResourceEx(bits,(uint)bits.Length,icon,0x30000,48,48,0);
        if(handle==IntPtr.Zero)throw new IOException("Windows could not decode an ANI preview frame.");
        try{var source=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());source.Freeze();return source;}
        finally{if(icon)DestroyIcon(handle);else Native.DestroyCursor(handle);}
    }
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr CreateIconFromResourceEx(byte[] bits,uint length,[MarshalAs(UnmanagedType.Bool)]bool icon,uint version,int width,int height,uint flags);
    [DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr icon);
    public void Clear(){timer.Stop();clock.Reset();frames=Array.Empty<BitmapSource>();timeline=null;resource=null;CurrentStep=0;image.Source=null;}
    public void Dispose(){Clear();timer.Tick-=Tick;}
}
