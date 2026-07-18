using Visual.Abstractions.Contracts;

namespace Visual.VisionBase.Contracts;

public sealed class RoiValidator
{
    public RoiValidator(int minimumWidth = 2, int minimumHeight = 2)
    {
        if (minimumWidth <= 0 || minimumHeight <= 0)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Minimum ROI dimensions must be positive.");
        }

        MinimumWidth = minimumWidth;
        MinimumHeight = minimumHeight;
    }

    public int MinimumWidth { get; }

    public int MinimumHeight { get; }

    public RoiRect Validate(RoiRect roi, ImageSize imageSize)
    {
        var clamped = Clamp(roi, imageSize);
        if (clamped.Width < MinimumWidth || clamped.Height < MinimumHeight)
        {
            throw new VisionException(
                VisionErrorCode.InvalidInput,
                $"ROI must be at least {MinimumWidth}x{MinimumHeight} pixels after clamping.");
        }

        return clamped;
    }

    public RoiRect Clamp(RoiRect roi, ImageSize imageSize)
    {
        if (!imageSize.IsValid)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Image size is invalid.");
        }

        return roi.ClampTo(imageSize);
    }
}
