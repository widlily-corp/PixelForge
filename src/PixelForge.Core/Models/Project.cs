using System.Text.Json.Serialization;

namespace PixelForge.Core.Models;

/// <summary>
/// Serializable project model representing a saved pixel art canvas and its palette (.pxproj).
/// </summary>
public class Project
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; } = 2;

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = AppVersionInfo.CurrentVersion;

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

    [JsonPropertyName("activeFrameIndex")]
    public int ActiveFrameIndex { get; set; } = 0;

    [JsonPropertyName("frames")]
    public List<FrameData> Frames { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<TagData> Tags { get; set; } = new();

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

        var project = new Project(canvas.Width, canvas.Height)
        {
            FormatVersion = 2,
            ActiveFrameIndex = canvas.CurrentFrameIndex
        };

        for (int i = 0; i < canvas.Layers.Count; i++)
        {
            var layer = canvas.Layers[i];
            project.Layers.Add(LayerData.FromLayer(layer));
            if (layer == canvas.ActiveLayer)
            {
                project.ActiveLayerIndex = i;
            }
        }

        foreach (var frame in canvas.Frames)
        {
            project.Frames.Add(FrameData.FromFrame(frame));
        }

        foreach (var tag in canvas.Tags)
        {
            project.Tags.Add(TagData.FromTag(tag));
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
        var canvas = new PixelCanvas(Width, Height, layers, layers[activeIdx]);

        if (FormatVersion == 1 || Frames == null || Frames.Count == 0)
        {
            canvas.SwitchToFrame(0);
            return canvas;
        }

        canvas.Frames.Clear();
        foreach (var frameData in Frames)
        {
            var frame = frameData.ToFrame(Width, Height);
            foreach (var layer in canvas.Layers)
            {
                if (!frame.Cels.ContainsKey(layer.Id))
                {
                    frame.Cels[layer.Id] = new AnimationCel(layer.Id, Width, Height);
                }
            }
            canvas.Frames.Add(frame);
        }

        if (canvas.Frames.Count == 0)
        {
            var fallbackFrame = new AnimationFrame(100);
            foreach (var layer in canvas.Layers)
            {
                fallbackFrame.Cels[layer.Id] = new AnimationCel(layer.Id, layer.Pixels);
            }
            canvas.Frames.Add(fallbackFrame);
        }

        canvas.Tags.Clear();
        if (Tags != null)
        {
            foreach (var tagData in Tags)
            {
                canvas.Tags.Add(tagData.ToTag());
            }
        }

        int targetFrameIdx = Math.Clamp(ActiveFrameIndex, 0, canvas.Frames.Count - 1);
        canvas.SwitchToFrame(targetFrameIdx);

        return canvas;
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

/// <summary>
/// Serializable data structure representing a single animation cel.
/// </summary>
public class CelData
{
    [JsonPropertyName("layerId")]
    public Guid LayerId { get; set; }

    [JsonPropertyName("isEmpty")]
    public bool IsEmpty { get; set; }

    /// <summary>
    /// Raw RGBA bytes (4 bytes per pixel) serialized as Base64 in JSON.
    /// </summary>
    [JsonPropertyName("pixels")]
    public byte[] Pixels { get; set; } = Array.Empty<byte>();

    public static CelData FromCel(AnimationCel cel)
    {
        ArgumentNullException.ThrowIfNull(cel);

        cel.UpdateIsEmpty();

        var data = new CelData
        {
            LayerId = cel.LayerId,
            IsEmpty = cel.IsEmpty,
            Pixels = new byte[cel.Pixels.Length * 4]
        };

        for (int i = 0; i < cel.Pixels.Length; i++)
        {
            int offset = i * 4;
            var pixel = cel.Pixels[i];
            data.Pixels[offset] = pixel.R;
            data.Pixels[offset + 1] = pixel.G;
            data.Pixels[offset + 2] = pixel.B;
            data.Pixels[offset + 3] = pixel.A;
        }

        return data;
    }

    public AnimationCel ToCel(int width, int height)
    {
        var pixels = new PixelColor[width * height];
        if (Pixels != null && Pixels.Length > 0)
        {
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
        }

        return new AnimationCel(LayerId, pixels, IsEmpty);
    }
}

/// <summary>
/// Serializable data structure representing an animation frame and its layer cels.
/// </summary>
public class FrameData
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("durationMs")]
    public int DurationMs { get; set; } = 100;

    [JsonPropertyName("cels")]
    public List<CelData> Cels { get; set; } = new();

    public static FrameData FromFrame(AnimationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var data = new FrameData
        {
            Id = frame.Id,
            DurationMs = frame.DurationMs,
            Cels = new List<CelData>(frame.Cels.Count)
        };

        foreach (var cel in frame.Cels.Values)
        {
            data.Cels.Add(CelData.FromCel(cel));
        }

        return data;
    }

    public AnimationFrame ToFrame(int width, int height)
    {
        var frame = new AnimationFrame(Math.Max(1, DurationMs), Id);
        if (Cels != null)
        {
            foreach (var celData in Cels)
            {
                var cel = celData.ToCel(width, height);
                frame.Cels[cel.LayerId] = cel;
            }
        }

        return frame;
    }
}

/// <summary>
/// Serializable data structure representing an animation tag.
/// </summary>
public class TagData
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("fromFrame")]
    public int FromFrame { get; set; }

    [JsonPropertyName("toFrame")]
    public int ToFrame { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#FF4A90E2";

    [JsonPropertyName("loopMode")]
    public string LoopMode { get; set; } = "Forward";

    [JsonPropertyName("repeatCount")]
    public int RepeatCount { get; set; } = 0;

    public static TagData FromTag(AnimationTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        return new TagData
        {
            Id = tag.Id,
            Name = tag.Name,
            FromFrame = tag.FromFrame,
            ToFrame = tag.ToFrame,
            Color = tag.Color.ToHex(true),
            LoopMode = tag.LoopMode.ToString(),
            RepeatCount = tag.RepeatCount
        };
    }

    public AnimationTag ToTag()
    {
        var tag = new AnimationTag(Id)
        {
            Name = Name,
            FromFrame = FromFrame,
            ToFrame = ToFrame,
            RepeatCount = Math.Max(0, RepeatCount)
        };

        if (PixelColor.TryParse(Color, out var parsedColor))
        {
            tag.Color = parsedColor;
        }

        if (Enum.TryParse<AnimationLoopMode>(LoopMode, true, out var parsedLoopMode))
        {
            tag.LoopMode = parsedLoopMode;
        }

        return tag;
    }
}
