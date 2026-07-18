using System.Diagnostics;
using System.Text.Json;
using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.Engine.OpenCv.Contracts;
using Visual.Vision.Contracts;

var options = RunnerOptions.Parse(args);
var outputPath = System.IO.Path.GetFullPath(options.OutputPath);
var outputDirectory = System.IO.Path.GetDirectoryName(outputPath)!;
Directory.CreateDirectory(outputDirectory);
var videoPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"visual-acceptance-{Guid.NewGuid():N}.avi");
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var processingTimes = new List<double>();
var samples = new List<ResourceSample>();
var process = Process.GetCurrentProcess();
var profile = DetectionProfile.CreateDefault();
var engine = new OpenCvEngineProvider().Get(OpenCvEngine.EngineId);
var stopwatch = Stopwatch.StartNew();
var measurementStartedAt = TimeSpan.FromSeconds(options.WarmupSeconds);
var measurementEndsAt = measurementStartedAt + TimeSpan.FromSeconds(options.DurationSeconds);
var nextSampleAt = measurementStartedAt;
long totalFrames = 0;
long measuredFrames = 0;
long validFrames = 0;
long notDetectedFrames = 0;
long failedFrames = 0;
long droppedFrames = 0;
var completed = false;

try
{
    CreateVideo(videoPath, options.Width, options.Height, options.Fps, options.VideoFrames);
    await using var service = new DistanceServiceBuilder()
        .WithEngine(engine)
        .WithDetectionProfile(profile)
        .Build();
    service.SetCalibration(new CalibrationInfo(
        options.TargetWidthMm,
        options.TargetPixelWidth,
        options.ReferenceDistanceMm,
        DistanceUnit.Mm,
        profile.MeasureAxis,
        options.SourceId,
        new ImageSize(options.Width, options.Height),
        profile.ProfileId,
        new DistanceRange(100, 10000)));
    var target = new TargetRegion(
        "acceptance-target",
        new RoiRect(20, 20, options.Width - 40, options.Height - 40));

    while (stopwatch.Elapsed < measurementEndsAt && !cancellation.IsCancellationRequested)
    {
        await using var source = new OpenCvVideoFileSource(options.SourceId, videoPath, bufferCapacity: 3);
        await foreach (var batch in service.MeasureStreamAsync(source, [target], cancellation.Token))
        {
            totalFrames++;
            var elapsed = stopwatch.Elapsed;
            if (elapsed >= measurementStartedAt)
            {
                var measurement = batch[0];
                measuredFrames++;
                processingTimes.Add(measurement.Timing.ProcessingDuration.TotalMilliseconds);
                switch (measurement.Status)
                {
                    case VisionResultStatus.Valid:
                        validFrames++;
                        break;
                    case VisionResultStatus.NotDetected:
                        notDetectedFrames++;
                        break;
                    default:
                        failedFrames++;
                        break;
                }

                if (elapsed >= nextSampleAt)
                {
                    process.Refresh();
                    var sample = new ResourceSample(
                        elapsed.TotalSeconds - options.WarmupSeconds,
                        GC.GetTotalMemory(false),
                        process.WorkingSet64,
                        process.PrivateMemorySize64,
                        measuredFrames,
                        droppedFrames + source.DroppedCount);
                    samples.Add(sample);
                    Console.WriteLine(
                        $"ACCEPTANCE_PROGRESS measured={sample.ElapsedSeconds:F0}s frames={measuredFrames} " +
                        $"workingSetMB={ToMb(sample.WorkingSetBytes):F1} privateMB={ToMb(sample.PrivateBytes):F1} " +
                        $"dropped={sample.DroppedFrames}");
                    nextSampleAt = elapsed + TimeSpan.FromSeconds(options.SampleSeconds);
                }
            }

            if (elapsed >= measurementEndsAt)
            {
                completed = true;
                break;
            }
        }

        droppedFrames += source.DroppedCount;
    }

    completed |= stopwatch.Elapsed >= measurementEndsAt;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
}
finally
{
    stopwatch.Stop();
    if (File.Exists(videoPath))
    {
        File.Delete(videoPath);
    }
}

process.Refresh();
var measuredSeconds = Math.Max(0, stopwatch.Elapsed.TotalSeconds - options.WarmupSeconds);
var sorted = processingTimes.Order().ToArray();
var baseline = samples.FirstOrDefault();
var final = samples.LastOrDefault();
var privateGrowthPercent = GrowthPercent(baseline?.PrivateBytes, final?.PrivateBytes);
var workingSetGrowthPercent = GrowthPercent(baseline?.WorkingSetBytes, final?.WorkingSetBytes);
var monotonicPrivateSampleCount = CountMonotonicIncreases(samples.Select(sample => sample.PrivateBytes));
var throughput = measuredSeconds > 0 ? measuredFrames / measuredSeconds : 0;
var meetsS7 = options.Mode == "performance" && completed && measuredSeconds >= 600 && throughput >= 15;
var meetsS8 = options.Mode == "stability" && completed && measuredSeconds >= 7200 &&
              privateGrowthPercent is < 10 && monotonicPrivateSampleCount < Math.Max(1, samples.Count - 1);
var report = new AcceptanceReport(
    DateTimeOffset.Now,
    options,
    completed,
    stopwatch.Elapsed.TotalSeconds,
    measuredSeconds,
    totalFrames,
    measuredFrames,
    validFrames,
    notDetectedFrames,
    failedFrames,
    droppedFrames,
    throughput,
    Average(sorted),
    Percentile(sorted, 0.95),
    Percentile(sorted, 0.99),
    privateGrowthPercent,
    workingSetGrowthPercent,
    monotonicPrivateSampleCount,
    meetsS7,
    meetsS8,
    samples);
await File.WriteAllTextAsync(
    outputPath,
    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine(
    $"ACCEPTANCE_RESULT mode={options.Mode} completed={completed} measuredSeconds={measuredSeconds:F1} " +
    $"throughput={throughput:F2} avgMs={report.AverageProcessingMilliseconds:F2} " +
    $"p95Ms={report.P95ProcessingMilliseconds:F2} p99Ms={report.P99ProcessingMilliseconds:F2} " +
    $"privateGrowth={privateGrowthPercent:F2}% workingSetGrowth={workingSetGrowthPercent:F2}% " +
    $"s7={meetsS7} s8={meetsS8} report={outputPath}");

return completed && measuredFrames > 0 && failedFrames == 0 ? 0 : 2;

static void CreateVideo(string path, int width, int height, double fps, int frameCount)
{
    using var writer = new VideoWriter(path, FourCC.MJPG, fps, new Size(width, height));
    if (!writer.IsOpened())
    {
        throw new InvalidOperationException("Could not create acceptance video.");
    }

    var targetWidth = Math.Max(32, width / 8);
    var targetHeight = Math.Max(24, height / 8);
    var travel = Math.Max(1, width - targetWidth - 80);
    for (var index = 0; index < frameCount; index++)
    {
        using var frame = new Mat(new Size(width, height), MatType.CV_8UC3, Scalar.Black);
        var x = 40 + index * 3 % travel;
        var y = height / 2 - targetHeight / 2;
        Cv2.Rectangle(frame, new Rect(x, y, targetWidth, targetHeight), Scalar.White, -1);
        writer.Write(frame);
    }
}

static double ToMb(long bytes) => bytes / 1024d / 1024d;

static double Average(double[] values) => values.Length == 0 ? 0 : values.Average();

static double Percentile(double[] values, double percentile)
{
    if (values.Length == 0)
    {
        return 0;
    }

    var index = Math.Clamp((int)Math.Ceiling(values.Length * percentile) - 1, 0, values.Length - 1);
    return values[index];
}

static double? GrowthPercent(long? baseline, long? final) =>
    baseline is > 0 && final is not null ? (final.Value - baseline.Value) * 100d / baseline.Value : null;

static int CountMonotonicIncreases(IEnumerable<long> values)
{
    var count = 0;
    long? previous = null;
    foreach (var value in values)
    {
        if (previous is not null && value > previous)
        {
            count++;
        }

        previous = value;
    }

    return count;
}

internal sealed record RunnerOptions(
    string Mode,
    double DurationSeconds,
    double WarmupSeconds,
    double SampleSeconds,
    string OutputPath,
    int Width,
    int Height,
    double Fps,
    int VideoFrames,
    string SourceId,
    double TargetWidthMm,
    double TargetPixelWidth,
    double ReferenceDistanceMm)
{
    public static RunnerOptions Parse(string[] arguments)
    {
        var values = arguments
            .Select((value, index) => (value, index))
            .Where(item => item.value.StartsWith("--", StringComparison.Ordinal))
            .ToDictionary(
                item => item.value[2..],
                item => item.index + 1 < arguments.Length ? arguments[item.index + 1] : string.Empty,
                StringComparer.OrdinalIgnoreCase);
        var mode = Read(values, "mode", "smoke").ToLowerInvariant();
        if (mode is not ("smoke" or "performance" or "stability"))
        {
            throw new ArgumentException("Mode must be smoke, performance or stability.");
        }

        var defaultDuration = mode switch { "performance" => 600, "stability" => 7200, _ => 15 };
        return new RunnerOptions(
            mode,
            ReadPositive(values, "duration-seconds", defaultDuration),
            ReadNonNegative(values, "warmup-seconds", mode == "smoke" ? 2 : 30),
            ReadPositive(values, "sample-seconds", mode == "smoke" ? 2 : 10),
            Read(values, "output", System.IO.Path.Combine("artifacts", "acceptance", $"{mode}.json")),
            ReadPositiveInt(values, "width", 1280),
            ReadPositiveInt(values, "height", 720),
            ReadPositive(values, "fps", 30),
            ReadPositiveInt(values, "video-frames", 90),
            "acceptance-video",
            100,
            160,
            1000);
    }

    private static string Read(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static double ReadPositive(IReadOnlyDictionary<string, string> values, string key, double fallback) =>
        values.TryGetValue(key, out var value) && double.TryParse(value, out var parsed) && double.IsFinite(parsed) && parsed > 0
            ? parsed
            : fallback;

    private static double ReadNonNegative(IReadOnlyDictionary<string, string> values, string key, double fallback) =>
        values.TryGetValue(key, out var value) && double.TryParse(value, out var parsed) && double.IsFinite(parsed) && parsed >= 0
            ? parsed
            : fallback;

    private static int ReadPositiveInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}

internal sealed record ResourceSample(
    double ElapsedSeconds,
    long ManagedHeapBytes,
    long WorkingSetBytes,
    long PrivateBytes,
    long Frames,
    long DroppedFrames);

internal sealed record AcceptanceReport(
    DateTimeOffset CreatedAt,
    RunnerOptions Options,
    bool Completed,
    double TotalSeconds,
    double MeasuredSeconds,
    long TotalFrames,
    long MeasuredFrames,
    long ValidFrames,
    long NotDetectedFrames,
    long FailedFrames,
    long DroppedFrames,
    double ThroughputPerSecond,
    double AverageProcessingMilliseconds,
    double P95ProcessingMilliseconds,
    double P99ProcessingMilliseconds,
    double? PrivateGrowthPercent,
    double? WorkingSetGrowthPercent,
    int MonotonicPrivateSampleCount,
    bool MeetsS7,
    bool MeetsS8,
    IReadOnlyList<ResourceSample> Samples);
