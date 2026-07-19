using OpenCvSharp;
using Visual.Abstractions.Contracts;
using Visual.Engine.OpenCv.Internal;
using Visual.Image.Contracts;
using Visual.IO.Contracts;

namespace Visual.Engine.OpenCv.Contracts;

public abstract class OpenCvFrameSourceBase : BufferedFrameSourceBase
{
    private readonly ImageMemoryPool _memoryPool = new();
    private readonly OpenCvImageAdapter _adapter;
    private long _frameIndex;

    protected OpenCvFrameSourceBase(string sourceId, int bufferCapacity)
        : base(sourceId, bufferCapacity)
    {
        _adapter = new OpenCvImageAdapter(new ImageFrameFactory(_memoryPool));
    }

    public ImageMemoryPoolStatistics MemoryPoolStatistics => _memoryPool.GetStatistics();

    private protected bool Publish(Mat mat)
    {
        var width = mat.Cols;
        var height = mat.Rows;
        var info = new FrameInfo(
            Interlocked.Increment(ref _frameIndex) - 1,
            DateTime.UtcNow,
            SourceId,
            new ImageSize(width, height));
        var lease = _adapter.FromMat(mat, info);
        if (TryPublishFrame(lease))
        {
            return true;
        }

        lease.Dispose();
        return false;
    }

    protected static double NormalizeFps(double fps, double fallback = 30) =>
        double.IsFinite(fps) && fps > 0 ? fps : fallback;
}
