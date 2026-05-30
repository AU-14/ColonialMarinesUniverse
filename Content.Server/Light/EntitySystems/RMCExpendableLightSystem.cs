using Content.Shared._AU14.Flare;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.Server.Light.EntitySystems
{
    [UsedImplicitly]
    public sealed partial class RMCExpendableLightSystem : SharedExpendableLightSystem
    {
        [Dependency] private SharedPhysicsSystem _physics = default!;
        [Dependency] private ExpendableLightSystem _light = default!;
        [Dependency] private SharedDoAfterSystem _doAfter = default!;
        [Dependency] private SharedPopupSystem _popup = default!;

        private static readonly TimeSpan StompDelay = TimeSpan.FromSeconds(5);

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ExpendableLightComponent, ExaminedEvent>(OnExpendableLightExamined);
            SubscribeLocalEvent<ExpendableLightComponent, GrenadeContentThrownEvent>(OnGrenadeContentThrown);
            SubscribeLocalEvent<ExpendableLightComponent, GetVerbsEvent<AlternativeVerb>>(OnAddStompVerb);
            SubscribeLocalEvent<ExpendableLightComponent, FlareStompDoAfterEvent>(OnFlareStompDoAfter);
        }

        /// <summary>
        ///     Changes the description if light can't be picked up while on.
        /// </summary>
        private void OnExpendableLightExamined(Entity<ExpendableLightComponent> ent, ref ExaminedEvent args)
        {
            if (!ent.Comp.PickupWhileOn && ent.Comp.CurrentState != ExpendableLightState.Dead)
                args.PushMarkup(Loc.GetString("rmc-laser-designator-signal-flare-examine"));
        }

        /// <summary>
        ///     Turns on the light and makes it's body type static if enabled in the component.
        /// </summary>
        private void OnGrenadeContentThrown(Entity<ExpendableLightComponent> ent, ref GrenadeContentThrownEvent args)
        {
            if(!ent.Comp.PickupWhileOn)
                _physics.SetBodyType(ent, BodyType.Static);
            _light.TryActivate((ent.Owner,ent.Comp));
        }

        private void OnAddStompVerb(Entity<ExpendableLightComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanAccess || !args.CanInteract || !HasComp<FlareStomperComponent>(args.User))
                return;

            if (!ent.Comp.Activated)
                return;

            var user = args.User;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("expendable-light-stomp-verb"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/delete_transparent.svg.192dpi.png")),
                Act = () =>
                {
                    if (!ent.Comp.Activated)
                        return;

                    var ev = new FlareStompDoAfterEvent();
                    var doAfterArgs = new DoAfterArgs(EntityManager, user, StompDelay, ev, ent)
                    {
                        BreakOnMove = true,
                        BreakOnDamage = true,
                        NeedHand = false,
                    };
                    if (_doAfter.TryStartDoAfter(doAfterArgs))
                        _popup.PopupEntity(Loc.GetString("expendable-light-stomp-start"), user, user);
                },
            });
        }

        private void OnFlareStompDoAfter(Entity<ExpendableLightComponent> ent, ref FlareStompDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled)
                return;

            args.Handled = true;
            _light.ExtinguishFlare(ent);

            if (args.User is { } user)
                _popup.PopupEntity(Loc.GetString("expendable-light-stomp-finish"), ent, user);
        }
    }
}
