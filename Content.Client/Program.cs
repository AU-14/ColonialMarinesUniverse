using Content.Shared.CMU;
using Robust.Client;

namespace Content.Client
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            var options = new GameControllerOptions();
            options.MountOptions.DirMounts.Add(CMUContentPaths.DevelopmentResourceRoot);
            ContentStart.StartLibrary(args, options);
        }
    }
}
