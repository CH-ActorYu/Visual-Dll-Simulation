using Visual.Distance.Contracts;
using Visual.Vision.Contracts;

namespace Visual.Distance.Internal;

internal sealed class RoiTargetTracker(Func<ITracker>? trackerFactory) : IDisposable
{
    private readonly Dictionary<string, ITracker> _trackers = new(StringComparer.Ordinal);
    private int _isDisposed;

    public IReadOnlyList<ITracker?> Prepare(IReadOnlyList<TargetRegion> targets)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        var activeIds = targets.Select(target => target.TargetId).ToHashSet(StringComparer.Ordinal);
        foreach (var removedId in _trackers.Keys.Where(id => !activeIds.Contains(id)).ToArray())
        {
            _trackers.Remove(removedId, out var tracker);
            tracker?.Dispose();
        }

        var result = new ITracker?[targets.Count];
        if (trackerFactory is null)
        {
            return result;
        }

        for (var index = 0; index < targets.Count; index++)
        {
            var targetId = targets[index].TargetId;
            if (!_trackers.TryGetValue(targetId, out var tracker))
            {
                tracker = trackerFactory();
                _trackers.Add(targetId, tracker);
            }

            result[index] = tracker;
        }

        return result;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        foreach (var tracker in _trackers.Values)
        {
            tracker.Dispose();
        }

        _trackers.Clear();
    }
}
