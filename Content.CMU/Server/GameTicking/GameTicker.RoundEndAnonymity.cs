using Content.Server.CMU14.GameTicking;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [Dependency] private CMURoundEndAnonymitySystem _cmuRoundEndAnonymity = default!;
}
