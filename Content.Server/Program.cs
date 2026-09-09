using Robust.Server;
using Robust.Shared;

namespace Content.Server
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            ContentStart.StartLibrary(args, new ServerOptions
            {
                MountOptions = new MountOptions(
                    dirMounts: new List<string> { "../../Content.CMU/Resources" },
                    zipMounts: new List<string>()),
            });
        }
    }
}
