using PixelForge.Core.Models;

namespace PixelForge.Core.Tools;

/// <summary>
/// Eyedropper tool to sample color from the active layer or composited canvas and update ToolContext.PrimaryColor.
/// </summary>
public class EyedropperTool : ITool
{
    private bool _isSampling;

    public string Name => "Eyedropper";
    public string Shortcut => "I";
    public string IconKey => "IconEyedropper";

    public void OnPointerDown(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        _isSampling = true;
        SampleColor(ctx, x, y);
    }

    public void OnPointerMove(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (_isSampling)
        {
            SampleColor(ctx, x, y);
        }
    }

    public void OnPointerUp(ToolContext ctx, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (_isSampling)
        {
            SampleColor(ctx, x, y);
            _isSampling = false;
        }
    }

    public IReadOnlyList<(int X, int Y, PixelColor Color)> GetPreviewPixels(ToolContext ctx)
    {
        return Array.Empty<(int X, int Y, PixelColor Color)>();
    }

    private static void SampleColor(ToolContext ctx, int x, int y)
    {
        if ((uint)x >= (uint)ctx.Canvas.Width || (uint)y >= (uint)ctx.Canvas.Height)
        {
            return;
        }

        var color = ctx.ActiveLayer.GetPixel(x, y);
        if (color.A == 0)
        {
            var composite = ctx.Canvas.GetCompositePixel(x, y);
            if (composite.A > 0)
            {
                color = composite;
            }
        }

        ctx.PrimaryColor = color;
    }
}
