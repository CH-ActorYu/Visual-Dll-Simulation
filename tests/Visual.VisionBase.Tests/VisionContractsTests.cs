using Visual.Abstractions.Contracts;
using Visual.Vision.Contracts;

namespace Visual.VisionBase.Tests;

public sealed class VisionContractsTests
{
    [Fact]
    public void Default_detection_profile_should_match_documented_baseline()
    {
        var profile = DetectionProfile.CreateDefault();

        Assert.Equal(ThresholdMode.Otsu, profile.ThresholdMode);
        Assert.Equal(ForegroundPolarity.Bright, profile.ForegroundPolarity);
        Assert.Equal(5, profile.BlurKernelSize);
        Assert.Equal(3, profile.MorphologyKernelSize);
        Assert.Equal(0.01, profile.MinAreaRatio);
        Assert.Equal(0.90, profile.MaxAreaRatio);
        Assert.Equal(PixelMeasureAxis.Horizontal, profile.MeasureAxis);
        Assert.Equal(10, profile.RedetectInterval);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(4, 3)]
    [InlineData(5, 2)]
    public void Detection_profile_should_reject_invalid_kernel_sizes(int blurKernel, int morphologyKernel)
    {
        var exception = Assert.Throws<VisionException>(() => CreateProfile(
            blurKernelSize: blurKernel,
            morphologyKernelSize: morphologyKernel));

        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
    }

    [Fact]
    public void Detection_profile_should_reject_reversed_area_range()
    {
        var exception = Assert.Throws<VisionException>(() => CreateProfile(minAreaRatio: 0.8, maxAreaRatio: 0.2));

        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
    }

    [Fact]
    public void Target_candidate_should_validate_pixel_size_and_confidence()
    {
        var invalidSize = Assert.Throws<VisionException>(() =>
            new TargetCandidate(new RoiRect(0, 0, 2, 2), new Point2D(1, 1), 0, 0.5));
        var invalidConfidence = Assert.Throws<VisionException>(() =>
            new TargetCandidate(new RoiRect(0, 0, 2, 2), new Point2D(1, 1), 2, 1.1));

        Assert.Equal(VisionErrorCode.InvalidInput, invalidSize.ErrorCode);
        Assert.Equal(VisionErrorCode.InvalidInput, invalidConfidence.ErrorCode);
    }

    [Fact]
    public void Engine_info_should_report_combined_capabilities()
    {
        var info = new VisionEngineInfo(
            "OpenCv",
            "OpenCV",
            new Version(1, 0),
            true,
            VisionLicenseState.NotRequired,
            VisionCapability.Detection | VisionCapability.Tracking);

        Assert.True(info.Supports(VisionCapability.Detection));
        Assert.True(info.Supports(VisionCapability.Detection | VisionCapability.Tracking));
        Assert.False(info.Supports(VisionCapability.Camera));
    }

    private static DetectionProfile CreateProfile(
        int blurKernelSize = 5,
        int morphologyKernelSize = 3,
        double minAreaRatio = 0.01,
        double maxAreaRatio = 0.9) => new(
            "test",
            ThresholdMode.Otsu,
            128,
            ForegroundPolarity.Bright,
            blurKernelSize,
            morphologyKernelSize,
            minAreaRatio,
            maxAreaRatio,
            0.1,
            10,
            2,
            0.5,
            PixelMeasureAxis.Horizontal,
            10);
}
