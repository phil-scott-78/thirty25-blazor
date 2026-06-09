using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace Thirty25.Web;

public class SocialImageService
{
    private const string InterFontPath = "Inter-VariableFont_opsz,wght.ttf";

    private static readonly SKTypeface BoldTypeface = LoadTypeface(SKFontStyleWeight.ExtraBold);
    private static readonly SKTypeface LightTypeface = LoadTypeface(SKFontStyleWeight.Light);
    private static readonly SKFont TitleFont = new(BoldTypeface, 40);
    private static readonly SKFont DescFont = new(LightTypeface, 24);
    private static readonly SKFont DateFont = new(LightTypeface, 20);
    private static readonly SKShaper TitleShaper = new(BoldTypeface);
    private static readonly SKShaper DescShaper = new(LightTypeface);

    public static byte[] RenderCard(string title, string description, string date)
    {
        const int width = 1200;
        const int height = 630;
        const int padding = 50;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(0x09, 0x09, 0x0b));

        if (File.Exists("social-bg.png"))
        {
            using var bgBitmap = SKBitmap.Decode("social-bg.png");
            if (bgBitmap != null)
            {
                var scale = (float)height / bgBitmap.Height * 1.25f;
                var scaledW = bgBitmap.Width * scale;
                var scaledH = bgBitmap.Height * scale;
                var dy = (height - scaledH) / 2f;
                using var bgImage = SKImage.FromBitmap(bgBitmap);
                canvas.DrawImage(bgImage,
                    new SKRect(width - scaledW, dy, width, dy + scaledH),
                    new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            }
        }

        using var gradientShader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(width * 0.7f, 0),
            [
                new SKColor(0x09, 0x09, 0x0b, 255),
                new SKColor(0x09, 0x09, 0x0b, 0xcc),
                new SKColor(0x09, 0x09, 0x0b, 0)
            ],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp);
        using var gradientPaint = new SKPaint();
        gradientPaint.Shader = gradientShader;
        canvas.DrawRect(new SKRect(0, 0, width * 0.7f, height), gradientPaint);

        float y = padding;

        using var titlePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        y += TitleFont.Size;
        y = DrawWrappedText(canvas, title, TitleShaper, TitleFont, titlePaint, padding, y, width * 0.55f - padding);
        y += 30;

        using var descPaint = new SKPaint { Color = new SKColor(255, 255, 255, 230), IsAntialias = true };
        DrawWrappedText(canvas, description, DescShaper, DescFont, descPaint, padding, y, width * 0.55f - padding, maxLines: 4);

        if (!string.IsNullOrEmpty(date))
        {
            using var datePaint = new SKPaint { Color = new SKColor(255, 255, 255, 200), IsAntialias = true };
            canvas.DrawText(date.ToUpperInvariant(), padding, height - padding, DateFont, datePaint);
        }

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static SKTypeface LoadTypeface(SKFontStyleWeight weight)
    {
        var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        // System Inter honors variable font weight axes; bundled file is a fallback at default weight
        return SKTypeface.FromFamilyName("Inter", style)
            ?? (File.Exists(InterFontPath) ? SKTypeface.FromFile(InterFontPath) : null)
            ?? SKTypeface.FromFamilyName("Segoe UI", style)
            ?? SKTypeface.FromFamilyName("Helvetica Neue", style)
            ?? SKTypeface.Default;
    }

    private static float DrawWrappedText(
        SKCanvas canvas, string text, SKShaper shaper, SKFont font, SKPaint paint,
        float x, float y, float maxWidth, int maxLines = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(text)) return y;

        var lineHeight = font.Size * 1.3f;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = string.Empty;
        var linesDrawn = 0;

        void DrawLine(string line)
        {
            var shaped = shaper.Shape(line, x, y, font);
            using var blob = SKTextBlob.CreatePositioned(line, font, shaped.Points);
            if (blob != null) canvas.DrawText(blob, 0, 0, paint);
        }

        foreach (var word in words)
        {
            if (linesDrawn >= maxLines) break;

            var candidate = currentLine.Length == 0 ? word : currentLine + " " + word;
            if (shaper.Shape(candidate, font).Width > maxWidth && currentLine.Length > 0)
            {
                DrawLine(currentLine);
                y += lineHeight;
                linesDrawn++;
                currentLine = word;
            }
            else
            {
                currentLine = candidate;
            }
        }

        if (linesDrawn < maxLines && currentLine.Length > 0)
        {
            DrawLine(currentLine);
            y += lineHeight;
        }

        return y;
    }
}
