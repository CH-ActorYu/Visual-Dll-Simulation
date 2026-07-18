using Visual.Abstractions.Contracts;

namespace Visual.Image.Contracts;

public sealed class ImageView
{
    public ImageView(IImageLease ownerLease)
    {
        if (ownerLease is null)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "An owner lease is required for an image view.");
        }

        var frame = ownerLease.Frame;
        OwnerLease = ownerLease;
        Offset = 0;
        Width = frame.Info.Size.Width;
        Height = frame.Info.Size.Height;
        Stride = frame.Stride;
    }

    private ImageView(IImageLease ownerLease, int offset, int width, int height, int stride)
    {
        OwnerLease = ownerLease;
        Offset = offset;
        Width = width;
        Height = height;
        Stride = stride;
    }

    public IImageLease OwnerLease { get; }

    public int Offset { get; }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public PixelFormat Format => OwnerLease.Frame.Format;

    public ReadOnlyMemory<byte> Data
    {
        get
        {
            var frame = OwnerLease.Frame;
            var rowBytes = checked(Width * frame.Format.BytesPerPixel());
            var length = checked((Height - 1) * Stride + rowBytes);
            return frame.Data.Slice(Offset, length);
        }
    }

    public ImageView Crop(RoiRect roi)
    {
        var cropped = roi.ClampTo(Width, Height);
        var bytesPerPixel = Format.BytesPerPixel();
        var offset = checked(Offset + cropped.Y * Stride + cropped.X * bytesPerPixel);
        return new ImageView(OwnerLease, offset, cropped.Width, cropped.Height, Stride);
    }
}
