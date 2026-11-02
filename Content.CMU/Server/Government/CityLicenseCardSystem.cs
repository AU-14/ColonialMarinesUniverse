// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.Government;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Government;

// What a card of a licence says when it is looked at: what for, whose, the number, and whether it is good, has run out, or was taken back.
public sealed class CityLicenseCardSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CityLicenseCardComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<CityLicenseCardComponent> card, ref ExaminedEvent args)
    {
        var comp = card.Comp;
        var now = _timing.CurTime;

        var status = comp.Revoked
            ? Loc.GetString("rumc-license-card-revoked")
            : comp.Permanent
                ? Loc.GetString("rumc-license-card-permanent")
                : now >= comp.ExpiresAt
                    ? Loc.GetString("rumc-license-card-expired")
                    : Loc.GetString("rumc-license-card-valid", ("minutes", (int) Math.Ceiling((comp.ExpiresAt - now).TotalMinutes)));

        args.PushMarkup(Loc.GetString("rumc-license-card-examine", ("type", comp.TypeName), ("holder", comp.Holder), ("serial", comp.Serial), ("status", status)));
    }
}
