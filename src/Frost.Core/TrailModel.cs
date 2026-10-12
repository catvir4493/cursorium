namespace Frost.Core;
public readonly record struct TrailPoint(double X,double Y,double Time,double Speed);
[Flags] public enum ClickButton {None=0,Left=1,Right=2,Middle=4}
public readonly record struct Halo(double X,double Y,double Time,ClickButton Button=ClickButton.Left);
public sealed class TrailModel
{
    private readonly List<TrailPoint> points=new(256);
    private readonly List<Halo> halos=new(8);
    public IReadOnlyList<TrailPoint> Points=>points;
    public IReadOnlyList<Halo> Halos=>halos;
    public bool HasContent=>points.Count>1||halos.Count>0;
    public void Add(double x,double y,double now,Settings settings)
    {
        if(points.Count>0)
        {
            var last=points[^1];double dx=x-last.X,dy=y-last.Y,distance=Math.Sqrt(dx*dx+dy*dy);
            if(distance<.4)return;
            double dt=now-last.Time;
            if(distance>Math.Max(700,settings.Length*4)||dt>settings.Duration){points.Clear();}
            else
            {
                var speed=distance/Math.Max(.001,dt/1000);points.Add(new(x,y,now,speed));
                TrimLength(settings.Length*(.38+.62*Math.Clamp(speed/1800,0,1)));
                if(points.Count>256)points.RemoveRange(0,points.Count-256);return;
            }
        }
        points.Add(new(x,y,now,0));
    }
    private void TrimLength(double limit)
    {
        double length=0;
        for(int i=points.Count-1;i>0;i--)
        {
            var a=points[i];var b=points[i-1];double dx=a.X-b.X,dy=a.Y-b.Y,segment=Math.Sqrt(dx*dx+dy*dy);
            if(length+segment>limit)
            {
                double ratio=(limit-length)/Math.Max(.001,segment);
                points[i-1]=new(a.X+(b.X-a.X)*ratio,a.Y+(b.Y-a.Y)*ratio,a.Time+(b.Time-a.Time)*ratio,b.Speed);
                if(i>1)points.RemoveRange(0,i-1);return;
            }
            length+=segment;
        }
    }
    public void Click(double x,double y,double now,ClickButton button=ClickButton.Left){if(halos.Count>=8)halos.RemoveAt(0);halos.Add(new(x,y,now,button));}
    public void Expire(double now,Settings settings)
    {
        for(int i=points.Count-1;i>=0;i--)if(now-points[i].Time>settings.Duration||!settings.TrailEnabled)points.RemoveAt(i);
        for(int i=halos.Count-1;i>=0;i--)if(now-halos[i].Time>settings.HaloDuration||!settings.ClickHalo||settings.ClickEffect=="off")halos.RemoveAt(i);
    }
    public void Clear(){points.Clear();halos.Clear();}
}
