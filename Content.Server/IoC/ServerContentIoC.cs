using Content.Server._RMC14.Actions;
using Content.Server._RMC14.Admin;
using Content.Server._RMC14.Commendations;
using Content.Server._RMC14.Discord;
using Content.Server._RMC14.LinkAccount;
using Content.Server._RMC14.Mentor;
using Content.Server._RMC14.PlayTimeTracking;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Notes;
using Content.Server.Afk;
using Content.Server.Chat.Managers;
using Content.Server.Connection;
using Content.Server.Database;
using Content.Server.Discord;
using Content.Server.Discord.DiscordLink;
using Content.Server.Discord.WebhookMessages;
using Content.Server.EUI;
using Content.Server.FeedbackSystem;
using Content.Server.GhostKick;
using Content.Server.Info;
using Content.Server.Mapping;
using Content.Server.Maps;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.Players.JobWhitelist;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Players.RateLimiting;
using Content.Server.Preferences.Managers;
using Content.Server.ServerInfo;
using Content.Server.ServerUpdates;
using Content.Server.Voting.Managers;
using Content.Shared.Administration.Logs;
using Content.Shared.Administration.Managers;
using Content.Shared.Chat;
using Content.Shared.FeedbackSystem;
using Content.Shared.IoC;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Players.RateLimiting;

namespace Content.Server.IoC;

            IoCManager.Register<DiscordLink>();
            IoCManager.Register<DiscordChatLink>();

            // RMC14
            IoCManager.Register<LinkAccountManager>();
            IoCManager.Register<RMCPlayTimeManager>();
            IoCManager.Register<RMCDiscordManager>();
            IoCManager.Register<MentorManager>();
            IoCManager.Register<CommendationManager>();
            IoCManager.Register<RMCActionsManager>();
            IoCManager.Register<RMCChatBansManager>();
        }
    }
}
