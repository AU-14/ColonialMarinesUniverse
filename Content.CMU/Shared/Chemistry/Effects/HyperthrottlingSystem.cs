/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared._RMC14.Language.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Chemistry.Effects.Special;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HyperthrottlingComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    /// <summary>Languages this component added, so only those are removed afterwards.</summary>
    [DataField]
    public HashSet<ProtoId<LanguagePrototype>> AddedSpoken = new();

    [DataField]
    public HashSet<ProtoId<LanguagePrototype>> AddedUnderstood = new();

    [DataField]
    public List<ProtoId<LanguagePrototype>> Excluded = new();
}

public sealed class HyperthrottlingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedLanguageSystem _language = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HyperthrottlingComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HyperthrottlingComponent, ComponentShutdown>(OnShutdown);
    }

    /// <summary>
    /// Called from the Hyperthrottling chemical effect each metabolism tick.
    /// Adds the component the first time and just extends the timer afterwards.
    /// </summary>
    public void Apply(EntityUid uid, TimeSpan duration, List<ProtoId<LanguagePrototype>> excluded)
    {
        if (_net.IsClient || !HasComp<LanguageComponent>(uid))
            return;

        var comp = EnsureComp<HyperthrottlingComponent>(uid);
        comp.EndTime = _timing.CurTime + duration;
        comp.Excluded = excluded;
        Dirty(uid, comp);
    }

    // Language sets are only edited on the server; the client receives them through LanguageComponent state.
    private void OnStartup(Entity<HyperthrottlingComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;

        _language.GrantAllLanguages(ent, ent.Comp.Excluded, ent.Comp.AddedSpoken, ent.Comp.AddedUnderstood);
    }

    private void OnShutdown(Entity<HyperthrottlingComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient || TerminatingOrDeleted(ent))
            return;

        _language.RevokeGrantedLanguages(ent, ent.Comp.AddedSpoken, ent.Comp.AddedUnderstood);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<HyperthrottlingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime >= comp.EndTime)
                RemCompDeferred<HyperthrottlingComponent>(uid);
        }
    }
}
