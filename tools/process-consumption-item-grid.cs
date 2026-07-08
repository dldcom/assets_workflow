using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

public static class ProcessConsumptionItemGrid
{
    private sealed class Config
    {
        public string Input = "";
        public string OutputDir = "";
        public string Atlas = "";
        public string Transparent = "";
        public string Validation = "";
        public int Columns = 4;
        public int Rows = 4;
        public int FrameW = 128;
        public int FrameH = 128;
        public int MaxDrawW = 106;
        public int MaxDrawH = 106;
        public int Padding = 8;
        public int CellInset = 10;
        public int MinArea = 180;
        public int WarningMargin = 4;
        public int KeyR = 0;
        public int KeyG = 255;
        public int KeyB = 0;
        public int TransparentThreshold = 70;
        public int OpaqueThreshold = 185;
        public bool Despill = true;
        public string[] Names = DefaultNames();
    }

    private sealed class ItemResult
    {
        public string Name = "";
        public Rectangle Cell;
        public Rectangle Bounds;
        public int Area;
        public List<string> Warnings = new List<string>();
    }

    public static int Main(string[] args)
    {
        try
        {
            var config = ParseArgs(args);
            Run(config);
            Console.WriteLine(config.OutputDir);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(Usage());
            return 1;
        }
    }

    private static void Run(Config config)
    {
        using (var source = new Bitmap(config.Input))
        using (var transparent = RemoveKeyBackground(source, config))
        {
            Directory.CreateDirectory(Path.GetFullPath(config.OutputDir));
            SaveIfSet(transparent, config.Transparent);

            var results = ExtractItems(transparent, config);
            SaveItems(transparent, results, config);
            PrintWarnings(results);

            if (!string.IsNullOrWhiteSpace(config.Atlas))
                SaveAtlas(config.OutputDir, results, config);

            if (!string.IsNullOrWhiteSpace(config.Validation))
                SaveValidation(transparent, results, config.Validation);
        }
    }

    private static Config ParseArgs(string[] args)
    {
        var config = new Config();
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--"))
                throw new ArgumentException("Unexpected argument: " + key);
            if (i + 1 >= args.Length)
                throw new ArgumentException("Missing value for " + key);

            var value = args[++i];
            switch (key)
            {
                case "--input": config.Input = value; break;
                case "--output-dir": config.OutputDir = value; break;
                case "--atlas": config.Atlas = value; break;
                case "--transparent": config.Transparent = value; break;
                case "--validation": config.Validation = value; break;
                case "--cols": config.Columns = int.Parse(value); break;
                case "--rows": config.Rows = int.Parse(value); break;
                case "--frame-width": config.FrameW = int.Parse(value); break;
                case "--frame-height": config.FrameH = int.Parse(value); break;
                case "--max-width": config.MaxDrawW = int.Parse(value); break;
                case "--max-height": config.MaxDrawH = int.Parse(value); break;
                case "--padding": config.Padding = int.Parse(value); break;
                case "--cell-inset": config.CellInset = int.Parse(value); break;
                case "--min-area": config.MinArea = int.Parse(value); break;
                case "--warning-margin": config.WarningMargin = int.Parse(value); break;
                case "--key-r": config.KeyR = int.Parse(value); break;
                case "--key-g": config.KeyG = int.Parse(value); break;
                case "--key-b": config.KeyB = int.Parse(value); break;
                case "--transparent-threshold": config.TransparentThreshold = int.Parse(value); break;
                case "--opaque-threshold": config.OpaqueThreshold = int.Parse(value); break;
                case "--despill": config.Despill = ParseBool(value); break;
                case "--names": config.Names = value.Split(',').Select(NormalizeName).ToArray(); break;
                default: throw new ArgumentException("Unknown option: " + key);
            }
        }

        if (string.IsNullOrWhiteSpace(config.Input) || string.IsNullOrWhiteSpace(config.OutputDir))
            throw new ArgumentException("--input and --output-dir are required.");
        if (!File.Exists(config.Input))
            throw new FileNotFoundException("Input file not found.", config.Input);
        if (config.Columns <= 0 || config.Rows <= 0)
            throw new ArgumentException("--cols and --rows must be positive.");
        if (config.FrameW <= 0 || config.FrameH <= 0 || config.MaxDrawW <= 0 || config.MaxDrawH <= 0)
            throw new ArgumentException("Frame and draw sizes must be positive.");
        if (config.MaxDrawW > config.FrameW || config.MaxDrawH > config.FrameH)
            throw new ArgumentException("--max-width and --max-height must fit inside the frame.");
        if (config.Names.Length != config.Columns * config.Rows)
            throw new ArgumentException("--names must contain exactly cols*rows names.");
        if (config.OpaqueThreshold <= config.TransparentThreshold)
            throw new ArgumentException("--opaque-threshold must be greater than --transparent-threshold.");

        return config;
    }

    private static string Usage()
    {
        return "Usage: process-consumption-item-grid.exe --input <source.png> --output-dir <dir> " +
            "[--atlas <atlas.png>] [--transparent <transparent.png>] [--validation <validation.png>] " +
            "[--cols 4] [--rows 4] [--frame-width 128] [--frame-height 128] [--max-width 106] [--max-height 106] " +
            "[--padding 8] [--cell-inset 10] [--min-area 180] [--warning-margin 4] [--key-r 0] [--key-g 255] [--key-b 0] " +
            "[--transparent-threshold 70] [--opaque-threshold 185] [--despill true] [--names comma,separated,names]";
    }

    private static string[] DefaultNames()
    {
        return new[]
        {
            "pizza", "icecream", "star-cape", "performance-ticket",
            "hamburger", "juice", "toy-robot", "colored-pencils",
            "story-book", "soccer-ball", "flower-bouquet", "cookie",
            "movie-ticket", "headphones", "board-game", "gift-box"
        };
    }

    private static bool ParseBool(string value)
    {
        if (value.Equals("1") || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value.Equals("0") || value.Equals("false", StringComparison.OrdinalIgnoreCase) || value.Equals("no", StringComparison.OrdinalIgnoreCase))
            return false;
        throw new ArgumentException("Expected boolean value, got: " + value);
    }

    private static string NormalizeName(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(c =>
            (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '-').ToArray();
        var normalized = new string(chars);
        while (normalized.Contains("--"))
            normalized = normalized.Replace("--", "-");
        normalized = normalized.Trim('-');
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Item names cannot be empty.");
        return normalized;
    }

    private static void SaveIfSet(Bitmap image, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        image.Save(path, ImageFormat.Png);
    }

    private static Bitmap RemoveKeyBackground(Bitmap source, Config config)
    {
        var output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        var key = Color.FromArgb(config.KeyR, config.KeyG, config.KeyB);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var color = source.GetPixel(x, y);
                output.SetPixel(x, y, RemoveKeyPixel(color, key, config));
            }
        }
        return output;
    }

    private static Color RemoveKeyPixel(Color color, Color key, Config config)
    {
        var distance = ColorDistance(color, key);
        var greenDominance = color.G - Math.Max(color.R, color.B);

        if (distance <= config.TransparentThreshold || IsStrongGreenKey(color))
            return Color.Transparent;

        var alpha = 255;
        if (distance < config.OpaqueThreshold && greenDominance > 18)
        {
            var t = (distance - config.TransparentThreshold) / (double)(config.OpaqueThreshold - config.TransparentThreshold);
            alpha = ClampByte((int)Math.Round(255 * Math.Max(0, Math.Min(1, t))));
        }

        int r = color.R;
        int g = color.G;
        int b = color.B;
        if (config.Despill && color.G > Math.Max(color.R, color.B))
        {
            var neutralG = Math.Max(color.R, color.B);
            var spillAmount = alpha < 255 ? 1.0 : Math.Min(0.55, greenDominance / 255.0);
            g = ClampByte((int)Math.Round(color.G + (neutralG - color.G) * spillAmount));
        }

        return Color.FromArgb(alpha, r, g, b);
    }

    private static bool IsStrongGreenKey(Color color)
    {
        return color.G >= 205 && color.R <= 85 && color.B <= 95 && color.G - Math.Max(color.R, color.B) >= 120;
    }

    private static double ColorDistance(Color a, Color b)
    {
        var dr = a.R - b.R;
        var dg = a.G - b.G;
        var db = a.B - b.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private static int ClampByte(int value)
    {
        return Math.Max(0, Math.Min(255, value));
    }

    private static List<ItemResult> ExtractItems(Bitmap image, Config config)
    {
        var items = new List<ItemResult>();
        var cellW = image.Width / config.Columns;
        var cellH = image.Height / config.Rows;
        var componentsByCell = FindGlobalComponents(image, config)
            .Where(component => component.Area >= config.MinArea)
            .GroupBy(component =>
            {
                var centerX = (component.MinX + component.MaxX) / 2;
                var centerY = (component.MinY + component.MaxY) / 2;
                var col = Math.Max(0, Math.Min(config.Columns - 1, centerX / cellW));
                var row = Math.Max(0, Math.Min(config.Rows - 1, centerY / cellH));
                return row * config.Columns + col;
            })
            .ToDictionary(group => group.Key, group => group.OrderByDescending(component => component.Area).ToList());
        var index = 0;

        for (var row = 0; row < config.Rows; row++)
        {
            for (var col = 0; col < config.Columns; col++)
            {
                var cell = new Rectangle(col * cellW, row * cellH, cellW, cellH);
                List<Component> components;
                if (!componentsByCell.TryGetValue(index, out components) || components.Count == 0)
                    throw new InvalidOperationException("Item " + config.Names[index] + " was not found in its grid cell.");

                var component = components[0];
                var bounds = Rectangle.FromLTRB(
                    Math.Max(0, component.MinX - config.Padding),
                    Math.Max(0, component.MinY - config.Padding),
                    Math.Min(image.Width, component.MaxX + config.Padding + 1),
                    Math.Min(image.Height, component.MaxY + config.Padding + 1));

                var warnings = GetWarnings(image, cell, bounds, components, config);
                items.Add(new ItemResult
                {
                    Name = config.Names[index],
                    Cell = cell,
                    Bounds = bounds,
                    Area = component.Area,
                    Warnings = warnings
                });
                index++;
            }
        }

        return items;
    }

    private static List<string> GetWarnings(Bitmap image, Rectangle cell, Rectangle bounds, List<Component> components, Config config)
    {
        var warnings = new List<string>();
        var margin = config.WarningMargin;
        var spillMargin = Math.Max(config.WarningMargin, 18);

        if (components.Count > 1)
            warnings.Add("multiple-components");

        if (bounds.Left <= margin || bounds.Top <= margin || bounds.Right >= image.Width - margin || bounds.Bottom >= image.Height - margin)
            warnings.Add("image-edge");

        if (bounds.Left < cell.Left - spillMargin || bounds.Top < cell.Top - spillMargin || bounds.Right > cell.Right + spillMargin || bounds.Bottom > cell.Bottom + spillMargin)
            warnings.Add("cell-spill");

        return warnings;
    }

    private static void PrintWarnings(List<ItemResult> results)
    {
        foreach (var item in results.Where(result => result.Warnings.Count > 0))
        {
            Console.Error.WriteLine("warning " + item.Name + ": " + string.Join(", ", item.Warnings.ToArray()));
        }
    }

    private sealed class Component
    {
        public int MinX;
        public int MinY;
        public int MaxX;
        public int MaxY;
        public int Area;
    }

    private static List<Component> FindGlobalComponents(Bitmap image, Config config)
    {
        var scan = Rectangle.FromLTRB(
            config.CellInset,
            config.CellInset,
            image.Width - config.CellInset,
            image.Height - config.CellInset);
        var visited = new bool[scan.Width, scan.Height];
        var components = new List<Component>();

        for (var y = scan.Top; y < scan.Bottom; y++)
        {
            for (var x = scan.Left; x < scan.Right; x++)
            {
                var localX = x - scan.Left;
                var localY = y - scan.Top;
                if (visited[localX, localY] || image.GetPixel(x, y).A <= 18)
                    continue;

                components.Add(FloodFillComponent(image, scan, visited, x, y));
            }
        }

        return components;
    }

    private static Component FloodFillComponent(Bitmap image, Rectangle scan, bool[,] visited, int startX, int startY)
    {
        var component = new Component
        {
            MinX = startX,
            MinY = startY,
            MaxX = startX,
            MaxY = startY,
            Area = 0
        };
        var queue = new Queue<Point>();
        queue.Enqueue(new Point(startX, startY));
        visited[startX - scan.Left, startY - scan.Top] = true;

        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            component.Area++;
            component.MinX = Math.Min(component.MinX, point.X);
            component.MinY = Math.Min(component.MinY, point.Y);
            component.MaxX = Math.Max(component.MaxX, point.X);
            component.MaxY = Math.Max(component.MaxY, point.Y);

            TryVisit(image, scan, visited, queue, point.X + 1, point.Y);
            TryVisit(image, scan, visited, queue, point.X - 1, point.Y);
            TryVisit(image, scan, visited, queue, point.X, point.Y + 1);
            TryVisit(image, scan, visited, queue, point.X, point.Y - 1);
        }

        return component;
    }

    private static void TryVisit(Bitmap image, Rectangle scan, bool[,] visited, Queue<Point> queue, int x, int y)
    {
        if (x < scan.Left || x >= scan.Right || y < scan.Top || y >= scan.Bottom)
            return;

        var localX = x - scan.Left;
        var localY = y - scan.Top;
        if (visited[localX, localY])
            return;

        visited[localX, localY] = true;
        if (image.GetPixel(x, y).A <= 18)
            return;

        queue.Enqueue(new Point(x, y));
    }

    private static void SaveItems(Bitmap source, List<ItemResult> results, Config config)
    {
        foreach (var item in results)
        {
            using (var output = RenderItem(source, item.Bounds, config))
            {
                var path = Path.Combine(config.OutputDir, item.Name + ".png");
                output.Save(path, ImageFormat.Png);
            }
        }
    }

    private static Bitmap RenderItem(Bitmap source, Rectangle bounds, Config config)
    {
        var output = new Bitmap(config.FrameW, config.FrameH, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(output))
        {
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var scale = Math.Min(config.MaxDrawW / (double)bounds.Width, config.MaxDrawH / (double)bounds.Height);
            var drawW = Math.Max(1, (int)Math.Round(bounds.Width * scale));
            var drawH = Math.Max(1, (int)Math.Round(bounds.Height * scale));
            var dest = new Rectangle((config.FrameW - drawW) / 2, (config.FrameH - drawH) / 2, drawW, drawH);
            graphics.DrawImage(source, dest, bounds, GraphicsUnit.Pixel);
        }
        return output;
    }

    private static void SaveAtlas(string outputDir, List<ItemResult> results, Config config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(config.Atlas)));
        using (var atlas = new Bitmap(config.Columns * config.FrameW, config.Rows * config.FrameH, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(atlas))
        {
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            for (var i = 0; i < results.Count; i++)
            {
                using (var item = new Bitmap(Path.Combine(outputDir, results[i].Name + ".png")))
                {
                    var col = i % config.Columns;
                    var row = i / config.Columns;
                    graphics.DrawImage(item, col * config.FrameW, row * config.FrameH, config.FrameW, config.FrameH);
                }
            }

            atlas.Save(config.Atlas, ImageFormat.Png);
        }
    }

    private static void SaveValidation(Bitmap image, List<ItemResult> results, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        using (var validation = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(validation))
        using (var cellPen = new Pen(Color.FromArgb(210, 30, 144, 255), 3))
        using (var boundsPen = new Pen(Color.FromArgb(230, 255, 70, 70), 4))
        using (var warningPen = new Pen(Color.FromArgb(255, 255, 20, 20), 8))
        using (var font = new Font(FontFamily.GenericSansSerif, 22, FontStyle.Bold))
        using (var warningFont = new Font(FontFamily.GenericSansSerif, 18, FontStyle.Bold))
        using (var brush = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
        using (var warningBrush = new SolidBrush(Color.FromArgb(245, 255, 50, 50)))
        using (var shadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
        {
            graphics.Clear(Color.FromArgb(255, 0, 255, 0));
            graphics.DrawImage(image, 0, 0);

            for (var i = 0; i < results.Count; i++)
            {
                var item = results[i];
                graphics.DrawRectangle(cellPen, item.Cell);
                graphics.DrawRectangle(boundsPen, item.Bounds);
                if (item.Warnings.Count > 0)
                    graphics.DrawRectangle(warningPen, item.Cell);
                var label = (i + 1) + " " + item.Name + " area " + item.Area;
                var p = new PointF(item.Cell.Left + 8, item.Cell.Top + 8);
                graphics.DrawString(label, font, shadow, p.X + 2, p.Y + 2);
                graphics.DrawString(label, font, brush, p);
                if (item.Warnings.Count > 0)
                {
                    var warning = "WARN " + string.Join(" ", item.Warnings.ToArray());
                    var warningPoint = new PointF(item.Cell.Left + 8, item.Cell.Top + 38);
                    graphics.DrawString(warning, warningFont, shadow, warningPoint.X + 2, warningPoint.Y + 2);
                    graphics.DrawString(warning, warningFont, warningBrush, warningPoint);
                }
            }

            validation.Save(path, ImageFormat.Png);
        }
    }
}
