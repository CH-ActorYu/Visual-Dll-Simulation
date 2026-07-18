using Visual.Abstractions.Contracts;
using Visual.AppCore.Interaction;

namespace Visual.AppCore.Tests;

public sealed class OverlayRendererTests
{
    [Fact]
    public void Project_UsesSameUniformTransformForRoiAndCenter()
    {
        var renderer = new OverlayRenderer();
        var snapshot = new OverlaySnapshot(
            new RoiRect(100, 100, 200, 100),
            null,
            new Point2D(200, 150),
            "1000.0 Mm",
            VisionResultStatus.Valid);

        var result = renderer.Project(snapshot, new ViewportTransform(1000, 1000, new ImageSize(800, 600)));

        Assert.Equal(125, result.SelectionX);
        Assert.Equal(250, result.SelectionY);
        Assert.Equal(250, result.SelectionWidth);
        Assert.Equal(125, result.SelectionHeight);
        Assert.Equal(250, result.CenterX);
        Assert.Equal(312.5, result.CenterY);
        Assert.True(result.HasSelection);
        Assert.True(result.HasCenter);
    }
}
