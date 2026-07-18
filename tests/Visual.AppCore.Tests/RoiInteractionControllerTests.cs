using Visual.Abstractions.Contracts;
using Visual.AppCore.Interaction;

namespace Visual.AppCore.Tests;

public sealed class RoiInteractionControllerTests
{
    [Fact]
    public void Complete_MapsLetterboxedViewportAndClampsToImage()
    {
        var controller = new RoiInteractionController();
        var transform = new ViewportTransform(1000, 1000, new ImageSize(800, 600));

        controller.Begin(new Point2D(-20, 100));
        var result = controller.Complete(new Point2D(1020, 900), transform);

        Assert.Equal(new RoiRect(0, 0, 800, 600), result);
    }

    [Fact]
    public void Complete_RejectsSelectionSmallerThanSixteenPixels()
    {
        var controller = new RoiInteractionController();
        var transform = new ViewportTransform(800, 600, new ImageSize(800, 600));

        controller.Begin(new Point2D(10, 10));
        var result = controller.Complete(new Point2D(25, 40), transform);

        Assert.Null(result);
        Assert.Null(controller.CurrentRoi);
    }

    [Fact]
    public void Clear_RemovesRestoredSelection()
    {
        var controller = new RoiInteractionController();
        controller.Restore(new RoiRect(1, 2, 30, 40));

        controller.Clear();

        Assert.Null(controller.CurrentRoi);
    }

    [Fact]
    public void ClampTo_ClampsPersistedSelectionForNewResolution()
    {
        var controller = new RoiInteractionController();
        controller.Restore(new RoiRect(600, 400, 300, 300));

        var result = controller.ClampTo(new ImageSize(800, 600));

        Assert.Equal(new RoiRect(600, 400, 200, 200), result);
        Assert.Equal(result, controller.CurrentRoi);
    }
}
