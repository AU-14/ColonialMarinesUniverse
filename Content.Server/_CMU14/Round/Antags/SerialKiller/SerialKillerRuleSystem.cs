using Content.Server.AU14.Systems;
using Content.Server.GameTicking.Rules;
using Robust.Shared.GameObjects;
using Content.Server._CMU14.Round.Antags.ColonyBounty;

namespace Content.Server._CMU14.Round.Antags.SerialKiller;

public sealed partial class SerialKillerRuleSystem : GameRuleSystem<SerialKillerRuleComponent>
{
    [Dependency] private readonly WantedSystem _wantedSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SerialKillerComponent, ComponentStartup>(OnSerialKillerSpawned);
    }

    private void OnSerialKillerSpawned(EntityUid uid, SerialKillerComponent component, ComponentStartup args)
    {
        _wantedSystem.SendPaperToGroup(ColonyCmbFax.MarshalBureauFaxGroup, "AUPaperSerialKiller");
    }
}
