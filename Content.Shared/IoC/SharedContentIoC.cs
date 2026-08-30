using Content.Shared._RMC14.Localizations;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Localizations;

namespace Content.Shared.IoC
{
    public static class SharedContentIoC
    {
        public static void Register(IDependencyCollection deps)
        {
            deps.Register<MarkingManager, MarkingManager>();
            deps.Register<ContentLocalizationManager, ContentLocalizationManager>();

            // RMC14
            deps.Register<RMCLocalizationManager>();
        }
    }
}
