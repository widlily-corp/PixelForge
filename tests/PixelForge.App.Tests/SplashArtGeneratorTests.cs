using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace PixelForge.App.Tests;

public class SplashArtGeneratorTests
{
    [Fact]
    public void GenerateSplashArtImage_ShouldCreateHighQualityPixelArtBanner()
    {
        int width = 540;
        int height = 180;

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;

        // 1. Dark Void / Space Gradient
        using (var bgPaint = new SKPaint())
        {
            bgPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, height),
                [new SKColor(10, 13, 24), new SKColor(20, 24, 44)],
                [0f, 1f],
                SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, width, height, bgPaint);
        }

        // 2. Distant Nebula Clouds (Cyan & Violet Glow)
        using (var nebulaPaint = new SKPaint())
        {
            nebulaPaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(width * 0.5f, height * 0.6f),
                180,
                [new SKColor(0, 210, 255, 45), new SKColor(129, 140, 248, 30), SKColors.Transparent],
                [0f, 0.5f, 1f],
                SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, width, height, nebulaPaint);
        }

        // 3. Pixel Stars in Sky
        var rand = new Random(42);
        using (var starPaint = new SKPaint { Style = SKPaintStyle.Fill })
        {
            for (int i = 0; i < 70; i++)
            {
                int sx = rand.Next(0, width);
                int sy = rand.Next(0, (int)(height * 0.65f));
                int brightness = rand.Next(140, 255);
                starPaint.Color = new SKColor((byte)brightness, (byte)brightness, 255, (byte)rand.Next(160, 255));
                int starSize = rand.Next(1, 3);
                canvas.DrawRect(sx, sy, starSize, starSize, starPaint);
            }
        }

        // 4. Distant Pixel Mountain Silhouettes
        using (var mtnPaint1 = new SKPaint { Color = new SKColor(22, 27, 48), Style = SKPaintStyle.Fill })
        using (var mtnPaint2 = new SKPaint { Color = new SKColor(32, 38, 68), Style = SKPaintStyle.Fill })
        {
            // Mountain back
            using var path1 = new SKPath();
            path1.MoveTo(0, height);
            path1.LineTo(0, 100);
            path1.LineTo(80, 55);
            path1.LineTo(160, 95);
            path1.LineTo(240, 45);
            path1.LineTo(320, 90);
            path1.LineTo(400, 50);
            path1.LineTo(480, 85);
            path1.LineTo(width, 60);
            path1.LineTo(width, height);
            path1.Close();
            canvas.DrawPath(path1, mtnPaint1);

            // Mountain front
            using var path2 = new SKPath();
            path2.MoveTo(0, height);
            path2.LineTo(0, 120);
            path2.LineTo(60, 80);
            path2.LineTo(140, 115);
            path2.LineTo(210, 75);
            path2.LineTo(270, 105);
            path2.LineTo(360, 70);
            path2.LineTo(430, 110);
            path2.LineTo(width, 85);
            path2.LineTo(width, height);
            path2.Close();
            canvas.DrawPath(path2, mtnPaint2);
        }

        // 5. Stylized Digital Floor Grid
        using (var gridPaint = new SKPaint { Color = new SKColor(0, 210, 255, 35), StrokeWidth = 1 })
        {
            for (int y = 140; y < height; y += 8)
            {
                canvas.DrawLine(0, y, width, y, gridPaint);
            }
            for (int x = 0; x < width; x += 16)
            {
                canvas.DrawLine(x, 140, (x - width / 2) * 1.5f + width / 2, height, gridPaint);
            }
        }

        // 6. Central Mystic Forge Anvil
        float cx = width * 0.5f;
        float cy = 110;

        // Pedestal
        using (var pedPaint = new SKPaint { Color = new SKColor(28, 33, 56), Style = SKPaintStyle.Fill })
        {
            canvas.DrawRoundRect(cx - 50, cy + 24, 100, 24, 4, 4, pedPaint);
            canvas.DrawRoundRect(cx - 65, cy + 44, 130, 16, 4, 4, pedPaint);
        }

        // Anvil Body (Cyberpunk Stylized)
        using (var anvilPaint = new SKPaint { Color = new SKColor(40, 48, 80), Style = SKPaintStyle.Fill })
        using (var hornPaint = new SKPaint { Color = new SKColor(55, 66, 110), Style = SKPaintStyle.Fill })
        using (var glowStroke = new SKPaint { Color = new SKColor(0, 210, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 2 })
        {
            // Base waist
            canvas.DrawRect(cx - 22, cy + 6, 44, 20, anvilPaint);

            // Horn left
            using var hornPath = new SKPath();
            hornPath.MoveTo(cx - 20, cy);
            hornPath.LineTo(cx - 58, cy + 2);
            hornPath.LineTo(cx - 20, cy + 14);
            hornPath.Close();
            canvas.DrawPath(hornPath, hornPaint);

            // Anvil face (top)
            canvas.DrawRoundRect(cx - 35, cy - 8, 75, 16, 2, 2, anvilPaint);
            // Highlight edge
            using var hlPaint = new SKPaint { Color = new SKColor(0, 210, 255, 200), StrokeWidth = 2, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(cx - 55, cy + 1, cx + 38, cy - 7, hlPaint);
        }

        // 7. Glowing Energy Core on Anvil (Neon Cyan / Purple Core)
        using (var corePaint = new SKPaint())
        {
            corePaint.Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy - 4),
                28,
                [new SKColor(255, 255, 255), new SKColor(0, 210, 255, 220), new SKColor(129, 140, 248, 0)],
                [0f, 0.4f, 1f],
                SKShaderTileMode.Clamp);
            canvas.DrawCircle(cx, cy - 4, 28, corePaint);
        }

        // 8. Pixel Sparks / Energy Runes rising from the anvil
        using (var sparkPaint = new SKPaint { Style = SKPaintStyle.Fill })
        {
            for (int i = 0; i < 45; i++)
            {
                float px = cx + (float)(rand.NextDouble() * 120 - 60);
                float py = cy - (float)(rand.NextDouble() * 80 + 5);
                float size = rand.Next(2, 5);

                sparkPaint.Color = (i % 3 == 0)
                    ? new SKColor(255, 240, 100, (byte)rand.Next(180, 255))
                    : new SKColor(0, 210, 255, (byte)rand.Next(180, 255));

                canvas.DrawRect(px, py, size, size, sparkPaint);
            }
        }

        // 9. Neon Vignette Rim Border
        using (var rimPaint = new SKPaint { Color = new SKColor(0, 210, 255, 60), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f })
        {
            canvas.DrawRect(1, 1, width - 2, height - 2, rimPaint);
        }

        // Save to Assets
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "PixelForge.sln")))
        {
            currentDir = currentDir.Parent;
        }

        currentDir.Should().NotBeNull("Solution root must be found");
        var assetsDir = Path.Combine(currentDir!.FullName, "src", "PixelForge.App", "Assets");
        Directory.Exists(assetsDir).Should().BeTrue();

        var targetPath = Path.Combine(assetsDir, "splash_art.png");
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var outStream = File.Open(targetPath, FileMode.Create, FileAccess.Write);
        data.SaveTo(outStream);
        File.Exists(targetPath).Should().BeTrue();
    }
}
