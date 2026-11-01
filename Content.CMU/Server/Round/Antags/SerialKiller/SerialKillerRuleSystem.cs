using Content.Server.CMU14.Systems;
using Content.Server.GameTicking.Rules;
using Robust.Shared.GameObjects;
using Content.Server.CMU14.Round.Antags.ColonyBounty;
using Content.Shared.CMU14.Yautja;

namespace Content.Server.CMU14.Round.Antags.SerialKiller;

public sealed partial class SerialKillerRuleSystem : GameRuleSystem<SerialKillerRuleComponent>
{
    [Dependency] private WantedSystem _wantedSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SerialKillerComponent, ComponentStartup>(OnSerialKillerSpawned);
    }

    private void OnSerialKillerSpawned(EntityUid uid, SerialKillerComponent component, ComponentStartup args)
    {
        _wantedSystem.SendPaperToGroup(ColonyCmbFax.MarshalBureauFaxGroup, "AUPaperSerialKiller");

        // same butcher the preds get, so they can get rid of a body
        EnsureComp<YautjaButcherAbilityComponent>(uid);
    }
}
