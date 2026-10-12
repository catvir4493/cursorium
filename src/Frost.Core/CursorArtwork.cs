using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Frost.Core;

public static class CursorArtwork
{
    public static readonly int[] Sizes = [32, 48, 64, 96];
    private static readonly Color White = ColorFrom("#F7FAFF"), Outline = ColorFrom("#243248"), Ice = ColorFrom("#91DEFF");
    public static Color ColorFrom(string value) => (Color)ColorConverter.ConvertFromString(value);
    private static readonly Dictionary<string, string> Paths = new()
    {
        ["Normal"] = "M4,3 L4.7,26.4 Q4.8,28.2 6.3,27 L12.6,20.9 L17,28 Q17.5,28.8 18.4,28.3 L20.2,27.2 Q21,26.7 20.5,25.8 L16.2,19.1 L25.3,18.3 Q27,18.1 25.7,16.8 L5.7,3.3 Q4,2.2 4,3 Z",
        ["Precision"] = "M14.8,4 L17.2,4 L17.2,14.8 L28,14.8 L28,17.2 L17.2,17.2 L17.2,28 L14.8,28 L14.8,17.2 L4,17.2 L4,14.8 L14.8,14.8 Z",
        ["Text"] = "M10,4 L14,4 Q16,6 18,4 L22,4 L22,6 L18,6 L18,26 L22,26 L22,28 L18,28 Q16,26 14,28 L10,28 L10,26 L14,26 L14,6 L10,6 Z",
        ["Handwriting"] = "M5,26 L6.7,19 L21.5,4.2 Q23,2.7 25,4.6 L27,6.6 Q28.3,8 26.6,9.5 L12,24 Z",
        ["Vertical"] = "M16,3 L23,10 Q23.7,11 22,11 L18,11 L18,21 L22,21 Q23.7,21 23,22 L16,29 L9,22 Q8.3,21 10,21 L14,21 L14,11 L10,11 Q8.3,11 9,10 Z",
        ["Move"] = "M16,3 L21,8 L18,8 L18,14 L24,14 L24,11 L29,16 L24,21 L24,18 L18,18 L18,24 L21,24 L16,29 L11,24 L14,24 L14,18 L8,18 L8,21 L3,16 L8,11 L8,14 L14,14 L14,8 L11,8 Z",
        ["Alternate"] = "M16,3 L25,13 Q26,14 24,14 L19,14 L19,28 L13,28 L13,14 L8,14 Q6,14 7,13 Z",
        ["Link"] = "M12,3 Q14,2 14,5 L14,13 L16,12 Q18,11 19,14 Q21,11 23,15 Q27,13 27,18 L27,23 Q26,29 19,29 L16,29 Q12,29 10,26 L5,19 Q3,16 5,15 Q6,14 8,16 L10,18 L10,5 Q10,3 12,3 Z"
    };

    public static BitmapSource Render(CursorRole role, int size, int frame = 0)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / 32d, size / 32d));
            if (role.Name is "Normal" or "Help" or "Working") DrawPath(dc, Paths["Normal"], role.Name == "Normal" ? 1 : 0.83);
            if (Paths.TryGetValue(role.Name, out var path) && role.Name is not "Normal") DrawPath(dc, path);
            if (role.Name is "Horizontal" or "DiagonalNWSE" or "DiagonalNESW")
            {
                var angle = role.Name == "Horizontal" ? 90 : role.Name == "DiagonalNWSE" ? -45 : 45;
                dc.PushTransform(new RotateTransform(angle, 16, 16)); DrawPath(dc, Paths["Vertical"]); dc.Pop();
            }
            if (role.Name == "Help")
            {
                var g = Geometry.Parse("M23,10 C23,5 30,5 30,10 C30,13 26,13 26,16 M26,19 L26,19.3");
                dc.DrawGeometry(null, Pen(Outline, 4), g); dc.DrawGeometry(null, Pen(White, 2), g);
            }
            if (role.Name == "Unavailable")
            {
                dc.DrawEllipse(new SolidColorBrush(White), Pen(Outline, 1.6), new Point(16,16), 11.5,11.5);
                dc.DrawEllipse(null, Pen(Ice, 2.7), new Point(16,16), 8.6,8.6);
                dc.DrawLine(Pen(Outline, 4.4), new Point(9,9), new Point(23,23));
                dc.DrawLine(Pen(White, 1.7), new Point(9,9), new Point(23,23));
            }
            if (role.Animated) DrawRing(dc, role.Name == "Busy" ? new Point(16,16) : new Point(25,24), role.Name == "Busy" ? 10 : 4.6, frame);
            dc.Pop();
        }
        var image = new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32); image.Render(visual); image.Freeze(); return image;
    }
    private static Pen Pen(Color color, double width) => new(new SolidColorBrush(color), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
    private static void DrawPath(DrawingContext dc, string path, double scale = 1)
    {
        dc.PushTransform(new ScaleTransform(scale,scale,4,3));
        var fill = new LinearGradientBrush(White, ColorFrom("#DCEEFF"),90);
        dc.DrawGeometry(fill, Pen(Outline,1.7), Geometry.Parse(path));
        dc.Pop();
    }
    private static void DrawRing(DrawingContext dc, Point c, double r, int frame)
    {
        dc.DrawEllipse(null, Pen(Outline, r > 8 ? 5 : 3.7), c,r,r);
        dc.DrawEllipse(null, Pen(ColorFrom("#C4DEEF"), r > 8 ? 3 : 2), c,r,r);
        for (int i = 0; i < 28; i++)
        {
            double a = (i / 28d * 1.6 * Math.PI) + frame / 24d * Math.PI * 2;
            double b = a + 0.14;
            var color = Color.FromArgb((byte)(90 + 165 * i / 27), Ice.R, Ice.G, Ice.B);
            dc.DrawLine(Pen(color, r > 8 ? 3 : 2), new Point(c.X+r*Math.Cos(a),c.Y+r*Math.Sin(a)),new Point(c.X+r*Math.Cos(b),c.Y+r*Math.Sin(b)));
        }
    }
    public static byte[] CreateCur(CursorRole role, int frame = 0)
    {
        var images = Sizes.Select(s => Dib(Render(role,s,frame),s)).ToArray();
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
        w.Write((ushort)0); w.Write((ushort)2); w.Write((ushort)Sizes.Length);
        int offset = 6 + 16 * Sizes.Length;
        for(int i=0;i<Sizes.Length;i++)
        {
            int s=Sizes[i]; w.Write((byte)s); w.Write((byte)s); w.Write((byte)0); w.Write((byte)0);
            w.Write((ushort)Math.Round(role.HotX*s/32)); w.Write((ushort)Math.Round(role.HotY*s/32));
            w.Write(images[i].Length); w.Write(offset); offset += images[i].Length;
        }
        foreach(var bytes in images) w.Write(bytes);
        return stream.ToArray();
    }
    private static byte[] Dib(BitmapSource bitmap,int size)
    {
        var pixels = new byte[size*size*4]; bitmap.CopyPixels(pixels,size*4,0);
        // WPF stores premultiplied alpha; CUR requires straight BGRA plus an AND mask.
        for(int p=0;p<pixels.Length;p+=4)
        {
            int alpha=pixels[p+3]; if(alpha==0) continue;
            for(int c=0;c<3;c++) pixels[p+c]=(byte)Math.Min(255,(pixels[p+c]*255+alpha/2)/alpha);
        }
        using var ms=new MemoryStream(); using var w=new BinaryWriter(ms);
        int maskStride=((size+31)/32)*4;
        w.Write(40);w.Write(size);w.Write(size*2);w.Write((ushort)1);w.Write((ushort)32);
        w.Write(0);w.Write(size*size*4+maskStride*size);w.Write(0);w.Write(0);w.Write(0);w.Write(0);
        for(int y=size-1;y>=0;y--) w.Write(pixels,y*size*4,size*4);
        for(int y=size-1;y>=0;y--)
        {
            var row=new byte[maskStride];
            for(int x=0;x<size;x++) if(pixels[(y*size+x)*4+3]==0) row[x/8]|=(byte)(0x80>>(x%8));
            w.Write(row);
        }
        return ms.ToArray();
    }
    private static byte[] Chunk(string id,byte[] bytes)
    {
        using var ms=new MemoryStream();using var w=new BinaryWriter(ms);w.Write(Encoding.ASCII.GetBytes(id));w.Write(bytes.Length);w.Write(bytes);if((bytes.Length&1)!=0)w.Write((byte)0);return ms.ToArray();
    }
    private static byte[] CreateAni(CursorRole role)
    {
        using var header=new MemoryStream();using(var w=new BinaryWriter(header,Encoding.UTF8,true))
        { foreach(uint v in new uint[]{36,24,24,0,0,32,1,3,1}) w.Write(v); }
        using var frames=new MemoryStream();frames.Write(Encoding.ASCII.GetBytes("fram"));
        for(int i=0;i<24;i++)frames.Write(Chunk("icon",CreateCur(role,i)));
        using var body=new MemoryStream();body.Write(Encoding.ASCII.GetBytes("ACON"));body.Write(Chunk("anih",header.ToArray()));body.Write(Chunk("LIST",frames.ToArray()));
        return Chunk("RIFF",body.ToArray());
    }
    public static void Generate(string output)
    {
        Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"preview"));Directory.CreateDirectory(Path.Combine(output,"vectors"));
        foreach(var role in CursorRoles.All)
        {
            File.WriteAllBytes(Path.Combine(output,role.Name+".cur"),CreateCur(role));
            if(role.Animated)File.WriteAllBytes(Path.Combine(output,role.FileName),CreateAni(role));
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Render(role,96)));using(var f=File.Create(Path.Combine(output,"preview",role.Name+".png")))encoder.Save(f);
            // Editable source geometry with SVG paths, never raster reference extraction.
            string svg=Svg(role);File.WriteAllText(Path.Combine(output,"vectors",role.Name+".svg"),svg);
        }
        File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(new { theme="FROST / WHITE COMET", sizes=Sizes, animation=new {frames=24,jiffies=3}, roles=CursorRoles.All },new JsonSerializerOptions{WriteIndented=true}));
    }
    private static string Svg(CursorRole role)
    {
        string Wrap(string p,string transform="")=>$"<path d=\"{p}\" transform=\"{transform}\" fill=\"url(#ice)\" stroke=\"#243248\" stroke-width=\"1.7\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>";
        string shapes="";
        if(role.Name is "Normal" or "Help" or "Working")shapes+=Wrap(Paths["Normal"],role.Name=="Normal"?"":"translate(4 3) scale(.83) translate(-4 -3)");
        else if(Paths.TryGetValue(role.Name,out var p))shapes+=Wrap(p);
        else if(role.Name is "Horizontal" or "DiagonalNWSE" or "DiagonalNESW")shapes+=Wrap(Paths["Vertical"],$"rotate({(role.Name=="Horizontal"?90:role.Name=="DiagonalNWSE"?-45:45)} 16 16)");
        if(role.Name=="Help")shapes+="<path d=\"M23 10 C23 5 30 5 30 10 C30 13 26 13 26 16 M26 19 L26 19.3\" fill=\"none\" stroke=\"#243248\" stroke-width=\"4\" stroke-linecap=\"round\"/><path d=\"M23 10 C23 5 30 5 30 10 C30 13 26 13 26 16 M26 19 L26 19.3\" fill=\"none\" stroke=\"#F7FAFF\" stroke-width=\"2\" stroke-linecap=\"round\"/>";
        if(role.Name=="Unavailable")shapes+="<circle cx=\"16\" cy=\"16\" r=\"11.5\" fill=\"#F7FAFF\" stroke=\"#243248\" stroke-width=\"1.6\"/><circle cx=\"16\" cy=\"16\" r=\"8.6\" fill=\"none\" stroke=\"#91DEFF\" stroke-width=\"2.7\"/><path d=\"M9 9 L23 23\" stroke=\"#243248\" stroke-width=\"4.4\" stroke-linecap=\"round\"/>";
        if(role.Animated){bool busy=role.Name=="Busy";shapes+=$"<circle cx=\"{(busy?16:25)}\" cy=\"{(busy?16:24)}\" r=\"{(busy?10:4.6).ToString(CultureInfo.InvariantCulture)}\" fill=\"none\" stroke=\"#243248\" stroke-width=\"{(busy?5:3.7).ToString(CultureInfo.InvariantCulture)}\"/><circle cx=\"{(busy?16:25)}\" cy=\"{(busy?16:24)}\" r=\"{(busy?10:4.6).ToString(CultureInfo.InvariantCulture)}\" fill=\"none\" stroke=\"#91DEFF\" stroke-width=\"2\" stroke-dasharray=\"20 10\"/>";}
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"96\" height=\"96\" viewBox=\"0 0 32 32\"><defs><linearGradient id=\"ice\" x2=\"0\" y2=\"1\"><stop stop-color=\"#F7FAFF\"/><stop offset=\"1\" stop-color=\"#DCEEFF\"/></linearGradient></defs>{shapes}</svg>";
    }
}
