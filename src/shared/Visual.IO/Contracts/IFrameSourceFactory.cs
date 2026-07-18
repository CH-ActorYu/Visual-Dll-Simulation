namespace Visual.IO.Contracts;

public interface IFrameSourceFactory
{
    IFrameSource Create(FrameSourceDescriptor descriptor);
}
