using Content.Shared.CMU14.Clothing.Gloves;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Clothing.Gloves;

/// <summary>
///     Cutting the fingertips off a pair of gloves with a knife or wirecutters, for the gloves that have a fingerless
///     version (see <see cref="CMUFingerlessGlovesComponent"/>).
/// </summary>
public sealed class CMUFingerlessGlovesSystem : EntitySystem
{
    private static readonly ProtoId<ToolQualityPrototype>[] Slicing = ["Slicing"];
    private static readonly ProtoId<ToolQualityPrototype>[] Cutting = ["Cutting"];

    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    /// <summary>Gloves prototype → its fingerless version.</summary>
    private readonly Dictionary<EntProtoId, (EntProtoId Result, TimeSpan Delay)> _cuts = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ClothingComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ClothingComponent, CMUCutGlovesDoAfterEvent>(OnCutDoAfter);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        BuildCuts();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            BuildCuts();
    }

    private void BuildCuts()
    {
        _cuts.Clear();
        foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract ||
                !proto.TryGetComponent(out CMUFingerlessGlovesComponent? fingerless, EntityManager.ComponentFactory))
            {
                continue;
            }

            foreach (var from in fingerless.From)
            {
                _cuts[from] = (proto.ID, fingerless.Delay);
            }
        }
    }

    private bool TryGetCut(EntityUid gloves, out EntProtoId result, out TimeSpan delay)
    {
        result = default;
        delay = default;
        if (MetaData(gloves).EntityPrototype is not { } proto ||
            !_cuts.TryGetValue(proto.ID, out var cut))
        {
            return false;
        }

        (result, delay) = cut;
        return true;
    }

    private void OnInteractUsing(Entity<ClothingComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryGetCut(ent, out _, out var delay))
            return;

        var qualities = _tool.HasQuality(args.Used, Slicing[0]) ? Slicing :
            _tool.HasQuality(args.Used, Cutting[0]) ? Cutting :
            null;
        if (qualities == null)
            return;

        args.Handled = true;

        if (_inventory.TryGetContainingSlot(ent.Owner, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-gloves-cut-take-off"), ent, args.User);
            return;
        }

        _tool.UseTool(args.Used, args.User, ent, (float) delay.TotalSeconds, qualities, new CMUCutGlovesDoAfterEvent());
    }

    private void OnCutDoAfter(Entity<ClothingComponent> ent, ref CMUCutGlovesDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || TerminatingOrDeleted(ent) || !TryGetCut(ent, out var result, out _))
            return;

        if (_inventory.TryGetContainingSlot(ent.Owner, out _))
            return;

        args.Handled = true;

        var user = args.User;
        var held = _hands.IsHolding(user, ent.Owner);
        var coords = Transform(ent).Coordinates;
        var name = Name(ent);
        Del(ent);

        var fingerless = Spawn(result, coords);
        if (held)
            _hands.PickupOrDrop(user, fingerless);

        _popup.PopupEntity(Loc.GetString("cmu-gloves-cut-done", ("gloves", name)), user, user);
    }
}
