namespace Visual.Vision.Contracts;

[Flags]
public enum VisionCapability
{
    None = 0,
    Preprocess = 1 << 0,
    Detection = 1 << 1,
    Tracking = 1 << 2,
    Camera = 1 << 3,
    IntrinsicCalibration = 1 << 4,
    ThreeD = 1 << 5
}
