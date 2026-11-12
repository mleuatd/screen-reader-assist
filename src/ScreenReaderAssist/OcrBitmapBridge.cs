using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ScreenReaderAssist;

/// <summary>
/// Converts a GDI+ screenshot to a Windows OCR-compatible bitmap.
/// Opaque 32bpp screenshots take the direct BGRA path. Other formats keep the
/// original PNG-decode path to preserve their previous color/alpha behavior.
/// </summary>
public static class OcrBitmapBridge
{
    public static async Task<SoftwareBitmap> CreateAsync(Bitmap image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if(image.PixelFormat is PixelFormat.Format32bppArgb or PixelFormat.Format32bppRgb)
        {
            try
            {
                var direct=CreateOpaque32bpp(image);
                if(direct is not null) return direct;
            }
            catch(ExternalException)
            {
                // GDI+ could not lock the bitmap. Keep the original behavior.
            }
            catch(COMException)
            {
                // Some Windows runtimes cannot use the direct buffer API.
            }
            catch(NotSupportedException)
            {
                // Preserve the original image-conversion implementation.
            }
        }
        return await CreateLegacyPngAsync(image);
    }

    /// <summary>
    /// Former conversion path; retained for unsupported bitmaps and regression
    /// comparisons. Never modifies the source image or its metadata.
    /// </summary>
    public static async Task<SoftwareBitmap> CreateLegacyPngAsync(Bitmap image)
    {
        using var ms=new MemoryStream();
        image.Save(ms,ImageFormat.Png);
        using var stream=new InMemoryRandomAccessStream();
        using(var writer=new DataWriter(stream))
        {
            writer.WriteBytes(ms.ToArray());
            await writer.StoreAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        var decoder=await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied);
    }

    private static SoftwareBitmap? CreateOpaque32bpp(Bitmap image)
    {
        var rowBytes=checked(image.Width*4);
        var bytes=new byte[checked(rowBytes*image.Height)];
        BitmapData? data=null;
        bool fullyOpaque=true;
        try
        {
            data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),
                ImageLockMode.ReadOnly,image.PixelFormat);
            for(var y=0;y<image.Height;y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0,checked(y*data.Stride)),
                    bytes,y*rowBytes,rowBytes);
            }
        }
        finally
        {
            if(data is not null) image.UnlockBits(data);
        }
        // The GDI+ decoder composites/changes alpha as necessary. Only bypass it
        // when the pixels are opaque, ensuring no alpha regression.
        for(var i=3;i<bytes.Length;i+=4)
        {
            if(image.PixelFormat==PixelFormat.Format32bppRgb)
                bytes[i]=255;
            else if(bytes[i]!=255) { fullyOpaque=false;break; }
        }
        if(!fullyOpaque) return null;
        using var writer=new DataWriter();
        writer.WriteBytes(bytes);
        var pixelBuffer=writer.DetachBuffer();
        return SoftwareBitmap.CreateCopyFromBuffer(pixelBuffer,
            BitmapPixelFormat.Bgra8,image.Width,image.Height,BitmapAlphaMode.Premultiplied);
    }
}
