using System.Text;
using Content.Server._RMC14.Marines.Roles.Ranks;
using Content.Server.CMU14.Marines.Roles.Chevrons;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Roles;

public sealed partial class CMUPersonalTodoSystem : EntitySystem
{
    private const string PaperPrototype = "CMUPaperGrey";
    private const string CashPrototype = "RMCSpaceCash100";
    private const int MinimumTaskCount = 5;
    private const int MaximumTaskCount = 7;

    private static readonly string[] Uniforms =
    [
        "cmu-personal-todo-uniform-dress-blues",
        "cmu-personal-todo-uniform-civilian",
        "cmu-personal-todo-uniform-utility",
    ];

    private static readonly (string Food, string Recipe)[] Foods =
    [
        ("cmu-personal-todo-food-applecake", "cmu-personal-todo-recipe-applecake"),
        ("cmu-personal-todo-food-applepie", "cmu-personal-todo-recipe-applepie"),
        ("cmu-personal-todo-food-baconburger", "cmu-personal-todo-recipe-baconburger"),
        ("cmu-personal-todo-food-bbqribs", "cmu-personal-todo-recipe-bbqribs"),
        ("cmu-personal-todo-food-bagel", "cmu-personal-todo-recipe-bagel"),
        ("cmu-personal-todo-food-baguette", "cmu-personal-todo-recipe-baguette"),
        ("cmu-personal-todo-food-bananabread", "cmu-personal-todo-recipe-bananabread"),
        ("cmu-personal-todo-food-bananacreampie", "cmu-personal-todo-recipe-bananacreampie"),
        ("cmu-personal-todo-food-muffinbanana", "cmu-personal-todo-recipe-muffinbanana"),
        ("cmu-personal-todo-food-bangersmash", "cmu-personal-todo-recipe-bangersmash"),
        ("cmu-personal-todo-food-banhmi", "cmu-personal-todo-recipe-banhmi"),
        ("cmu-personal-todo-food-tacobeef", "cmu-personal-todo-recipe-tacobeef"),
        ("cmu-personal-todo-food-tacobeefsupreme", "cmu-personal-todo-recipe-tacobeefsupreme"),
        ("cmu-personal-todo-food-beetsoup", "cmu-personal-todo-recipe-beetsoup"),
        ("cmu-personal-todo-food-berryclafoutis", "cmu-personal-todo-recipe-berryclafoutis"),
        ("cmu-personal-todo-food-berrydelight", "cmu-personal-todo-recipe-berrydelight"),
        ("cmu-personal-todo-food-muffinberry", "cmu-personal-todo-recipe-muffinberry"),
        ("cmu-personal-todo-food-bigbiteburger", "cmu-personal-todo-recipe-bigbiteburger"),
        ("cmu-personal-todo-food-birthdaycake", "cmu-personal-todo-recipe-birthdaycake"),
        ("cmu-personal-todo-food-bisque", "cmu-personal-todo-recipe-bisque"),
        ("cmu-personal-todo-food-ricegumbo", "cmu-personal-todo-recipe-ricegumbo"),
        ("cmu-personal-todo-food-blueberrycake", "cmu-personal-todo-recipe-blueberrycake"),
        ("cmu-personal-todo-food-blueberrypancake", "cmu-personal-todo-recipe-blueberrypancake"),
        ("cmu-personal-todo-food-boiledrice", "cmu-personal-todo-recipe-boiledrice"),
        ("cmu-personal-todo-food-boiledspaghetti", "cmu-personal-todo-recipe-boiledspaghetti"),
        ("cmu-personal-todo-food-hotchili", "cmu-personal-todo-recipe-hotchili"),
        ("cmu-personal-todo-food-bread", "cmu-personal-todo-recipe-bread"),
        ("cmu-personal-todo-food-bun", "cmu-personal-todo-recipe-bun"),
        ("cmu-personal-todo-food-butternoodles", "cmu-personal-todo-recipe-butternoodles"),
        ("cmu-personal-todo-food-butteredtoast", "cmu-personal-todo-recipe-butteredtoast"),
        ("cmu-personal-todo-food-caesarsalad", "cmu-personal-todo-recipe-caesarsalad"),
        ("cmu-personal-todo-food-cake", "cmu-personal-todo-recipe-cake"),
        ("cmu-personal-todo-food-rmcpizzacalabresa", "cmu-personal-todo-recipe-rmcpizzacalabresa"),
        ("cmu-personal-todo-food-carbonara", "cmu-personal-todo-recipe-carbonara"),
        ("cmu-personal-todo-food-carpsilog", "cmu-personal-todo-recipe-carpsilog"),
        ("cmu-personal-todo-food-carrotcake", "cmu-personal-todo-recipe-carrotcake"),
        ("cmu-personal-todo-food-carrotfries", "cmu-personal-todo-recipe-carrotfries"),
        ("cmu-personal-todo-food-cheeseburger", "cmu-personal-todo-recipe-cheeseburger"),
        ("cmu-personal-todo-food-cheesecake", "cmu-personal-todo-recipe-cheesecake"),
        ("cmu-personal-todo-food-omelette", "cmu-personal-todo-recipe-omelette"),
        ("cmu-personal-todo-food-cheesesteak", "cmu-personal-todo-recipe-cheesesteak"),
        ("cmu-personal-todo-food-cheesyfries", "cmu-personal-todo-recipe-cheesyfries"),
        ("cmu-personal-todo-food-nachoscheesy", "cmu-personal-todo-recipe-nachoscheesy"),
        ("cmu-personal-todo-food-muffincherry", "cmu-personal-todo-recipe-muffincherry"),
        ("cmu-personal-todo-food-cherrypie", "cmu-personal-todo-recipe-cherrypie"),
        ("cmu-personal-todo-food-chickensandwich", "cmu-personal-todo-recipe-chickensandwich"),
        ("cmu-personal-todo-food-tacochicken", "cmu-personal-todo-recipe-tacochicken"),
        ("cmu-personal-todo-food-tacochickensupreme", "cmu-personal-todo-recipe-tacochickensupreme"),
        ("cmu-personal-todo-food-chowmein", "cmu-personal-todo-recipe-chowmein"),
        ("cmu-personal-todo-food-citrussalad", "cmu-personal-todo-recipe-citrussalad"),
        ("cmu-personal-todo-food-coleslaw", "cmu-personal-todo-recipe-coleslaw"),
        ("cmu-personal-todo-food-copypasta", "cmu-personal-todo-recipe-copypasta"),
        ("cmu-personal-todo-food-corninbutter", "cmu-personal-todo-recipe-corninbutter"),
        ("cmu-personal-todo-food-cornbread", "cmu-personal-todo-recipe-cornbread"),
        ("cmu-personal-todo-food-cornedbeef", "cmu-personal-todo-recipe-cornedbeef"),
        ("cmu-personal-todo-food-creamcheesebread", "cmu-personal-todo-recipe-creamcheesebread"),
        ("cmu-personal-todo-food-croissant", "cmu-personal-todo-recipe-croissant"),
        ("cmu-personal-todo-food-rmccubancarp", "cmu-personal-todo-recipe-rmccubancarp"),
        ("cmu-personal-todo-food-donut", "cmu-personal-todo-recipe-donut"),
        ("cmu-personal-todo-food-dumplings", "cmu-personal-todo-recipe-dumplings"),
        ("cmu-personal-todo-food-eggrice", "cmu-personal-todo-recipe-eggrice"),
        ("cmu-personal-todo-food-eggplantparm", "cmu-personal-todo-recipe-eggplantparm"),
        ("cmu-personal-todo-food-enchiladas", "cmu-personal-todo-recipe-enchiladas"),
        ("cmu-personal-todo-food-carpburger", "cmu-personal-todo-recipe-carpburger"),
        ("cmu-personal-todo-food-rmcfishfingers", "cmu-personal-todo-recipe-rmcfishfingers"),
        ("cmu-personal-todo-food-rmcfishsushi", "cmu-personal-todo-recipe-rmcfishsushi"),
        ("cmu-personal-todo-food-tacofish", "cmu-personal-todo-recipe-tacofish"),
        ("cmu-personal-todo-food-fiveburger", "cmu-personal-todo-recipe-fiveburger"),
        ("cmu-personal-todo-food-frosteddonut", "cmu-personal-todo-recipe-frosteddonut"),
        ("cmu-personal-todo-food-onionsoup", "cmu-personal-todo-recipe-onionsoup"),
        ("cmu-personal-todo-food-frenchtoast", "cmu-personal-todo-recipe-frenchtoast"),
        ("cmu-personal-todo-food-friedegg", "cmu-personal-todo-recipe-friedegg"),
        ("cmu-personal-todo-food-breakfastamerican", "cmu-personal-todo-recipe-breakfastamerican"),
        ("cmu-personal-todo-food-gabagool", "cmu-personal-todo-recipe-gabagool"),
        ("cmu-personal-todo-food-garlicbread", "cmu-personal-todo-recipe-garlicbread"),
        ("cmu-personal-todo-food-grapetart", "cmu-personal-todo-recipe-grapetart"),
        ("cmu-personal-todo-food-grilledcheesesandwich", "cmu-personal-todo-recipe-grilledcheesesandwich"),
        ("cmu-personal-todo-food-rmcgrilledfish", "cmu-personal-todo-recipe-rmcgrilledfish"),
        ("cmu-personal-todo-food-honeybun", "cmu-personal-todo-recipe-honeybun"),
        ("cmu-personal-todo-food-hotdog", "cmu-personal-todo-recipe-hotdog"),
        ("cmu-personal-todo-food-kimchisalad", "cmu-personal-todo-recipe-kimchisalad"),
        ("cmu-personal-todo-food-lemoncake", "cmu-personal-todo-recipe-lemoncake"),
        ("cmu-personal-todo-food-limecake", "cmu-personal-todo-recipe-limecake"),
        ("cmu-personal-todo-food-loadedbakedpotato", "cmu-personal-todo-recipe-loadedbakedpotato"),
        ("cmu-personal-todo-food-margheritapizza", "cmu-personal-todo-recipe-margheritapizza"),
        ("cmu-personal-todo-food-burgermcguffin", "cmu-personal-todo-recipe-burgermcguffin"),
        ("cmu-personal-todo-food-meatbread", "cmu-personal-todo-recipe-meatbread"),
        ("cmu-personal-todo-food-meatballsoup", "cmu-personal-todo-recipe-meatballsoup"),
        ("cmu-personal-todo-food-meatballsub", "cmu-personal-todo-recipe-meatballsub"),
        ("cmu-personal-todo-food-meatpie", "cmu-personal-todo-recipe-meatpie"),
        ("cmu-personal-todo-food-meatpizza", "cmu-personal-todo-recipe-meatpizza"),
        ("cmu-personal-todo-food-muffin", "cmu-personal-todo-recipe-muffin"),
        ("cmu-personal-todo-food-mushroompizza", "cmu-personal-todo-recipe-mushroompizza"),
        ("cmu-personal-todo-food-mushroomsoup", "cmu-personal-todo-recipe-mushroomsoup"),
        ("cmu-personal-todo-food-nachos", "cmu-personal-todo-recipe-nachos"),
        ("cmu-personal-todo-food-rmconigiri", "cmu-personal-todo-recipe-rmconigiri"),
        ("cmu-personal-todo-food-onionrings", "cmu-personal-todo-recipe-onionrings"),
        ("cmu-personal-todo-food-orangecake", "cmu-personal-todo-recipe-orangecake"),
        ("cmu-personal-todo-food-pancake", "cmu-personal-todo-recipe-pancake"),
        ("cmu-personal-todo-food-piemash", "cmu-personal-todo-recipe-piemash"),
        ("cmu-personal-todo-food-plainburger", "cmu-personal-todo-recipe-plainburger"),
        ("cmu-personal-todo-food-popcorn", "cmu-personal-todo-recipe-popcorn"),
        ("cmu-personal-todo-food-queso", "cmu-personal-todo-recipe-queso"),
        ("cmu-personal-todo-food-ricepork", "cmu-personal-todo-recipe-ricepork"),
        ("cmu-personal-todo-food-ricepudding", "cmu-personal-todo-recipe-ricepudding"),
        ("cmu-personal-todo-food-sashimi", "cmu-personal-todo-recipe-sashimi"),
        ("cmu-personal-todo-food-sausagebread", "cmu-personal-todo-recipe-sausagebread"),
        ("cmu-personal-todo-food-spaghettimeatballs", "cmu-personal-todo-recipe-spaghettimeatballs"),
        ("cmu-personal-todo-food-fries", "cmu-personal-todo-recipe-fries"),
        ("cmu-personal-todo-food-pastatomato", "cmu-personal-todo-recipe-pastatomato"),
        ("cmu-personal-todo-food-stewsoup", "cmu-personal-todo-recipe-stewsoup"),
        ("cmu-personal-todo-food-sugarcookie", "cmu-personal-todo-recipe-sugarcookie"),
        ("cmu-personal-todo-food-superbiteburger", "cmu-personal-todo-recipe-superbiteburger"),
        ("cmu-personal-todo-food-rmcpizzasupreme", "cmu-personal-todo-recipe-rmcpizzasupreme"),
        ("cmu-personal-todo-food-tacoshell", "cmu-personal-todo-recipe-tacoshell"),
        ("cmu-personal-todo-food-tapsilog", "cmu-personal-todo-recipe-tapsilog"),
        ("cmu-personal-todo-food-tocilog", "cmu-personal-todo-recipe-tocilog"),
        ("cmu-personal-todo-food-tofubread", "cmu-personal-todo-recipe-tofubread"),
        ("cmu-personal-todo-food-tofuburger", "cmu-personal-todo-recipe-tofuburger"),
        ("cmu-personal-todo-food-tomatosoup", "cmu-personal-todo-recipe-tomatosoup"),
        ("cmu-personal-todo-food-uncookedsausage", "cmu-personal-todo-recipe-uncookedsausage"),
        ("cmu-personal-todo-food-vanillacake", "cmu-personal-todo-recipe-vanillacake"),
        ("cmu-personal-todo-food-rmcvegetablepizza", "cmu-personal-todo-recipe-rmcvegetablepizza"),
        ("cmu-personal-todo-food-vegetablesoup", "cmu-personal-todo-recipe-vegetablesoup"),
        ("cmu-personal-todo-food-yorkshirepudding", "cmu-personal-todo-recipe-yorkshirepudding"),
    ];

    private static readonly string[] Exercises =
    [
        "cmu-personal-todo-exercise-bench-press",
        "cmu-personal-todo-exercise-chest-machine",
        "cmu-personal-todo-exercise-punching-bag",
        "cmu-personal-todo-exercise-treadmill",
    ];

    private static readonly string[] Materials =
    [
        "cmu-personal-todo-material-reinforced-glass",
        "cmu-personal-todo-material-wood",
        "cmu-personal-todo-material-metal",
    ];

    private static readonly string[] Bottles =
    [
        "cmu-personal-todo-bottle-absinthe",
        "cmu-personal-todo-bottle-cognac",
        "cmu-personal-todo-bottle-davenport",
        "cmu-personal-todo-bottle-gin",
        "cmu-personal-todo-bottle-grenadine",
        "cmu-personal-todo-bottle-melon-liquor",
        "cmu-personal-todo-bottle-rum",
        "cmu-personal-todo-bottle-tequila",
        "cmu-personal-todo-bottle-vodka",
        "cmu-personal-todo-bottle-wine",
    ];

    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRankSystem _rank = default!;
    [Dependency] private ChevronSystem _chevrons = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private PlayTimeTrackingManager _tracking = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete, after: [typeof(RankSystem)]);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null ||
            !HasComp<HumanoidProfileComponent>(ev.Mob) ||
            HasComp<CMUPersonalTodoIssuedComponent>(ev.Mob) ||
            !_prototypes.TryIndex<JobPrototype>(ev.JobId, out var job))
        {
            return;
        }

        EnsureComp<CMUPersonalTodoIssuedComponent>(ev.Mob);

        var characterName = MetaData(ev.Mob).EntityName;
        var rank = GetAccurateRank(ev, job);
        var accountPin = GetAccountPin(ev.Mob);
        var content = BuildPaperContent(characterName, rank, accountPin);

        var paper = Spawn(PaperPrototype, _transform.GetMoverCoordinates(ev.Mob));
        _metadata.SetEntityName(paper,
            Loc.GetString("cmu-personal-todo-paper-name", ("name", characterName)));
        _paper.SetContent(paper, content);
        GiveItem(ev.Mob, paper);

        var cash = Spawn(CashPrototype, _transform.GetMoverCoordinates(ev.Mob));
        GiveItem(ev.Mob, cash);
    }

    private string GetAccurateRank(PlayerSpawnCompleteEvent ev, JobPrototype job)
    {
        if (!_tracking.TryGetTrackerTimes(ev.Player, out var playTimes))
            playTimes = new Dictionary<string, TimeSpan>();

        if (ev.JobId != null &&
            _chevrons.ResolveIntendedRank(ev.Mob, ev.JobId, ev.Profile, playTimes) is { } intendedRank)
        {
            var rankName = Loc.TryGetString($"rank-{intendedRank.ID}", out var localizedName)
                ? localizedName
                : intendedRank.Name;

            return intendedRank.Paygrade == null
                ? rankName
                : $"({intendedRank.Paygrade}) {rankName}";
        }

        return _rank.GetRankString(ev.Mob, hasPaygrade: true) ?? job.LocalizedName;
    }

    private string GetAccountPin(EntityUid mob)
    {
        if (!_idCard.TryFindIdCard(mob, out var idCard) || idCard.Comp.AtmPin == 0)
            return Loc.GetString("cmu-personal-todo-pin-unavailable");

        var pin = idCard.Comp.AtmPin;
        return pin.ToString("D4");
    }

    private string BuildPaperContent(string characterName, string rank, string accountPin)
    {
        var availableTasks = new List<Func<string>>
        {
            BuildFoodTask,
            () => Loc.GetString("cmu-personal-todo-task-dry-clean"),
            BuildShoppingTask,
            () => Loc.GetString("cmu-personal-todo-task-reenlist"),
            BuildExerciseTask,
            () => Loc.GetString("cmu-personal-todo-task-ice-cream"),
            () => Loc.GetString("cmu-personal-todo-task-coffee"),
            BuildMaterialsTask,
            () => Loc.GetString("cmu-personal-todo-task-haircut"),
            BuildBoozeTask,
            () => Loc.GetString("cmu-personal-todo-task-armorer"),
            () => Loc.GetString("cmu-personal-todo-task-tv"),
        };

        _random.Shuffle(availableTasks);
        var taskCount = _random.Next(MinimumTaskCount, MaximumTaskCount + 1);
        var builder = new StringBuilder();
        builder.AppendLine(Loc.GetString("cmu-personal-todo-paper-header",
            ("name", FormattedMessage.EscapeText(characterName)),
            ("rank", FormattedMessage.EscapeText(rank)),
            ("pin", FormattedMessage.EscapeText(accountPin)),
            ("uniform", Loc.GetString(_random.Pick(Uniforms)))));

        for (var i = 0; i < taskCount - 1; i++)
            builder.AppendLine($"[check] {availableTasks[i]()}");

        builder.Append($"[check] {Loc.GetString("cmu-personal-todo-task-atm")}");
        return builder.ToString();
    }

    private string BuildFoodTask()
    {
        var food = _random.Pick(Foods);
        return Loc.GetString("cmu-personal-todo-task-food",
            ("food", Loc.GetString(food.Food)),
            ("recipe", Loc.GetString(food.Recipe)));
    }

    private string BuildShoppingTask()
    {
        return Loc.GetString("cmu-personal-todo-task-shopping",
            ("fruits", _random.Next(2, 7)),
            ("proteins", _random.Next(2, 7)),
            ("baking", _random.Next(2, 7)));
    }

    private string BuildExerciseTask()
    {
        return Loc.GetString("cmu-personal-todo-task-exercise",
            ("exercise", Loc.GetString(_random.Pick(Exercises))));
    }

    private string BuildMaterialsTask()
    {
        return Loc.GetString("cmu-personal-todo-task-materials",
            ("amount", _random.Next(5, 16)),
            ("material", Loc.GetString(_random.Pick(Materials))));
    }

    private string BuildBoozeTask()
    {
        return Loc.GetString("cmu-personal-todo-task-booze",
            ("bottle", Loc.GetString(_random.Pick(Bottles))));
    }

    private void GiveItem(EntityUid recipient, EntityUid item)
    {
        var slots = _inventory.GetSlotEnumerator(
            recipient,
            SlotFlags.BACK | SlotFlags.BELT | SlotFlags.INNERCLOTHING);

        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } container ||
                !TryComp(container, out StorageComponent? storage))
            {
                continue;
            }

            if (_storage.Insert(container, item, out _, storageComp: storage, playSound: false))
                return;
        }

        // The item was spawned at the recipient's feet, so a failed pickup remains visible and recoverable.
        _hands.TryPickupAnyHand(recipient, item, checkActionBlocker: false);
    }
}

[RegisterComponent]
public sealed partial class CMUPersonalTodoIssuedComponent : Component;
