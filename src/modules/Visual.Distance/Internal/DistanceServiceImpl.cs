using System.Runtime.CompilerServices;
using System.Text.Json;
using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.IO.Contracts;
using Visual.Vision.Contracts;
using Visual.VisionBase.Contracts;

namespace Visual.Distance.Internal;

internal sealed class DistanceServiceImpl : IDistanceService
{
    private const int ConfigurationSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object _configurationSync = new();
    private readonly SemaphoreSlim _processingGate = new(1, 1);
    private readonly ITargetDetector _detector;
    private readonly DetectionProfile _profile;
    private readonly string _engineId;
    private readonly DetectionPipeline _detectionPipeline = new();
    private readonly RoiTargetTracker _tracking;
    private CalibrationInfo? _calibration;
    private int _isDisposed;

    public DistanceServiceImpl(
        ITargetDetector detector,
        Func<ITracker>? trackerFactory,
        DetectionProfile profile,
        string engineId)
    {
        _detector = detector ?? throw Invalid("A target detector is required.");
        _profile = profile ?? throw Invalid("A detection profile is required.");
        _engineId = string.IsNullOrWhiteSpace(engineId)
            ? throw Invalid("An engine ID is required.")
            : engineId;
        _tracking = new RoiTargetTracker(trackerFactory);
    }

    public string Name => "Distance";

    public Version Version { get; } = new(1, 0, 0);

    public bool IsCalibrated
    {
        get
        {
            lock (_configurationSync)
            {
                return _calibration is not null;
            }
        }
    }

    public void SetCalibration(CalibrationInfo calibration)
    {
        ThrowIfDisposed();
        ValidateCalibration(calibration);
        lock (_configurationSync)
        {
            _calibration = calibration;
        }
    }

    public void Configure(ModuleConfiguration configuration)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.SchemaVersion != ConfigurationSchemaVersion)
        {
            throw Invalid($"Unsupported distance configuration schema version {configuration.SchemaVersion}.");
        }

        if (!string.Equals(configuration.EngineId, _engineId, StringComparison.OrdinalIgnoreCase))
        {
            throw Invalid($"Configuration engine '{configuration.EngineId}' does not match '{_engineId}'.");
        }

        DistanceConfigurationPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<DistanceConfigurationPayload>(configuration.Json, JsonOptions)
                ?? throw Invalid("Distance configuration payload is empty.");
        }
        catch (VisionException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Distance configuration payload is invalid.", exception);
        }

        if (!string.Equals(payload.DetectionProfileId, _profile.ProfileId, StringComparison.Ordinal))
        {
            throw Invalid("Configuration detection profile does not match the active service profile.");
        }

        if (payload.Calibration is not null)
        {
            ValidateCalibration(payload.Calibration);
        }

        lock (_configurationSync)
        {
            _calibration = payload.Calibration;
        }
    }

    public ModuleConfiguration ExportConfig()
    {
        ThrowIfDisposed();
        CalibrationInfo? calibration;
        lock (_configurationSync)
        {
            calibration = _calibration;
        }

        var json = JsonSerializer.Serialize(
            new DistanceConfigurationPayload(_profile.ProfileId, calibration),
            JsonOptions);
        return new ModuleConfiguration(ConfigurationSchemaVersion, _engineId, json);
    }

    public async ValueTask<IReadOnlyList<DistanceMeasurement>> MeasureAsync(
        DistanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var calibration = GetCalibrationOrThrow();
        EnsureCompatible(calibration, request.Frame.Info);

        await _processingGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            var processingStartedAt = LaterOf(DateTime.UtcNow, request.Frame.Info.Timestamp);
            var trackers = _tracking.Prepare(request.Targets);
            var candidates = _detectionPipeline.Run(
                request.Frame,
                request.Targets.Select(target => target.Bounds).ToArray(),
                _detector,
                trackers,
                cancellationToken);
            var completedAt = LaterOf(DateTime.UtcNow, processingStartedAt);
            var timing = new FrameTiming(request.Frame.Info.Timestamp, processingStartedAt, completedAt);
            var model = MonocularCalibrator.Create(calibration);
            var results = new DistanceMeasurement[request.Targets.Count];

            for (var index = 0; index < request.Targets.Count; index++)
            {
                var target = request.Targets[index];
                var candidate = candidates[index];
                if (candidate is null)
                {
                    results[index] = DistanceResultBuilder.NotDetected(
                        target,
                        calibration.Unit,
                        request.Frame.Info,
                        timing);
                    continue;
                }

                var distance = DistanceConverter.Convert(model, candidate.PixelSize);
                results[index] = calibration.ValidDistanceRange.Contains(distance)
                    ? DistanceResultBuilder.Valid(
                        target,
                        candidate,
                        distance,
                        calibration.Unit,
                        request.Frame.Info,
                        timing)
                    : DistanceResultBuilder.Failed(
                        target,
                        calibration.Unit,
                        request.Frame.Info,
                        timing,
                        VisionErrorCode.ModuleSpecific);
            }

            return results;
        }
        finally
        {
            _processingGate.Release();
        }
    }

    public async IAsyncEnumerable<IReadOnlyList<DistanceMeasurement>> MeasureStreamAsync(
        IFrameSource source,
        IReadOnlyList<TargetRegion> targets,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(targets);
        DistanceRequest.ValidateTargets(targets);
        var targetSnapshot = targets.ToArray();
        var startedHere = !source.IsRunning;

        if (startedHere)
        {
            await source.OpenAsync(cancellationToken).ConfigureAwait(false);
            await source.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var frames = source.ReadFramesAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                bool hasFrame;
                try
                {
                    hasFrame = await frames.MoveNextAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (!hasFrame)
                {
                    yield break;
                }

                var lease = frames.Current;
                IReadOnlyList<DistanceMeasurement> results;
                using (lease)
                {
                    try
                    {
                        results = await MeasureAsync(
                            new DistanceRequest(lease.Frame, targetSnapshot),
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (NotCalibratedException)
                    {
                        results = CreateStatusResults(
                            lease.Frame.Info,
                            targetSnapshot,
                            VisionResultStatus.NotCalibrated,
                            null);
                    }
                    catch (VisionException exception) when (exception.ErrorCode != VisionErrorCode.InvalidInput)
                    {
                        var status = exception.ErrorCode is VisionErrorCode.EngineUnavailable or VisionErrorCode.DeviceLost
                            ? VisionResultStatus.Unavailable
                            : VisionResultStatus.Failed;
                        results = CreateStatusResults(
                            lease.Frame.Info,
                            targetSnapshot,
                            status,
                            exception.ErrorCode);
                    }
                }

                yield return results;
            }
        }
        finally
        {
            if (startedHere)
            {
                await source.StopAsync(CancellationToken.None).ConfigureAwait(false);
                await source.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        await _processingGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            _tracking.Dispose();
            lock (_configurationSync)
            {
                _calibration = null;
            }
        }
        finally
        {
            _processingGate.Release();
        }
    }

    private IReadOnlyList<DistanceMeasurement> CreateStatusResults(
        FrameInfo frame,
        IReadOnlyList<TargetRegion> targets,
        VisionResultStatus status,
        VisionErrorCode? errorCode)
    {
        CalibrationInfo? calibration;
        lock (_configurationSync)
        {
            calibration = _calibration;
        }

        var startedAt = LaterOf(DateTime.UtcNow, frame.Timestamp);
        var timing = new FrameTiming(frame.Timestamp, startedAt, startedAt);
        var unit = calibration?.Unit ?? DistanceUnit.Mm;
        return targets.Select(target => status switch
        {
            VisionResultStatus.NotCalibrated => DistanceResultBuilder.NotCalibrated(target, unit, frame, timing),
            VisionResultStatus.Unavailable => DistanceResultBuilder.Unavailable(
                target,
                unit,
                frame,
                timing,
                errorCode ?? VisionErrorCode.EngineUnavailable),
            _ => DistanceResultBuilder.Failed(
                target,
                unit,
                frame,
                timing,
                errorCode ?? VisionErrorCode.ModuleSpecific)
        }).ToArray();
    }

    private CalibrationInfo GetCalibrationOrThrow()
    {
        lock (_configurationSync)
        {
            return _calibration ?? throw new NotCalibratedException();
        }
    }

    private void EnsureCompatible(CalibrationInfo calibration, FrameInfo frame)
    {
        if (frame.Size == calibration.ImageSize &&
            string.Equals(frame.SourceId, calibration.CameraId, StringComparison.Ordinal))
        {
            return;
        }

        lock (_configurationSync)
        {
            if (ReferenceEquals(_calibration, calibration))
            {
                _calibration = null;
            }
        }

        throw new NotCalibratedException("Calibration is invalid for the current camera or image size.");
    }

    private void ValidateCalibration(CalibrationInfo calibration)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        _ = MonocularCalibrator.Create(calibration);
        if (!string.Equals(calibration.DetectionProfileId, _profile.ProfileId, StringComparison.Ordinal) ||
            calibration.MeasureAxis != _profile.MeasureAxis)
        {
            throw Invalid("Calibration profile or measure axis does not match the active detection profile.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
    }

    private static DateTime LaterOf(DateTime first, DateTime second) => first >= second ? first : second;

    private static VisionException Invalid(string message) => new(VisionErrorCode.InvalidInput, message);
}
