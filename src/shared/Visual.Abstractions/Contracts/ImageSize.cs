using System.Text.Json.Serialization;

namespace Visual.Abstractions.Contracts;

public readonly record struct ImageSize
{
    [JsonConstructor]
    public ImageSize(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new VisionException(
                VisionErrorCode.InvalidInput,
                $"Image dimensions must be positive. Received {width}x{height}.");
        }

        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public long PixelCount => (long)Width * Height;

    public bool IsValid => Width > 0 && Height > 0;
}
