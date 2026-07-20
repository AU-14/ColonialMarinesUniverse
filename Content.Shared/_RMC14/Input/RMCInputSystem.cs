using Content.Shared._RMC14.CCVar;
using Content.Shared.Movement.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Input;

public sealed partial class RMCInputSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private INetManager _net = default!;

    private bool _activeInputMoverEnabled;

    private EntityQuery<ActorComponent> _actorQuery;

    public override void Initialize()
    {
        _actorQuery = GetEntityQuery<ActorComponent>();

        SubscribeLocalEvent<RMCActiveInputMoverComponent, MapInitEvent>(OnActiveMapInit);
        SubscribeLocalEvent<RMCActiveInputMoverComponent, PlayerAttachedEvent>(OnActiveAttached);
        SubscribeLocalEvent<RMCActiveInputMoverComponent, PlayerDetachedEvent>(OnActiveDetached);

        Subs.CVar(_config, RMCCVars.RMCActiveInputMoverEnabled, v => _activeInputMoverEnabled = v, true);
    }

    private void OnActiveMapInit(Entity<RMCActiveInputMoverComponent> ent, ref MapInitEvent args)
    {
        if (!_activeInputMoverEnabled || _net.IsClient)
            return;

        if (_actorQuery.HasComp(ent))
            EnsureComp<InputMoverComponent>(ent);
        else
            RemCompDeferred<InputMoverComponent>(ent);
    }

    private void OnActiveAttached(Entity<RMCActiveInputMoverComponent> ent, ref PlayerAttachedEvent args)
    {
        if (!_activeInputMoverEnabled)
            return;

        EnsureComp<InputMoverComponent>(ent);
    }

    private void OnActiveDetached(Entity<RMCActiveInputMoverComponent> ent, ref PlayerDetachedEvent args)
    {
        if (!_activeInputMoverEnabled)
            return;

        RemCompDeferred<InputMoverComponent>(ent);
    }
}
