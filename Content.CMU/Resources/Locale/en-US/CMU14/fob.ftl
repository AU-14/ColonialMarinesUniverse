ent-CMUFobRelay = FOB relay
    .desc = A field relay that can replenish a FOB's support reserve when assigned for repair.
ent-CMUFobControl = FOB reserve control
    .desc = Local controls for the FOB's support reserve. Requires a registered FOB site.
cmu-fob-disabled = FOB reserve equipment is disabled for this round.
cmu-fob-control-title = FOB Support Reserve
cmu-fob-control-denied = Control unavailable. Use an intact control at the registered FOB site as a faction engineer or squad leader.
cmu-fob-control-wait = The reserve cannot change mode yet. Check its charge and activation timers.
cmu-fob-control-broken = The control is broken. A faction engineer or squad leader can repair it with a screwdriver or lit, fueled welder.
cmu-fob-control-examine = Faction engineers and squad leaders can operate this control. Use a screwdriver or lit, fueled welder if it breaks.
cmu-fob-control-repaired = Control restored. Support remains in standby.
cmu-fob-relay-unavailable = No repair reward is available here. The job may have ended, or the reserve may be full.
cmu-fob-repair-denied = You must be a conscious Marine of the owning faction, within reach, holding a screwdriver or lit, fueled welder.
cmu-fob-repair-busy = Someone is already repairing this device.
cmu-fob-relay-repaired = Relay repaired. Added {$seconds} seconds to the FOB reserve.
cmu-fob-relay-examine-active = This relay is assigned for repair. Use a screwdriver or lit, fueled welder; completed work transfers reserve immediately.
cmu-fob-relay-examine-inactive = No repair reward is currently available at this relay.
cmu-fob-relay-location = {$name} ({$x}, {$y})
cmu-fob-relay-announcement = FOB relay pair available: {$first} OR {$second}. Repair either relay to replenish support reserve.
cmu-fob-reserve = Stored support time: {$time}
cmu-fob-state-standby = Standby — reserve is preserved.
cmu-fob-state-starting = Starting support — reserve is draining.
cmu-fob-state-active = Support active — reserve is draining.
cmu-fob-state-empty = Reserve empty — support is off.
cmu-fob-state-closed = FOB network closed.
cmu-fob-restart = Restart lock: {$time}
cmu-fob-minimum = Earliest shutdown: {$time}
cmu-fob-activate = Activate support
cmu-fob-deactivate = Standby
cmu-fob-job-active = Repair either relay. Offer expires in {$time}.
cmu-fob-job-next = Next relay offer available in {$time}.
cmu-fob-job-waiting = Awaiting a relay assignment.
cmu-fob-job-reward = Completing either relay currently adds {$time}.
ent-CMUFobGunMount = FOB support mount
    .desc = A deployable support-gun position. Its weapon and ammunition allocation belong to the FOB network.
ent-CMUFobSupportGun = FOB support gun
    .desc = A magazine-fed support gun for a deployed FOB platform. Requires an active FOB reserve, warns before firing, and tracks targets in every direction.
cmu-fob-gun-starting = FOB support starting in {$seconds} seconds. Keep clear of the firing arcs!
cmu-fob-gun-acquiring = The support gun sounds a targeting warning!
cmu-fob-gun-restart = This gun requires repair if broken, then a new activation at the FOB control. Repair never restores ammunition.
cmu-fob-gun-examine = Tracks targets through 360 degrees. An engineer or squad leader can replace its empty drum.
cmu-fob-round-stock = Round allowance: { $current }/{ $max } remaining; no restock
cmu-fob-launcher-examine = Targets through any powered sensor in this FOB. Rockets track their original target until it moves more than 11 tiles from the spotting sensor, enters a roofed area or loses its sensor; they then strike its last tracked position. An engineer or squad leader can replace an empty cassette.
cmu-fob-gun-ammo = Ammunition remaining: {$count}.
cmu-fob-gun-rotation-denied = Put the FOB reserve in standby before turning this gun.
cmu-fob-gun-reloaded = Support gun reloaded. Target acquisition must start again.
cmu-fob-startup = Startup warning: {$time} until support is available.

cmu-fob-evacuation-boarding = Evacuation: defend the route and board the dropship.
cmu-fob-evacuation-reserve = Empty reserve does not prevent normal departure.

cmu-fob-map-relay = Repair either relay: +up to {$time}
cmu-fob-warning-low = FOB support reserve is at or below {$seconds} seconds. Coordinate a relay patrol or prepare to hold without support guns.
cmu-fob-warning-empty = FOB support reserve is empty. Support guns are off. Ordinary defenses and normal dropship departure remain available.
cmu-fob-gun-status = Gun {$slot}: {$condition} | {$ammo} rounds | {$damage} damage
cmu-fob-gun-condition-missing = Missing
cmu-fob-gun-condition-displaced = Disconnected from mount
cmu-fob-gun-condition-broken = Broken
cmu-fob-gun-condition-reloading = Reloading
cmu-fob-gun-condition-restartrequired = Needs a new activation
cmu-fob-gun-condition-empty = Out of ammunition
cmu-fob-gun-condition-standby = Standby
cmu-fob-gun-condition-starting = Starting
cmu-fob-gun-condition-active = Active
cmu-fob-equipment-standby = Support power is in standby.
cmu-fob-equipment-starting = Support power is starting; the warning sequence is active.
cmu-fob-equipment-active = Support power is energized. The guns can still be damaged and broken.
cmu-fob-equipment-empty = Support power is depleted. The guns have no reserve power.
cmu-fob-equipment-closed = The support network is offline.
cmu-fob-guide-open = FOB relay instructions
cmu-fob-guide-title = FOB Relay Pilot
cmu-fob-guide-intro = [bold]FOB relay pilot[/bold]
    This experimental system is available only in enabled Distress Signal rounds on the supported colony maps. The first friendly landing selects one FOB with two support guns. Another LZ or dropship does not grant more equipment or reserve.
    The reserve powers the two weapon platforms, deployed floodlights, sensors and electric fence coils. Barricades, ordinary sentries, medical work, construction supplies and dropship controls keep working when it is empty.
cmu-fob-guide-reserve = [bold]Local control[/bold]
    A faction engineer or squad leader can use the intact local panel. It shows reserve, relay alternatives, gun condition, ammunition and activation locks. You do not need an occupied Foxtrot slot.
    The bank starts with 8 minutes and stores at most 12. Activation spends reserve during a 10-second warning and while active. Voluntary shutdown requires 60 seconds; restarting requires 30 seconds and another startup warning. Standby saves remaining time.
    Warnings occur at 2 minutes, 1 minute and depletion. Nearby Marines see a local warning; the command-radio call uses normal comms and can be blocked or jammed. Replenishment must rise 15 seconds above a warning threshold to rearm it.
cmu-fob-guide-relays = [bold]Repair either relay[/bold]
    The faction map marks two alternatives for one reward. The local panel gives their names, coordinates, expiry and actual available reward. Use a screwdriver or lit welder in your active hand: construction skill 3 takes 30 seconds; other faction Marines take 45. A welder must remain lit and fueled throughout the repair.
    Each new pair is announced once on command radio with both relay names and coordinates. Normal comms outages and jamming can block the call; the map and terminal still show the objectives.
    Movement, damage, incapacitation or losing the tool interrupts repair. Only one worker can repair a relay. Completing either alternative removes both markers and adds up to 6 minutes, capped by bank capacity. At a full bank the job is not consumed. There is no return-trip delivery or need to garrison the relay afterward.
    The next pair is offered 5 minutes after success. Offers expire after 8 minutes, then retry after 30 seconds. Later pairs reach farther inland. No available route means no free reserve.
cmu-fob-guide-guns = [bold]Breachable support guns[/bold]
    Each UH-46 heavy sentry tracks through 360 degrees within six tiles and uses a finite 30mm drum. Walls, closed doors and concealed targets prevent fire. An engineer or squad leader may rotate a gun with a screwdriver while support is off, or replace an empty drum through an interruptible reload.
    Wrench a gun to remove its module while support is off, then use a powerloader to clear it and install a module on the empty UH-02 platform. Ammunition stays inside the removed gun. Empty platforms and terminals can also be folded with a wrench and relocated within 40 tiles of the selected LZ center, outside the landing footprint. Moving them does not create more weapon slots or reserve.
    The UH-99 rocket module replaces one heavy gun, never adds a third platform. Any powered UE-04 sensor in this FOB can spot a target within seven tiles and clear sight, in any direction from the launcher. A three-second warning precedes every launch. Its twelve-rocket cassette is finite. Each rocket homes on the original target at twelve tiles per second. After launch, walls do not break tracking, but terrain can intercept the missile. If the target moves more than eleven tiles from the original sensor, goes under a roof, or the sensor loses power or is destroyed, tracking ends permanently and the rocket strikes the last tracked position. Losing sight during acquisition still cancels the launch. Replacing a module requires a fresh support activation.
    A direct rocket hit can critically injure a healthy, unshielded T3. Active shields still absorb damage. Wounded T3s and smaller xenos can be killed. Nearby splash remains much weaker than a direct hit.
    Guns take damage even when powered. Broken wrecks do not block movement or shots. Use the normal cutters, welder, cable, screwdriver and tuner repair sequence, then activate support again. Repairing or replacing equipment never refills ammunition or reserve. A broken control takes a 20-second screwdriver or lit-welder repair; it returns in standby.
cmu-fob-guide-xenos = [bold]Attacking the FOB[/bold]
    Flank the limited firing sectors, use cover, interrupt relay patrols or break the guns and control. Powered machinery has no immunity. Burrowing, venting or leaving line of fire cancels acquisition; reacquisition must warn again.
    You can examine a nearby relay to see whether it is assigned. Marine mission markers and remote reserve/gun details are private. Damaging a relay never drains already stored reserve. Marines may continue fighting with ordinary defenses after support expires.
cmu-fob-guide-extraction = [bold]Evacuation is still contested[/bold]
    Defend a route, carry casualties aboard and use normal piloting access, startup and flight. Remaining reserve may cover a retreat, but cannot clear ramps, eject enemies, protect passengers, accelerate departure or declare survivors safe.
    Empty reserve does not lock departure. Marines left groundside remain there, and active support keeps spending reserve. Normal boarding and applicable hijack rules still apply.

cmu-fob-guide-equipment = [bold]Deployable equipment[/bold]
    Retrieve the one-time weaponry, support and monitoring crates through shipside requisitions. Requisitions can also buy two additional weaponry crates at 4,000 points each and three additional support crates at 1,500 each, shared across all friendly carrier terminals for the whole round. These limits do not replenish and the kits cannot be bought piecemeal. Transport the hardware aboard the dropship. The first friendly landing selects the FOB's LZ. All deployable FOB hardware can be placed on clear ground within 40 tiles of that LZ's center, outside its reserved dropship landing footprint. Wrench down the platforms and terminal wherever you choose inside that area, then install gun modules with a powerloader. Support must be off while assembling or changing modules. There are only two active weapon slots.
    The crates also supply two UE-92 illuminators, six UE-04 sensors, three stacks of ten UE-02 fence sections and one UE-11 generator. Put a fence stack down and wrench it to deploy one section; split and merge unused stacks normally. Recovered sections remain separate and retain damage. Put units on clear ground within 40 tiles of the selected LZ center and deploy with a wrench. Engineers or squad leaders operate these devices. Floodlights and generators use powerloader transport; sensors and folded fences can be carried by hand.
    Support must be off to fold machinery. Use cutters for fences and a wrench for the other units. Sensors never obstruct movement or intercept rockets. Powered fences shock nearby xeno attackers but always take normal damage; destroying one opens a breach. Loss of reserve or the control disables the powered utilities.
    Wrench down the UE-11, then activate it with an empty hand. Ten seconds of interruptible work adds up to two minutes to the existing reserve. The FOB can use this backup charge only once, even if the generator is moved or replaced. It does not bypass startup warnings or change evacuation rules.

cmu-fob-guide-feedback = [bold]Equipment feedback[/bold]
    Blue indicators identify assigned relays. Amber indicators show work, weapon startup or target acquisition; they do not mean a weapon is already firing. Green indicates powered support. Red warns of a broken unit or an empty gun. Check the terminal or examine the equipment for its exact condition.
    Repairs give sparks and a confirmation tone only when completed. Interrupted work stops the work indicator. The backup generator stays still and rumbles while charging, then switches to its depleted appearance. Electric arcs accompany actual fence shocks. Weapons recoil only when firing, and rocket lock-on markers disappear if tracking is lost.

ent-CMUFobRelayMarker = FOB relay site

ent-CMUFobNetwork = FOB support
cmu-fob-trial-announcement = FOB relay trial active. Retrieve the three FOB crates through shipside requisitions, transport and assemble equipment within 40 tiles of this LZ center, keeping the landing footprint clear. Use equipment help for instructions. Repair either marked relay for reserve; defenses remain breachable and evacuation requires normal boarding.
ent-CMUFobIlluminator = UE-92 area illuminator
    .desc = A powerloader-transported floodlight. Wrench it down near the selected FOB; it draws power from the active reserve.
ent-CMUFobSensor = UE-04 grid sensor
    .desc = A portable laser sensor for the UH-99 launcher. Deploy with a wrench. It needs an active reserve and a clear view of its target.
ent-CMUFobFence = UE-02 deployable fence
    .desc = A folded electric fence. Wrench to deploy, cutters to fold while support is off. Powered coils shock attackers, but the mesh can always be breached.
ent-CMUFobGenerator = UE-11 generator unit
    .desc = A one-use backup generator. Wrench it down, then activate it to add up to two minutes to the existing reserve. Only one backup charge is available per FOB.
ent-CMUFobRocketLauncher = UH-99 smart rocket launcher
    .desc = A sensor-guided, magazine-fed launcher for a UH-02 platform. A three-second warning precedes each homing launch. Roofs and distance from the spotting sensor break guidance; terrain can intercept rockets.
ent-CMUFobRocketModule = UH-99 smart rocket launcher module
    .desc = A stowed UH-99 with one twelve-rocket cassette. Use a powerloader to install it on an empty approved UH-02 platform while support is off.
ent-CMUFob30mmMagazine = UH-46 30mm ammunition drum
    .desc = One hundred 30mm shells for the UH-46 heavy sentry. Reload only when its current drum is empty.
ent-CMUFobRocketMagazine = UH-99 72mm rocket cassette
    .desc = A sealed cassette holding twelve AMLE rockets. Reload only when the launcher is empty.
ent-CMUFob30mmCartridge = 30mm sentry shell
ent-CMUFob30mmProjectile = 30mm sentry projectile
ent-CMUFob72mmCartridge = 72mm AMLE rocket
ent-CMUFob72mmProjectile = 72mm AMLE rocket
ent-CMUFobTargetWarning = rocket lock warning
cmu-fob-equipment-work-denied = Place the unit on clear ground within 40 tiles of the selected LZ center, outside its landing footprint. A trained operator needs a wrench to deploy it; switch support off before folding it. Fences require cutters to fold.
cmu-fob-generator-denied = Deploy the generator first. The FOB must have reserve capacity and its one backup charge must be unused.
cmu-fob-generator-used = Backup charge transferred to the FOB reserve. Normal startup and evacuation rules still apply.
cmu-fob-generator-spent = The backup charge has been consumed. Moving or replacing this unit cannot restore it.
cmu-fob-equipment-deployed = Deployed. Requires the selected FOB's active reserve. Switch support off before folding; use cutters for fences and a wrench for other equipment.
cmu-fob-equipment-stowed = Stowed. Put it on clear ground near the selected FOB and use a wrench to deploy it.

cmu-fob-job-unavailable = No relay mission available. Retrying route selection in {$time}. Stored reserve is unchanged.
cmu-fob-platform-site-required = Place the platform on clear ground within 40 tiles of the selected LZ center and outside its landing footprint. Switch support off and remove its gun before folding. Only two platforms can be registered.
cmu-fob-fence-stack = electric fence sections
ent-CMUFobFenceStack = folded UE-02 fence sections
    .desc = Up to ten electric fence sections. Put the stack down and use a wrench to deploy one section. Unused sections can be split or merged.
ent-CMUFobFenceStack10 = folded UE-02 fence sections
    .desc = { ent-CMUFobFenceStack.desc }
ent-CMUFobWeaponryCrate = FOB weaponry crate
    .desc = Two platforms, two loaded heavy guns, one loaded rocket module and finite spare ammunition. Load aboard the dropship and assemble within 40 tiles of the selected LZ center.
ent-CMUFobSupportCrate = FOB support crate
    .desc = Two illuminators, thirty fence sections in stacks of ten, and engineering tools. Bring the heavy equipment down with a powerloader.
ent-CMUFobMonitoringCrate = FOB monitoring crate
    .desc = A service terminal, one backup generator and six grid sensors. Wrench the terminal down within 40 tiles of the selected LZ center, outside the landing footprint.
ent-CMUFobPlatformModule = folded UH-02 systems platform
    .desc = Transport with a powerloader. Put it on clear ground within 40 tiles of the selected LZ center, outside the landing footprint, and wrench it down before installing a gun.
ent-CMUFobHeavyModule = UH-46 heavy sentry module
    .desc = A folded heavy sentry with a finite 30mm drum. Use a powerloader to install it on an assembled platform while support is off.
ent-CMUFobControlModule = folded UE-09 service terminal
    .desc = Transport with a powerloader. Wrench it down within 40 tiles of the selected LZ center, outside the landing footprint.

cmu-fob-hopes-retreat-relay-east-road = East road relay
cmu-fob-hopes-retreat-relay-central-road = Central road relay
cmu-fob-hopes-retreat-relay-west-road = West road relay
cmu-fob-hopes-retreat-relay-west-clearing = West clearing relay
cmu-fob-hopes-retreat-relay-southeast-road = Southeast road relay
cmu-fob-hopes-retreat-relay-southern-road = Southern road relay
cmu-fob-hopes-retreat-relay-engineering-approach = Engineering approach relay
cmu-fob-hopes-retreat-relay-southwest-junction = Southwest junction relay
cmu-fob-hopes-retreat-relay-southeast-cave-mouth = Southeast cave mouth relay
cmu-fob-hopes-retreat-relay-southeast-caves = Southeast caves relay
cmu-fob-hopes-retreat-relay-east-cavern-junction = East cavern junction relay
cmu-fob-hopes-retreat-relay-octanis-approach = Octanis approach relay
cmu-fob-hopes-retreat-relay-north-cavern-west = North cavern west relay
cmu-fob-hopes-retreat-relay-north-cavern-bend = North cavern bend relay
cmu-fob-hopes-retreat-relay-north-cavern-east = North cavern east relay
cmu-fob-hopes-retreat-relay-east-cavern-mouth = East cavern mouth relay
cmu-fob-hopes-retreat-relay-research-west-approach = Research west approach relay
cmu-fob-hopes-retreat-relay-research-south-approach = Research south approach relay
cmu-fob-hopes-retreat-relay-octanis-north-approach = Octanis north approach relay
cmu-fob-hopes-retreat-relay-octanis-administration-approach = Octanis administration approach relay
cmu-fob-hopes-retreat-relay-research-south-cavern = Research south cavern relay
cmu-fob-hopes-retreat-relay-southwest-cavern = Southwest cavern relay
cmu-fob-hopes-retreat-relay-east-cavern-south = East cavern south relay
cmu-fob-hopes-retreat-relay-retreat-southwest-approach = Retreat southwest approach relay
cmu-fob-bosenmori-relay-west-riverbank = West riverbank relay
cmu-fob-bosenmori-relay-western-crossing = Western crossing relay
cmu-fob-bosenmori-relay-north-sector-west = North sector west relay
cmu-fob-bosenmori-relay-north-sector-road = North sector road relay
cmu-fob-bosenmori-relay-northwest-barrens = Northwest barrens relay
cmu-fob-bosenmori-relay-mining-approach = Mining approach relay
cmu-fob-bosenmori-relay-filtration-west-approach = Filtration west approach relay
cmu-fob-bosenmori-relay-filtration-north-approach = Filtration north approach relay
cmu-fob-bosenmori-relay-mining-western-clearing = Mining western clearing relay
cmu-fob-bosenmori-relay-mining-northern-clearing = Mining northern clearing relay
cmu-fob-bosenmori-relay-field-research-south = Field research south relay
cmu-fob-bosenmori-relay-field-research-east = Field research east relay
cmu-fob-bosenmori-relay-southwest-sector-south = Southwest sector south relay
cmu-fob-bosenmori-relay-southwest-sector-junction = Southwest sector junction relay
cmu-fob-bosenmori-relay-northern-crossing = Northern crossing relay
cmu-fob-bosenmori-relay-west-river-approach = West river approach relay
cmu-fob-bosenmori-relay-pilgrim-path = Pilgrim path relay
cmu-fob-port-nereid-relay-medical-north-approach = Medical north approach relay
cmu-fob-port-nereid-relay-northeast-jungle-south = Northeast jungle south relay
cmu-fob-port-nereid-relay-habitation-west-road = Habitation west road relay
cmu-fob-port-nereid-relay-habitation-north-road = Habitation north road relay
cmu-fob-port-nereid-relay-northeast-jungle-road = Northeast jungle road relay
cmu-fob-port-nereid-relay-port-north-approach = Port north approach relay
cmu-fob-port-nereid-relay-northwest-jungle-east = Northwest jungle east relay
cmu-fob-port-nereid-relay-processor-southwest = Processor southwest relay
cmu-fob-port-nereid-relay-disposals-north = Disposals north relay
cmu-fob-port-nereid-relay-east-perimeter = East perimeter relay
cmu-fob-port-nereid-relay-northwest-jungle-west = Northwest jungle west relay
cmu-fob-port-nereid-relay-western-processor-approach = Western processor approach relay
cmu-fob-port-nereid-relay-south-jungle-clearing = South jungle clearing relay
cmu-fob-port-nereid-relay-southeast-jungle-coast = Southeast jungle coast relay
cmu-fob-port-nereid-relay-habitation-southwest = Habitation southwest relay
cmu-fob-sorokyne-relay-marsh-crash-south = Marsh crash south relay
cmu-fob-sorokyne-relay-marsh-center = Marsh center relay
cmu-fob-sorokyne-relay-telecomms-vehicle-yard = Telecomms vehicle yard relay
cmu-fob-sorokyne-relay-flight-control-approach = Flight control approach relay
cmu-fob-sorokyne-relay-near-south-landing = Near south landing relay
cmu-fob-sorokyne-relay-relay-marshes = Relay marshes relay
cmu-fob-sorokyne-relay-outpost-decks = Outpost decks relay
cmu-fob-sorokyne-relay-north-outpost-west = North outpost west relay
cmu-fob-sorokyne-relay-parts-storage-caves = Parts storage caves relay
cmu-fob-sorokyne-relay-south-outpost-east = South outpost east relay
cmu-fob-sorokyne-relay-north-outpost-east = North outpost east relay
cmu-fob-sorokyne-relay-northeast-outpost-passage = Northeast outpost passage relay
cmu-fob-solaris-relay-medical-east-approach = Medical east approach relay
cmu-fob-solaris-relay-dormitory-road = Dormitory road relay
cmu-fob-solaris-relay-north-colony-road = North colony road relay
cmu-fob-solaris-relay-northern-clearing = Northern clearing relay
cmu-fob-solaris-relay-central-colony-road = Central colony road relay
cmu-fob-solaris-relay-office-approach = Office approach relay
cmu-fob-solaris-relay-north-cave-approach = North cave approach relay
cmu-fob-solaris-relay-hydroponics-approach = Hydroponics approach relay
cmu-fob-solaris-relay-south-colony-road = South colony road relay
cmu-fob-solaris-relay-filtration-approach = Filtration approach relay
cmu-fob-solaris-relay-eastern-colony-road = Eastern colony road relay
cmu-fob-solaris-relay-lambda-cave-approach = Lambda cave approach relay
cmu-fob-solaris-relay-southwest-cave-bend = Southwest cave bend relay
cmu-fob-solaris-relay-mining-eastern-approach = Mining eastern approach relay
cmu-fob-solaris-relay-engineering-east-approach = Engineering east approach relay
cmu-fob-solaris-relay-filtration-west-approach = Filtration west approach relay
cmu-fob-solaris-relay-eta-approach = Eta approach relay
cmu-fob-solaris-relay-research-west-approach = Research west approach relay
cmu-fob-solaris-relay-research-southern-passage = Research southern passage relay
cmu-fob-solaris-relay-research-eastern-passage = Research eastern passage relay
cmu-fob-fort-mcneil-relay-eastern-road-north = Eastern road north relay
cmu-fob-fort-mcneil-relay-smuggler-approach = Smuggler approach relay
cmu-fob-fort-mcneil-relay-hydroponics-south-approach = Hydroponics south approach relay
cmu-fob-fort-mcneil-relay-southeast-exterior = Southeast exterior relay
cmu-fob-fort-mcneil-relay-northern-exterior-east = Northern exterior east relay
cmu-fob-fort-mcneil-relay-northern-drill-approach = Northern drill approach relay
cmu-fob-fort-mcneil-relay-southern-exterior-east = Southern exterior east relay
cmu-fob-fort-mcneil-relay-southern-drill-approach = Southern drill approach relay
cmu-fob-fort-mcneil-relay-northern-exterior-west = Northern exterior west relay
cmu-fob-fort-mcneil-relay-northwest-drill-passage = Northwest drill passage relay
cmu-fob-fort-mcneil-relay-western-exterior-south = Western exterior south relay
cmu-fob-fort-mcneil-relay-central-caves-east = Central caves east relay
cmu-fob-raijin-relay-northwest-valley = Northwest valley relay
cmu-fob-raijin-relay-labs-valley = Labs valley relay
cmu-fob-raijin-relay-western-dam-caves = Western dam caves relay
cmu-fob-raijin-relay-west-tunnel = West tunnel relay
cmu-fob-raijin-relay-north-dam-riverbank = North dam riverbank relay
cmu-fob-raijin-relay-dam-east-valley = Dam east valley relay
cmu-fob-raijin-relay-telecomms-valley-north = Telecomms valley north relay
cmu-fob-raijin-relay-south-tunnel-junction = South tunnel junction relay
cmu-fob-raijin-relay-wilderness-road = Wilderness road relay
cmu-fob-raijin-relay-crashsite-approach = Crashsite approach relay
cmu-fob-raijin-relay-south-tunnel-entrance = South tunnel entrance relay
cmu-fob-raijin-relay-hydro-valley-north = Hydro valley north relay
cmu-fob-raijin-relay-cargo-valley-east = Cargo valley east relay
cmu-fob-raijin-relay-container-yard = Container yard relay
cmu-fob-raijin-relay-treatment-south-approach = Treatment south approach relay
cmu-fob-raijin-relay-treatment-east-approach = Treatment east approach relay
cmu-fob-raijin-relay-hydro-valley-west = Hydro valley west relay
cmu-fob-raijin-relay-hydroponics-loading-approach = Hydroponics loading approach relay
cmu-fob-raijin-relay-southeast-riverbank = Southeast riverbank relay
cmu-fob-raijin-relay-dam-southern-riverbank = Dam southern riverbank relay
cmu-fob-raijin-relay-dam-valley-south = Dam valley south relay
cmu-fob-shivas-relay-colony-northwest-clearing = Colony northwest clearing relay
cmu-fob-shivas-relay-colony-north-approach = Colony north approach relay
cmu-fob-shivas-relay-camp-west-passage = Camp west passage relay
cmu-fob-shivas-relay-camp-east-passage = Camp east passage relay
cmu-fob-shivas-relay-colony-central-approach = Colony central approach relay
cmu-fob-shivas-relay-colony-east-road = Colony east road relay
cmu-fob-shivas-relay-junkyard-north-approach = Junkyard north approach relay
cmu-fob-shivas-relay-south-administration-approach = South administration approach relay
cmu-fob-shivas-relay-colony-east-clearing = Colony east clearing relay
cmu-fob-shivas-relay-garage-east-approach = Garage east approach relay
cmu-fob-shivas-relay-junkyard-central = Junkyard central relay
cmu-fob-shivas-relay-junkyard-south = Junkyard south relay
cmu-fob-shivas-relay-southwest-valley-east = Southwest valley east relay
cmu-fob-shivas-relay-warehouse-caves-east = Warehouse caves east relay
cmu-fob-shivas-relay-research-south-approach = Research south approach relay
cmu-fob-shivas-relay-research-west-clearing = Research west clearing relay
cmu-fob-lv624-relay-west-barrens-south = West barrens south relay
cmu-fob-lv624-relay-southwest-caves-mouth = Southwest caves mouth relay
cmu-fob-lv624-relay-north-jungle-west = North jungle west relay
cmu-fob-lv624-relay-central-road-north = Central road north relay
cmu-fob-lv624-relay-west-barrens-north = West barrens north relay
cmu-fob-lv624-relay-central-barrens-west = Central barrens west relay
cmu-fob-lv624-relay-east-barrens = East barrens relay
cmu-fob-lv624-relay-eastern-riverbank = Eastern riverbank relay
cmu-fob-lv624-relay-north-barrens = North barrens relay
cmu-fob-lv624-relay-north-pass = North pass relay
cmu-fob-lv624-relay-east-cave-approach = East cave approach relay
cmu-fob-lv624-relay-temple-northern-passage = Temple northern passage relay
cmu-fob-lv624-relay-southeast-barrens-south = Southeast barrens south relay
cmu-fob-lv624-relay-southeast-barrens-north = Southeast barrens north relay
cmu-fob-lv624-relay-temple-approach = Temple approach relay
cmu-fob-lv624-relay-central-barrens-south = Central barrens south relay
cmu-fob-lv624-relay-containers-approach = Containers approach relay
