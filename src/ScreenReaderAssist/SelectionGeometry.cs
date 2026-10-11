using System.Drawing;
namespace ScreenReaderAssist;

/// <summary>Pure rectangle calculations shared by mouse and synthesized touch input.</summary>
public static class SelectionGeometry
{
    public static Rectangle FromDrag(Point start,Point end,Rectangle bounds)
    {
        var raw=Rectangle.FromLTRB(Math.Min(start.X,end.X),Math.Min(start.Y,end.Y),
            Math.Max(start.X,end.X),Math.Max(start.Y,end.Y));
        return Rectangle.Intersect(raw,bounds);
    }
    public static Rectangle AroundTap(Point point,Rectangle bounds,int width=360,int height=160)
    {
        if(bounds.Width<1 || bounds.Height<1) return Rectangle.Empty;
        var w=Math.Min(bounds.Width,Math.Max(1,width));
        var h=Math.Min(bounds.Height,Math.Max(1,height));
        var x=Math.Clamp(point.X-w/2,bounds.Left,bounds.Right-w);
        var y=Math.Clamp(point.Y-h/2,bounds.Top,bounds.Bottom-h);
        return new Rectangle(x,y,w,h);
    }
    public static Rectangle ToScreen(Rectangle local,Rectangle desktop)
        => new Rectangle(local.X+desktop.X,local.Y+desktop.Y,local.Width,local.Height);
}
