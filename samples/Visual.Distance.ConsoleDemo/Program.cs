using System.Globalization;
using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.Engine.OpenCv.Contracts;
using Visual.Vision.Contracts;

var generatedVideo = args.Length == 0;
var videoPath = generatedVideo ? CreateDemonstrationVideo() : Path.GetFullPath(args[0]);
var referencePixelWidth = ReadDouble(args, 1, 80);
var referenceDistance = ReadDouble(args, 2, 1000);
var targetPhysicalWidth = ReadDouble(args, 3, 100);
var maximumFrames = ReadInt(args, 4, 10);

try
{
    var engine = new OpenCvEngineProvider().Get(OpenCvEngine.EngineId);
    var profile = DetectionProfile.CreateDefault();
    await using var source = new OpenCvVideoFileSource("console-demo-video", videoPath);
    await source.OpenAsync();
    var margin = Math.Max(4, Math.Min(source.Width, source.Height) / 20);
    var target = new TargetRegion(
        "demo-target",
        new RoiRect(margin, margin, source.Width - margin * 2, source.Height - margin * 2));
    await using var service = new DistanceServiceBuilder()
        .WithEngine(engine)
        .WithDetectionProfile(profile)
        .Build();
    service.SetCalibration(new CalibrationInfo(
        targetPhysicalWidth,
        referencePixelWidth,
        referenceDistance,
        DistanceUnit.Mm,
        profile.MeasureAxis,
        source.SourceId,
        new ImageSize(source.Width, source.Height),
        profile.ProfileId,
        new DistanceRange(referenceDistance / 10, referenceDistance * 10)));

    var count = 0;
    await foreach (var measurements in service.MeasureStreamAsync(source, [target]))
    {
        var measurement = measurements[0];
        var distance = measurement.Distance is { } value
            ? $"{value:F2} {measurement.Unit}"
            : "--";
        Console.WriteLine(
            $"frame={measurement.FrameIndex,3} status={measurement.Status,-13} " +
            $"pixels={measurement.TargetPixelSize?.ToString("F2", CultureInfo.InvariantCulture) ?? "--",6} " +
            $"distance={distance}");
        if (++count >= maximumFrames)
        {
            break;
        }
    }

    return count > 0 ? 0 : 2;
}
catch (VisionException exception)
{
    Console.Error.WriteLine($"{exception.ErrorCode}: {exception.Message}");
    return 1;
}
finally
{
    if (generatedVideo && File.Exists(videoPath))
    {
        File.Delete(videoPath);
    }
}

static double ReadDouble(string[] arguments, int index, double fallback) =>
    arguments.Length > index &&
    double.TryParse(arguments[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
    double.IsFinite(value) && value > 0
        ? value
        : fallback;

static int ReadInt(string[] arguments, int index, int fallback) =>
    arguments.Length > index && int.TryParse(arguments[index], out var value) && value > 0
        ? value
        : fallback;

static string CreateDemonstrationVideo()
{
    var path = Path.Combine(Path.GetTempPath(), $"visual-distance-demo-{Guid.NewGuid():N}.avi");
    using var writer = new VideoWriter(path, FourCC.MJPG, 30, new Size(320, 240));
    if (!writer.IsOpened())
    {
        throw new VisionException(VisionErrorCode.DecodingFailed, "Could not create the demonstration video.");
    }

    for (var frameIndex = 0; frameIndex < 30; frameIndex++)
    {
        using var frame = new Mat(new Size(320, 240), MatType.CV_8UC3, Scalar.Black);
        var x = 120 + frameIndex / 3;
        Cv2.Rectangle(frame, new Rect(x, 90, 80, 60), Scalar.White, -1);
        writer.Write(frame);
    }

    return path;
}
