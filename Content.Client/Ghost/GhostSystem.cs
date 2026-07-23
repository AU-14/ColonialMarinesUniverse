using Content.Client._RMC14.NightVision;
using Content.Client.Movement.Systems;
using Content.Shared._RMC14.Mentor.ImaginaryFriend;
using Content.Shared.Actions;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Ghost
{
    public sealed partial class GhostSystem : SharedGhostSystem
    {
        [Dependency] private IClientConsoleHost _console = default!;
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private SharedActionsSystem _actions = default!;
        [Dependency] private PointLightSystem _pointLightSystem = default!;
        [Dependency] private ContentEyeSystem _contentEye = default!;
        [Dependency] private SpriteSystem _sprite = default!;
        //RMC14
        [Dependency] private IOverlayManager _overlay = default!;

        public int AvailableGhostRoleCount { get; private set; }

        private bool _ghostVisibility = true;

        private bool GhostVisibility
        {
            get => _ghostVisibility;
            set
            {
                if (_ghostVisibility == value)
                {
                    return;
                }

                _ghostVisibility = value;

                var query = AllEntityQuery<GhostComponent, SpriteComponent>();
                while (query.MoveNext(out var uid, out _, out var sprite))
                {
                    _sprite.SetVisible((uid, sprite), value || uid == _playerManager.LocalEntity || HasComp<ImaginaryFriendComponent>(uid)); //RMC14
                }
            }
        }

        public GhostComponent? Player => CompOrNull<GhostComponent>(_playerManager.LocalEntity);
        public bool IsGhost => Player != null;

        public event Action<GhostComponent>? PlayerRemoved;
        public event Action<GhostComponent>? PlayerUpdated;
        public event Action<GhostComponent>? PlayerAttached;
        public event Action? PlayerDetached;
        public event Action<GhostWarpsResponseEvent>? GhostWarpsResponse;
        public event Action? GhostWarpsReset;
        public event Action<GhostUpdateGhostRoleCountEvent>? GhostRoleCountUpdated;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GhostComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<GhostComponent, ComponentRemove>(OnGhostRemove);
            SubscribeLocalEvent<GhostComponent, AfterAutoHandleStateEvent>(OnGhostState);

            SubscribeLocalEvent<GhostComponent, LocalPlayerAttachedEvent>(OnGhostPlayerAttach);
            SubscribeLocalEvent<GhostComponent, LocalPlayerDetachedEvent>(OnGhostPlayerDetach);

            SubscribeNetworkEvent<GhostWarpsResponseEvent>(OnGhostWarpsResponse);
            SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
            SubscribeNetworkEvent<GhostUpdateGhostRoleCountEvent>(OnUpdateGhostRoleCount);

            SubscribeLocalEvent<EyeComponent, ToggleLightingActionEvent>(OnToggleLighting);
            SubscribeLocalEvent<EyeComponent, ToggleFoVActionEvent>(OnToggleFoV);
            SubscribeLocalEvent<GhostComponent, ToggleGhostsActionEvent>(OnToggleGhosts);
            SubscribeLocalEvent<GhostComponent, ToggleSelfGhostActionEvent>(OnToggleSelfGhost); // CMU14
        }

        private void OnStartup(EntityUid uid, GhostComponent component, ComponentStartup args)
        {
            if (TryComp(uid, out SpriteComponent? sprite))
                _sprite.SetVisible((uid, sprite), GhostVisibility || uid == _playerManager.LocalEntity);
        }

        //RMC14
        private void OnToggleLighting(EntityUid uid, EyeComponent component, ToggleLightingActionEvent args)
        {
            if (args.Handled)
                return;

            TryComp<PointLightComponent>(uid, out var light);

            if (!component.DrawLight)
            {
                // normal lighting
                Popup.PopupEntity(Loc.GetString("ghost-gui-toggle-lighting-manager-popup-normal"), args.Performer);
                _contentEye.RequestEye(component.DrawFov, true);
            }
            else if ((!light?.Enabled ?? false) && !_overlay.HasOverlay<HalfNightVisionBrightnessOverlay>()) // skip this option if we have no PointLightComponent
            {
                // enable personal light
                Popup.PopupEntity(Loc.GetString("ghost-gui-toggle-lighting-manager-popup-personal-light"), args.Performer);
                _pointLightSystem.SetEnabled(uid, true, light);
            }
            else if ((light?.Enabled ?? false) && !_overlay.HasOverlay<HalfNightVisionBrightnessOverlay>())
            {
                //RMC14 half bright mode
                Popup.PopupEntity(Loc.GetString("rmc-ghost-gui-toggle-lighting-manager-popup-halfbright"), args.Performer);
                _pointLightSystem.SetEnabled(uid, false, light);
                _overlay.AddOverlay(new HalfNightVisionBrightnessOverlay());
            }
            else
            {
                // fullbright mode
                Popup.PopupEntity(Loc.GetString("ghost-gui-toggle-lighting-manager-popup-fullbright"), args.Performer);
                _contentEye.RequestEye(component.DrawFov, false);
                _pointLightSystem.SetEnabled(uid, false, light);
                _overlay.RemoveOverlay<HalfNightVisionBrightnessOverlay>();
            }
            args.Handled = true;
        }
        //RMC14

        private void OnToggleFoV(EntityUid uid, EyeComponent component, ToggleFoVActionEvent args)
        {
            if (args.Handled)
                return;

            Popup.PopupEntity(Loc.GetString("ghost-gui-toggle-fov-popup"), args.Performer);
            _contentEye.RequestToggleFov(uid, component);
            args.Handled = true;
        }

        private void OnToggleGhosts(EntityUid uid, GhostComponent component, ToggleGhostsActionEvent args)
        {
            if (args.Handled)
                return;

            var locId = GhostVisibility ? "ghost-gui-toggle-other-ghosts-visibility-popup-off" : "ghost-gui-toggle-other-ghosts-visibility-popup-on";
            Popup.PopupEntity(Loc.GetString(locId), args.Performer);
            if (uid == _playerManager.LocalEntity)
                ToggleGhostVisibility();

            args.Handled = true;
        }

// CMU14
        private void OnToggleSelfGhost(EntityUid uid, GhostComponent component, ToggleSelfGhostActionEvent args)
        {
            if (args.Handled)
                return;

            if (uid == _playerManager.LocalEntity && TryToggleSelfGhostVisibility(uid, out var isNowVisible))
            {
                var locId = isNowVisible ? "ghost-gui-toggle-self-ghost-visibility-popup-on" : "ghost-gui-toggle-self-ghost-visibility-popup-off";
                Popup.PopupEntity(Loc.GetString(locId), args.Performer);
            }

            args.Handled = true;
        }
        
        private void OnGhostRemove(EntityUid uid, GhostComponent component, ComponentRemove args)
        {
            _actions.RemoveAction(uid, component.ToggleLightingActionEntity);
            _actions.RemoveAction(uid, component.ToggleFoVActionEntity);
            _actions.RemoveAction(uid, component.ToggleGhostsActionEntity);
            _actions.RemoveAction(uid, component.ToggleGhostHearingActionEntity);
            _actions.RemoveAction(uid, component.ToggleSelfGhostActionEntity); // CMU14

            if (uid != _playerManager.LocalEntity)
                return;

            _overlay.RemoveOverlay<HalfNightVisionBrightnessOverlay>(); //RMC14

            GhostVisibility = false;
            PlayerRemoved?.Invoke(component);
        }

        private void OnGhostPlayerAttach(EntityUid uid, GhostComponent component, LocalPlayerAttachedEvent localPlayerAttachedEvent)
        {
            GhostVisibility = true;
            PlayerAttached?.Invoke(component);
        }

        private void OnGhostState(EntityUid uid, GhostComponent component, ref AfterAutoHandleStateEvent args)
        {
            if (TryComp<SpriteComponent>(uid, out var sprite))
                _sprite.LayerSetColor((uid, sprite), 0, component.Color);

            if (uid != _playerManager.LocalEntity)
                return;

            PlayerUpdated?.Invoke(component);
        }

        private void OnGhostPlayerDetach(EntityUid uid, GhostComponent component, LocalPlayerDetachedEvent args)
        {
            GhostVisibility = false;
            PlayerDetached?.Invoke();
            _overlay.RemoveOverlay<HalfNightVisionBrightnessOverlay>(); //RMC14
        }

        private void OnGhostWarpsResponse(GhostWarpsResponseEvent msg)
        {
            if (!IsGhost)
            {
                return;
            }

            GhostWarpsResponse?.Invoke(msg);
        }

        private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
        {
            GhostWarpsReset?.Invoke();
        }

        private void OnUpdateGhostRoleCount(GhostUpdateGhostRoleCountEvent msg)
        {
            AvailableGhostRoleCount = msg.AvailableGhostRoles;
            GhostRoleCountUpdated?.Invoke(msg);
        }

        public void RequestWarps()
        {
            RaiseNetworkEvent(new GhostWarpsRequestEvent());
        }

        public void ReturnToBody()
        {
            var msg = new GhostReturnToBodyRequest();
            RaiseNetworkEvent(msg);
        }

        public void OpenGhostRoles()
        {
            _console.RemoteExecuteCommand(null, "ghostroles");
        }

        public void ToggleGhostVisibility(bool? visibility = null)
        {
            GhostVisibility = visibility ?? !GhostVisibility;
        }
        
        // CMU14
        /// <summary>
        /// Toggles player's own ghost sprite visibility.
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="visibility"></param>
        /// <returns>The newly setvisibility state.</returns> <summary>
        public bool TryToggleSelfGhostVisibility(EntityUid? uid, out bool isNowVisible, bool? visibility = null)
        {
            isNowVisible = false;
            if (!uid.HasValue)
                return false;

            var entityManager = IoCManager.Resolve<IEntityManager>();
            if (!entityManager.HasComponent<GhostComponent>(uid))
                return false;

            if (!entityManager.TryGetComponent(uid, out SpriteComponent? spriteComponent))
                return false;

            spriteComponent.Visible = visibility ?? !spriteComponent.Visible;

            isNowVisible = spriteComponent.Visible;
            return true;
        }
    }
}
