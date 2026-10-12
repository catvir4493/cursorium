namespace Frost.Core;

public sealed class ClickEffectManager
{
    private readonly TrailModel model;
    public ClickEffectManager(TrailModel model)=>this.model=model;
    public static ClickButton DecodeRawButtons(ushort flags)
    {
        var result=ClickButton.None;
        if((flags&1)!=0)result|=ClickButton.Left;
        if((flags&4)!=0)result|=ClickButton.Right;
        if((flags&16)!=0)result|=ClickButton.Middle;
        return result;
    }
    public void Observe(ClickButton buttons,double x,double y,double now,Settings settings)
    {
        if(buttons==ClickButton.None||!settings.Enabled||!settings.ClickHalo||settings.ClickEffect=="off")return;
        if((buttons&ClickButton.Left)!=0)model.Click(x,y,now,ClickButton.Left);
        if((buttons&ClickButton.Right)!=0)model.Click(x,y,now,ClickButton.Right);
        if((buttons&ClickButton.Middle)!=0)model.Click(x,y,now,ClickButton.Middle);
    }
}
