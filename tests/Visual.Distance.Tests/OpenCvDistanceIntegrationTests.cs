using System.Diagnostics;
using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Distance.Contracts;
using Visual.Engine.OpenCv.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Distance.Tests;

public sealed class OpenCvDistanceIntegrationTests
{
    [Fact]
    public async Task Generated_video_should_produce_valid_distance_above_fifteen_results_per_second()
    {
        var path = Path.Combine(Path.GetTempPath(), $"visual-distance-integration-{Guid.NewGuid():N}.avi");
        WriteVideo(path);
        try
        {
            var profile = DetectionProfile.CreateDefault();
            var engine = new OpenCvEngineProvider().Get(OpenCvEngine.EngineId);
            await using var source = new OpenCvVideoFileSource("integration-camera", path);
            await source.OpenAsync();
            await using var service = new DistanceServiceBuilder()
                .WithEngine(engine)
                .WithDetectionProfile(profile)
                .Build();
            service.SetCalibration(new CalibrationInfo(
                100,
                40,
                1000,
                DistanceUnit.Mm,
                profile.MeasureAxis,
                source.SourceId,
                new ImageSize(source.Width, source.Height),
                profile.ProfileId,
                new DistanceRange(100, 5000)));
            var target = new TargetRegion("target", new RoiRect(5, 5, 150, 110));
            var stopwatch = Stopwatch.StartNew();
            var count = 0;

            await foreach (var results in service.MeasureStreamAsync(source, [target]))
            {
                var measurement = Assert.Single(results);
                Assert.Equal(VisionResultStatus.Valid, measurement.Status);
                Assert.InRange(measurement.Distance!.Value, 950, 1050);
                if (++count == 30)
                {
                    break;
                }
            }

            stopwatch.Stop();
            Assert.Equal(30, count);
            Assert.True(
                count / stopwatch.Elapsed.TotalSeconds >= 15,
                $"Measured throughput was {count / stopwatch.Elapsed.TotalSeconds:F2} results/s.");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void WriteVideo(string path)
    {
        using var writer = new VideoWriter(path, FourCC.MJPG, 60, new Size(160, 120));
        Assert.True(writer.IsOpened());
        for (var index = 0; index < 45; index++)
        {
            using var frame = new Mat(new Size(160, 120), MatType.CV_8UC3, Scalar.Black);
            Cv2.Rectangle(frame, new Rect(60 + index / 10, 40, 40, 30), Scalar.White, -1);
            writer.Write(frame);
        }
    }
}
