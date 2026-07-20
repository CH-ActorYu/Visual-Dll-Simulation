using Visual.AppCore.Runtime;

namespace Visual.AppCore.Tests;

public sealed class LatestDisposableSlotTests
{
    [Fact]
    public void Publish_replaces_and_disposes_stale_value()
    {
        using var slot = new LatestDisposableSlot<Probe>();
        var stale = new Probe();
        var latest = new Probe();

        slot.Publish(stale);
        slot.Publish(latest);

        Assert.True(stale.IsDisposed);
        Assert.False(latest.IsDisposed);
        Assert.Same(latest, slot.Take());
        Assert.False(slot.HasValue);
        latest.Dispose();
    }

    [Fact]
    public void Dispose_releases_pending_value_and_rejects_publish()
    {
        var slot = new LatestDisposableSlot<Probe>();
        var pending = new Probe();
        slot.Publish(pending);

        slot.Dispose();

        Assert.True(pending.IsDisposed);
        var rejected = new Probe();
        Assert.Throws<ObjectDisposedException>(() => slot.Publish(rejected));
        Assert.False(rejected.IsDisposed);
        rejected.Dispose();
    }

    private sealed class Probe : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
