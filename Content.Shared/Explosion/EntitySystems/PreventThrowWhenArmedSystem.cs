using Content.Shared.Explosion.Components;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Localization;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Containers;

namespace Content.Shared.Explosion.EntitySystems
{
	[UsedImplicitly]
	public sealed partial class PreventThrowWhenArmedSystem : EntitySystem
	{
		[Dependency] private SharedPopupSystem _popup = default!;
		[Dependency] private INetManager _net = default!;

		public override void Initialize()
		{
			base.Initialize();

			SubscribeLocalEvent<PreventThrowWhenArmedComponent, ThrowItemAttemptEvent>(OnThrowAttempt);
			SubscribeLocalEvent<PreventThrowWhenArmedComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt);
		}

		private void OnThrowAttempt(EntityUid uid, PreventThrowWhenArmedComponent comp, ref ThrowItemAttemptEvent args)
		{
			if (args.Cancelled)
				return;

			// If the timer is active (primed), disallow throwing.
			if (!HasComp<ActiveTimerTriggerComponent>(uid))
				return;

			args.Cancelled = true;

			// Only show a popup from the server side.
			if (_net.IsClient)
				return;

			_popup.PopupEntity(Loc.GetString("Cannot throw a primed IED"), args.User, args.User, PopupType.SmallCaution);
		}

		private void OnInsertAttempt(EntityUid uid, PreventThrowWhenArmedComponent comp, ref ContainerGettingInsertedAttemptEvent args)
		{
			if (args.Cancelled)
				return;

			if (!HasComp<ActiveTimerTriggerComponent>(uid))
				return;

			// Prevent putting the primed explosive into containers.
			args.Cancel();
		}
	}
}


