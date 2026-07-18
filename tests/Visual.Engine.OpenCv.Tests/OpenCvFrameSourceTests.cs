using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.IO.Contracts;

namespace Visual.Engine.OpenCv.Tests;

public sealed class OpenCvFrameSourceTests
{
    [Fact]
    public async Task Pause_should_clear_a_stale_resume_signal_before_single_step()
    {
        var playback = new PlaybackControl();
        playback.Resume();
        playback.Pause();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await playback.WaitAsync(timeout.Token));

        playback.Step();
        await playback.WaitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Image_folder_should_support_paused_single_step_playback()
    {
        var folder = CreateTemporaryFolder();
        try
        {
            WriteImage(Path.Combine(folder, "01.png"), 10);
            WriteImage(Path.Combine(folder, "02.png"), 20);
            var source = new OpenCvImageFolderSource("folder", folder, fps: 100);

            await source.OpenAsync();
            await source.PauseAsync();
            await source.StartAsync();
            await using var frames = source.ReadFramesAsync().GetAsyncEnumerator();

            await source.StepAsync();
            Assert.True(await frames.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            using var first = frames.Current;
            Assert.Equal(32, first.Frame.Info.Size.Width);
            Assert.True(source.IsPaused);

            await source.StopAsync();
            await source.CloseAsync();
            await source.DisposeAsync();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Empty_image_folder_and_missing_video_should_report_decoding_failure()
    {
        var folder = CreateTemporaryFolder();
        try
        {
            await using var images = new OpenCvImageFolderSource("empty", folder);
            var imageError = await Assert.ThrowsAsync<VisionException>(async () => await images.OpenAsync());
            Assert.Equal(VisionErrorCode.DecodingFailed, imageError.ErrorCode);

            await using var video = new OpenCvVideoFileSource("missing", Path.Combine(folder, "missing.avi"));
            var videoError = await Assert.ThrowsAsync<VisionException>(async () => await video.OpenAsync());
            Assert.Equal(VisionErrorCode.DecodingFailed, videoError.ErrorCode);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Video_file_source_should_decode_generated_video_without_camera()
    {
        var folder = CreateTemporaryFolder();
        try
        {
            var path = Path.Combine(folder, "sample.avi");
            WriteVideo(path);
            await using var source = new OpenCvVideoFileSource("video", path);

            await source.OpenAsync();
            await source.StartAsync();
            await using var frames = source.ReadFramesAsync().GetAsyncEnumerator();

            Assert.True(await frames.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            using var frame = frames.Current;
            Assert.Equal(new ImageSize(32, 24), frame.Frame.Info.Size);
            await source.StopAsync();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Unavailable_camera_should_report_device_lost()
    {
        await using var source = new OpenCvCameraSource("unavailable", int.MaxValue);

        var exception = await Assert.ThrowsAsync<VisionException>(async () => await source.OpenAsync());

        Assert.Equal(VisionErrorCode.DeviceLost, exception.ErrorCode);
    }

    [Fact]
    public void Frame_source_factory_should_map_descriptors_and_validate_options()
    {
        var factory = new OpenCvFrameSourceFactory();
        var camera = factory.Create(new FrameSourceDescriptor(
            "camera",
            FrameSourceKind.Camera,
            options: new Dictionary<string, string> { ["DeviceIndex"] = "2" }));

        Assert.IsType<OpenCvCameraSource>(camera);

        var exception = Assert.Throws<VisionException>(() => factory.Create(new FrameSourceDescriptor(
            "camera",
            FrameSourceKind.Camera,
            options: new Dictionary<string, string> { ["BufferCapacity"] = "0" })));
        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
    }

    [Fact]
    public async Task Device_provider_should_allow_device_free_discovery_and_reject_foreign_id()
    {
        var provider = new OpenCvDeviceProvider(maximumDevices: 0);

        Assert.Empty(await provider.EnumerateAsync());
        var descriptor = new CameraDescriptor(
            "foreign",
            "Foreign",
            null,
            null,
            new CameraCapabilities([new ImageSize(1, 1)], [1]));
        var exception = await Assert.ThrowsAsync<VisionException>(async () => await provider.OpenAsync(descriptor));
        Assert.Equal(VisionErrorCode.InvalidInput, exception.ErrorCode);
    }

    private static string CreateTemporaryFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"visual-opencv-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void WriteImage(string path, byte value)
    {
        using var image = new Mat(new Size(32, 24), MatType.CV_8UC3, new Scalar(value, value, value));
        Assert.True(Cv2.ImWrite(path, image));
    }

    private static void WriteVideo(string path)
    {
        using var writer = new VideoWriter(path, FourCC.MJPG, 10, new Size(32, 24));
        Assert.True(writer.IsOpened());
        using var first = new Mat(new Size(32, 24), MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var second = new Mat(new Size(32, 24), MatType.CV_8UC3, new Scalar(40, 50, 60));
        writer.Write(first);
        writer.Write(second);
    }
}
