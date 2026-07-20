using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Engine.OpenCv.Tests;

public sealed class OpenCvTargetModelTrackerTests
{
    [Fact]
    public void Tracker_should_follow_rotated_scaled_target_and_report_current_shape()
    {
        using var reference = CreateTargetFrame(120, 90, 35, 35, 1, 0, "reference");
        using var current = CreateTargetFrame(120, 90, 45, 41, 1.2, 10, "current");
        var profile = CreateProfile();
        using var model = new OpenCvTargetModelFactory().Create(
            reference.Frame,
            TargetSelection.FromBounds(new RoiRect(15, 17, 42, 38)),
            profile);
        using var tracker = new OpenCvTargetModelTracker(profile);
        tracker.Initialize(reference.Frame, model);

        var result = tracker.Update(current.Frame);

        Assert.Equal(TargetTrackingStatus.Tracking, result.Status);
        Assert.NotNull(result.Shape);
        Assert.InRange(result.Shape.Center.X, 43, 47);
        Assert.InRange(result.Shape.Center.Y, 39, 43);
        Assert.True(result.Shape.MeasureAxisPixelSize > model.ReferenceShape.MeasureAxisPixelSize);
        Assert.True(result.Confidence >= profile.MinimumConfidence);
        Assert.NotEqual(model.ReferenceShape.Bounds, result.Shape.Bounds);
    }

    [Fact]
    public void Tracker_should_expose_lost_states_without_reusing_stale_shape_and_reacquire_globally()
    {
        using var reference = CreateTargetFrame(160, 110, 30, 30, 1, 0, "reference");
        using var blank = CreateBlankFrame(160, 110, "blank");
        using var reappeared = CreateTargetFrame(160, 110, 125, 78, 1, -8, "reappeared");
        var profile = CreateProfile();
        using var model = new OpenCvTargetModelFactory().Create(
            reference.Frame,
            TargetSelection.FromBounds(new RoiRect(10, 12, 42, 38)),
            profile);
        using var tracker = new OpenCvTargetModelTracker(profile);
        tracker.Initialize(reference.Frame, model);

        var first = tracker.Update(blank.Frame);
        var second = tracker.Update(blank.Frame);
        var third = tracker.Update(blank.Frame);
        var recovered = tracker.Update(reappeared.Frame);

        Assert.Equal(TargetTrackingStatus.TemporarilyLost, first.Status);
        Assert.Equal(TargetTrackingStatus.TemporarilyLost, second.Status);
        Assert.Equal(TargetTrackingStatus.Reacquiring, third.Status);
        Assert.Null(first.Shape);
        Assert.Null(second.Shape);
        Assert.Null(third.Shape);
        Assert.Equal(TargetTrackingStatus.Tracking, recovered.Status);
        Assert.NotNull(recovered.Shape);
        Assert.InRange(recovered.Shape.Center.X, 122, 128);
        Assert.InRange(recovered.Shape.Center.Y, 75, 81);
    }

    [Fact]
    public void Tracker_should_finish_as_not_found_after_bounded_reacquisition()
    {
        using var reference = CreateTargetFrame(100, 80, 30, 30, 1, 0, "reference");
        using var blank = CreateBlankFrame(100, 80, "blank");
        var profile = CreateProfile();
        using var model = new OpenCvTargetModelFactory().Create(
            reference.Frame,
            TargetSelection.FromBounds(new RoiRect(10, 12, 42, 38)),
            profile);
        using var tracker = new OpenCvTargetModelTracker(profile);
        tracker.Initialize(reference.Frame, model);

        TargetTrackingResult? result = null;
        for (var index = 0; index < 9; index++)
        {
            result = tracker.Update(blank.Frame);
        }

        Assert.NotNull(result);
        Assert.Equal(TargetTrackingStatus.NotFound, result.Status);
        Assert.Null(result.Shape);
        Assert.Equal(0, result.Confidence);

        using var reappeared = CreateTargetFrame(100, 80, 65, 50, 1, 0, "too-late");
        var terminal = tracker.Update(reappeared.Frame);
        Assert.Equal(TargetTrackingStatus.NotFound, terminal.Status);
        Assert.Null(terminal.Shape);
    }

    [Fact]
    public void Global_reacquisition_should_not_switch_to_a_same_shape_different_appearance()
    {
        using var reference = CreateTargetFrame(180, 110, 30, 30, 1, 0, "reference");
        using var blank = CreateBlankFrame(180, 110, "blank");
        using var scene = CreateTargetAndDistractorFrame();
        var profile = CreateProfile();
        using var model = new OpenCvTargetModelFactory().Create(
            reference.Frame,
            TargetSelection.FromBounds(new RoiRect(10, 12, 42, 38)),
            profile);
        using var tracker = new OpenCvTargetModelTracker(profile);
        tracker.Initialize(reference.Frame, model);
        tracker.Update(blank.Frame);
        tracker.Update(blank.Frame);
        tracker.Update(blank.Frame);

        var result = tracker.Update(scene.Frame);

        Assert.Equal(TargetTrackingStatus.Tracking, result.Status);
        Assert.NotNull(result.Shape);
        Assert.InRange(result.Shape.Center.X, 127, 133);
    }

    [Fact]
    public void Tracker_should_own_its_cache_without_owning_the_registered_model()
    {
        using var reference = CreateTargetFrame(100, 80, 30, 30, 1, 0, "reference");
        var profile = CreateProfile();
        var model = new OpenCvTargetModelFactory().Create(
            reference.Frame,
            TargetSelection.FromBounds(new RoiRect(10, 12, 42, 38)),
            profile);
        var tracker = new OpenCvTargetModelTracker(profile);
        tracker.Initialize(reference.Frame, model);

        tracker.Dispose();
        tracker.Dispose();
        using var preview = model.AcquireReferencePreview();

        Assert.NotEmpty(preview.Frame.Data.ToArray());
        Assert.Throws<ObjectDisposedException>(() => tracker.Update(reference.Frame));
        model.Dispose();
    }

    [Fact]
    public void Tracker_should_continue_from_its_own_cache_after_model_is_disposed()
    {
        using var reference = CreateTargetFrame(100, 80, 30, 30, 1, 0, "reference");
        using var current = CreateTargetFrame(100, 80, 35, 34, 1.1, 5, "current");
        var profile = CreateProfile();
        var model = new OpenCvTargetModelFactory().Create(
            reference.Frame,
            TargetSelection.FromBounds(new RoiRect(10, 12, 42, 38)),
            profile);
        using var tracker = new OpenCvTargetModelTracker(profile);
        tracker.Initialize(reference.Frame, model);

        model.Dispose();
        var result = tracker.Update(current.Frame);

        Assert.Equal(TargetTrackingStatus.Tracking, result.Status);
        Assert.NotNull(result.Shape);
    }

    [Fact]
    public void Tracker_should_reject_foreign_models_and_profile_mismatch()
    {
        using var reference = CreateTargetFrame(100, 80, 30, 30, 1, 0, "reference");
        var profile = CreateProfile();
        using var model = new OpenCvTargetModelFactory().Create(
            reference.Frame,
            TargetSelection.FromBounds(new RoiRect(10, 12, 42, 38)),
            profile);
        using var mismatched = new OpenCvTargetModelTracker(DetectionProfile.CreateDefault());

        var exception = Assert.Throws<VisionException>(() => mismatched.Initialize(reference.Frame, model));

        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
    }

    private static IImageLease CreateTargetFrame(
        int width,
        int height,
        double centerX,
        double centerY,
        double scale,
        double angleDegrees,
        string sourceId)
    {
        using var image = new Mat(new Size(width, height), MatType.CV_8UC3, Scalar.Black);
        DrawTarget(image, centerX, centerY, scale, angleDegrees, new Scalar(50, 190, 250));
        return new OpenCvImageAdapter().FromMat(
            image,
            new FrameInfo(1, DateTime.UnixEpoch, sourceId, new ImageSize(width, height)));
    }

    private static IImageLease CreateTargetAndDistractorFrame()
    {
        using var image = new Mat(new Size(180, 110), MatType.CV_8UC3, Scalar.Black);
        DrawTarget(image, 55, 65, 1, 0, Scalar.White);
        DrawTarget(image, 130, 65, 1, -8, new Scalar(50, 190, 250));
        return new OpenCvImageAdapter().FromMat(
            image,
            new FrameInfo(1, DateTime.UnixEpoch, "target-and-distractor", new ImageSize(180, 110)));
    }

    private static void DrawTarget(
        Mat image,
        double centerX,
        double centerY,
        double scale,
        double angleDegrees,
        Scalar color)
    {
        var radians = angleDegrees * Math.PI / 180d;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        Point[] template =
        [
            new(-14, -10),
            new(8, -10),
            new(14, -3),
            new(4, 2),
            new(10, 12),
            new(-12, 8)
        ];
        var points = template.Select(point => new Point(
            checked((int)Math.Round(centerX + scale * (point.X * cosine - point.Y * sine))),
            checked((int)Math.Round(centerY + scale * (point.X * sine + point.Y * cosine))))).ToArray();
        Cv2.FillPoly(image, [points], color);
        Cv2.Circle(
            image,
            new Point((int)Math.Round(centerX - 5 * scale), (int)Math.Round(centerY - 3 * scale)),
            Math.Max(1, (int)Math.Round(2 * scale)),
            Scalar.White,
            -1);
    }

    private static IImageLease CreateBlankFrame(int width, int height, string sourceId)
    {
        using var image = new Mat(new Size(width, height), MatType.CV_8UC3, Scalar.Black);
        return new OpenCvImageAdapter().FromMat(
            image,
            new FrameInfo(1, DateTime.UnixEpoch, sourceId, new ImageSize(width, height)));
    }

    private static DetectionProfile CreateProfile() => new(
        "target-tracking-test",
        ThresholdMode.Otsu,
        128,
        ForegroundPolarity.Bright,
        3,
        3,
        0.005,
        0.95,
        0.1,
        10,
        1,
        0.5,
        PixelMeasureAxis.Horizontal,
        10);
}
