using System.Linq;
using System.Numerics;
using System.Threading;
using Content.Client.Verbs;
using Content.Shared.CCVar; // CMU14
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Input;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Verbs;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration; // CMU14
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using static Content.Shared.Interaction.SharedInteractionSystem;
using static Robust.Client.UserInterface.Controls.BoxContainer;
using Direction = Robust.Shared.Maths.Direction;

namespace Content.Client.Examine
{
    [UsedImplicitly]
    public sealed partial class ExamineSystem : ExamineSystemShared
    {
        [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private IPlayerManager _playerManager = default!;
        [Dependency] private IEyeManager _eyeManager = default!;
        [Dependency] private VerbSystem _verbSystem = default!;
        [Dependency] private SpriteSystem _sprite = default!;
        [Dependency] private IConfigurationManager _config = default!; // CMU14

        private List<Verb> _verbList = new();

        public const string StyleClassEntityTooltip = "entity-tooltip";

        private EntityUid _examinedEntity;
        private Popup? _examineTooltipOpen;
        private ScreenCoordinates _popupPos;
        private CancellationTokenSource? _requestCancelTokenSource;
        private int _idCounter;

        public override void Initialize()
        {
            base.Initialize();

            UpdatesOutsidePrediction = true;

            SubscribeLocalEvent<GetVerbsEvent<ExamineVerb>>(AddExamineVerb);

            SubscribeNetworkEvent<ExamineSystemMessages.ExamineInfoResponseMessage>(OnExamineInfoResponse);

            SubscribeLocalEvent<ItemComponent, DroppedEvent>(OnExaminedItemDropped);

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.ExamineEntity, new PointerInputCmdHandler(HandleExamine, outsidePrediction: true))
                .Register<ExamineSystem>();

            _idCounter = 0;
        }

        private void OnExaminedItemDropped(EntityUid item, ItemComponent comp, DroppedEvent args)
        {
            if (!args.User.Valid)
                return;
            if (_examineTooltipOpen == null)
                return;

            if (item == _examinedEntity && args.User == _playerManager.LocalEntity)
                CloseTooltip();
        }

        public override void Update(float frameTime)
        {
            if (_examineTooltipOpen is not {Visible: true}) return;
            if (!_examinedEntity.Valid || _playerManager.LocalEntity is not { } player) return;

            if (!CMUCanStillExamine(player)) // CMU14: reuse the pair predicate, keep live visibility checks.
                CloseTooltip();
        }

        public override void Shutdown()
        {
            CloseTooltip(); // CMU14: release UI references when the system shuts down.
            CommandBinds.Unregister<ExamineSystem>();
            base.Shutdown();
        }

        public override bool CanExamine(EntityUid examiner, MapCoordinates target, Ignored? predicate = null, EntityUid? examined = null, ExaminerComponent? examinerComp = null)
        {
            if (!Resolve(examiner, ref examinerComp, false))
                return false;

            if (examinerComp.SkipChecks)
                return true;

            if (examinerComp.CheckInRangeUnOccluded)
            {
                // TODO fix this. This should be using the examiner's eye component, not eye manager.
                var b = _eyeManager.GetWorldViewbounds();
                if (!b.Contains(target.Position))
                    return false;
            }

            return base.CanExamine(examiner, target, predicate, examined, examinerComp);
        }

        private bool HandleExamine(in PointerInputCmdHandler.PointerInputCmdArgs args)
        {
            var entity = args.EntityUid;

            if (!args.EntityUid.IsValid() || !Exists(entity))
            {
                return false;
            }

            if (_playerManager.LocalEntity is not { } player ||
                !CanExamine(player, entity))
            {
                return false;
            }

            DoExamine(entity);
            return true;
        }

        private void AddExamineVerb(GetVerbsEvent<ExamineVerb> args)
        {
            if (!CanExamine(args.User, args.Target))
                return;

            // Basic examine verb.
            ExamineVerb verb = new();
            verb.Category = VerbCategory.Examine;
            verb.Priority = 10;
            // Center it on the entity if they use the verb instead.
            verb.Act = () => DoExamine(args.Target, false);
            verb.Text = Loc.GetString("examine-verb-name");
            verb.Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"));
            verb.ShowOnExamineTooltip = false;
            verb.ClientExclusive = true;
            args.Verbs.Add(verb);
        }

        private void OnExamineInfoResponse(ExamineSystemMessages.ExamineInfoResponseMessage ev)
        {
            var player = _playerManager.LocalEntity;
            if (player == null)
                return;

            // Prevent updating a new tooltip.
            if (ev.Id != 0 && ev.Id != _idCounter)
                return;

            // Tooltips coming in from the server generally prioritize
            // opening at the old tooltip rather than the cursor/another entity,
            // since there's probably one open already if it's coming in from the server.
            var entity = GetEntity(ev.EntityUid);
            if (!Exists(entity) || !HasComp<TransformComponent>(entity))
                return;

            OpenTooltip(player.Value, entity, ev.CenterAtCursor, ev.OpenAtOldTooltip, ev.KnowTarget);
            UpdateTooltipInfo(player.Value, entity, ev.Message, ev.Verbs, getVerbs: false);
        }

        public override void SendExamineTooltip(EntityUid player, EntityUid target, FormattedMessage message, bool getVerbs, bool centerAtCursor)
        {
            OpenTooltip(player, target, centerAtCursor);
            UpdateTooltipInfo(player, target, message, getVerbs: getVerbs);
        }

        /// <summary>
        ///     Opens the tooltip window and sets spriteview/name/etc, but does
        ///     not fill it with information. This is done when the server sends examine info/verbs,
        ///     or immediately if it's entirely clientside.
        /// </summary>
        // CMU14: retain the popup and its layout while refreshing the same examined target.
        public void OpenTooltip(EntityUid player, EntityUid target, bool centeredOnCursor=true, bool openAtOldTooltip=true, bool knowTarget = true)
        {
            CMUOpenTooltip(player, target, centeredOnCursor, openAtOldTooltip, knowTarget);
        }

        /// <summary>
        ///     Fills the examine tooltip with a message and buttons if applicable.
        /// </summary>
        public void UpdateTooltipInfo(EntityUid player, EntityUid target, FormattedMessage message, List<Verb>? verbs=null, bool getVerbs = true)
        {
            var vBox = _examineTooltipOpen?.GetChild(0).GetChild(0);
            if (vBox == null)
            {
                return;
            }

            // CMU14: replace the contents of one body label instead of appending another label.
            CMUUpdateBody(message);

            var totalVerbs = _verbSystem.GetLocalVerbs(target, player, typeof(ExamineVerb));

            // We still need client-exclusive verbs even when the server sends its data in so if that's the case
            // we remove any non-client-exclusive verbs.
            if (!getVerbs)
            {
                _verbList.AddRange(totalVerbs);

                foreach (var verb in _verbList)
                {
                    if (!verb.ClientExclusive)
                    {
                        totalVerbs.Remove(verb);
                    }
                }

                _verbList.Clear();
            }

            if (verbs != null)
            {
                totalVerbs.UnionWith(verbs);
            }

            AddVerbsToTooltip(totalVerbs);
        }

        // CMU14: keep the button layout, but bind each refresh to its freshly generated verbs.
        private void AddVerbsToTooltip(IEnumerable<Verb> verbs)
        {
            if (_examineTooltipOpen == null)
                return;
            CMUClearButtons();
            foreach (var verb in verbs)
            {
                if (verb is not ExamineVerb examine || examine.Icon == null || !examine.ShowOnExamineTooltip)
                    continue;
                var button = new ExamineButton(examine, _sprite);
                if (examine.HoverVerb)
                    _cmuHoverButtons!.AddChild(button);
                else
                {
                    button.OnPressed += VerbButtonPressed;
                    _cmuClickButtons!.AddChild(button);
                }
            }
        }

        /// <summary>
        /// Handler for the OnClick Event of a Verb Button.
        /// </summary>
        /// <param name="obj">The event args</param>
        public void VerbButtonPressed(BaseButton.ButtonEventArgs obj)
        {
            if (obj.Button is ExamineButton button)
            {
                _verbSystem.ExecuteVerb(_examinedEntity, button.Verb);
                if (button.Verb.CloseMenu ?? button.Verb.CloseMenuDefault)
                    CloseTooltip();
            }
        }

        /// <summary>
        /// Open and fills by query to server an examine menu with the correct tooltip and menu items.
        /// </summary>
        /// <param name="entity">The entity examined.</param>
        /// <param name="centeredOnCursor">If the popup should be on the cursor or the entity's screen position.</param>
        /// <param name="userOverride">An optional user object; if null we assume the client's player entity.</param>
        public void DoExamine(EntityUid entity, bool centeredOnCursor = true, EntityUid? userOverride = null)
        {
            var playerEnt = userOverride ?? _playerManager.LocalEntity;
            if (playerEnt == null)
                return;

            FormattedMessage message;

            OpenTooltip(playerEnt.Value, entity, centeredOnCursor, false);

            // Always update tooltip info from client first.
            // If we get it wrong, server will correct us later anyway.
            // This will usually be correct (barring server-only components, which generally only adds, not replaces text)
            message = GetExamineText(entity, playerEnt);
            UpdateTooltipInfo(playerEnt.Value, entity, message);

            if (!IsClientSide(entity))
            {
                // Ask server for extra examine info.
                unchecked
                {
                    _idCounter += 1;
                }
                RaiseNetworkEvent(new ExamineSystemMessages.RequestExamineInfoMessage(GetNetEntity(entity), _idCounter, true));
            }

            RaiseLocalEvent(entity, new ClientExaminedEvent(entity, playerEnt.Value));
        }

        // CMU14: clear nested button handlers and all cached controls with the popup.
        private void CloseTooltip()
        {
            var popup = _examineTooltipOpen;
            _examineTooltipOpen = null;
            CMUResetTooltip();
            popup?.Dispose();
            if (_requestCancelTokenSource != null)
            {
                _requestCancelTokenSource.Cancel();
                _requestCancelTokenSource.Dispose();
                _requestCancelTokenSource = null;
            }
        }

    }

    /// <summary>
    /// An entity was examined on the client.
    /// </summary>
    public sealed partial class ClientExaminedEvent : EntityEventArgs
    {
        /// <summary>
        ///     The entity performing the examining.
        /// </summary>
        public readonly EntityUid Examiner;

        /// <summary>
        ///     Entity being examined, for broadcast event purposes.
        /// </summary>
        public readonly EntityUid Examined;

        public ClientExaminedEvent(EntityUid examined, EntityUid examiner)
        {
            Examined = examined;
            Examiner = examiner;
        }
    }
}
