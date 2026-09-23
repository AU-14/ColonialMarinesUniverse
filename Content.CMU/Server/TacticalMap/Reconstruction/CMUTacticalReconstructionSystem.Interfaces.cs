using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;

namespace Content.Server.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUTacticalReconstructionSystem
{
    private Enum UiKey(EntityUid source) => HasComp<CMUTacticalReconstructionComponent>(source) ? Key :
        HasComp<TacticalMapUserComponent>(source) ? TacticalMapUserUi.Key : TacticalMapComputerUi.Key;

    private void RegisterInterface<T>(Enum key) where T : Component
    {
        Subs.BuiEvents<T>(key, subs =>
        {
            subs.Event<CMUReconViewMessage>((Entity<T> e, ref CMUReconViewMessage m) => OnView(e.Owner, ref m));
            subs.Event<CMUReconOrderMessage>((Entity<T> e, ref CMUReconOrderMessage m) => OnOrder(e.Owner, ref m));
            subs.Event<CMUReconRouteMessage>((Entity<T> e, ref CMUReconRouteMessage m) => OnRoute(e.Owner, ref m));
            subs.Event<CMUReconSendMessage>((Entity<T> e, ref CMUReconSendMessage m) => OnSend(e.Owner, ref m));
            subs.Event<CMUReconCancelOrderMessage>((Entity<T> e, ref CMUReconCancelOrderMessage m) => OnCancelOrder(e.Owner, ref m));
            subs.Event<CMUReconClearOrdersMessage>((Entity<T> e, ref CMUReconClearOrdersMessage m) => OnClear(e.Owner, ref m));
        });
    }

    public void CloseSurvey(EntityUid source, EntityUid actor) => _surveys.Remove((source, actor));

    private static bool HasDrawingFaction(string? faction) => faction is
        SharedTacticalMapSystem.MarinesFaction or SharedTacticalMapSystem.XenosFaction or
        SharedTacticalMapSystem.GovforFaction or SharedTacticalMapSystem.OpforFaction or
        SharedTacticalMapSystem.ClfFaction or SharedTacticalMapSystem.WeYuFaction;

    private bool TrySource(EntityUid source, out EntityUid? map, out string faction)
    {
        if (TryComp<TacticalMapUserComponent>(source, out var user))
        {
            map = user.Map;
            faction = user.Govfor ? SharedTacticalMapSystem.GovforFaction :
                user.Opfor ? SharedTacticalMapSystem.OpforFaction :
                user.Clf ? SharedTacticalMapSystem.ClfFaction :
                user.WeYu ? SharedTacticalMapSystem.WeYuFaction :
                user.Xenos ? SharedTacticalMapSystem.XenosFaction : user.Marines ? SharedTacticalMapSystem.MarinesFaction : "";
            return true;
        }
        if (TryComp<TacticalMapComputerComponent>(source, out var computer))
        {
            map = computer.Map;
            faction = SharedTacticalMapSystem.NormalizeMapFaction(computer.Faction) ?? SharedTacticalMapSystem.MarinesFaction;
            if (!HasDrawingFaction(faction)) faction = "";
            return true;
        }
        map = null;
        faction = "";
        return false;
    }
}
