using PixelForge.Core.Models;

namespace PixelForge.Core.Palettes;

/// <summary>
/// Represents a named color palette for pixel art creation.
/// </summary>
public class Palette
{
    public string Name { get; set; }

    public List<PixelColor> Colors { get; }

    public int Count => Colors.Count;

    public PixelColor this[int index]
    {
        get => Colors[index];
        set => Colors[index] = value;
    }

    public Palette(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Colors = new List<PixelColor>();
    }

    public Palette(string name, IEnumerable<PixelColor> colors)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ArgumentNullException.ThrowIfNull(colors);
        Colors = new List<PixelColor>(colors);
    }

    public void Add(PixelColor color) => Colors.Add(color);

    public bool Remove(PixelColor color) => Colors.Remove(color);

    public void Clear() => Colors.Clear();

    public Palette Clone() => new(Name, Colors);
}
