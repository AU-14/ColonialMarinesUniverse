using Content.Shared.CMU;
using Robust.Client;

namespace Content.Replay;

internal static class Program
{
    public static void Main(string[] args)
    {
        var options = new GameControllerOptions
        {
            Sandboxing = true,
            ContentModulePrefix = "Content.",
            ContentBuildDirectory = "Content.Replay",
            DefaultWindowTitle = "SS14 Replay",
            UserDataDirectoryName = "Space Station 14",
            ConfigFileName = "replay.toml"
        };
        options.MountOptions.DirMounts.Add(CMUContentPaths.DevelopmentResourceRoot);
        ContentStart.StartLibrary(args, options);
    }
}
