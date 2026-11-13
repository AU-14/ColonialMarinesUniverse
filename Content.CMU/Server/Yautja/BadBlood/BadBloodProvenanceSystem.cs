using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

// stamps trophies and meat with the body they came from. every hunter's cuts get stamped, only the Bad Blood
// Lodge reads the stamp
public sealed partial class BadBloodProvenanceSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> XenoLarvaTag = "RMCXenoLarva";

    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private YautjaTrophySystem _trophies = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodTrophyOriginComponent, StackSplitEvent>(OnTrophySplit);
    }

    // split spawns a fresh prototype, so the hide split off a stack would forget its body
    private void OnTrophySplit(Entity<BadBloodTrophyOriginComponent> ent, ref StackSplitEvent args)
    {
        CopyTrophyOrigin(ent.Owner, args.NewId);
    }

    /// <summary>
    /// Stamps <paramref name="item"/> as taken from <paramref name="source"/>. The source is a body, or a severed
    /// part that already carries a stamp, in which case that stamp is carried over.
    /// </summary>
    public void StampTrophy(EntityUid item, EntityUid source)
    {
        if (TerminatingOrDeleted(item) || CopyTrophyOrigin(source, item))
            return;

        var origin = EnsureComp<BadBloodTrophyOriginComponent>(item);
        origin.Body = source;
        origin.WasPlayer = WasPlayer(source);
        origin.IsYautja = IsClanKind(source);
        origin.IsSynth = HasComp<SynthComponent>(source);
        origin.IsXeno = _trophies.IsXenoTrophyTarget(source);
        origin.IsLarva = IsLarva(source);
        origin.WasKill = _mobState.IsDead(source);
    }

    /// <summary>
    /// The meat version of <see cref="StampTrophy"/>.
    /// </summary>
    public void StampMeat(EntityUid meat, EntityUid source)
    {
        if (TerminatingOrDeleted(meat))
            return;

        var origin = EnsureComp<BadBloodMeatOriginComponent>(meat);
        if (TryComp(source, out BadBloodTrophyOriginComponent? limb))
        {
            origin.Body = limb.Body;
            origin.WasPlayer = limb.WasPlayer;
            origin.IsYautja = limb.IsYautja;
            origin.IsSynth = limb.IsSynth;
            origin.IsXeno = limb.IsXeno;
            origin.IsLarva = limb.IsLarva;
            origin.WasKill = limb.WasKill;
            return;
        }

        origin.Body = source;
        origin.WasPlayer = WasPlayer(source);
        origin.IsYautja = IsClanKind(source);
        origin.IsSynth = HasComp<SynthComponent>(source);
        origin.IsXeno = _trophies.IsXenoTrophyTarget(source);
        origin.IsLarva = IsLarva(source);
        origin.WasKill = _mobState.IsDead(source);
    }

    /// <summary>
    /// Carries a stamp from one item to the next, e.g. a flayed limb to the bone the cauldron makes of it.
    /// False if <paramref name="from"/> has no stamp.
    /// </summary>
    public bool CopyTrophyOrigin(EntityUid from, EntityUid to)
    {
        if (!TryComp(from, out BadBloodTrophyOriginComponent? source) || TerminatingOrDeleted(to))
            return false;

        var origin = EnsureComp<BadBloodTrophyOriginComponent>(to);
        origin.Body = source.Body;
        origin.WasPlayer = source.WasPlayer;
        origin.IsYautja = source.IsYautja;
        origin.IsSynth = source.IsSynth;
        origin.IsXeno = source.IsXeno;
        origin.IsLarva = source.IsLarva;
        origin.WasKill = source.WasKill;
        return true;
    }

    /// <summary>
    /// The body is prey worth Superiority Points: any human or xeno, of any faction and with or without a player,
    /// that is not a Yautja, synth, larva or parasite.
    /// </summary>
    public bool IsWorthyPrey(EntityUid body)
    {
        return (HasComp<HumanoidProfileComponent>(body) || _trophies.IsXenoTrophyTarget(body)) &&
               !IsClanKind(body) &&
               !HasComp<SynthComponent>(body) &&
               !IsLarva(body);
    }

    // Yautja and everything that hunts for them. a hellhound is a xeno to the trophy code and a thrall is its
    // master's servant, so without this a clan could feed the Lodge its own hounds
    public bool IsClanKind(EntityUid body)
    {
        return HasComp<YautjaComponent>(body) ||
               HasComp<YautjaHellhoundComponent>(body) ||
               HasComp<YautjaThrallComponent>(body);
    }

    // the mind leaves a body when its player ghosts, LastMind remembers it was there. recorded on the stamp for
    // the admin log and the tests, the Lodge no longer asks for it
    private bool WasPlayer(EntityUid body)
    {
        return TryComp(body, out MindContainerComponent? mind) &&
               (mind.Mind != null || mind.LastMind != null);
    }

    private bool IsLarva(EntityUid body)
    {
        return HasComp<XenoParasiteComponent>(body) || _tags.HasTag(body, XenoLarvaTag);
    }
}
