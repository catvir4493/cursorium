using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Frost.App.Presentation;

public static class UiMotion
{
    public static readonly DependencyProperty HoverEnabledProperty=DependencyProperty.RegisterAttached("HoverEnabled",typeof(bool),typeof(UiMotion),new PropertyMetadata(false,Changed));
    private static readonly List<WeakReference<FrameworkElement>> elements=new();
    public static bool Allowed {get;private set;}=SystemParameters.ClientAreaAnimation;
    public static void SetHoverEnabled(DependencyObject item,bool value)=>item.SetValue(HoverEnabledProperty,value);
    public static bool GetHoverEnabled(DependencyObject item)=>(bool)item.GetValue(HoverEnabledProperty);
    private static void Changed(DependencyObject item,DependencyPropertyChangedEventArgs e)
    {
        if(item is not FrameworkElement element)return;
        if((bool)e.NewValue){element.RenderTransform=new TranslateTransform();element.MouseEnter+=Enter;element.MouseLeave+=Leave;elements.Add(new(element));}
        else{element.MouseEnter-=Enter;element.MouseLeave-=Leave;}
    }
    private static void Enter(object sender,System.Windows.Input.MouseEventArgs e)=>Move((FrameworkElement)sender,-1.5);
    private static void Leave(object sender,System.Windows.Input.MouseEventArgs e)=>Move((FrameworkElement)sender,0);
    private static void Move(FrameworkElement element,double target)
    {
        if(element.RenderTransform is not TranslateTransform transform)return;
        if(!Allowed){transform.BeginAnimation(TranslateTransform.YProperty,null);transform.Y=0;return;}
        transform.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(target,TimeSpan.FromMilliseconds(140)){EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseOut}});
    }
    public static void SetAllowed(bool value)
    {
        Allowed=value&&SystemParameters.ClientAreaAnimation;
        for(int i=elements.Count-1;i>=0;i--){if(!elements[i].TryGetTarget(out var element))elements.RemoveAt(i);else if(!Allowed)Move(element,0);}
    }
    public static void Reveal(UIElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=1;
        if(Allowed)element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.55,1,TimeSpan.FromMilliseconds(180)));
    }
}
