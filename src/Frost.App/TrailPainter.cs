using System.Windows;
using System.Windows.Media;
using Frost.Core;

namespace Frost.App;
public sealed class TrailPainter:FrameworkElement
{
    public TrailModel Model {get;}
    public Settings Settings {get;}
    public double Now {get;set;}
    public double OriginX {get;set;}
    public double OriginY {get;set;}
    public bool PhysicalCoordinates {get;set;}=true;
    private string lastColor="";
    private Color color;
    private readonly Dictionary<int,Pen> pens=new();
    private readonly Dictionary<int,Brush> brushes=new();
    public TrailPainter(TrailModel model,Settings settings){Model=model;Settings=settings;IsHitTestVisible=false;ClipToBounds=true;}
    private Brush Brush(double alpha)
    {
        int key=Math.Clamp((int)(alpha*100),0,100);if(!brushes.TryGetValue(key,out var brush)){brush=new SolidColorBrush(Color.FromArgb((byte)(key*255/100),color.R,color.G,color.B));brush.Freeze();brushes[key]=brush;}return brush;
    }
    private Pen Pen(double alpha,double width)
    {
        int a=Math.Clamp((int)(alpha*100),0,100),w=Math.Max(1,(int)(width*10)),key=a*1000+w;
        if(!pens.TryGetValue(key,out var pen)){pen=new Pen(Brush(a/100d),w/10d){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};pen.Freeze();if(pens.Count>4096)pens.Clear();pens[key]=pen;}return pen;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);if(lastColor!=Settings.Color){lastColor=Settings.Color;color=CursorArtwork.ColorFrom(lastColor);pens.Clear();brushes.Clear();}
        var dpi=VisualTreeHelper.GetDpi(this);double sx=PhysicalCoordinates?dpi.DpiScaleX:1,sy=PhysicalCoordinates?dpi.DpiScaleY:1;
        Point Local(double x,double y)=>new((x-OriginX)/sx,(y-OriginY)/sy);
        var points=Model.Points;
        for(int i=1;Settings.TrailEnabled&&Settings.TrailEffect!="stardust"&&i<points.Count;i++)
        {
            var a=points[i-1];var b=points[i];double age=Math.Clamp((Now-b.Time)/Settings.Duration,0,1);
            double speed=Math.Clamp(b.Speed/1800,0,1),strength=Settings.Opacity*(1-age)*(1-age)*(.28+.72*speed)*(i/(double)points.Count);
            var p=Local(a.X,a.Y);var q=Local(b.X,b.Y);
            if(Math.Max(p.X,q.X)<-30||Math.Min(p.X,q.X)>ActualWidth+30||Math.Max(p.Y,q.Y)<-30||Math.Min(p.Y,q.Y)>ActualHeight+30)continue;
            if(Settings.TrailEffect=="minimal"){dc.DrawLine(Pen(strength,1.5),p,q);continue;}
            if(Settings.TrailEffect=="neon")
            {
                dc.DrawLine(Pen(strength*.14,12),p,q);dc.DrawLine(Pen(strength*.4,5),p,q);dc.DrawLine(Pen(strength,2),p,q);continue;
            }
            dc.DrawLine(Pen(strength*.12,8+speed*5),p,q);dc.DrawLine(Pen(strength*.25,3.5+speed*2),p,q);dc.DrawLine(Pen(strength,1.1+speed*.5),p,q);
            // A restrained second filament follows the same history, never cursor copies.
            if(speed>.55&&Settings.Preset!="Minimal")dc.DrawLine(Pen(strength*.24,.7),new Point(p.X,p.Y+2.2),new Point(q.X,q.Y+2.2));
        }
        int count=Settings.TrailEnabled&&Settings.TrailEffect is "comet" or "stardust"?Math.Min(Settings.Particles,Math.Max(0,points.Count-1)):0;
        for(int i=0;i<count;i++)
        {
            int index=(int)((i+1d)/(count+1)*(points.Count-1));var p=points[index];double age=Math.Clamp((Now-p.Time)/Settings.Duration,0,1),speed=Math.Clamp(p.Speed/1800,0,1);
            double alpha=Settings.Opacity*(1-age)*(1-age)*speed*.6; if(alpha<.01)continue;
            var q=Local(p.X,p.Y);double drift=Math.Sin(p.Time*.027+i)*2.8*speed;q.Offset(drift,-drift);
            if(Settings.TrailEffect=="stardust")
            {
                double phase=p.Time*.027+i*2.4,spread=3+age*32; q.Offset(Math.Cos(phase)*spread,Math.Sin(phase)*spread);
                alpha=Settings.Opacity*(1-age)*(.4+.6*speed);
            }
            dc.DrawEllipse(Brush(alpha*.15),null,q,3,3);dc.DrawEllipse(Brush(alpha),null,q,.8,.8);
        }
        ClickEffectPainter.Draw(dc,Model.Halos,Settings,Now,sx,Local,Brush,Pen);
    }
}
