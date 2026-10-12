using System.Windows;
using System.Windows.Media;
using Frost.Core;

namespace Frost.App;
public static class ClickEffectPainter
{
    public static void Draw(DrawingContext dc,IReadOnlyList<Halo> events,Settings settings,double now,double scale,Func<double,double,Point> local,Func<double,Brush> brush,Func<double,double,Pen> pen)
    {
        if(!settings.ClickHalo||settings.ClickEffect=="off")return;
        foreach(var click in events)
        {
            double t=Math.Clamp((now-click.Time)/settings.HaloDuration,0,1),opacity=settings.ClickOpacity*(1-t);
            var p=local(click.X,click.Y);double r=(5+(settings.ClickRadius-5)*t)/scale;
            if(settings.ClickEffect=="halo"){dc.DrawEllipse(brush(opacity*.06),null,p,r,r);dc.DrawEllipse(null,pen(opacity*.3,4/scale),p,r,r);}
            dc.DrawEllipse(null,pen(opacity,1.2/scale),p,r,r);
            // Right clicks use a second ring; left clicks use a single ring, with no synthetic input.
            if(click.Button==ClickButton.Right)dc.DrawEllipse(null,pen(opacity*.65,1/scale),p,r*.7,r*.7);
        }
    }
}
