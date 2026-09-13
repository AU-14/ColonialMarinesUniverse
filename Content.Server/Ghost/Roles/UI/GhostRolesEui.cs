using Content.Server.EUI;
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

        public GhostRolesEui()
        {
            _ghostRoleSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<GhostRoleSystem>();
            _forceInterest = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ForceInterestSystem>();
        }

        public override GhostRolesEuiState GetNewState()
        {
            return new(_ghostRoleSystem.GetGhostRolesInfo(Player), _forceInterest.GetForces(Player));
        }

        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);

            switch (msg)
            {
                case SetForceInterestMessage interest:
                    _forceInterest.SetInterest(Player, interest.Identifier, interest.Interested);
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
