using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public static class ProcessGeneratedCharacterSpritesheetLinux
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

    private struct RectI
    {
        public int Left;
        public int Top;
        public int Width;
        public int Height;
        public int Right { get { return Left + Width; } }
        public int Bottom { get { return Top + Height; } }

        public RectI(int left, int top, int width, int height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public static RectI FromLTRB(int left, int top, int right, int bottom)
        {
            return new RectI(left, top, right - left, bottom - top);
        }
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

        public RectI Padded(int width, int height, int pad)
        {
            return RectI.FromLTRB(
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
        using (var source = Image.Load<Rgba32>(config.Input))
        using (var transparent = RemoveGreenBackground(source, config))
        {
            CreateParentDirectory(config.Output);
            SaveIfSet(transparent, config.Transparent);

            var expected = config.Rows * config.Columns;
            var allBoxes = FindSpriteBoxes(transparent, config.MinArea)
                .OrderByDescending(box => box.Area)
                .ToList();
            var boxes = allBoxes.Take(expected).ToList();

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
        return "Usage: dotnet run --project tools/dotnet-linux/CharacterSpritesheet/CharacterSpritesheet.Linux.csproj -- --input <source.png> --output <atlas.png> " +
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

    private static void SaveIfSet(Image<Rgba32> image, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        CreateParentDirectory(path);
        image.SaveAsPng(path);
    }

    private static void CreateParentDirectory(string path)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);
    }

    private static Image<Rgba32> RemoveGreenBackground(Image<Rgba32> source, Config config)
    {
        var output = new Image<Rgba32>(source.Width, source.Height);
        var key = new Rgba32((byte)config.KeyR, (byte)config.KeyG, (byte)config.KeyB, 255);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                output[x, y] = RemoveKeyPixel(source[x, y], key, config);
            }
        }

        return output;
    }

    private static Rgba32 RemoveKeyPixel(Rgba32 color, Rgba32 key, Config config)
    {
        var distance = ColorDistance(color, key);
        var greenDominance = color.G - Math.Max(color.R, color.B);

        if (distance <= config.TransparentThreshold || IsStrongGreenKey(color))
            return new Rgba32(0, 0, 0, 0);

        var alpha = 255;
        if (distance < config.OpaqueThreshold && greenDominance > 18)
        {
            var t = (distance - config.TransparentThreshold) / (double)(config.OpaqueThreshold - config.TransparentThreshold);
            t = Math.Max(0, Math.Min(1, t));
            alpha = (int)Math.Round(255 * t);
        }

        var g = (int)color.G;
        if (config.Despill && color.G > Math.Max(color.R, color.B))
        {
            var neutralG = Math.Max(color.R, color.B);
            var spillAmount = alpha < 255 ? 1.0 : Math.Min(0.55, greenDominance / 255.0);
            g = ClampByte((int)Math.Round(color.G + (neutralG - color.G) * spillAmount));
        }

        return new Rgba32(color.R, (byte)g, color.B, (byte)alpha);
    }

    private static double ColorDistance(Rgba32 a, Rgba32 b)
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

    private static List<SpriteBox> FindSpriteBoxes(Image<Rgba32> image, int minArea)
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
                if (visited[x, y] || image[x, y].A == 0)
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
                        if (visited[nx, ny] || image[nx, ny].A == 0)
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

    private static void AddWarnings(Image<Rgba32> image, List<SpriteBox> ordered, List<SpriteBox> allBoxes, Config config)
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
            box.Warnings.AddRange(GetWarnings(image, cell, bounds, box, componentsByCell, config));
        }
    }

    private static RectI GetCell(Image<Rgba32> image, int index, Config config)
    {
        var cellW = image.Width / config.Columns;
        var cellH = image.Height / config.Rows;
        var col = index % config.Columns;
        var row = index / config.Columns;
        return new RectI(col * cellW, row * cellH, cellW, cellH);
    }

    private static List<string> GetWarnings(Image<Rgba32> image, RectI cell, RectI bounds, SpriteBox box, Dictionary<int, List<SpriteBox>> componentsByCell, Config config)
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

    private static void SaveValidationImage(Image<Rgba32> transparent, List<SpriteBox> boxes, string validationPath)
    {
        CreateParentDirectory(validationPath);
        using (var validation = new Image<Rgba32>(transparent.Width, transparent.Height, new Rgba32(0, 255, 0, 255)))
        {
            CopyImage(transparent, validation, 0, 0);
            for (var i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i].Padded(transparent.Width, transparent.Height, 3);
                DrawRect(validation, box, new Rgba32(255, 0, 0, 255), 4);
                if (boxes[i].Warnings.Count > 0)
                    DrawRect(validation, box, new Rgba32(255, 20, 20, 255), 8);
            }
            validation.SaveAsPng(validationPath);
        }
    }

    private static void SaveAtlas(Image<Rgba32> transparent, List<SpriteBox> boxes, string outputPath, Config config)
    {
        CreateParentDirectory(outputPath);
        var maxW = boxes.Max(box => box.Width + 6);
        var maxH = boxes.Max(box => box.Height + 6);
        var scale = Math.Min((double)config.MaxDrawW / maxW, (double)config.MaxDrawH / maxH);

        using (var atlas = new Image<Rgba32>(config.FrameW * config.Columns, config.FrameH * config.Rows, new Rgba32(0, 0, 0, 0)))
        {
            for (var i = 0; i < boxes.Count; i++)
            {
                var row = i / config.Columns;
                var col = i % config.Columns;
                var crop = boxes[i].Padded(transparent.Width, transparent.Height, 3);
                var drawW = Math.Max(1, (int)Math.Round(crop.Width * scale));
                var drawH = Math.Max(1, (int)Math.Round(crop.Height * scale));
                var dx = col * config.FrameW + (config.FrameW - drawW) / 2;
                var dy = row * config.FrameH + config.FrameH - drawH - 2;

                using (var frame = transparent.Clone(ctx => ctx.Crop(ToRectangle(crop)).Resize(new ResizeOptions
                {
                    Size = new Size(drawW, drawH),
                    Sampler = KnownResamplers.NearestNeighbor
                })))
                {
                    CopyImage(frame, atlas, dx, dy);
                }
            }
            atlas.SaveAsPng(outputPath);
        }
    }

    private static Rectangle ToRectangle(RectI rect)
    {
        return new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    private static void CopyImage(Image<Rgba32> source, Image<Rgba32> dest, int dx, int dy)
    {
        for (var y = 0; y < source.Height; y++)
        {
            var ty = dy + y;
            if (ty < 0 || ty >= dest.Height)
                continue;
            for (var x = 0; x < source.Width; x++)
            {
                var tx = dx + x;
                if (tx < 0 || tx >= dest.Width)
                    continue;
                dest[tx, ty] = source[x, y];
            }
        }
    }

    private static void DrawRect(Image<Rgba32> image, RectI rect, Rgba32 color, int thickness)
    {
        for (var t = 0; t < thickness; t++)
        {
            var left = Math.Max(0, rect.Left - t);
            var top = Math.Max(0, rect.Top - t);
            var right = Math.Min(image.Width - 1, rect.Right - 1 + t);
            var bottom = Math.Min(image.Height - 1, rect.Bottom - 1 + t);
            for (var x = left; x <= right; x++)
            {
                image[x, top] = color;
                image[x, bottom] = color;
            }
            for (var y = top; y <= bottom; y++)
            {
                image[left, y] = color;
                image[right, y] = color;
            }
        }
    }

    private static bool IsStrongGreenKey(Rgba32 color)
    {
        return color.G > 145 && color.R < 95 && color.B < 130 && color.G > color.R * 1.8 && color.G > color.B * 1.25;
    }
}
