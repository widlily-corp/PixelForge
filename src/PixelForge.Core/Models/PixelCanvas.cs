using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelForge.Core.Models;

/// <summary>
/// Represents a multi-layered pixel canvas and manages compositing and layer operations.
/// </summary>
public partial class PixelCanvas : ObservableObject
{
    private Layer? _activeLayer;
    private int _width;
    private int _height;

    public int Width
    {
        get => _width;
        private set => SetProperty(ref _width, value);
    }

    public int Height
    {
        get => _height;
        private set => SetProperty(ref _height, value);
    }

    public ObservableCollection<Layer> Layers { get; } = new();

    public Layer? ActiveLayer
    {
        get => _activeLayer;
        set => SetProperty(ref _activeLayer, value);
    }

    public PixelCanvas(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        _width = width;
        _height = height;

        var defaultLayer = new Layer("Layer 1", width, height);
        Layers.Add(defaultLayer);
        ActiveLayer = defaultLayer;
    }

    /// <summary>
    /// Updates the canvas dimensions during a resize operation.
    /// </summary>
    public void SetDimensions(int newWidth, int newHeight)
    {
        if (newWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newWidth), "Width must be greater than zero.");
        }

        if (newHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newHeight), "Height must be greater than zero.");
        }

        Width = newWidth;
        Height = newHeight;
    }

    public PixelCanvas(int width, int height, IEnumerable<Layer> layers, Layer? activeLayer = null)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(layers);

        Width = width;
        Height = height;

        foreach (var layer in layers)
        {
            if (layer.Width != width || layer.Height != height)
            {
                throw new ArgumentException($"Layer '{layer.Name}' dimensions ({layer.Width}x{layer.Height}) do not match canvas ({width}x{height}).");
            }
            Layers.Add(layer);
        }

        if (Layers.Count == 0)
        {
            var defaultLayer = new Layer("Layer 1", width, height);
            Layers.Add(defaultLayer);
            ActiveLayer = defaultLayer;
        }
        else
        {
            ActiveLayer = activeLayer != null && Layers.Contains(activeLayer)
                ? activeLayer
                : Layers[^1];
        }
    }

    /// <summary>
    /// Adds a new transparent layer at the top of the stack and makes it active.
    /// </summary>
    public Layer AddLayer(string? name = null)
    {
        var layerName = name ?? $"Layer {Layers.Count + 1}";
        var layer = new Layer(layerName, Width, Height);
        Layers.Add(layer);
        ActiveLayer = layer;
        return layer;
    }

    /// <summary>
    /// Inserts a layer at the specified index.
    /// </summary>
    public void InsertLayer(int index, Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (layer.Width != Width || layer.Height != Height)
        {
            throw new ArgumentException($"Layer dimensions ({layer.Width}x{layer.Height}) do not match canvas ({Width}x{Height}).");
        }

        if (index < 0 || index > Layers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        Layers.Insert(index, layer);
        ActiveLayer = layer;
    }

    /// <summary>
    /// Removes the specified layer. Will not remove if it is the only remaining layer.
    /// </summary>
    public bool RemoveLayer(Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        if (Layers.Count <= 1)
        {
            return false;
        }

        int index = Layers.IndexOf(layer);
        if (index < 0)
        {
            return false;
        }

        Layers.RemoveAt(index);

        if (ActiveLayer == layer)
        {
            int newActiveIndex = Math.Clamp(index, 0, Layers.Count - 1);
            ActiveLayer = Layers[newActiveIndex];
        }

        return true;
    }

    /// <summary>
    /// Removes layer at specified index.
    /// </summary>
    public bool RemoveLayerAt(int index)
    {
        if (index < 0 || index >= Layers.Count || Layers.Count <= 1)
        {
            return false;
        }

        var layer = Layers[index];
        return RemoveLayer(layer);
    }

    /// <summary>
    /// Merges the layer at index with the layer directly beneath it (index - 1).
    /// </summary>
    public Layer MergeLayerDown(int index)
    {
        if (index <= 0 || index >= Layers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be greater than 0 and less than Layers.Count.");
        }

        var topLayer = Layers[index];
        var bottomLayer = Layers[index - 1];

        // Bake composite into bottom layer
        float topOpacity = topLayer.IsVisible ? topLayer.Opacity : 0f;
        float botOpacity = bottomLayer.IsVisible ? bottomLayer.Opacity : 0f;

        for (int i = 0; i < bottomLayer.Pixels.Length; i++)
        {
            var botPixel = bottomLayer.Pixels[i];
            var topPixel = topLayer.Pixels[i];
            bottomLayer.Pixels[i] = BlendColors(botPixel, botOpacity, topPixel, topOpacity);
        }

        bottomLayer.Opacity = 1.0f;
        bottomLayer.IsVisible = true;

        Layers.RemoveAt(index);
        ActiveLayer = bottomLayer;
        return bottomLayer;
    }

    /// <summary>
    /// Merges the specified layer with the layer directly beneath it.
    /// </summary>
    public Layer MergeLayerDown(Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        int index = Layers.IndexOf(layer);
        if (index < 0)
        {
            throw new ArgumentException("Layer not found in canvas.", nameof(layer));
        }

        return MergeLayerDown(index);
    }

    /// <summary>
    /// Computes the composite pixel at (x, y) by blending all visible layers bottom-to-top.
    /// </summary>
    public PixelColor GetCompositePixel(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            return PixelColor.Transparent;
        }

        PixelColor result = PixelColor.Transparent;
        float resultAlpha = 0f;

        for (int i = 0; i < Layers.Count; i++)
        {
            var layer = Layers[i];
            if (!layer.IsVisible || layer.Opacity <= 0f)
            {
                continue;
            }

            var pixel = layer.GetPixel(x, y);
            if (pixel.A == 0)
            {
                continue;
            }

            result = BlendColors(result, resultAlpha > 0f ? 1.0f : 0f, pixel, layer.Opacity);
            resultAlpha = result.A / 255f;
            if (result.A == 255)
            {
                // Fully opaque pixel, cannot be affected by subsequent transparent layers unless partially transparent
                // But in bottom-to-top, later layers are ON TOP, so later layers paint OVER result!
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the full flattened composite image into the destination buffer or creates a new one.
    /// </summary>
    public PixelColor[] GetCompositePixels(PixelColor[]? destination = null)
    {
        int totalPixels = Width * Height;
        if (destination == null || destination.Length != totalPixels)
        {
            destination = new PixelColor[totalPixels];
        }
        else
        {
            Array.Clear(destination, 0, totalPixels);
        }

        for (int layerIdx = 0; layerIdx < Layers.Count; layerIdx++)
        {
            var layer = Layers[layerIdx];
            if (!layer.IsVisible || layer.Opacity <= 0f)
            {
                continue;
            }

            float layerOpacity = layer.Opacity;
            var layerPixels = layer.Pixels;

            for (int p = 0; p < totalPixels; p++)
            {
                var srcPixel = layerPixels[p];
                if (srcPixel.A == 0)
                {
                    continue;
                }

                var dstPixel = destination[p];
                destination[p] = BlendColors(dstPixel, 1.0f, srcPixel, layerOpacity);
            }
        }

        return destination;
    }

    /// <summary>
    /// Performs Porter-Duff Source-Over alpha compositing of source onto destination.
    /// </summary>
    public static PixelColor BlendColors(PixelColor dst, float dstOpacity, PixelColor src, float srcOpacity)
    {
        float srcAlpha = (src.A / 255f) * srcOpacity;
        if (srcAlpha <= 0f)
        {
            return dstOpacity >= 1f ? dst : new PixelColor(dst.R, dst.G, dst.B, (byte)Math.Clamp(MathF.Round(dst.A * dstOpacity), 0, 255));
        }

        float dstAlpha = (dst.A / 255f) * dstOpacity;
        if (dstAlpha <= 0f)
        {
            return new PixelColor(src.R, src.G, src.B, (byte)Math.Clamp(MathF.Round(srcAlpha * 255f), 0, 255));
        }

        float outAlpha = srcAlpha + dstAlpha * (1f - srcAlpha);
        if (outAlpha <= 0.0001f)
        {
            return PixelColor.Transparent;
        }

        float invSrcAlpha = 1f - srcAlpha;
        float outR = (src.R * srcAlpha + dst.R * dstAlpha * invSrcAlpha) / outAlpha;
        float outG = (src.G * srcAlpha + dst.G * dstAlpha * invSrcAlpha) / outAlpha;
        float outB = (src.B * srcAlpha + dst.B * dstAlpha * invSrcAlpha) / outAlpha;

        return new PixelColor(
            (byte)Math.Clamp(MathF.Round(outR), 0, 255),
            (byte)Math.Clamp(MathF.Round(outG), 0, 255),
            (byte)Math.Clamp(MathF.Round(outB), 0, 255),
            (byte)Math.Clamp(MathF.Round(outAlpha * 255f), 0, 255));
    }
}
