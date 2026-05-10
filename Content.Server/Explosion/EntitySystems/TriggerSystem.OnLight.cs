using Content.Server.Explosion.Components;
using Content.Shared._RMC14.Repairable;
using Content.Shared._RMC14.Smokeables;
using Content.Shared.Examine;
using Content.Shared.Explosion.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Sticky;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Discord;
using Robust.Shared.Prototypes;

namespace Content.Server.Explosion.EntitySystems;

public sealed partial class TriggerSystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    private ISawmill _sawmill = default!;

    public void InitializeOnLight()
    {
        SubscribeLocalEvent<CMUOnLightTimerTriggerComponent, UseInHandEvent>(OnLightUse);
        SubscribeLocalEvent<CMUOnLightTimerTriggerComponent, ExaminedEvent>(OnLightExamined);
        SubscribeLocalEvent<CMUOnLightTimerTriggerComponent, MapInitEvent>(RandomTimerGeneration);
    }

    private void OnLightExamined(EntityUid uid, CMUOnLightTimerTriggerComponent component, ExaminedEvent args)
    {
        if (args.IsInDetailsRange && component.Examinable)
            args.PushText(Loc.GetString("examine-trigger-timer", ("time", component.Delay)));
    }

    private void RandomTimerGeneration(Entity<CMUOnLightTimerTriggerComponent> ent, ref MapInitEvent args)
    {
        var (_, comp) = ent;

        if (!TryComp<CMUOnLightTimerTriggerComponent>(ent, out var timerTriggerComp))
            return;

        timerTriggerComp.Delay = _random.NextFloat(comp.Randmin, comp.Randmax);
    }

    private void OnLightUse(EntityUid uid, CMUOnLightTimerTriggerComponent component, ref UseInHandEvent args)
    {
        if (!TryComp<HandsComponent>(args.User, out var handsComponent))
            return;

        float randomroll = 0f;
        var used = args.User;
        var checkedHands = 0;
        var passCheck = false;

        foreach (var hand in _hands.EnumerateHands((used, handsComponent)))
        {
            if (!_hands.TryGetHeldItem((used, handsComponent), hand, out var hold))
                continue;

            if (_hands.TryGetHeldItem((used, handsComponent), hand, out var held))
            {
                checkedHands++;
                if (!HasComp<RMCLighterComponent>(held) && !HasComp<BlowtorchComponent>(held))
                    continue;

                if (TryComp<ItemToggleComponent>(held, out var toggle) && toggle.Activated)
                {
                    passCheck = true;
                    break;
                }
                else if (checkedHands>=2)
                {
                    return;
                }
            }
        }

        if (passCheck == false)
            return;

        if (args.Handled || HasComp<AutomatedTimerComponent>(uid))
            return;

        if (component.DoPopup)
            _popupSystem.PopupEntity(Loc.GetString("trigger-activated", ("device", uid)), args.User, args.User);

        if (component.InstantFuseChance == true) {
            randomroll = _random.NextFloat(1f,100f);
            if (randomroll <= component.Instfusechance)
                component.Delay = 0f;
        }

        StartFuseTimer((uid, component), args.User);

        args.Handled = true;
    }
}
