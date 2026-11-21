## Rolled grievances (Datasets/strike_grievances.yml picks LocIds from this set)
cmu-strike-grievance-generic = better conditions and honest pay.
cmu-strike-grievance-overtime = an end to the Company skimming three quarters of every overtime hour.
cmu-strike-grievance-smelter = the true count of the smelter accident the Company never reported.
cmu-strike-grievance-water = water rations lifted for the colonists while the Company's fountains keep running.
cmu-strike-grievance-store = Company-store prices rolled back to be sustained by colonial wages.
cmu-strike-grievance-scrubbers = air scrubbers/filters to be replaced on schedule, not on the budget allocation.
cmu-strike-grievance-medical = on-colony medical care instead of quiet medevacs no one hears from again.
cmu-strike-grievance-evictions = the common barracks evictions undone, the Company research team quartered elsewhere.
cmu-strike-grievance-hazard = hazard pay for hauling unstable ore and extremely dangerous work environments.
cmu-strike-grievance-net = the colony broadcasting net to be kept open during emergencies.
cmu-strike-grievance-contracts = end-of-tour work migration contracts to be honored, not processed.

## Binding stances, rolled per round on the rule
cmu-strike-stance-clf-aligned = The strikers voice open sympathy for the Colonial Liberation Front (INSFOR).
cmu-strike-stance-clf-neutral = The strikers keep the Front at arm's length: this fight is their own.
cmu-strike-stance-clf-opposed = The strikers denounce the Front: its bombs killed our innocent own.
cmu-strike-stance-govfor-aligned = The strikers believe the colonial administration can still be bargained with.
cmu-strike-stance-govfor-neutral = The strikers see the administration as watchers, not listeners.
cmu-strike-stance-govfor-opposed = The strikers hold that every badge on this rock answers to the Company agenda.

## Organizer briefing, sent at spawn alongside the role greeting
cmu-strike-organizer-venture = Your venture was shaped before the shift began. Its stated cause: {$grievance}
    {$clfStance}
    {$govforStance}
    These stances bind: play within them, whoever joins you. The grievance is a seed, not a script. Grow your own cause if a truer one takes root.

## Participant briefing, sent on signing
cmu-strike-participant-briefing = You signed the strike petition and joined the strike. Its cause: {$grievance}
    {$clfStance}
    {$govforStance}
    You are a strike participant now. The strike is your conscience: push it as far as it must go, up to and past taking the colony. The stances bind. The cause is yours to reinterpret.

## Sent to the living roster when the signature goal is reached
cmu-strike-vote-passed-briefing = The vote passed: the workforce is out and the strike is official. From here you strike together or not at all. The Company knows. Act like it.

## Signing rite
cmu-strike-sign-dialog-title = Sign the strike petition?
cmu-strike-sign-dialog-message = The petition demands {$grievance}
    {$clfStance}
    {$govforStance}
    Signing makes you a strike participant, an antagonist of the round. The Company would rather you didn't. Are you in?
cmu-strike-sign-begin = You put pen to paper...
cmu-strike-sign-already = You have already signed this petition.
cmu-strike-sign-dead = You are in no state to sign anything.
cmu-strike-sign-refused-antag = Your loyalties already lie elsewhere.
cmu-strike-sign-refused-dept = You are not signing that. Your post sits on the other side of this fight.
cmu-strike-sign-success = You sign the petition ({$count}/{$goal}). You are a strike participant now.

## Petition
# The venture lives on the rule; every petition is a token onto the same ledger.
cmu-petition-examine = A strike petition: {$count}/{$goal} signatures. It demands {$grievance} Signed by: {$names}
cmu-petition-examine-empty = A strike petition: 0/{$goal} signatures. It demands {$grievance} No one has signed yet.
cmu-petition-examine-relic = A strike petition from a strike nobody carries anymore. The ink has dried for good.
cmu-strike-no-venture = No living strike carries this cause.
cmu-strike-petition-destroyed = The petition is gone, but its signatures live in the workforce. Redraft when it is safe.

## Redraft action
ent-CMUActionStrikeRedraft = Redraft Petition
    .desc = Draft a fresh strike petition. Only the paper burns; the strike lives on.
cmu-strike-redraft-done = You draft a fresh petition. The strike carries on.

## Transmit action: the radio pack's one-way wire to the CMB
ent-CMUActionStrikeFax = Transmit Communique
    .desc = Send a one-way communique from the radio pack to the Colonial Marshal Bureau. It cannot receive a reply.
cmu-strike-fax-input-title = Transmit Communique
cmu-strike-fax-input = The radio pack reaches only the Colonial Marshal Bureau. Compose your communique. There is no reply channel.
cmu-strike-fax-communique-title = Strike Communique
cmu-strike-fax-sent = Your communique is on the wire. Do not expect a reply.

## Round-end roster: mid-round converts, invisible to the round-start antag summary
## that ColonyAntagSummarySystem walks
cmu-strike-summary-header = Strike participants:
cmu-strike-summary-entry = {"["}bold]{$name}[/bold] ([italic]{$user}[/italic]): {$status}
cmu-strike-summary-alive = still on strike
cmu-strike-summary-dead = fell on the line

## Succession: first signer after the organizer's fall
cmu-strike-succession-briefing = The organizer has fallen, and your signature was the first after. The strike looks to you now: hold the stances, carry the cause. Whoever holds the radio pack holds the press.

## CMB escalation faxes
cmu-strike-fax-half-title = Labor Unrest Escalation
cmu-strike-fax-half = The strike petition circulating in your colony is gaining momentum. Its stated cause: {$grievance} Mediate with the organizers before this gets out of hand.
cmu-strike-fax-full-title = Strike Vote Passed
cmu-strike-fax-full = A petition of {$goal} signatures has been filed citing: {$grievance} The colony's workforce is now in a legal strike position. Weyland-Yutani production contracts are in jeopardy.
    Intelligence note: {$clfStance} {$govforStance}
cmu-strike-fax-witness = A sympathizer described one of the organizers as: {$description}.
