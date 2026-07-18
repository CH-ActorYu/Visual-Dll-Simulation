using Visual.Abstractions.Contracts;
using Visual.Image.Contracts;
using Visual.Vision.Contracts;

namespace Visual.VisionBase.Contracts;

public sealed class DetectionPipeline
{
    private readonly RoiValidator _roiValidator;

    public DetectionPipeline(RoiValidator? roiValidator = null)
    {
        _roiValidator = roiValidator ?? new RoiValidator();
    }

    public IReadOnlyList<TargetCandidate?> Run(
        ImageFrame frame,
        IReadOnlyList<RoiRect> regions,
        ITargetDetector detector,
        ITracker? tracker,
        CancellationToken cancellationToken = default)
    {
        if (regions?.Count != 1)
        {
            throw new VisionException(
                VisionErrorCode.InvalidInput,
                "The single-tracker overload requires exactly one ROI. Use the tracker-list overload for multiple ROIs.");
        }

        return Run(frame, regions, detector, new ITracker?[] { tracker }, cancellationToken);
    }

    public IReadOnlyList<TargetCandidate?> Run(
        ImageFrame frame,
        IReadOnlyList<RoiRect> regions,
        ITargetDetector detector,
        IReadOnlyList<ITracker?> trackers,
        CancellationToken cancellationToken = default)
    {
        if (frame is null || detector is null || regions is null || trackers is null)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Frame, regions, detector and trackers are required.");
        }

        if (regions.Count == 0 || trackers.Count != regions.Count)
        {
            throw new VisionException(VisionErrorCode.InvalidInput, "Each ROI must have a matching tracker entry.");
        }

        var results = new TargetCandidate?[regions.Count];
        for (var index = 0; index < regions.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var roi = _roiValidator.Validate(regions[index], frame.Info.Size);
            var tracker = trackers[index];
            TargetCandidate? candidate = null;

            if (tracker is not null)
            {
                candidate = InvokeTracker(tracker, frame);
                if (candidate is not null && !IsInside(candidate, roi))
                {
                    candidate = null;
                }
            }

            if (candidate is null)
            {
                candidate = InvokeDetector(detector, frame, roi);
                if (candidate is not null)
                {
                    EnsureInside(candidate, roi);
                    if (tracker is not null)
                    {
                        InitializeTracker(tracker, frame, candidate.BoundingBox);
                    }
                }
            }

            results[index] = candidate;
        }

        return results;
    }

    private static TargetCandidate? InvokeDetector(ITargetDetector detector, ImageFrame frame, RoiRect roi)
    {
        try
        {
            return detector.Detect(frame, roi);
        }
        catch (VisionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new VisionException(VisionErrorCode.ModuleSpecific, "Target detection failed.", exception);
        }
    }

    private static TargetCandidate? InvokeTracker(ITracker tracker, ImageFrame frame)
    {
        try
        {
            return tracker.Update(frame);
        }
        catch (VisionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new VisionException(VisionErrorCode.ModuleSpecific, "Target tracking failed.", exception);
        }
    }

    private static void InitializeTracker(ITracker tracker, ImageFrame frame, RoiRect region)
    {
        try
        {
            tracker.Initialize(frame, region);
        }
        catch (VisionException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new VisionException(VisionErrorCode.ModuleSpecific, "Target tracker initialization failed.", exception);
        }
    }

    private static bool IsInside(TargetCandidate candidate, RoiRect roi) =>
        Contains(roi, candidate.BoundingBox) && roi.Contains(candidate.Center);

    private static void EnsureInside(TargetCandidate candidate, RoiRect roi)
    {
        if (!IsInside(candidate, roi))
        {
            throw new VisionException(VisionErrorCode.ModuleSpecific, "Detector returned a candidate outside the requested ROI.");
        }
    }

    private static bool Contains(RoiRect outer, RoiRect inner) =>
        inner.X >= outer.X && inner.Y >= outer.Y &&
        (long)inner.X + inner.Width <= (long)outer.X + outer.Width &&
        (long)inner.Y + inner.Height <= (long)outer.Y + outer.Height;
}
