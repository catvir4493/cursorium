using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Frost.Core;

namespace Frost.App.Presentation;

// Static presentation only: original cursor frames and the trusted production painter.
// No theme application, package mutation, background animation or network access.
internal static class ThemeCoverRenderer
{
    public const int Width=720, Height=540;
    public static BitmapSource Cursor(CursorTheme theme, Func<string,BitmapSource>? original=null)
    {
        var accent=Accent(theme.Id);var visual=Background(accent);RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.NearestNeighbor);
        using(var dc=visual.RenderOpen())
        {
            PaintBackground(dc,accent);
            var roles=new[]{"Normal","Link","Help","Busy","Working","Text","Move","Precision"}.Where(r=>!theme.InheritedRoles.ContainsKey(r)).Take(4).ToArray();
            var positions=new[]{new Rect(65,122,238,238),new Rect(348,83,132,132),new Rect(471,238,148,148),new Rect(325,347,102,102)};
            for(int i=0;i<roles.Length;i++)
            {
                var image=original!=null?original(roles[i]):Frame(theme.Resource(roles[i]));
                // Integer scaling preserves the source pixels and aspect ratio.
                int scale=Math.Max(1,(int)(positions[i].Width/Math.Max(image.PixelWidth,image.PixelHeight)));
                var bounds=new Rect(positions[i].X,positions[i].Y,image.PixelWidth*scale,image.PixelHeight*scale);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(20,0,0,0)),null,new Rect(bounds.X+10,bounds.Y+14,bounds.Width,bounds.Height),14,14);
                dc.DrawImage(EnlargePixels(image,scale),bounds);
            }
        }
        return Render(visual);
    }
    public static BitmapSource Trail(TrailPreset preset)
    {
        var settings=new Settings();preset.Apply(settings);var accent=(Color)ColorConverter.ConvertFromString(preset.Color);
        // A reproducible fast gesture gives the actual renderer a readable snapshot.
        // All preset parameters remain unchanged; only the simulated pointer motion varies.
        var model=new TrailModel();for(int i=0;i<160;i++){double t=i/159d;model.Add(60+t*230,145-Math.Sin(t*Math.PI*1.65)*60,i*.5,settings);}
        var painter=new TrailPainter(model,settings){Width=360,Height=270,Now=79.5,PhysicalCoordinates=false};
        painter.Measure(new Size(360,270));painter.Arrange(new Rect(0,0,360,270));
        var effect=new RenderTargetBitmap(360,270,96,96,PixelFormats.Pbgra32);effect.Render(painter);
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){PaintBackground(dc,accent);dc.DrawImage(effect,new Rect(0,0,Width,Height));}return Render(visual);
    }
    public static BitmapSource Frame(string path)
    {
        if(path.EndsWith(".ani",StringComparison.OrdinalIgnoreCase))
        {
            return AnimatedCursorPreview.DecodeInitialFrame(File.ReadAllBytes(path));
        }
        var handle=Native.LoadOwnedCursor(path);if(handle==IntPtr.Zero)throw new IOException("Cannot render verified cursor resource.");
        try{var bitmap=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());bitmap.Freeze();return bitmap;}
        finally{Native.DestroyCursor(handle);}
    }
    public static void Save(BitmapSource source,string path){Directory.CreateDirectory(Path.GetDirectoryName(path)!);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using var file=File.Create(path);encoder.Save(file);}
    private static BitmapSource EnlargePixels(BitmapSource source,int scale)
    {
        var input=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);int w=input.PixelWidth,h=input.PixelHeight;var pixels=new byte[w*h*4];input.CopyPixels(pixels,w*4,0);var enlarged=new byte[w*h*4*scale*scale];
        for(int y=0;y<h*scale;y++)for(int x=0;x<w*scale;x++)Buffer.BlockCopy(pixels,((y/scale)*w+x/scale)*4,enlarged,(y*w*scale+x)*4,4);
        var result=BitmapSource.Create(w*scale,h*scale,96,96,PixelFormats.Pbgra32,null,enlarged,w*scale*4);result.Freeze();return result;
    }
    private static Color Accent(string id)=>id.Contains("bunny")?Color.FromRgb(243,170,201):id.Contains("heart")?Color.FromRgb(248,136,184):id.Contains("cool-cat")?Color.FromRgb(237,182,105):id.Contains("kawaii-cat")?Color.FromRgb(197,155,233):id.Contains("pastel")?Color.FromRgb(166,215,195):id.Contains("obsidian")?Color.FromRgb(101,130,166):Color.FromRgb(135,207,255);
    private static DrawingVisual Background(Color accent){var visual=new DrawingVisual();using var dc=visual.RenderOpen();PaintBackground(dc,accent);return visual;}
    private static void PaintBackground(DrawingContext dc,Color accent)
    {
        var dark=Color.FromRgb(16,25,43);var tint=Color.FromRgb((byte)(accent.R*.25+dark.R*.75),(byte)(accent.G*.25+dark.G*.75),(byte)(accent.B*.25+dark.B*.75));
        dc.DrawRectangle(new LinearGradientBrush(tint,dark,new Point(0,0),new Point(1,1)),null,new Rect(0,0,Width,Height));
        var light=new RadialGradientBrush(Color.FromArgb(60,accent.R,accent.G,accent.B),Color.FromArgb(0,accent.R,accent.G,accent.B));dc.DrawEllipse(light,null,new Point(245,200),290,260);
        var edge=new Pen(new SolidColorBrush(Color.FromArgb(22,accent.R,accent.G,accent.B)),1);
        dc.DrawEllipse(null,edge,new Point(380,265),238,238);dc.DrawEllipse(null,edge,new Point(380,265),294,294);
        for(int i=0;i<12;i++)dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(45,accent.R,accent.G,accent.B)),null,new Point(34+(i*137)%650,35+(i*89)%465),2,2);
    }
    private static BitmapSource Render(Visual visual){var target=new RenderTargetBitmap(Width,Height,96,96,PixelFormats.Pbgra32);target.Render(visual);target.Freeze();return target;}
}
