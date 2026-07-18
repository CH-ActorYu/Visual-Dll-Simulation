using Visual.Abstractions.Contracts;
using Visual.IO.Contracts;

namespace Visual.Distance.Contracts;

public interface IDistanceService : IVisionModule, IAsyncDisposable
{
    bool IsCalibrated { get; }

    void SetCalibration(CalibrationInfo calibration);

    ValueTask<IReadOnlyList<DistanceMeasurement>> MeasureAsync(
        DistanceRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<IReadOnlyList<DistanceMeasurement>> MeasureStreamAsync(
        IFrameSource source,
        IReadOnlyList<TargetRegion> targets,
        CancellationToken cancellationToken = default);
}
