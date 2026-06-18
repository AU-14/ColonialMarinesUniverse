using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Clothing;

namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedAtmosphereSystem
{
    private void InitializeBreathTool()
    {
        SubscribeLocalEvent<BreathToolComponent, ComponentShutdown>(OnBreathToolShutdown);
        SubscribeLocalEvent<BreathToolComponent, ItemMaskToggledEvent>(OnMaskToggled);
    }

    private void OnBreathToolShutdown(Entity<BreathToolComponent> entity, ref ComponentShutdown args)
    {
        DisconnectInternals(entity);
    }

    public void DisconnectInternals(Entity<BreathToolComponent> entity, bool forced = false)
    {
        var old = entity.Comp.ConnectedInternalsEntity;

        if (old == null)
            return;

        // CMU14 start
        DebugBreathTool(entity, forced ? "disconnect-forced" : "disconnect", old);
        // CMU14 end

        entity.Comp.ConnectedInternalsEntity = null;

        if (_internalsQuery.TryComp(old, out var internalsComponent))
        {
            _internals.DisconnectBreathTool((old.Value, internalsComponent), entity.Owner, forced: forced);
        }

        Dirty(entity);
    }

    private void OnMaskToggled(Entity<BreathToolComponent> ent, ref ItemMaskToggledEvent args)
    {
        // CMU14 start
        DebugBreathTool(ent, args.Mask.Comp.IsToggled ? "mask-toggled-down" : "mask-toggled-up", args.Wearer);
        // CMU14 end

        if (args.Mask.Comp.IsToggled)
        {
            DisconnectInternals(ent, forced: true);
        }
        else
        {
            if (_internalsQuery.TryComp(args.Wearer, out var internals))
            {
                _internals.ConnectBreathTool((args.Wearer.Value, internals), ent);
            }
        }
    }

    // CMU14 start
    private void DebugBreathTool(
        Entity<BreathToolComponent> ent,
        string stage,
        EntityUid? wearer)
    {
        if (!IsDebugMask(ent.Owner))
            return;

        InternalsComponent? internals = null;
        var wearerHasInternals = wearer != null && _internalsQuery.TryComp(wearer.Value, out internals);
        _anesthesiaSawmill.Debug(
            $"[CMU anesthesia] atmosphere-breath-tool-{stage}: tool={DebugEntity(ent.Owner)}, wearer={DebugEntity(wearer)}, connected={DebugEntity(ent.Comp.ConnectedInternalsEntity)}, allowed={ent.Comp.AllowedSlots}, wearerHasInternals={wearerHasInternals}, wearerBreathTools={internals?.BreathTools.Count ?? -1}");
    }

    private bool IsDebugMask(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return false;

        var id = MetaData(uid).EntityPrototype?.ID;
        return id?.Contains("Mask", StringComparison.OrdinalIgnoreCase) == true ||
            id?.Contains("Gas", StringComparison.OrdinalIgnoreCase) == true ||
            id?.Contains("Breath", StringComparison.OrdinalIgnoreCase) == true;
    }

    private string DebugEntity(EntityUid? uid)
    {
        if (uid == null)
            return "null";

        if (TerminatingOrDeleted(uid.Value))
            return $"{uid.Value} deleted";

        var proto = MetaData(uid.Value).EntityPrototype?.ID ?? "no-proto";

        return $"{ToPrettyString(uid.Value)} proto={proto}";
    }
    // CMU14 end
}
