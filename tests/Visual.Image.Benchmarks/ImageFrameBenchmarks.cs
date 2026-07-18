using BenchmarkDotNet.Attributes;
using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;

namespace Visual.Image.Benchmarks;

[MemoryDiagnoser]
public class ImageFrameBenchmarks
{
    private ImageFrameFactory _factory = null!;
    private IImageLease _sourceLease = null!;

    [Params(640, 1920)]
    public int Width { get; set; }

    [Params(480, 1080)]
    public int Height { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _factory = new ImageFrameFactory(new ImageMemoryPool());
        _sourceLease = _factory.Rent(Width, Height, PixelFormat.Bgr24);
    }

    [GlobalCleanup]
    public void Cleanup() => _sourceLease.Dispose();

    [Benchmark(Baseline = true)]
    public int RentAndRelease()
    {
        using var lease = _factory.Rent(Width, Height, PixelFormat.Bgr24);
        return lease.Frame.Data.Length;
    }

    [Benchmark]
    public int CloneFrame()
    {
        using var lease = _factory.Clone(_sourceLease.Frame);
        return lease.Frame.Data.Length;
    }

    [Benchmark]
    public int CropView()
    {
        var roi = new RoiRect(Width / 4, Height / 4, Width / 2, Height / 2);
        var view = new ImageView(_sourceLease).Crop(roi);
        return view.Data.Length;
    }
}
