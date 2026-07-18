using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.Engine.OpenCv.Contracts;
using Visual.Vision.Contracts;

const int expectedFrames = 30;
var videoPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"visual-s4-{Guid.NewGuid():N}.avi");

try
{
    CreateVideo(videoPath, expectedFrames);
    var profile = DetectionProfile.CreateDefault();
    var engine = new OpenCvEngineProvider().Get(OpenCvEngine.EngineId);
    await using var source = new OpenCvVideoFileSource("s4-dll-consumer", videoPath);
    await source.OpenAsync();
    await using var service = new DistanceServiceBuilder()
        .WithEngine(engine)
        .WithDetectionProfile(profile)
        .Build();
    service.SetCalibration(new CalibrationInfo(
        100,
        80,
        1000,
        DistanceUnit.Mm,
        profile.MeasureAxis,
        source.SourceId,
        new ImageSize(source.Width, source.Height),
        profile.ProfileId,
        new DistanceRange(100, 10000)));

    var target = new TargetRegion("s4-target", new RoiRect(20, 20, source.Width - 40, source.Height - 40));
    var measurements = new List<DistanceMeasurement>();
    await foreach (var batch in service.MeasureStreamAsync(source, [target]))
    {
        measurements.Add(batch[0]);
        if (measurements.Count == expectedFrames)
        {
            break;
        }
    }

    var valid = measurements.Where(item => item.Status == VisionResultStatus.Valid).ToArray();
    var average = valid.Average(item => item.Distance!.Value);
    var references = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(assembly => assembly.GetReferencedAssemblies())
        .Select(name => name.Name)
        .Where(name => name is not null)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var uiReference = references.FirstOrDefault(name =>
        name!.StartsWith("Presentation", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("WindowsBase", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("System.Windows.Forms", StringComparison.OrdinalIgnoreCase));

    Console.WriteLine($"S4 frames={measurements.Count} valid={valid.Length} averageMm={average:F2} uiReference={uiReference ?? "none"}");
    return measurements.Count == expectedFrames && valid.Length == expectedFrames &&
           Math.Abs(average - 1000) <= 0.01 && uiReference is null
        ? 0
        : 2;
}
finally
{
    if (File.Exists(videoPath))
    {
        File.Delete(videoPath);
    }
}

static void CreateVideo(string path, int frameCount)
{
    using var writer = new VideoWriter(path, FourCC.MJPG, 30, new Size(320, 240));
    if (!writer.IsOpened())
    {
        throw new InvalidOperationException("Could not create S4 acceptance video.");
    }

    for (var index = 0; index < frameCount; index++)
    {
        using var frame = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(frame, new Rect(120 + index / 3, 90, 80, 60), Scalar.White, -1);
        writer.Write(frame);
    }
}
