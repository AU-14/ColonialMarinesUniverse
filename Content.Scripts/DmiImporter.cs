using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SpaceWizards.RsiLib.DMI.Metadata;

namespace Content.Scripts;

public static class DmiImporter
{
    public static void Run()
    {
        Console.WriteLine("Enter a path to a .dmi file or a directory to search recursively:");
        var path = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("No path given, exiting.");
            return;
        }

        string[] files;
        if (File.Exists(path))
        {
            files = new[] { path };
        }
        else if (Directory.Exists(path))
        {
            files = Directory.EnumerateFiles(path, "*.dmi", SearchOption.AllDirectories).ToArray();
        }
        else
        {
            Console.WriteLine($"No file or directory found at {path}");
            return;
        }

        if (files.Length == 0)
        {
            Console.WriteLine($"No .dmi files found at {path}");
            return;
        }

        Console.WriteLine($"Found:\n{string.Join('\n', files)}");
        Console.WriteLine($"{files.Length} total. Converting to .rsi next to each source file. Continue? [Y/N]");
        var response = Console.ReadLine()?.Trim().ToUpper();
        if (response != "Y")
        {
            Console.WriteLine("Cancelling operation");
            return;
        }

        var parser = new MetadataParser();
        var converted = 0;

        foreach (var file in files)
        {
            Console.WriteLine($"Converting {file}");

            IMetadata? metadata;
            ParseError? error;
            using (var stream = File.OpenRead(file))
            {
                if (!parser.TryGetFileMetadata(stream, out metadata, out error))
                {
                    Console.WriteLine($"  Skipped: {error!.Message}");
                    continue;
                }
            }

            using var image = Image.Load<Rgba32>(file);
            using var rsi = metadata.ToRsi(image);

            var outputDir = Path.Combine(
                Path.GetDirectoryName(file) ?? ".",
                $"{Path.GetFileNameWithoutExtension(file)}.rsi");

            rsi.SaveToFolder(outputDir);
            Console.WriteLine($"  Saved {outputDir}");
            converted++;
        }

        Console.WriteLine($"Converted {converted}/{files.Length} files");
        Console.WriteLine("Check each meta.json for correct license/copyright before committing.");
    }
}
