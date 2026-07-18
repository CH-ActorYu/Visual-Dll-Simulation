using System.Globalization;
using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.Vision.Contracts;

namespace Visual.App.ViewModels;

public sealed class CalibrationViewModel : ObservableObject
{
    private string _targetWidth = "100";
    private string _referencePixelWidth = "80";
    private string _referenceDistance = "1000";
    private string _minimumDistance = "100";
    private string _maximumDistance = "10000";

    public string TargetWidth { get => _targetWidth; set => SetProperty(ref _targetWidth, value); }
    public string ReferencePixelWidth { get => _referencePixelWidth; set => SetProperty(ref _referencePixelWidth, value); }
    public string ReferenceDistance { get => _referenceDistance; set => SetProperty(ref _referenceDistance, value); }
    public string MinimumDistance { get => _minimumDistance; set => SetProperty(ref _minimumDistance, value); }
    public string MaximumDistance { get => _maximumDistance; set => SetProperty(ref _maximumDistance, value); }

    public CalibrationInfo Create(string sourceId, ImageSize imageSize, DetectionProfile profile)
    {
        return new CalibrationInfo(
            ParsePositive(TargetWidth, "目标宽度"),
            ParsePositive(ReferencePixelWidth, "参照像素宽度"),
            ParsePositive(ReferenceDistance, "参照距离"),
            DistanceUnit.Mm,
            profile.MeasureAxis,
            sourceId,
            imageSize,
            profile.ProfileId,
            new DistanceRange(ParsePositive(MinimumDistance, "最小距离"), ParsePositive(MaximumDistance, "最大距离")));
    }

    private static double ParsePositive(string text, string name) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
        double.IsFinite(value) && value > 0
            ? value
            : throw new VisionException(VisionErrorCode.InvalidInput, $"{name}必须是正数。");
}
