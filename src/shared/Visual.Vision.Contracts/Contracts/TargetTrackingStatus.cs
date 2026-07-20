namespace Visual.Vision.Contracts;

public enum TargetTrackingStatus
{
    Unregistered,
    Registering,
    Ready,
    Tracking,
    TemporarilyLost,
    Reacquiring,
    NotFound
}
