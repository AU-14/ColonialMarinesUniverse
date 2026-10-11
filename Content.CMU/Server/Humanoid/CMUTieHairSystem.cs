using System.Linq;
using Content.Server.DoAfter;
using Content.Server.Humanoid;
using Content.Shared.CMU14.Humanoid;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Humanoid;

/// <summary>
/// Lets a humanoid with a long hairstyle tie their hair back into one of a curated set of
/// tied-back styles via a right-click verb, and later untie it to restore the original style.
/// Characters who spawn with a tied-back style can re-tie it into any of the other tied-back styles,
/// or untie it into <see cref="CMUHairStyles.SpawnedTiedUntieHairStyle"/>.
/// </summary>
public sealed partial class CMUTieHairSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private HumanoidOrganAppearanceSystem _humanoidAppearance = default!;

    private static readonly TimeSpan TieHairDelay = TimeSpan.FromSeconds(1.5);

    private static readonly VerbCategory TieHairBackCategory =
        new("cmu-tie-hair-back-verb-category", "/Textures/Interface/VerbIcons/outfit.svg.192dpi.png");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidProfileComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<HumanoidProfileComponent, CMUTieHairDoAfterEvent>(OnTieHairDoAfter);
        SubscribeLocalEvent<HumanoidProfileComponent, CMUUntieHairDoAfterEvent>(OnUntieHairDoAfter);
    }

    private void OnGetVerbs(Entity<HumanoidProfileComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        var uid = ent.Owner;
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

        if (!_humanoidAppearance.TryGetMarkings(
                uid,
                HumanoidVisualLayers.Hair,
                out _,
                out _,
                out var hairMarkings) ||
            hairMarkings.Count == 0)
        {
            return;
        }

        var currentHairId = hairMarkings[0].MarkingId;

        // Hair that spawned already tied back has no loose style to untie to, but can be
        // re-tied into any of the other tied-back styles.
        var tieable = IsTieable(currentHairId);
        if (!tieable && !IsTiedBack(currentHairId))
            return;

        if (!tieable)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cmu-tie-hair-back-untie-verb"),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
                Act = () => StartUntieHair(uid, CMUHairStyles.SpawnedTiedUntieHairStyle),
            });
        }

        foreach (var styleId in CMUHairStyles.TiedBackHairStyles)
        {
            if ((!tieable && styleId.Id == currentHairId) ||
                !ProtoMan.TryIndex<MarkingPrototype>(styleId, out var prototype))
            {
                continue;
            }

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

    private static bool IsTieable(string hairId)
    {
        return CMUHairStyles.TieableHairStyles.Any(style => style.Id == hairId);
    }

    private static bool IsTiedBack(string hairId)
    {
        return CMUHairStyles.TiedBackHairStyles.Any(style => style.Id == hairId);
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

    private void StartUntieHair(EntityUid uid, string? looseStyleId = null)
    {
        _popup.PopupEntity(Loc.GetString("cmu-tie-hair-back-untying-self"), uid, uid);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, TieHairDelay, new CMUUntieHairDoAfterEvent { LooseStyleId = looseStyleId }, uid, target: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnTieHairDoAfter(EntityUid uid, HumanoidProfileComponent component, CMUTieHairDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!_humanoidAppearance.TryGetMarkings(
                uid,
                HumanoidVisualLayers.Hair,
                out var organ,
                out _,
                out var hairMarkings) ||
            hairMarkings.Count == 0 ||
            !ProtoMan.TryIndex<MarkingPrototype>(args.TiedStyleId, out var tiedPrototype))
        {
            return;
        }

        var currentHair = hairMarkings[0];

        // Hair that spawned tied back has no loose style to return to, so it is re-tied
        // without being marked as tied (and gets no untie verb).
        if (IsTieable(currentHair.MarkingId))
        {
            var tied = EnsureComp<CMUTiedHairComponent>(uid);
            tied.OriginalHairId = currentHair.MarkingId;
            tied.OriginalHairColors = new List<Color>(currentHair.MarkingColors);
        }

        var replacement = tiedPrototype.AsMarking() with { Forced = currentHair.Forced };
        for (var i = 0; i < replacement.MarkingColors.Count && i < currentHair.MarkingColors.Count; i++)
        {
            replacement = replacement.WithColorAt(i, currentHair.MarkingColors[i]);
        }

        var updated = hairMarkings.ToList();
        updated[0] = replacement;
        _humanoidAppearance.SetMarkings(uid, organ, HumanoidVisualLayers.Hair, updated);
        _popup.PopupEntity(Loc.GetString("cmu-tie-hair-back-tied-self"), uid, uid);
        args.Handled = true;
    }

    private void OnUntieHairDoAfter(EntityUid uid, HumanoidProfileComponent component, CMUUntieHairDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!_humanoidAppearance.TryGetMarkings(
                uid,
                HumanoidVisualLayers.Hair,
                out var organ,
                out _,
                out var hairMarkings) ||
            hairMarkings.Count == 0)
        {
            return;
        }

        // Hair tied with the verb restores its stored original; hair that spawned tied back
        // unties into the fallback loose style, keeping its current colors.
        string originalHairId;
        List<Color> originalColors;
        if (TryComp<CMUTiedHairComponent>(uid, out var tied))
        {
            originalHairId = tied.OriginalHairId;
            originalColors = tied.OriginalHairColors;
        }
        else if (args.LooseStyleId != null && IsTiedBack(hairMarkings[0].MarkingId))
        {
            originalHairId = args.LooseStyleId;
            originalColors = hairMarkings[0].MarkingColors.ToList();
        }
        else
        {
            return;
        }

        if (!ProtoMan.TryIndex<MarkingPrototype>(originalHairId, out var originalPrototype))
            return;

        var replacement = originalPrototype.AsMarking() with { Forced = hairMarkings[0].Forced };
        for (var i = 0; i < replacement.MarkingColors.Count && i < originalColors.Count; i++)
        {
            replacement = replacement.WithColorAt(i, originalColors[i]);
        }

        var updated = hairMarkings.ToList();
        updated[0] = replacement;
        _humanoidAppearance.SetMarkings(uid, organ, HumanoidVisualLayers.Hair, updated);
        RemComp<CMUTiedHairComponent>(uid);
        _popup.PopupEntity(Loc.GetString("cmu-tie-hair-back-untied-self"), uid, uid);
        args.Handled = true;
    }
}
