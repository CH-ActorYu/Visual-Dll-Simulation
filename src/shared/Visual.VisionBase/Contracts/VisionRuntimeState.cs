namespace Visual.VisionBase.Contracts;

public enum VisionRuntimeState
{
    Created,
    Starting,
    Running,
    Completed,
    Stopping,
    Stopped,
    Failed,
    Disposed
}
