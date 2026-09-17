ent-CMURiderHatchling = writhing parasite
    .desc = A small, writhing thing that moves far too deliberately for an animal.

ghost-role-information-rider-name = the writhing parasite
ghost-role-information-rider-description = A catalogued-impossible Weyland-Yutani hatchling. Slip inside an unconscious or willing host, whisper from the dark, and never be seen. Bullets bounce off the hide; fire is the only thing that kills it quickly.
ghost-role-information-rider-rules = You are a solo antagonist. Hide, ride, and scheme. You may kill to survive or to serve a scheme, not for revenge.

roles-antag-rider-role-name = The Rider
roles-antag-rider-objective = Ride someone to the end of the round without ever being seen.

language-RiderCant-name = The Hum
language-RiderCant-description = A low vibration in the chest, felt more than heard.

rider-role-greeting = You are the thing that got out. Small. Old. Clever.
    Weyland-Yutani grew you in a jar and catalogued you as HOLOTYPE W-7, and the lab that did it no longer admits you exist.
    You have no body worth the name - find a host, slip inside, and stay hidden.
    You cannot force them for long - you can only make the alternative worse.
    Whisper. Wait. Choose your hosts like you choose your moments: both must hold.
    Information is your currency: what hosts mutter, what your borrowed eyes see.
    Trade it, leverage it, sell one host's secret to another. You play people, not levers.

rider-latch-invalid = Your hooks find no purchase. Not this one.
rider-latch-riding = You already have a host.
rider-latch-awake = That one is awake. They must let you in themselves.
rider-latch-start = You coil, ready to leap...
rider-latch-failed = They shifted. You missed.

rider-verb-accept = Let it in

rider-host-latched = Something is inside you. It whispers. Doctors could cut it out. Or you can listen.
rider-host-latched-wrap = *"Something is inside you. It whispers. Doctors could cut it out. Or you can listen."*

rider-no-host = You have no host.
rider-grip-low = Your grip is too weak for that.

rider-whisper-wrap = Something whispers: { $text }
rider-whisper-echo = You whisper to your host: { $text }
rider-speak-blocked = Their throat will not answer.

rider-punish-host = Something burns behind your eyes!

rider-seize-host = Your body moves on its own. You watch from somewhere behind your own eyes.
rider-seize-end-host = Control floods back. Your hands are yours again - for now.

rider-eject-loud = A small, writhing thing bursts from { $host }!

rider-resist-start = { $host } strains and twitches, fighting something no one else can see!

rider-soothe = The pain goes quiet. You feel oddly calm.
rider-soothe-end = The calm lifts. You feel everything again.

rider-grip-tight = Whatever is inside you has gone very, very still. That is worse.
rider-grip-loose = Something inside you loosens its grip, just slightly.
rider-grip-thread = The presence inside you is barely there - a thread of what it was.

rider-verb-inspect = Inspect rider
rider-verb-eject = Force-eject rider
rider-admin-inspect = Rider: {$rider}
    Host: {$host}
    Grip: {$grip}
    Hosts ridden: {$hosts}
    Total ride time: {$minutes} min

rider-vent-examine = A small shape slips into the vent and is gone.

cmu-summary-detail-rider-riding = Rode {$hosts} host(s) for {$minutes} minutes and was still riding someone at round end.
cmu-summary-detail-rider-free = Rode {$hosts} host(s) for {$minutes} minutes and ended the round hostless.

rider-tell-mild = { $entName } rubs the back of their neck.
rider-tell-medium = { $entName } twitches and loses the thread of their sentence.
rider-tell-heavy = { $entName } shudders hard, hands trembling.

rider-examine-mild = They keep touching their neck, like something itches under the skin.
rider-examine-medium = Something is off - the pauses are wrong and the voice runs flat.
rider-examine-heavy = Tremors, unfocused eyes, a face wrung-out from the inside.

rider-analyzer-abnormal = The readout flickers. Something is slightly abnormal - cranial region.
rider-analyzer-foreign = FOREIGN BODY DETECTED - cranial cavity. It is moving.
rider-crawl-residue = traces of an unknown biological film

rider-extracted = A slick, finger-length thing drops out of the incision and hits the floor running!

rider-surge-refused = Only a willing host can be rewarded.
rider-surge-host = A cold flush floods your chest - pain gone, heartbeat huge, hands steady. The thing did this.
rider-coax-host = Your aches quiet down and your wounds itch as they close. Something inside you did this.
rider-sustain-host = Warmth spreads from your spine into every hurt. Bruises you never saw fade, and the pull inside you goes deeper, holding you together.

rider-flavor-hitchhiker = Tonight you are a hitchhiker: still be riding someone when the round ends. Patience is the whole game.
rider-flavor-leapfrog = Tonight you are a leapfrog: ride three different hosts who were awake and steerable before the round ends. Sleepers do not count.
rider-flavor-puppeteer = Tonight you are a puppeteer: when the round ends, be riding a host who carries the { $item }. Whisper, bargain, seize - their hands, your goal.

rider-choir-hum = The choir hums at the edge of your thoughts: another of your kind, { $direction }, { $distance }.
rider-choir-close = close enough to touch
rider-choir-near = not far
rider-choir-far = far away
rider-choir-north = to the north
rider-choir-northeast = to the northeast
rider-choir-east = to the east
rider-choir-southeast = to the southeast
rider-choir-south = to the south
rider-choir-southwest = to the southwest
rider-choir-west = to the west
rider-choir-northwest = to the northwest

cmu-summary-detail-rider-leapfrog-win = Leapt between {$credited} credited hosts over {$minutes} minutes and made the count.
cmu-summary-detail-rider-leapfrog-fail = Rode {$hosts} host(s) for {$minutes} minutes but only {$credited} counted - the count was never made.
cmu-summary-detail-rider-puppeteer-win = Steered a host true for {$minutes} minutes across {$hosts} host(s) - the puppeteer's goal was in hand at the end.
cmu-summary-detail-rider-puppeteer-fail = Rode {$hosts} host(s) for {$minutes} minutes but never steered the right hands to the goal.
ent-CMURiderLatchedMarker = writhing shape
ent-CMURiderManifest = flickering parasite

rider-locator-title = Rider locator
rider-locator-follow = Jump to rider
rider-locator-free = free-roaming
