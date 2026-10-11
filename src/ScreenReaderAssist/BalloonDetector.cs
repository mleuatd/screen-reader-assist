using System.Drawing;
namespace ScreenReaderAssist;

/// <summary>Experimental light-area flood-fill; NOT semantic bubble detection.</summary>
public static class BalloonDetector
{
    public static Rectangle Estimate(Bitmap bmp, Point click)
    {
        if (click.X < 0 || click.Y < 0 || click.X >= bmp.Width || click.Y >= bmp.Height) throw new ArgumentOutOfRangeException(nameof(click));
        // Find a near-white pixel inside the clicked balloon, even if the click lands on text.
        Point? seed = null;
        for (int radius=0; radius<=24 && seed is null; radius++)
          for (int dy=-radius; dy<=radius && seed is null; dy++)
            for (int dx=-radius; dx<=radius; dx++) {
                int x=click.X+dx,y=click.Y+dy;
                if (x>=0 && y>=0 && x<bmp.Width && y<bmp.Height && IsLight(bmp.GetPixel(x,y))) {seed=new Point(x,y);break;}
            }
        if (seed is null) return Fallback(bmp, click);
        int stride = bmp.Width;
        var visited = new HashSet<int>();
        var q = new Queue<Point>(); q.Enqueue(seed.Value);
        visited.Add(seed.Value.Y * stride + seed.Value.X);
        int minX=seed.Value.X,maxX=minX,minY=seed.Value.Y,maxY=minY;
        int count=0;
        int cap=Math.Min(1_000_000, bmp.Width*bmp.Height/2);
        while(q.Count>0 && count<cap) {
            var p=q.Dequeue(); count++;
            minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);minY=Math.Min(minY,p.Y);maxY=Math.Max(maxY,p.Y);
            foreach(var (ox,oy) in new[]{(-1,0),(1,0),(0,-1),(0,1)}) {
                int nx=p.X+ox,ny=p.Y+oy;
                if(nx<0||ny<0||nx>=bmp.Width||ny>=bmp.Height) continue;
                int key=ny*stride+nx;
                if(!visited.Add(key)) continue;
                if(IsLight(bmp.GetPixel(nx,ny))) q.Enqueue(new Point(nx,ny));
            }
        }
        int w=maxX-minX+1,h=maxY-minY+1;
        // Reject page background, tiny regions and regions touching screen edge.
        if(count>=cap || w<45 || h<35 || w>bmp.Width*.7 || h>bmp.Height*.7 || minX==0 || minY==0 || maxX==bmp.Width-1 || maxY==bmp.Height-1)
            return Fallback(bmp,click);
        return Rectangle.Intersect(Rectangle.FromLTRB(minX-15,minY-15,maxX+16,maxY+16),new Rectangle(0,0,bmp.Width,bmp.Height));
    }
    private static bool IsLight(Color c) => c.R>215 && c.G>215 && c.B>215;
    private static Rectangle Fallback(Bitmap bmp,Point click) => Rectangle.Intersect(new Rectangle(click.X-180,click.Y-130,360,260),new Rectangle(0,0,bmp.Width,bmp.Height));
}
