using System.Globalization;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Guidebook;

/// <summary>All industry chapters share one localized index for the current prototype set.</summary>
public sealed partial class CMUGuideRecipeCatalogSystem : EntitySystem
{
    [Dependency] private readonly ILocalizationManager _localization = default!;

    private CMUGuideRecipeCatalog? _catalog;
    private CultureInfo? _culture;

    public override void Initialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _catalog = null);
    }

    public CMUGuideRecipeCatalog GetCatalog()
    {
        var culture = _localization.DefaultCulture;
        if (_catalog == null || !Equals(culture, _culture))
        {
            _catalog = new CMUGuideRecipeCatalog(EntityManager, ProtoMan);
            _culture = culture;
        }
        return _catalog;
    }
}
