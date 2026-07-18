using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;

namespace Visual.AppCore.Interaction;

public sealed record OverlaySnapshot(
    RoiRect? Selection,
    RoiRect? TargetBounds,
    Point2D? TargetCenter,
    string DistanceText,
    VisionResultStatus? ResultStatus);

public sealed record ViewportOverlay(
    double SelectionX,
    double SelectionY,
    double SelectionWidth,
    double SelectionHeight,
    bool HasSelection,
    double TargetX,
    double TargetY,
    double TargetWidth,
    double TargetHeight,
    bool HasTarget,
    double CenterX,
    double CenterY,
    bool HasCenter,
    string DistanceText);

public sealed class OverlayRenderer
{
    public OverlaySnapshot Build(RoiRect? selection, DistanceMeasurement? measurement)
    {
        var text = measurement?.Distance is { } value
            ? $"{value:F1} {measurement.Unit}"
            : measurement?.Status == VisionResultStatus.NotDetected ? "未检出" : string.Empty;
        return new OverlaySnapshot(
            selection,
            measurement?.TargetBounds,
            measurement?.TargetCenter,
            text,
            measurement?.Status);
    }

    public ViewportOverlay Project(OverlaySnapshot snapshot, ViewportTransform transform)
    {
        var roi = snapshot.Selection;
        var topLeft = roi is { } value ? transform.ToViewport(new Point2D(value.X, value.Y)) : default;
        var target = snapshot.TargetBounds;
        var targetTopLeft = target is { } targetValue
            ? transform.ToViewport(new Point2D(targetValue.X, targetValue.Y))
            : default;
        var center = snapshot.TargetCenter is { } point ? transform.ToViewport(point) : default;
        return new ViewportOverlay(
            topLeft.X,
            topLeft.Y,
            roi?.Width * transform.Scale ?? 0,
            roi?.Height * transform.Scale ?? 0,
            roi is not null,
            targetTopLeft.X,
            targetTopLeft.Y,
            target?.Width * transform.Scale ?? 0,
            target?.Height * transform.Scale ?? 0,
            target is not null,
            center.X,
            center.Y,
            snapshot.TargetCenter is not null,
            snapshot.DistanceText);
    }
}
