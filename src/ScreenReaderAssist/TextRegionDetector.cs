using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ScreenReaderAssist;

/// <summary>
/// Lightweight local estimate of text-like rectangles. This is not a trained text detector;
/// OCR must still confirm the content, and unreadable pixels are never guessed.
/// </summary>
public static class TextRegionDetector
{
    private const int CellWidth=24, CellHeight=18;

    public static IReadOnlyList<Rectangle> Propose(Bitmap image,Point pointer,int limit=2)
    {
        if(limit<1 || image.Width<32 || image.Height<20) return Array.Empty<Rectangle>();
        var w=(image.Width+CellWidth-1)/CellWidth;
        var h=(image.Height+CellHeight-1)/CellHeight;
        var active=new bool[w*h];
        for(int cy=0;cy<h;cy++)
        for(int cx=0;cx<w;cx++)
        {
            int x0=cx*CellWidth, y0=cy*CellHeight;
            int x1=Math.Min(image.Width,x0+CellWidth), y1=Math.Min(image.Height,y0+CellHeight);
            int edges=0, samples=0;
            for(int y=Math.Max(1,y0+1);y<y1-1;y+=2)
            for(int x=Math.Max(1,x0+1);x<x1-1;x+=2)
            {
                var center=Brightness(image.GetPixel(x,y));
                var left=Brightness(image.GetPixel(x-1,y));
                var above=Brightness(image.GetPixel(x,y-1));
                if(Math.Abs(center-left)>28 || Math.Abs(center-above)>28)edges++;
                samples++;
            }
            var density=samples==0 ? 0 : (double)edges/samples;
            active[cy*w+cx]=density>=0.035 && density<=0.78;
        }
        var visited=new bool[active.Length];
        var proposals=new List<(Rectangle region,double score)>();
        for(int start=0;start<active.Length;start++)
        {
            if(visited[start] || !active[start])continue;
            var queue=new Queue<int>();
            queue.Enqueue(start);visited[start]=true;
            int minX=w,maxX=-1,minY=h,maxY=-1,count=0;
            while(queue.Count>0)
            {
                int index=queue.Dequeue(),cx=index%w,cy=index/w;
                count++;
                minX=Math.Min(minX,cx);maxX=Math.Max(maxX,cx);
                minY=Math.Min(minY,cy);maxY=Math.Max(maxY,cy);
                // One empty cell between letters/words must not split a text line.
                for(int dy=-1;dy<=1;dy++)
                for(int dx=-2;dx<=2;dx++)
                {
                    int nx=cx+dx,ny=cy+dy;
                    if(nx<0||nx>=w||ny<0||ny>=h)continue;
                    int next=ny*w+nx;
                    if(!active[next] || visited[next])continue;
                    visited[next]=true;queue.Enqueue(next);
                }
            }
            if(count<2 || count>w*h*0.85)continue;
            var box=Rectangle.Intersect(
                Rectangle.FromLTRB(minX*CellWidth-14,minY*CellHeight-12,
                    (maxX+1)*CellWidth+14,(maxY+1)*CellHeight+12),
                new Rectangle(0,0,image.Width,image.Height));
            if(box.Width<36 || box.Height<24)continue;
            // Long text strips and vertical bubbles are both accepted.
            if(box.Width>Math.Min(image.Width,580) || box.Height>Math.Min(image.Height,490))continue;
            double distanceX=Math.Max(box.Left-pointer.X,Math.Max(0,pointer.X-box.Right));
            double distanceY=Math.Max(box.Top-pointer.Y,Math.Max(0,pointer.Y-box.Bottom));
            double distance=Math.Sqrt(distanceX*distanceX+distanceY*distanceY);
            if(distance>Math.Max(160,Math.Min(320,Math.Max(image.Width,image.Height)*0.55)))continue;
            proposals.Add((box,distance+0.0001*box.Width*box.Height));
        }
        var selected=new List<Rectangle>();
        foreach(var item in proposals.OrderBy(p=>p.score))
        {
            if(selected.Any(s=>Rectangle.Intersect(s,item.region).Width*Rectangle.Intersect(s,item.region).Height > item.region.Width*item.region.Height*.75))continue;
            selected.Add(item.region);
            if(selected.Count==limit)break;
        }
        return selected;
    }

    public static Bitmap EnhanceForOcr(Bitmap image)
    {
        int zoom=Math.Min(image.Width*2<=1600 && image.Height*2<=1600 ? 2 : 1,2);
        var result=new Bitmap(image.Width*zoom,image.Height*zoom,PixelFormat.Format32bppArgb);
        try {
            using var g=Graphics.FromImage(result);
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            using var attributes=new ImageAttributes();
            const float contrast=1.55f;
            const float offset=-0.275f;
            attributes.SetColorMatrix(new ColorMatrix(new float[][] {
                new float[]{contrast,0,0,0,0},
                new float[]{0,contrast,0,0,0},
                new float[]{0,0,contrast,0,0},
                new float[]{0,0,0,1,0},
                new float[]{offset,offset,offset,0,1}
            }));
            g.DrawImage(image,new Rectangle(0,0,result.Width,result.Height),
                0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);
            return result;
        } catch { result.Dispose(); throw; }
    }

    private static int Brightness(Color p)=>(p.R*77+p.G*150+p.B*29)>>8;
}
