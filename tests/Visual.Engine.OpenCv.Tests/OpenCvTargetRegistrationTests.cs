using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;
using PixelFormat = Visual.Image.Contracts.PixelFormat;

namespace Visual.Engine.OpenCv.Tests;

public sealed class OpenCvTargetRegistrationTests
{
    [Fact]
    public void Registration_should_extract_irregular_shape_and_transparent_preview()
    {
        using var frame = CreateScene("reference-file");
        var selection = new TargetSelection(
        [
            new Point2D(10, 10),
            new Point2D(80, 10),
            new Point2D(10, 70)
        ]);
        using var model = new OpenCvTargetModelFactory().Create(
            frame.Frame,
            selection,
            DetectionProfile.CreateDefault());
        using var preview = model.AcquireReferencePreview();

        Assert.Equal(OpenCvEngine.EngineId, model.EngineId);
        Assert.Equal(OpenCvTargetModel.CurrentModelVersion, model.ModelVersion);
        Assert.Equal(DetectionProfile.CreateDefault().ProfileId, model.DetectionProfileId);
        Assert.InRange(model.ReferenceShape.Bounds.X, 17, 19);
        Assert.InRange(model.ReferenceShape.Bounds.Y, 17, 19);
        Assert.InRange(model.ReferenceShape.Bounds.Width, 23, 27);
        Assert.True(model.ReferenceShape.Contour.Count >= 4);
        Assert.Equal(PixelFormat.Bgra32, preview.Frame.Format);
        Assert.Equal(model.ReferenceShape.Bounds.Width, preview.Frame.Info.Size.Width);
        Assert.Equal(model.ReferenceShape.Bounds.Height, preview.Frame.Info.Size.Height);

        var alpha = preview.Frame.Data.Span
            .ToArray()
            .Where((_, index) => index % 4 == 3)
            .ToArray();
        Assert.Contains((byte)0, alpha);
        Assert.Contains((byte)255, alpha);
        Assert.False(frame.IsReleased);
    }

    [Fact]
    public void Reference_image_and_video_frame_should_create_equivalent_models()
    {
        using var referenceImage = CreateScene("reference-file");
        using var videoFrame = CreateScene("video-frame");
        var selection = TargetSelection.FromBounds(new RoiRect(12, 12, 38, 38));
        var factory = new OpenCvTargetModelFactory();
        var profile = DetectionProfile.CreateDefault();

        using var referenceModel = factory.Create(referenceImage.Frame, selection, profile);
        using var videoModel = factory.Create(videoFrame.Frame, selection, profile);
        using var referencePreview = referenceModel.AcquireReferencePreview();
        using var videoPreview = videoModel.AcquireReferencePreview();

        Assert.Equal(referenceModel.ReferenceShape.Bounds, videoModel.ReferenceShape.Bounds);
        Assert.Equal(referenceModel.ReferenceShape.Center, videoModel.ReferenceShape.Center);
        Assert.Equal(referenceModel.ReferenceShape.MeasureAxisPixelSize, videoModel.ReferenceShape.MeasureAxisPixelSize);
        Assert.Equal(referenceModel.ReferenceShape.Contour, videoModel.ReferenceShape.Contour);
        Assert.True(referencePreview.Frame.Data.Span.SequenceEqual(videoPreview.Frame.Data.Span));
    }

    [Fact]
    public void Disposing_model_should_release_owned_preview_but_not_a_retained_preview()
    {
        using var frame = CreateScene("lifecycle");
        var pool = new ImageMemoryPool();
        var model = new OpenCvTargetModelFactory(new ImageFrameFactory(pool)).Create(
            frame.Frame,
            TargetSelection.FromBounds(new RoiRect(12, 12, 38, 38)),
            DetectionProfile.CreateDefault());
        var retainedPreview = model.AcquireReferencePreview();

        model.Dispose();
        model.Dispose();

        Assert.False(retainedPreview.IsReleased);
        Assert.NotEmpty(retainedPreview.Frame.Data.ToArray());
        Assert.Equal(1, pool.GetStatistics().Outstanding);
        retainedPreview.Dispose();
        Assert.Equal(0, pool.GetStatistics().Outstanding);
        Assert.Throws<ObjectDisposedException>(() => model.AcquireReferencePreview());
    }

    [Fact]
    public void Registration_should_reject_out_of_frame_selection_and_missing_target()
    {
        using var scene = CreateScene("invalid");
        using var blank = CreateBlank("blank");
        var factory = new OpenCvTargetModelFactory();
        var profile = DetectionProfile.CreateDefault();

        var outside = Assert.Throws<VisionException>(() => factory.Create(
            scene.Frame,
            TargetSelection.FromBounds(new RoiRect(80, 60, 30, 30)),
            profile));
        var missing = Assert.Throws<VisionException>(() => factory.Create(
            blank.Frame,
            TargetSelection.FromBounds(new RoiRect(10, 10, 60, 50)),
            profile));

        Assert.Equal(VisionErrorCode.InvalidInput, outside.ErrorCode);
        Assert.Equal(VisionErrorCode.ModuleSpecific, missing.ErrorCode);
    }

    [Fact]
    public void Registration_should_allow_a_target_tightly_filling_the_user_selection()
    {
        using var image = new Mat(new Size(80, 60), MatType.CV_8UC3, Scalar.Black);
        var targetBounds = new RoiRect(15, 10, 50, 40);
        Cv2.Rectangle(image, new Rect(15, 10, 50, 40), Scalar.White, -1);
        using var frame = new OpenCvImageAdapter().FromMat(
            image,
            new FrameInfo(1, DateTime.UnixEpoch, "tight", new ImageSize(80, 60)));

        using var model = new OpenCvTargetModelFactory().Create(
            frame.Frame,
            TargetSelection.FromBounds(targetBounds),
            DetectionProfile.CreateDefault());

        Assert.Equal(targetBounds, model.ReferenceShape.Bounds);
    }

    private static IImageLease CreateScene(string sourceId)
    {
        using var image = new Mat(new Size(100, 80), MatType.CV_8UC3, Scalar.Black);
        Cv2.Circle(image, new Point(30, 30), 12, new Scalar(40, 180, 250), -1);
        Cv2.Rectangle(image, new Rect(62, 50, 15, 12), Scalar.White, -1);
        return new OpenCvImageAdapter().FromMat(
            image,
            new FrameInfo(7, DateTime.UnixEpoch, sourceId, new ImageSize(100, 80)));
    }

    private static IImageLease CreateBlank(string sourceId)
    {
        using var image = new Mat(new Size(100, 80), MatType.CV_8UC3, Scalar.Black);
        return new OpenCvImageAdapter().FromMat(
            image,
            new FrameInfo(7, DateTime.UnixEpoch, sourceId, new ImageSize(100, 80)));
    }
}
