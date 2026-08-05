using System.Collections.Generic;
using System.IO;
using System.Linq;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Utility;

public static partial class GameDataScrounger
{
    private readonly record struct ContentFile(string DiskPath, ResPath VfsPath);

    /// <summary>
    ///     Returns all files in a given content location that match a pattern.
    /// </summary>
    /// <param name="location">The directory within the VFS to search</param>
    /// <param name="pattern">A glob pattern to use as a filter</param>
    /// <param name="recursive">Whether to search directories within the given directory.</param>
    /// <returns>A list of all files within the VFS directory matching the pattern.</returns>
    public static string[] FilesInDirectory(string location, string pattern, bool recursive = true)
    {
        return EnumerateContentFiles(location, pattern, recursive)
            .Select(file => file.DiskPath)
            .ToArray();
    }

    /// <summary>
    ///     Returns all files in a given content location that match a pattern, as their VFS paths.
    /// </summary>
    /// <param name="location">The directory within the VFS to search</param>
    /// <param name="pattern">A glob pattern to use as a filter</param>
    /// <param name="recursive">Whether to search directories within the given directory.</param>
    /// <returns>A list of all file paths within the VFS directory matching the pattern.</returns>
    public static ResPath[] FilesInDirectoryInVfs(string location, string pattern, bool recursive = true)
    {
        return EnumerateContentFiles(location, pattern, recursive)
            .Select(file => file.VfsPath)
            .ToArray();
    }

    /// <summary>
    ///     Enumerates physical content files in VFS mount order, keeping the first file for duplicate VFS paths.
    /// </summary>
    private static IEnumerable<ContentFile> EnumerateContentFiles(
        string location,
        string pattern,
        bool recursive = true)
    {
        Assert.That(location, Does.StartWith("/"), "Path must be rooted.");

        var resourceLocation = new ResPath(location);
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var seen = new HashSet<ResPath>();

        foreach (var root in ContentResourceRoots())
        {
            var directory = Path.Combine(root, resourceLocation.ToRelativeSystemPath());
            if (!Directory.Exists(directory))
                continue;

            foreach (var file in Directory.EnumerateFiles(directory, pattern ?? "*", searchOption)
                         .Order(StringComparer.Ordinal))
            {
                var relativePath = Path.GetRelativePath(root, file);
                var vfsPath = ResPath.FromRelativeSystemPath(relativePath).ToRootedPath();
                if (seen.Add(vfsPath))
                    yield return new ContentFile(file, vfsPath);
            }
        }
    }

    /// <summary>
    ///     Get the full directory path that the executable is located in.
    /// </summary>
    private static string GetExecutableDirectory()
    {
        // TODO: remove this shitty hack, either through making it less hardcoded into shared,
        //   or by making our file structure less spaghetti somehow.
        var assembly = typeof(IResourceManager).Assembly;
        var location = assembly.Location;
        if (location == string.Empty)
        {
            // See https://docs.microsoft.com/en-us/dotnet/api/system.reflection.assembly.location?view=net-5.0#remarks
            // This doesn't apply to us really because we don't do that kind of publishing, but whatever.
            throw new InvalidOperationException("Cannot find path of executable.");
        }

        return Path.GetDirectoryName(location)!;
    }

    /// <summary>
    ///     Turns a relative path from the executable directory into a full path.
    /// </summary>
    private static string ExecutableRelativeFile(string file)
    {
        return Path.GetFullPath(Path.Combine(GetExecutableDirectory(), file));
    }

    /// <summary>
    ///     Mirrors a function in the engine for ease of maintenance.
    /// </summary>
    private static string FindContentRootDir()
    {
        return "../../";
    }

    /// <summary>
    ///     Mirrors a function in the engine for ease of maintenance.
    /// </summary>
    internal static string[] ContentResourceRoots()
    {
        return
        [
            ExecutableRelativeFile($"{FindContentRootDir()}Resources"),
            ExecutableRelativeFile(Content.Shared.CMU.CMUContentPaths.DevelopmentResourceRoot),
        ];
    }

    /// <summary>
    ///     Gets the real path of a path within the VFS.
    /// </summary>
    /// <param name="path">The path to get the real filesystem path of.</param>
    /// <returns>The real filesystem path.</returns>
    public static string GetContentPathOnDisk(string path)
    {
        Assert.That(path, Does.StartWith("/"), "Path must be rooted.");

        var relativePath = new ResPath(path).ToRelativeSystemPath();
        var roots = ContentResourceRoots();
        foreach (var root in roots)
        {
            var candidate = Path.Combine(root, relativePath);
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;
        }

        return Path.Combine(roots[0], relativePath);
    }
}
