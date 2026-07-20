using Visual.Abstractions.Contracts;
using Visual.Vision.Contracts;

namespace Visual.VisionBase.Tests;

public sealed class TargetModelContractsTests
{
    [Fact]
    public void Selection_should_snapshot_polygon_and_derive_bounds()
    {
        var polygon = new List<Point2D>
        {
            new(10, 20),
            new(40, 20),
            new(35, 60),
            new(10, 55)
        };

        var selection = new TargetSelection(polygon);
        polygon.Clear();

        Assert.Equal(4, selection.Polygon.Count);
        Assert.Equal(new RoiRect(10, 20, 30, 40), selection.Bounds);
    }

    [Fact]
    public void Rectangular_selection_should_use_same_polygon_contract()
    {
        var bounds = new RoiRect(4, 6, 30, 20);

        var selection = TargetSelection.FromBounds(bounds);

        Assert.Equal(bounds, selection.Bounds);
        Assert.Equal(4, selection.Polygon.Count);
    }

    [Fact]
    public void Selection_should_reject_degenerate_or_negative_polygon()
    {
        var tooShort = Assert.Throws<VisionException>(() => new TargetSelection(
        [
            new Point2D(0, 0),
            new Point2D(10, 0)
        ]));
        var collinear = Assert.Throws<VisionException>(() => new TargetSelection(
        [
            new Point2D(0, 0),
            new Point2D(5, 5),
            new Point2D(10, 10)
        ]));
        var negative = Assert.Throws<VisionException>(() => new TargetSelection(
        [
            new Point2D(-1, 0),
            new Point2D(5, 0),
            new Point2D(5, 5)
        ]));

        Assert.All([tooShort, collinear, negative], exception =>
            Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode));
    }

    [Fact]
    public void Shape_should_retain_irregular_contour_and_measurement_axis()
    {
        var contour = new List<Point2D>
        {
            new(10, 10),
            new(30, 12),
            new(24, 28),
            new(12, 24)
        };

        var shape = new TargetShape(
            contour,
            new RoiRect(10, 10, 20, 18),
            new Point2D(19, 18),
            PixelMeasureAxis.MajorAxis,
            22.5);
        contour.Clear();

        Assert.Equal(4, shape.Contour.Count);
        Assert.Equal(PixelMeasureAxis.MajorAxis, shape.MeasureAxis);
        Assert.Equal(22.5, shape.MeasureAxisPixelSize);
    }

    [Fact]
    public void Shape_should_reject_contour_outside_bounds_and_invalid_pixel_size()
    {
        var outside = Assert.Throws<VisionException>(() => new TargetShape(
            [new Point2D(0, 0), new Point2D(20, 0), new Point2D(10, 10)],
            new RoiRect(0, 0, 10, 10),
            new Point2D(5, 5),
            PixelMeasureAxis.Horizontal,
            10));
        var invalidSize = Assert.Throws<VisionException>(() => new TargetShape(
            [new Point2D(0, 0), new Point2D(10, 0), new Point2D(5, 10)],
            new RoiRect(0, 0, 10, 10),
            new Point2D(5, 5),
            PixelMeasureAxis.Horizontal,
            0));
        var collinear = Assert.Throws<VisionException>(() => new TargetShape(
            [new Point2D(0, 0), new Point2D(5, 5), new Point2D(10, 10)],
            new RoiRect(0, 0, 10, 10),
            new Point2D(5, 5),
            PixelMeasureAxis.Horizontal,
            10));

        Assert.Equal(VisionErrorCode.InvalidInput, outside.ErrorCode);
        Assert.Equal(VisionErrorCode.InvalidInput, invalidSize.ErrorCode);
        Assert.Equal(VisionErrorCode.InvalidInput, collinear.ErrorCode);
    }

    [Fact]
    public void Tracking_result_should_only_expose_current_shape_while_tracking()
    {
        var shape = CreateShape();

        var tracking = new TargetTrackingResult(TargetTrackingStatus.Tracking, shape, 0.92);
        var lost = new TargetTrackingResult(TargetTrackingStatus.TemporarilyLost, null, 0.2);

        Assert.Same(shape, tracking.Shape);
        Assert.Null(lost.Shape);
        Assert.Throws<VisionException>(() =>
            new TargetTrackingResult(TargetTrackingStatus.Tracking, null, 0.5));
        Assert.Throws<VisionException>(() =>
            new TargetTrackingResult(TargetTrackingStatus.Reacquiring, shape, 0.5));
        Assert.Throws<VisionException>(() =>
            new TargetTrackingResult(TargetTrackingStatus.NotFound, null, 1.1));
    }

    [Fact]
    public void Target_model_and_tracker_contracts_should_have_explicit_release_semantics()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(ITargetModel)));
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(ITargetModelTracker)));
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(ITargetModelFactory)));
    }

    [Fact]
    public void Reference_image_and_video_frame_should_share_one_model_factory_entry()
    {
        var create = Assert.Single(typeof(ITargetModelFactory).GetMethods());
        var parameters = create.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Equal(nameof(ITargetModelFactory.Create), create.Name);
        Assert.Equal(
            [
                typeof(Visual.Image.Contracts.ImageFrame),
                typeof(TargetSelection),
                typeof(DetectionProfile)
            ],
            parameters);
        Assert.Equal(typeof(ITargetModel), create.ReturnType);
    }

    private static TargetShape CreateShape() => new(
        [new Point2D(0, 0), new Point2D(10, 0), new Point2D(5, 10)],
        new RoiRect(0, 0, 10, 10),
        new Point2D(5, 4),
        PixelMeasureAxis.Horizontal,
        10);
}
