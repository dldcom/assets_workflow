using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public static class ProcessConsumptionItemGridLinux
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

    private sealed class ItemResult
    {
        public string Name = "";
        public RectI Cell;
        public RectI Bounds;
        public int Area;
        public List<string> Warnings = new List<string>();
    }

    private sealed class Component
    {
        public int MinX;
        public int MinY;
        public int MaxX;
        public int MaxY;
        public int Area;
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
        using (var source = Image.Load<Rgba32>(config.Input))
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
        return "Usage: dotnet run --project tools/dotnet-linux/ConsumptionItemGrid/ConsumptionItemGrid.Linux.csproj -- --input <source.png> --output-dir <dir> " +
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

    private static Image<Rgba32> RemoveKeyBackground(Image<Rgba32> source, Config config)
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
            alpha = ClampByte((int)Math.Round(255 * Math.Max(0, Math.Min(1, t))));
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

    private static bool IsStrongGreenKey(Rgba32 color)
    {
        return color.G >= 205 && color.R <= 85 && color.B <= 95 && color.G - Math.Max(color.R, color.B) >= 120;
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
        return Math.Max(0, Math.Min(255, value));
    }

    private static List<ItemResult> ExtractItems(Image<Rgba32> image, Config config)
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
                var cell = new RectI(col * cellW, row * cellH, cellW, cellH);
                List<Component> components;
                if (!componentsByCell.TryGetValue(index, out components) || components.Count == 0)
                    throw new InvalidOperationException("Item " + config.Names[index] + " was not found in its grid cell.");

                var component = components[0];
                var bounds = RectI.FromLTRB(
                    Math.Max(0, component.MinX - config.Padding),
                    Math.Max(0, component.MinY - config.Padding),
                    Math.Min(image.Width, component.MaxX + config.Padding + 1),
                    Math.Min(image.Height, component.MaxY + config.Padding + 1));

                items.Add(new ItemResult
                {
                    Name = config.Names[index],
                    Cell = cell,
                    Bounds = bounds,
                    Area = component.Area,
                    Warnings = GetWarnings(image, cell, bounds, components, config)
                });
                index++;
            }
        }

        return items;
    }

    private static List<string> GetWarnings(Image<Rgba32> image, RectI cell, RectI bounds, List<Component> components, Config config)
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
            Console.Error.WriteLine("warning " + item.Name + ": " + string.Join(", ", item.Warnings.ToArray()));
    }

    private static List<Component> FindGlobalComponents(Image<Rgba32> image, Config config)
    {
        var scan = RectI.FromLTRB(config.CellInset, config.CellInset, image.Width - config.CellInset, image.Height - config.CellInset);
        var visited = new bool[scan.Width, scan.Height];
        var components = new List<Component>();

        for (var y = scan.Top; y < scan.Bottom; y++)
        {
            for (var x = scan.Left; x < scan.Right; x++)
            {
                var localX = x - scan.Left;
                var localY = y - scan.Top;
                if (visited[localX, localY] || image[x, y].A <= 18)
                    continue;

                components.Add(FloodFillComponent(image, scan, visited, x, y));
            }
        }

        return components;
    }

    private static Component FloodFillComponent(Image<Rgba32> image, RectI scan, bool[,] visited, int startX, int startY)
    {
        var component = new Component { MinX = startX, MinY = startY, MaxX = startX, MaxY = startY, Area = 0 };
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

    private static void TryVisit(Image<Rgba32> image, RectI scan, bool[,] visited, Queue<Point> queue, int x, int y)
    {
        if (x < scan.Left || x >= scan.Right || y < scan.Top || y >= scan.Bottom)
            return;

        var localX = x - scan.Left;
        var localY = y - scan.Top;
        if (visited[localX, localY])
            return;

        visited[localX, localY] = true;
        if (image[x, y].A <= 18)
            return;

        queue.Enqueue(new Point(x, y));
    }

    private static void SaveItems(Image<Rgba32> source, List<ItemResult> results, Config config)
    {
        foreach (var item in results)
        {
            using (var output = RenderItem(source, item.Bounds, config))
            {
                output.SaveAsPng(Path.Combine(config.OutputDir, item.Name + ".png"));
            }
        }
    }

    private static Image<Rgba32> RenderItem(Image<Rgba32> source, RectI bounds, Config config)
    {
        var output = new Image<Rgba32>(config.FrameW, config.FrameH, new Rgba32(0, 0, 0, 0));
        var scale = Math.Min(config.MaxDrawW / (double)bounds.Width, config.MaxDrawH / (double)bounds.Height);
        var drawW = Math.Max(1, (int)Math.Round(bounds.Width * scale));
        var drawH = Math.Max(1, (int)Math.Round(bounds.Height * scale));
        var dx = (config.FrameW - drawW) / 2;
        var dy = (config.FrameH - drawH) / 2;

        using (var item = source.Clone(ctx => ctx.Crop(ToRectangle(bounds)).Resize(new ResizeOptions
        {
            Size = new Size(drawW, drawH),
            Sampler = KnownResamplers.Bicubic
        })))
        {
            CopyImage(item, output, dx, dy);
        }

        return output;
    }

    private static void SaveAtlas(string outputDir, List<ItemResult> results, Config config)
    {
        CreateParentDirectory(config.Atlas);
        using (var atlas = new Image<Rgba32>(config.Columns * config.FrameW, config.Rows * config.FrameH, new Rgba32(0, 0, 0, 0)))
        {
            for (var i = 0; i < results.Count; i++)
            {
                using (var item = Image.Load<Rgba32>(Path.Combine(outputDir, results[i].Name + ".png")))
                {
                    var col = i % config.Columns;
                    var row = i / config.Columns;
                    CopyImage(item, atlas, col * config.FrameW, row * config.FrameH);
                }
            }
            atlas.SaveAsPng(config.Atlas);
        }
    }

    private static void SaveValidation(Image<Rgba32> image, List<ItemResult> results, string path)
    {
        CreateParentDirectory(path);
        using (var validation = new Image<Rgba32>(image.Width, image.Height, new Rgba32(0, 255, 0, 255)))
        {
            CopyImage(image, validation, 0, 0);
            foreach (var item in results)
            {
                DrawRect(validation, item.Cell, new Rgba32(30, 144, 255, 210), 3);
                DrawRect(validation, item.Bounds, new Rgba32(255, 70, 70, 230), 4);
                if (item.Warnings.Count > 0)
                    DrawRect(validation, item.Cell, new Rgba32(255, 20, 20, 255), 8);
            }
            validation.SaveAsPng(path);
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
}
