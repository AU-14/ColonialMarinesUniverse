using System.Linq;
using Content.Server.DoAfter;
using Content.Server.Humanoid;
using Content.Shared._CMU14.Humanoid;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Humanoid;

/// <summary>
/// Lets a humanoid with a long hairstyle tie their hair back into one of a curated set of
/// tied-back styles via a right-click verb, and later untie it to restore the original style.
/// </summary>
public sealed partial class CMUTieHairSystem : EntitySystem
{
    [Dependency] private HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private MarkingManager _markingManager = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;

    private static readonly TimeSpan TieHairDelay = TimeSpan.FromSeconds(1.5);

    private static readonly VerbCategory TieHairBackCategory =
        new("cmu-tie-hair-back-verb-category", "/Textures/Interface/VerbIcons/outfit.svg.192dpi.png");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, CMUTieHairDoAfterEvent>(OnTieHairDoAfter);
        SubscribeLocalEvent<HumanoidAppearanceComponent, CMUUntieHairDoAfterEvent>(OnUntieHairDoAfter);
    }

    /// <summary>
    /// Adds the "Tie Hair Back"/"Untie Hair" verbs for this humanoid, if applicable. Called from
    /// <see cref="HumanoidAppearanceSystem"/>'s own GetVerbsEvent&lt;Verb&gt; handler, since only one
    /// system may subscribe to that event for HumanoidAppearanceComponent.
    /// </summary>
    public void AddVerbs(EntityUid uid, HumanoidAppearanceComponent component, GetVerbsEvent<Verb> args)
    {
        if (args.User != args.Target || !args.CanInteract)
            return;

        if (HasComp<CMUTiedHairComponent>(uid))
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-tie-hair-back-untie-verb"),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
                Act = () => StartUntieHair(uid),
            });
            return;
        }

        if (!component.MarkingSet.TryGetCategory(MarkingCategories.Hair, out var hairMarkings) || hairMarkings.Count == 0)
            return;

        var currentHair = hairMarkings[0];

        if (!CMUHairStyles.TieableHairStyles.Any(style => style.Id == currentHair.MarkingId))
            return;

        foreach (var styleId in CMUHairStyles.TiedBackHairStyles)
        {
            if (!_markingManager.Markings.TryGetValue(styleId, out var prototype))
                continue;

            var tiedStyleId = prototype.ID;
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString($"marking-{prototype.ID}"),
                Category = TieHairBackCategory,
                Icon = prototype.Sprites.Count > 0 ? prototype.Sprites[0] : null,
                Act = () => StartTieHair(uid, tiedStyleId),
            });
        }
    }

    private void StartTieHair(EntityUid uid, string tiedStyleId)
    {
        _popup.PopupEntity(Loc.GetString("cmu-tie-hair-back-tying-self"), uid, uid);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, TieHairDelay, new CMUTieHairDoAfterEvent { TiedStyleId = tiedStyleId }, uid, target: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void StartUntieHair(EntityUid uid)
    {
        _popup.PopupEntity(Loc.GetString("cmu-tie-hair-back-untying-self"), uid, uid);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, TieHairDelay, new CMUUntieHairDoAfterEvent(), uid, target: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnTieHairDoAfter(EntityUid uid, HumanoidAppearanceComponent component, CMUTieHairDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!component.MarkingSet.TryGetCategory(MarkingCategories.Hair, out var hairMarkings) || hairMarkings.Count == 0)
            return;

        var currentHair = hairMarkings[0];

        var tied = EnsureComp<CMUTiedHairComponent>(uid);
        tied.OriginalHairId = currentHair.MarkingId;
        tied.OriginalHairColors = new List<Color>(currentHair.MarkingColors);

        _humanoid.SetMarkingId(uid, MarkingCategories.Hair, 0, args.TiedStyleId, component);
        _popup.PopupEntity(Loc.GetString("cmu-tie-hair-back-tied-self"), uid, uid);
        args.Handled = true;
    }

    private void OnUntieHairDoAfter(EntityUid uid, HumanoidAppearanceComponent component, CMUUntieHairDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<CMUTiedHairComponent>(uid, out var tied))
            return;

        _humanoid.SetMarkingId(uid, MarkingCategories.Hair, 0, tied.OriginalHairId, component);
        _humanoid.SetMarkingColor(uid, MarkingCategories.Hair, 0, tied.OriginalHairColors, component);
        RemComp<CMUTiedHairComponent>(uid);
        _popup.PopupEntity(Loc.GetString("cmu-tie-hair-back-untied-self"), uid, uid);
        args.Handled = true;
    }
}
