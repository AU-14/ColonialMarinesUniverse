using Content.Server.EUI;
using Content.Server.CMU14.Ops.ThirdParty; // CMU14
using Content.Server.CMU14.Threats;
using Content.Shared.CMU14.Threats;
using Content.Shared.Eui;
using Content.Shared.Ghost.Roles;

namespace Content.Server.Ghost.Roles.UI
{
    public sealed class GhostRolesEui : BaseEui
    {
        private readonly GhostRoleSystem _ghostRoleSystem;
        private readonly ForceInterestSystem _forceInterest;
        private readonly GhostThirdPartyCallSystem _ghostThirdPartyCall; // CMU14

        public GhostRolesEui()
        {
            var systems = IoCManager.Resolve<IEntitySystemManager>();
            _ghostRoleSystem = systems.GetEntitySystem<GhostRoleSystem>();
            _forceInterest = systems.GetEntitySystem<ForceInterestSystem>();
            _ghostThirdPartyCall = systems.GetEntitySystem<GhostThirdPartyCallSystem>(); // CMU14
        }

        public override GhostRolesEuiState GetNewState()
        {
            return new(_ghostRoleSystem.GetGhostRolesInfo(Player), _forceInterest.GetForces(Player),
                _ghostThirdPartyCall.GetState()); // CMU14
        }

        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);

            switch (msg)
            {
                case SetForceInterestMessage interest:
                    _forceInterest.SetInterest(Player, interest.Identifier, interest.Interested);
                    break;
                case GhostThirdPartyCallMessage call: // CMU14
                    _ghostThirdPartyCall.RequestCall(Player);
                    break;
                case RequestGhostRoleMessage req:
                    _ghostRoleSystem.Request(Player, req.Identifier);
                    break;
                case FollowGhostRoleMessage req:
                    _ghostRoleSystem.Follow(Player, req.Identifier);
                    break;
                case LeaveGhostRoleRaffleMessage req:
                    _ghostRoleSystem.LeaveRaffle(Player, req.Identifier);
                    break;
            }
        }

        public override void Closed()
        {
            base.Closed();

            _ghostRoleSystem.CloseEui(Player);
        }
    }
}
