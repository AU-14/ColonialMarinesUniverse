# Cash and cutlery source drafts

Seven exact mappings: RMCSpaceCash1/10/20/100/1000, RMCFork and RMCForkPlastic. Original world source RSI references are retained. Each rotating single-direction model preserves source origin, zero Sprite offset and arbitrary saved yaw. All are surface props; the existing exact authored table-footprint resolver supplies support height plus 0.002. It never invents a table when none is modeled.

Cash uses all eight original static world states. Content.Client/Stack/StackSystem.cs owns the threshold selection [10,20,50,100,200,500,1000], then ItemCounterSystem.ProcessOpaqueSprite selects the mapped base layer through ContentHelpers.RoundToEqualLevels. Existing spriteStates consumes that actual layer; no chemistry, cash count, merge/split or new animation controller is invented. Unknown states, extra visible layers or unsupported transforms retain the existing source fallback. Offline exports require the corresponding source-owned stack resolver. Native gameplay validation is separate from these asset proofs.

Cash printed surfaces preserve every original RGBA pixel and transparent margin in an exact plan reconstruction. Separate pale/shaded paper plies and stepped bundle heights make closed solids. The source bundle side stripes become shallow descending sheet steps. This is an explicit depth interpretation, not proof of a recovered perspective camera. Hidden backs, paper thickness and bundle tier heights are inferred. No denomination is rendered as a floating image card.

Both forks have three actual solid tines and two empty 0.015625-wide slots, a head bridge, shoulder, neck, handle and end. One-pixel tine-center crops and full bridge/neck/handle crops are verbatim. Touching dark sprite outlines are narrowed into genuine 3D slots; the cropped original source reference is never modified. Z thickness, bevel/shoulder interpretation and unobserved underside are inferred. In-hand four-direction art is not a world-state model. RMCForkPlastic inherits RMCFork, not upstream ForkPlastic. UtensilSystem consumes food directly and deletes a broken utensil; there is no authored food-on-fork or invented broken animation.

Raw map inventory covers 46 saved records: cash16 Redux/9 classic and forks15 Redux/6 classic. Only33 are exported world props: cash7 Redux/5 classic, forks15 Redux/6 classic. Thirteen cash entries are hidden containers and stay hidden. The only saved count override is hidden Redux surface UID18033 count5000, selecting the existing1000 state. All visible props have only Transform overrides. Fork poses include -90,0,+90 degrees; no offset recentering is applied.

Reproduce dedicated assets with `python Tools/three_d/author_cash_cutlery.py`; use `--check --skip-reviews` for byte equality. No global library/scene build or game launch is performed. The cached raw map evidence is generated/cash-cutlery-source-audit.json; generated/cash-cutlery-proof.json records crops, physical slots, part counts and saved-context checks against the frozen950 baseline. Conservative AABB contact counts are not live gameplay collision claims.

## Attribution

Original source sheets and derived crops use CC-BY-SA-3.0. Preserve original attribution on redistribution.

_RMC14/Objects/Misc/spacecash.rsi: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6791699de749afc1c62bc65ec6f2326a00b3bb61/icons/obj/items/items.dmi

_RMC14/Objects/Tools/Kitchen/fork.rsi: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/kitchen_tools.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/equipment/kitchen_tools_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/equipment/kitchen_tools_righthand.dmi
