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
    private int _currentFrameIndex;

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

    /// <summary>
    /// Gets the collection of animation frames in the timeline.
    /// </summary>
    public ObservableCollection<AnimationFrame> Frames { get; } = new();

    /// <summary>
    /// Gets the collection of animation tags grouping frame ranges.
    /// </summary>
    public ObservableCollection<AnimationTag> Tags { get; } = new();

    /// <summary>
    /// Gets the onion skinning overlay configuration.
    /// </summary>
    public OnionSkinSettings OnionSkin { get; } = new();

    /// <summary>
    /// Gets or sets the active frame index (0-based) in the animation timeline.
    /// Setting this property automatically switches to the frame and swaps cel pixel buffers.
    /// </summary>
    public int CurrentFrameIndex
    {
        get => _currentFrameIndex;
        set => SwitchToFrame(value);
    }

    /// <summary>
    /// Gets the currently active animation frame, or null if no frames exist.
    /// </summary>
    public AnimationFrame? CurrentFrame => (uint)_currentFrameIndex < (uint)Frames.Count ? Frames[_currentFrameIndex] : null;

    /// <summary>
    /// Event raised when the current animation frame index changes.
    /// </summary>
    public event Action<int>? FrameChanged;

    /// <summary>
    /// Event raised when any layer's pixels or layer composition changes.
    /// </summary>
    public event Action? PixelsChanged;

    /// <summary>
    /// Manually signals that the composite visual is dirty and needs re-rendering.
    /// </summary>
    public void InvalidateComposite() => PixelsChanged?.Invoke();

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
        InitializeLayerEvents();
        InitializeAnimationTimeline();
    }

    /// <summary>
    /// Updates the canvas dimensions during a resize operation, adjusting all layer buffers and animation cels across all frames.
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

        int oldWidth = Width;
        int oldHeight = Height;

        Width = newWidth;
        Height = newHeight;

        if (oldWidth == newWidth && oldHeight == newHeight)
        {
            InvalidateComposite();
            return;
        }

        int copyW = Math.Min(oldWidth, newWidth);
        int copyH = Math.Min(oldHeight, newHeight);

        // Resize cels across all frames
        foreach (var frame in Frames)
        {
            foreach (var layer in Layers)
            {
                if (!frame.Cels.TryGetValue(layer.Id, out var cel))
                {
                    cel = new AnimationCel(layer.Id, newWidth, newHeight);
                    frame.Cels[layer.Id] = cel;
                }
                else if (frame == CurrentFrame && layer.Pixels.Length == newWidth * newHeight)
                {
                    // Active layer buffer has already been resized (e.g., via SetDimensionsAndPixels in ResizeCanvasCommand)
                    if (cel.Pixels != layer.Pixels)
                    {
                        cel.SetPixels(layer.Pixels, cel.IsEmpty);
                    }
                }
                else if (cel.Pixels.Length != newWidth * newHeight)
                {
                    var oldPixels = cel.Pixels;
                    var newPixels = new PixelColor[newWidth * newHeight];
                    if (oldPixels.Length == oldWidth * oldHeight)
                    {
                        for (int y = 0; y < copyH; y++)
                        {
                            Array.Copy(oldPixels, y * oldWidth, newPixels, y * newWidth, copyW);
                        }
                    }
                    cel.SetPixels(newPixels);
                    cel.UpdateIsEmpty();
                }
            }
        }

        // Ensure layers also match new dimensions
        foreach (var layer in Layers)
        {
            if (layer.Width != newWidth || layer.Height != newHeight || layer.Pixels.Length != newWidth * newHeight)
            {
                if (CurrentFrame != null && CurrentFrame.Cels.TryGetValue(layer.Id, out var activeCel))
                {
                    layer.SetDimensionsAndPixels(newWidth, newHeight, activeCel.Pixels);
                }
                else
                {
                    var newPixels = new PixelColor[newWidth * newHeight];
                    int copyLayerW = Math.Min(layer.Width, newWidth);
                    int copyLayerH = Math.Min(layer.Height, newHeight);
                    for (int y = 0; y < copyLayerH; y++)
                    {
                        Array.Copy(layer.Pixels, y * layer.Width, newPixels, y * newWidth, copyLayerW);
                    }
                    layer.SetDimensionsAndPixels(newWidth, newHeight, newPixels);
                }
            }
        }

        InvalidateComposite();
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
            InitializeLayerEvents();
            InitializeAnimationTimeline();
        }
        else
        {
            ActiveLayer = activeLayer != null && Layers.Contains(activeLayer)
                ? activeLayer
                : Layers[^1];
            InitializeLayerEvents();
            InitializeAnimationTimeline();
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
    /// <summary>
    /// Merges the layer at index with the layer directly beneath it (index - 1).
    /// Blends cels across all animation frames in the timeline.
    /// </summary>
    public Layer MergeLayerDown(int index)
    {
        if (index <= 0 || index >= Layers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be greater than 0 and less than Layers.Count.");
        }

        var topLayer = Layers[index];
        var bottomLayer = Layers[index - 1];

        float topOpacity = topLayer.IsVisible ? topLayer.Opacity : 0f;
        float botOpacity = bottomLayer.IsVisible ? bottomLayer.Opacity : 0f;

        // Blend cels across all frames in the timeline
        foreach (var frame in Frames)
        {
            if (!frame.Cels.TryGetValue(bottomLayer.Id, out var botCel))
            {
                botCel = new AnimationCel(bottomLayer.Id, Width, Height);
                frame.Cels[bottomLayer.Id] = botCel;
            }

            if (frame.Cels.TryGetValue(topLayer.Id, out var topCel))
            {
                int len = Math.Min(botCel.Pixels.Length, topCel.Pixels.Length);
                for (int i = 0; i < len; i++)
                {
                    botCel.Pixels[i] = BlendColors(botCel.Pixels[i], botOpacity, topCel.Pixels[i], topOpacity);
                }
                botCel.UpdateIsEmpty();
            }
        }

        bottomLayer.Opacity = 1.0f;
        bottomLayer.IsVisible = true;

        Layers.RemoveAt(index);
        ActiveLayer = bottomLayer;
        bottomLayer.Invalidate();
        InvalidateComposite();
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

    private void InitializeLayerEvents()
    {
        Layers.CollectionChanged += OnLayersCollectionChanged;
        foreach (var layer in Layers)
        {
            HookLayerEvents(layer);
        }
    }

    private void HookLayerEvents(Layer layer)
    {
        layer.PixelsChanged += OnLayerPixelsChanged;
    }

    private void UnhookLayerEvents(Layer layer)
    {
        layer.PixelsChanged -= OnLayerPixelsChanged;
    }

    private void OnLayerPixelsChanged()
    {
        InvalidateComposite();
    }

    /// <summary>
    /// Switches the active animation frame, updating all layer pixel buffers via zero-allocation reference swapping.
    /// </summary>
    /// <param name="index">Target frame index.</param>
    public void SwitchToFrame(int index)
    {
        if (Frames.Count == 0)
        {
            return;
        }

        index = Math.Clamp(index, 0, Frames.Count - 1);
        var targetFrame = Frames[index];

        for (int i = 0; i < Layers.Count; i++)
        {
            var layer = Layers[i];
            if (!targetFrame.Cels.TryGetValue(layer.Id, out var cel))
            {
                cel = new AnimationCel(layer.Id, Width, Height);
                targetFrame.Cels[layer.Id] = cel;
            }

            if (cel.Pixels.Length != Width * Height)
            {
                cel.SetPixels(new PixelColor[Width * Height], true);
            }

            layer.SetPixelsDirect(cel.Pixels);
        }

        SetProperty(ref _currentFrameIndex, index, nameof(CurrentFrameIndex));
        OnPropertyChanged(nameof(CurrentFrame));
        InvalidateComposite();
        FrameChanged?.Invoke(index);
    }

    /// <summary>
    /// Adds a new animation frame with empty cels for all layers and switches to it.
    /// </summary>
    /// <param name="insertIndex">Optional 0-based insertion index; if null, appends to the end.</param>
    /// <param name="durationMs">Frame duration in milliseconds (default: 100ms).</param>
    /// <returns>The newly created <see cref="AnimationFrame"/>.</returns>
    public AnimationFrame AddFrame(int? insertIndex = null, int durationMs = 100)
    {
        var frame = new AnimationFrame(durationMs);
        foreach (var layer in Layers)
        {
            frame.Cels[layer.Id] = new AnimationCel(layer.Id, Width, Height);
        }

        int targetIndex = insertIndex.HasValue
            ? Math.Clamp(insertIndex.Value, 0, Frames.Count)
            : Frames.Count;

        Frames.Insert(targetIndex, frame);
        SwitchToFrame(targetIndex);
        return frame;
    }

    /// <summary>
    /// Duplicates an existing frame by deep-copying its cels, inserts it immediately after the source frame, and switches to it.
    /// </summary>
    /// <param name="sourceIndex">Index of the frame to duplicate.</param>
    /// <returns>The newly cloned <see cref="AnimationFrame"/>.</returns>
    public AnimationFrame DuplicateFrame(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= Frames.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceIndex), "Source frame index is out of range.");
        }

        var sourceFrame = Frames[sourceIndex];
        var newFrame = new AnimationFrame(sourceFrame.DurationMs);

        foreach (var layer in Layers)
        {
            if (sourceFrame.Cels.TryGetValue(layer.Id, out var sourceCel))
            {
                var pixelsCopy = new PixelColor[sourceCel.Pixels.Length];
                Array.Copy(sourceCel.Pixels, pixelsCopy, sourceCel.Pixels.Length);
                newFrame.Cels[layer.Id] = new AnimationCel(layer.Id, pixelsCopy, sourceCel.IsEmpty);
            }
            else
            {
                newFrame.Cels[layer.Id] = new AnimationCel(layer.Id, Width, Height);
            }
        }

        int targetIndex = sourceIndex + 1;
        Frames.Insert(targetIndex, newFrame);
        SwitchToFrame(targetIndex);
        return newFrame;
    }

    /// <summary>
    /// Removes the frame at the specified index. Ensures at least one frame always remains in the timeline.
    /// </summary>
    /// <param name="index">Index of the frame to remove.</param>
    /// <returns>True if the frame was successfully removed; otherwise false.</returns>
    public bool RemoveFrame(int index)
    {
        if (Frames.Count <= 1 || index < 0 || index >= Frames.Count)
        {
            return false;
        }

        Frames.RemoveAt(index);

        // Clamp tag frame bounds to remaining frames
        foreach (var tag in Tags)
        {
            tag.FromFrame = Math.Clamp(tag.FromFrame, 0, Frames.Count - 1);
            tag.ToFrame = Math.Clamp(tag.ToFrame, 0, Frames.Count - 1);
        }

        int targetIndex;
        if (CurrentFrameIndex == index)
        {
            targetIndex = Math.Min(CurrentFrameIndex, Frames.Count - 1);
        }
        else if (CurrentFrameIndex > index)
        {
            targetIndex = CurrentFrameIndex - 1;
        }
        else
        {
            targetIndex = CurrentFrameIndex;
        }

        SwitchToFrame(targetIndex);
        return true;
    }

    /// <summary>
    /// Adds an animation tag grouping a range of frames.
    /// </summary>
    public AnimationTag AddTag(string name, int fromFrame, int toFrame, PixelColor? color = null, AnimationLoopMode loopMode = AnimationLoopMode.Forward, int repeatCount = 0)
    {
        int maxFrame = Math.Max(0, Frames.Count - 1);
        var tag = new AnimationTag
        {
            Name = name,
            FromFrame = Math.Clamp(fromFrame, 0, maxFrame),
            ToFrame = Math.Clamp(toFrame, 0, maxFrame),
            Color = color ?? new PixelColor(0x4A, 0x90, 0xE2, 0xFF),
            LoopMode = loopMode,
            RepeatCount = repeatCount
        };
        Tags.Add(tag);
        return tag;
    }

    /// <summary>
    /// Computes the flattened composite image for a specific animation frame without changing the active frame.
    /// </summary>
    /// <param name="frameIndex">0-based frame index to composite.</param>
    /// <param name="destination">Optional destination buffer. If null or wrong size, a new array is allocated.</param>
    /// <param name="singleLayerId">Optional layer ID. If specified, only that layer's cel is composited.</param>
    /// <returns>Pixel array with composite colors.</returns>
    public PixelColor[] GetFrameCompositePixels(int frameIndex, PixelColor[]? destination = null, Guid? singleLayerId = null)
    {
        if (frameIndex < 0 || frameIndex >= Frames.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        int totalPixels = Width * Height;
        if (destination == null || destination.Length != totalPixels)
        {
            destination = new PixelColor[totalPixels];
        }
        else
        {
            Array.Clear(destination, 0, totalPixels);
        }

        var frame = Frames[frameIndex];

        for (int layerIdx = 0; layerIdx < Layers.Count; layerIdx++)
        {
            var layer = Layers[layerIdx];
            if (!layer.IsVisible || layer.Opacity <= 0f)
            {
                continue;
            }

            if (singleLayerId.HasValue && layer.Id != singleLayerId.Value)
            {
                continue;
            }

            if (!frame.Cels.TryGetValue(layer.Id, out var cel))
            {
                continue;
            }

            float layerOpacity = layer.Opacity;
            var celPixels = cel.Pixels;

            for (int p = 0; p < totalPixels; p++)
            {
                var srcPixel = celPixels[p];
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

    private void InitializeAnimationTimeline()
    {
        OnionSkin.PropertyChanged += (_, _) => InvalidateComposite();

        if (Frames.Count == 0)
        {
            var initialFrame = new AnimationFrame(100);
            foreach (var layer in Layers)
            {
                initialFrame.Cels[layer.Id] = new AnimationCel(layer.Id, layer.Pixels);
            }
            Frames.Add(initialFrame);
            _currentFrameIndex = 0;
        }
    }

    private void OnLayersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (Layer oldLayer in e.OldItems)
            {
                UnhookLayerEvents(oldLayer);

                foreach (var frame in Frames)
                {
                    frame.Cels.Remove(oldLayer.Id);
                }
            }
        }

        if (e.NewItems != null)
        {
            foreach (Layer newLayer in e.NewItems)
            {
                HookLayerEvents(newLayer);

                foreach (var frame in Frames)
                {
                    if (!frame.Cels.ContainsKey(newLayer.Id))
                    {
                        if (frame == CurrentFrame)
                        {
                            frame.Cels[newLayer.Id] = new AnimationCel(newLayer.Id, newLayer.Pixels);
                        }
                        else
                        {
                            frame.Cels[newLayer.Id] = new AnimationCel(newLayer.Id, Width, Height);
                        }
                    }
                }
            }
        }

        InvalidateComposite();
    }
}
