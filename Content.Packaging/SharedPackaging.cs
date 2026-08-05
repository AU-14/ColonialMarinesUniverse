using Robust.Packaging;

namespace Content.Packaging;

public sealed class SharedPackaging
{
    public const string CMUContentDirectory = "Content.CMU";

    public static readonly IReadOnlySet<string> AdditionalIgnoredResources = new HashSet<string>
    {
        // MapRenderer outputs into Resources. Avoid these getting included in packaging.
        "MapImages",
    };

    /// <summary>
    /// Ensures the standard and CMU resource roots cannot resolve the same VFS path.
    /// </summary>
    public static void ValidateCMUResourcePaths(string contentDir)
    {
        var standardRoot = Path.Combine(contentDir, "Resources");
        var cmuRoot = Path.Combine(contentDir, CMUContentDirectory, "Resources");

        if (!Directory.Exists(standardRoot))
            throw new DirectoryNotFoundException($"Standard resource root does not exist: {standardRoot}");

        if (!Directory.Exists(cmuRoot))
            throw new DirectoryNotFoundException($"CMU resource root does not exist: {cmuRoot}");

        var ignored = RobustSharedPackaging.SharedIgnoredResources
            .Union(AdditionalIgnoredResources)
            .ToHashSet();
        var resources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var collisions = new List<string>();

        AddRoot(standardRoot);
        AddRoot(cmuRoot);

        if (collisions.Count == 0)
            return;

        collisions.Sort(StringComparer.Ordinal);
        throw new InvalidOperationException(
            "Resources and Content.CMU/Resources contain duplicate VFS paths. " +
            "Resource paths must be unique across both roots:\n" +
            string.Join('\n', collisions));

        void AddRoot(string root)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                         .Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(root, file);
                var separator = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
                var topLevel = separator < 0 ? relative : relative[..separator];
                if (ignored.Contains(topLevel))
                    continue;

                var vfsPath = relative
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');

                if (resources.TryAdd(vfsPath, file))
                    continue;

                collisions.Add($"/{vfsPath}\n  {resources[vfsPath]}\n  {file}");
            }
        }
    }
}
