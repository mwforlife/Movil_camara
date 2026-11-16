using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MovilCamara.Services;

public static class ObsFrameEncoder
{
    public static byte[] NoSignal() => EncodeBitmap(BitmapSource.Create(640, 360, 96, 96,
        PixelFormats.Bgr32, null, new byte[640 * 360 * 4], 640 * 4));
    public static byte[] Encode(VideoFrame frame)
    {
        if (frame.Bgr32 == null && frame.Rotation == 0 && !frame.Mirrored) return frame.Jpeg;
        BitmapSource image;
        if (frame.Bgr32 != null)
            image = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr32, null, frame.Bgr32, frame.Width * 4);
        else
        {
            using var stream = new MemoryStream(frame.Jpeg, false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit();
            image = bitmap;
        }
        image.Freeze();
        if (frame.Rotation != 0) { image = new TransformedBitmap(image, new RotateTransform(frame.Rotation)); image.Freeze(); }
        if (frame.Mirrored) { image = new TransformedBitmap(image, new ScaleTransform(-1, 1)); image.Freeze(); }
        return EncodeBitmap(image);
    }
    private static byte[] EncodeBitmap(BitmapSource image)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
