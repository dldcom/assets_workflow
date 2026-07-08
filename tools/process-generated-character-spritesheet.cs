using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

public static class ProcessGeneratedCharacterSpritesheet
{
    private sealed class Config
    {
        public string Input = "";
        public string Output = "";
        public string Transparent = "";
        public string Validation = "";
        public int Columns = 4;
        public int Rows = 4;
        public int FrameW = 48;
        public int FrameH = 64;
        public int MaxDrawW = 43;
        public int MaxDrawH = 60;
        public int MinArea = 500;
        public int WarningMargin = 4;
        public int KeyR = 0;
        public int KeyG = 255;
        public int KeyB = 0;
        public int TransparentThreshold = 70;
        public int OpaqueThreshold = 180;
        public bool Despill = true;
    }

    private sealed class SpriteBox
    {
        public int MinX;
        public int MinY;
        public int MaxX;
        public int MaxY;
        public int Area;
        public List<string> Warnings = new List<string>();

        public int Width { get { return MaxX - MinX + 1; } }
        public int Height { get { return MaxY - MinY + 1; } }
        public double CenterX { get { return (MinX + MaxX) / 2.0; } }
        public double CenterY { get { return (MinY + MaxY) / 2.0; } }

        public Rectangle Padded(int width, int height, int pad)
        {
            return Rectangle.FromLTRB(
                Math.Max(0, MinX - pad),
                Math.Max(0, MinY - pad),
                Math.Min(width, MaxX + pad + 1),
                Math.Min(height, MaxY + pad + 1));
        }
    }

    public static int Main(string[] args)
    {
        try
        {
            var config = ParseArgs(args);
            Run(config);
            Console.WriteLine(config.Output);
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
        using (var transparent = RemoveGreenBackground(source, config))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(config.Output)));
            SaveIfSet(transparent, config.Transparent);

            var expected = config.Rows * config.Columns;
            var allBoxes = FindSpriteBoxes(transparent, config.MinArea)
                .OrderByDescending(box => box.Area)
                .ToList();
            var boxes = allBoxes
                .Take(expected)
                .ToList();

            if (boxes.Count != expected)
                throw new InvalidOperationException("Expected " + expected + " sprite components, found " + boxes.Count + ".");

            var ordered = OrderByGrid(boxes, config.Columns, config.Rows);
            AddWarnings(transparent, ordered, allBoxes, config);
            PrintWarnings(ordered);
            if (!string.IsNullOrWhiteSpace(config.Validation))
                SaveValidationImage(transparent, ordered, config.Validation);
            SaveAtlas(transparent, ordered, config.Output, config);
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
                case "--output": config.Output = value; break;
                case "--transparent": config.Transparent = value; break;
                case "--validation": config.Validation = value; break;
                case "--cols": config.Columns = int.Parse(value); break;
                case "--rows": config.Rows = int.Parse(value); break;
                case "--frame-width": config.FrameW = int.Parse(value); break;
                case "--frame-height": config.FrameH = int.Parse(value); break;
                case "--max-width": config.MaxDrawW = int.Parse(value); break;
                case "--max-height": config.MaxDrawH = int.Parse(value); break;
                case "--min-area": config.MinArea = int.Parse(value); break;
                case "--warning-margin": config.WarningMargin = int.Parse(value); break;
                case "--key-r": config.KeyR = int.Parse(value); break;
                case "--key-g": config.KeyG = int.Parse(value); break;
                case "--key-b": config.KeyB = int.Parse(value); break;
                case "--transparent-threshold": config.TransparentThreshold = int.Parse(value); break;
                case "--opaque-threshold": config.OpaqueThreshold = int.Parse(value); break;
                case "--despill": config.Despill = ParseBool(value); break;
                default: throw new ArgumentException("Unknown option: " + key);
            }
        }

        if (string.IsNullOrWhiteSpace(config.Input) || string.IsNullOrWhiteSpace(config.Output))
            throw new ArgumentException("--input and --output are required.");
        if (!File.Exists(config.Input))
            throw new FileNotFoundException("Input file not found.", config.Input);
        if (config.TransparentThreshold < 0 || config.OpaqueThreshold <= config.TransparentThreshold)
            throw new ArgumentException("--opaque-threshold must be greater than --transparent-threshold.");

        return config;
    }

    private static string Usage()
    {
        return "Usage: process-generated-character-spritesheet.exe --input <source.png> --output <atlas.png> " +
            "[--transparent <transparent.png>] [--validation <validation.png>] [--cols 4] [--rows 4] " +
            "[--frame-width 48] [--frame-height 64] [--max-width 43] [--max-height 60] [--min-area 500] " +
            "[--warning-margin 4] [--key-r 0] [--key-g 255] [--key-b 0] " +
            "[--transparent-threshold 70] [--opaque-threshold 180] [--despill true]";
    }

    private static bool ParseBool(string value)
    {
        if (value.Equals("1") || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value.Equals("0") || value.Equals("false", StringComparison.OrdinalIgnoreCase) || value.Equals("no", StringComparison.OrdinalIgnoreCase))
            return false;
        throw new ArgumentException("Expected boolean value, got: " + value);
    }

    private static void SaveIfSet(Bitmap image, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        image.Save(path, ImageFormat.Png);
    }

    private static Bitmap RemoveGreenBackground(Bitmap source, Config config)
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
            t = Math.Max(0, Math.Min(1, t));
            alpha = (int)Math.Round(255 * t);
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

    private static double ColorDistance(Color a, Color b)
    {
        var dr = a.R - b.R;
        var dg = a.G - b.G;
        var db = a.B - b.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private static int ClampByte(int value)
    {
        if (value < 0) return 0;
        if (value > 255) return 255;
        return value;
    }

    private static List<SpriteBox> FindSpriteBoxes(Bitmap image, int minArea)
    {
        var visited = new bool[image.Width, image.Height];
        var boxes = new List<SpriteBox>();
        var queue = new Queue<Point>();
        var dx = new[] { 1, -1, 0, 0 };
        var dy = new[] { 0, 0, 1, -1 };

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (visited[x, y] || image.GetPixel(x, y).A == 0)
                    continue;

                var box = new SpriteBox { MinX = x, MaxX = x, MinY = y, MaxY = y };
                visited[x, y] = true;
                queue.Enqueue(new Point(x, y));

                while (queue.Count > 0)
                {
                    var point = queue.Dequeue();
                    box.Area++;
                    if (point.X < box.MinX) box.MinX = point.X;
                    if (point.X > box.MaxX) box.MaxX = point.X;
                    if (point.Y < box.MinY) box.MinY = point.Y;
                    if (point.Y > box.MaxY) box.MaxY = point.Y;

                    for (var i = 0; i < 4; i++)
                    {
                        var nx = point.X + dx[i];
                        var ny = point.Y + dy[i];
                        if (nx < 0 || ny < 0 || nx >= image.Width || ny >= image.Height)
                            continue;
                        if (visited[nx, ny] || image.GetPixel(nx, ny).A == 0)
                            continue;
                        visited[nx, ny] = true;
                        queue.Enqueue(new Point(nx, ny));
                    }
                }

                if (box.Area >= minArea)
                    boxes.Add(box);
            }
        }

        return boxes;
    }

    private static List<SpriteBox> OrderByGrid(List<SpriteBox> boxes, int columns, int rows)
    {
        var ordered = new List<SpriteBox>();
        var rowGroups = boxes
            .OrderBy(box => box.CenterY)
            .Select((box, index) => new { box, index })
            .GroupBy(item => item.index / columns)
            .Select(group => group.Select(item => item.box).OrderBy(box => box.CenterX).ToList())
            .ToList();

        if (rowGroups.Count != rows)
            throw new InvalidOperationException("Expected " + rows + " rows, found " + rowGroups.Count + ".");

        foreach (var row in rowGroups)
        {
            if (row.Count != columns)
                throw new InvalidOperationException("A detected row does not contain " + columns + " sprites.");
            ordered.AddRange(row);
        }

        return ordered;
    }

    private static void AddWarnings(Bitmap image, List<SpriteBox> ordered, List<SpriteBox> allBoxes, Config config)
    {
        var cellW = image.Width / config.Columns;
        var cellH = image.Height / config.Rows;
        var componentsByCell = allBoxes
            .GroupBy(box =>
            {
                var col = Math.Max(0, Math.Min(config.Columns - 1, (int)(box.CenterX / cellW)));
                var row = Math.Max(0, Math.Min(config.Rows - 1, (int)(box.CenterY / cellH)));
                return row * config.Columns + col;
            })
            .ToDictionary(group => group.Key, group => group.ToList());

        for (var i = 0; i < ordered.Count; i++)
        {
            var box = ordered[i];
            var cell = GetCell(image, i, config);
            var bounds = box.Padded(image.Width, image.Height, 3);
            var warnings = GetWarnings(image, cell, bounds, box, componentsByCell, config);
            box.Warnings.AddRange(warnings);
        }
    }

    private static Rectangle GetCell(Bitmap image, int index, Config config)
    {
        var cellW = image.Width / config.Columns;
        var cellH = image.Height / config.Rows;
        var col = index % config.Columns;
        var row = index / config.Columns;
        return new Rectangle(col * cellW, row * cellH, cellW, cellH);
    }

    private static List<string> GetWarnings(Bitmap image, Rectangle cell, Rectangle bounds, SpriteBox box, Dictionary<int, List<SpriteBox>> componentsByCell, Config config)
    {
        var warnings = new List<string>();
        var margin = config.WarningMargin;
        var spillMargin = Math.Max(config.WarningMargin, 18);
        var cellW = image.Width / config.Columns;
        var cellH = image.Height / config.Rows;
        var col = Math.Max(0, Math.Min(config.Columns - 1, (int)(box.CenterX / cellW)));
        var row = Math.Max(0, Math.Min(config.Rows - 1, (int)(box.CenterY / cellH)));
        var cellIndex = row * config.Columns + col;

        List<SpriteBox> components;
        if (componentsByCell.TryGetValue(cellIndex, out components) && components.Count > 1)
            warnings.Add("multiple-components");

        if (bounds.Left <= margin || bounds.Top <= margin || bounds.Right >= image.Width - margin || bounds.Bottom >= image.Height - margin)
            warnings.Add("image-edge");

        if (bounds.Left < cell.Left - spillMargin || bounds.Top < cell.Top - spillMargin || bounds.Right > cell.Right + spillMargin || bounds.Bottom > cell.Bottom + spillMargin)
            warnings.Add("cell-spill");

        return warnings;
    }

    private static void PrintWarnings(List<SpriteBox> boxes)
    {
        for (var i = 0; i < boxes.Count; i++)
        {
            if (boxes[i].Warnings.Count > 0)
                Console.Error.WriteLine("warning sprite " + (i + 1) + ": " + string.Join(", ", boxes[i].Warnings.ToArray()));
        }
    }

    private static void SaveValidationImage(Bitmap transparent, List<SpriteBox> boxes, string validationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(validationPath)));
        using (var validation = new Bitmap(transparent.Width, transparent.Height, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(validation))
        using (var pen = new Pen(Color.Red, 4))
        using (var warningPen = new Pen(Color.FromArgb(255, 255, 20, 20), 8))
        using (var font = new Font(FontFamily.GenericSansSerif, 28, FontStyle.Bold))
        using (var warningFont = new Font(FontFamily.GenericSansSerif, 18, FontStyle.Bold))
        using (var brush = new SolidBrush(Color.Red))
        using (var warningBrush = new SolidBrush(Color.FromArgb(245, 255, 50, 50)))
        using (var shadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
        {
            g.Clear(Color.FromArgb(255, 0, 255, 0));
            g.DrawImage(transparent, 0, 0);

            for (var i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i].Padded(transparent.Width, transparent.Height, 3);
                g.DrawRectangle(pen, box);
                if (boxes[i].Warnings.Count > 0)
                    g.DrawRectangle(warningPen, box);
                g.DrawString((i + 1).ToString(), font, brush, box.Left + 4, box.Top + 4);
                if (boxes[i].Warnings.Count > 0)
                {
                    var warning = "WARN " + string.Join(" ", boxes[i].Warnings.ToArray());
                    var warningPoint = new PointF(box.Left + 4, box.Top + 36);
                    g.DrawString(warning, warningFont, shadow, warningPoint.X + 2, warningPoint.Y + 2);
                    g.DrawString(warning, warningFont, warningBrush, warningPoint);
                }
            }

            validation.Save(validationPath, ImageFormat.Png);
        }
    }

    private static void SaveAtlas(Bitmap transparent, List<SpriteBox> boxes, string outputPath, Config config)
    {
        var maxW = boxes.Max(box => box.Width + 6);
        var maxH = boxes.Max(box => box.Height + 6);
        var scale = Math.Min((double)config.MaxDrawW / maxW, (double)config.MaxDrawH / maxH);

        using (var atlas = new Bitmap(config.FrameW * config.Columns, config.FrameH * config.Rows, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(atlas))
        {
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceOver;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            for (var i = 0; i < boxes.Count; i++)
            {
                var row = i / config.Columns;
                var col = i % config.Columns;
                var crop = boxes[i].Padded(transparent.Width, transparent.Height, 3);
                var drawW = Math.Max(1, (int)Math.Round(crop.Width * scale));
                var drawH = Math.Max(1, (int)Math.Round(crop.Height * scale));
                var dx = col * config.FrameW + (config.FrameW - drawW) / 2;
                var dy = row * config.FrameH + config.FrameH - drawH - 2;

                g.DrawImage(transparent, new Rectangle(dx, dy, drawW, drawH), crop, GraphicsUnit.Pixel);
            }

            atlas.Save(outputPath, ImageFormat.Png);
        }
    }

    private static bool IsStrongGreenKey(Color color)
    {
        return color.G > 145 && color.R < 95 && color.B < 130 && color.G > color.R * 1.8 && color.G > color.B * 1.25;
    }
}
