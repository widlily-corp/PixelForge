using System.Text.Json.Serialization;

namespace PixelForge.Core.Models;

/// <summary>
/// Serializable project model representing a saved pixel art canvas and its palette (.pxproj).
/// </summary>
public class Project
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; } = 1;

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = "0.0.1";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("layers")]
    public List<LayerData> Layers { get; set; } = new();

    [JsonPropertyName("palette")]
    public List<string> Palette { get; set; } = new();

    [JsonPropertyName("activeLayerIndex")]
    public int ActiveLayerIndex { get; set; } = 0;

    public Project()
    {
    }

    public Project(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Creates a Project snapshot from a PixelCanvas and an optional palette.
    /// </summary>
    public static Project FromCanvas(PixelCanvas canvas, IEnumerable<PixelColor>? palette = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var project = new Project(canvas.Width, canvas.Height);

        for (int i = 0; i < canvas.Layers.Count; i++)
        {
            var layer = canvas.Layers[i];
            project.Layers.Add(LayerData.FromLayer(layer));
            if (layer == canvas.ActiveLayer)
            {
                project.ActiveLayerIndex = i;
            }
        }

        if (palette != null)
        {
            foreach (var color in palette)
            {
                project.Palette.Add(color.ToHex(true));
            }
        }

        return project;
    }

    /// <summary>
    /// Constructs a PixelCanvas from this project data.
    /// </summary>
    public PixelCanvas ToCanvas()
    {
        if (Width <= 0 || Height <= 0)
        {
            throw new InvalidOperationException($"Invalid project dimensions: {Width}x{Height}");
        }

        var layers = new List<Layer>();
        foreach (var layerData in Layers)
        {
            layers.Add(layerData.ToLayer(Width, Height));
        }

        if (layers.Count == 0)
        {
            layers.Add(new Layer("Layer 1", Width, Height));
        }

        int activeIdx = Math.Clamp(ActiveLayerIndex, 0, layers.Count - 1);
        return new PixelCanvas(Width, Height, layers, layers[activeIdx]);
    }
}

/// <summary>
/// Serializable data structure representing a single layer.
/// </summary>
public class LayerData
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; } = true;

    [JsonPropertyName("opacity")]
    public float Opacity { get; set; } = 1.0f;

    /// <summary>
    /// Raw RGBA bytes (4 bytes per pixel) serialized as Base64 in JSON.
    /// </summary>
    [JsonPropertyName("pixels")]
    public byte[] Pixels { get; set; } = Array.Empty<byte>();

    public static LayerData FromLayer(Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        var data = new LayerData
        {
            Id = layer.Id,
            Name = layer.Name,
            IsVisible = layer.IsVisible,
            Opacity = layer.Opacity,
            Pixels = new byte[layer.Pixels.Length * 4]
        };

        for (int i = 0; i < layer.Pixels.Length; i++)
        {
            int offset = i * 4;
            var pixel = layer.Pixels[i];
            data.Pixels[offset] = pixel.R;
            data.Pixels[offset + 1] = pixel.G;
            data.Pixels[offset + 2] = pixel.B;
            data.Pixels[offset + 3] = pixel.A;
        }

        return data;
    }

    public Layer ToLayer(int width, int height)
    {
        var pixels = new PixelColor[width * height];
        int maxPixels = Math.Min(pixels.Length, Pixels.Length / 4);

        for (int i = 0; i < maxPixels; i++)
        {
            int offset = i * 4;
            pixels[i] = new PixelColor(
                Pixels[offset],
                Pixels[offset + 1],
                Pixels[offset + 2],
                Pixels[offset + 3]);
        }

        return new Layer(Name, width, height, pixels, Id)
        {
            IsVisible = IsVisible,
            Opacity = Opacity
        };
    }
}
