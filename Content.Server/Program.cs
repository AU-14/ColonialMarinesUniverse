using Content.Shared.CMU;
using Robust.Server;

namespace Content.Server
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var options = new ServerOptions();
            options.MountOptions.DirMounts.Add(CMUContentPaths.DevelopmentResourceRoot);
            ContentStart.StartLibrary(args, options);
        }
    }
}
