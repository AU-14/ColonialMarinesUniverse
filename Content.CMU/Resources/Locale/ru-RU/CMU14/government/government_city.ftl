# The tabs
rumc-government-tab-relations = Фракции
rumc-government-tab-cities = Города
rumc-government-tab-credit = Кредиты
rumc-government-tab-licenses = Лицензии

# The kinds of cities
rumc-government-cap-tradehub = Торговый узел
rumc-government-cap-armsmarket = Рынок оружия
rumc-government-cap-intelligence = Разведка
rumc-government-cap-blackmarket = Чёрный рынок

# Credit
rumc-government-credit-rating = Кредитный рейтинг: { $value }
rumc-government-credit-rating-hint = Чем он выше, тем больше дают взаймы и тем лучше условия. Платёж в срок поднимает рейтинг, просрочка бьёт по нему, дефолт обрушивает.
rumc-government-credit-lenders = Кредиторы
rumc-government-credit-terms = Процент за весь срок: { $interest }%, платежей: { $count }, раз в { $time }
rumc-government-credit-available = Можно взять: { $amount }
rumc-government-credit-take = Взять { $amount }
rumc-government-credit-loans = Кредиты города
rumc-government-credit-no-loans = Кредитов нет.
rumc-government-credit-loan-line = { $lender }: платёж { $instalment }, осталось платежей { $left }, следующий через { $time }
rumc-government-credit-missed-line = Просрочек: { $count }
rumc-government-credit-repay = Погасить сразу ({ $sum })
rumc-government-investments = Вложения
rumc-government-investments-hint = Вложение идёт какое-то время и либо окупается с прибылью, либо проваливается и возвращает малую часть.
rumc-government-investment-line = Цена { $cost }, срок { $time }, шанс { $chance }%, вернёт при успехе { $payout }
rumc-government-investment-income = После успеха: деньги { $income } в минуту
rumc-government-invest = Вложить
rumc-government-investments-active = Вложения в работе
rumc-government-investment-running = { $name }: ещё { $time }
rumc-government-investment-success = { $name }: окупилось, приносит доход
rumc-government-investment-failure = { $name }: провалилось
rumc-government-loan-blocked-rating = Нужен рейтинг от { $rating }.
rumc-government-loan-blocked-default = После дефолта не даёт ещё { $minutes } мин.
rumc-government-loan-blocked-many = Уже { $max } кредита.
rumc-government-loan-blocked-limit = Лимит этого кредитора исчерпан.
rumc-government-investment-blocked-many = Уже { $max } вложения в работе.

# The cities of the map
rumc-government-cities-title = Другие города карты
rumc-government-cities-hint = Подарки и помощь в беде улучшают отношение города. С хорошим отношением можно заключать соглашения.
rumc-government-city-relation = Отношение к вам: { $value }
rumc-government-city-gift = Подарок ({ $cost })
rumc-government-city-aid = Помочь в беде ({ $cost })
rumc-government-city-crisis = Беда: { $crisis }, ещё { $time }
rumc-government-city-calm = Спокойно.
rumc-government-accords = Соглашения с городами
rumc-government-accords-slots = Соглашений с городами: { $used } из { $slots }
rumc-government-no-accords = Соглашений нет.
rumc-government-accord-cost = Цена: { $money }, срок { $time }, отношение от { $relation }
rumc-government-accord-requires = Нужен город типа: { $kind }
rumc-government-accord-sign = Заключить
rumc-government-accord-left = осталось { $time }
rumc-government-accord-in-force = Уже действует.
rumc-government-accord-slots-full = Все места для соглашений заняты.
rumc-government-accord-need = Нужно отношение { $relation }.
rumc-government-accord-city-lacks = Город не того типа.

# Licences
rumc-government-licenses-title = Выдача лицензий
rumc-government-licenses-hint = Лицензию получает человек рядом с консолью. Пошлина списывается с его банковского счёта в бюджет города, мэру выдаётся карточка. Нарушения фиксирует и штрафует БКМ.
rumc-government-license-cost = Пошлина { $fee }, срок { $time }
rumc-government-license-nearby = Рядом с консолью
rumc-government-license-no-nearby = Рядом никого нет.
rumc-government-license-issue = Выдать
rumc-government-licenses-issued = Выданные лицензии
rumc-government-license-none = Лицензий нет.
rumc-government-license-line = № { $serial }, { $type }: { $holder }
rumc-government-license-valid = действует, осталось { $time }
rumc-government-license-expired = истекла
rumc-government-license-revoked = отозвана
rumc-government-license-revoke = Отозвать

# The card
rumc-license-card-examine = Лицензия: { $type }. Владелец: { $holder }. № { $serial }. { $status }
rumc-license-card-valid = Действительна, осталось { $minutes } мин.
rumc-license-card-expired = Срок истёк.
rumc-license-card-revoked = ОТОЗВАНА.
ent-RUMCCityLicenseCard = карточка городской лицензии
    .desc = Карточка лицензии, выданной мэром. Осмотрите её, чтобы прочитать, что в ней написано.
ent-RUMCCityLicenseCardWeapon = карточка разрешения на оружие
    .desc = Карточка лицензии, выданной администрацией. Осмотрите её, чтобы прочитать, что в ней написано.
ent-RUMCCityLicenseCardTrade = карточка торговой лицензии
    .desc = Карточка лицензии, выданной администрацией. Осмотрите её, чтобы прочитать, что в ней написано.
ent-RUMCCityLicenseCardMining = карточка лицензии на добычу
    .desc = Карточка лицензии, выданной администрацией. Осмотрите её, чтобы прочитать, что в ней написано.
ent-RUMCCityLicenseCardNightWork = карточка разрешения на ночную работу
    .desc = Карточка лицензии, выданной администрацией. Осмотрите её, чтобы прочитать, что в ней написано.
ent-RUMCCityLicenseCardBuild = карточка разрешения на строительство
    .desc = Карточка лицензии, выданной администрацией. Осмотрите её, чтобы прочитать, что в ней написано.
ent-RUMCCityDeedCard = документ на участок
    .desc = Документ на участок города. Осмотрите его, чтобы прочитать, чей он и где находится.

# The lenders
rumc-lender-city-bank-name = Городской банк
rumc-lender-city-bank-desc = Небольшие суммы и высокий процент. Даёт всем, даже с плохим рейтингом.
rumc-lender-trading-house-name = Торговый дом
rumc-lender-trading-house-desc = Средние суммы и разумный процент. Нужен приличный рейтинг.
rumc-lender-metropolis-name = Метрополия
rumc-lender-metropolis-desc = Большие суммы и низкий процент, но только городу с хорошей репутацией.

# The investments
rumc-investment-market-name = Городской рынок
rumc-investment-market-desc = Новый торговый ряд. Надёжно, но прибыль небольшая.
rumc-investment-port-name = Порт
rumc-investment-port-desc = Причал и склады. Дорого и долго, но потом приносит доход.
rumc-investment-mine-name = Шахта на окраине
rumc-investment-mine-desc = Рискованно: может не найти ничего. Зато при удаче платит больше всех.

# The agreements with the cities
rumc-accord-trade-union-name = Торговый союз
rumc-accord-trade-union-desc = Единые правила торговли с городом-узлом: доход и скидка на пошлину.
rumc-accord-mutual-aid-name = Взаимопомощь
rumc-accord-mutual-aid-desc = Города помогают друг другу в беде: вызовы третьих сторон дешевле.
rumc-accord-joint-works-name = Совместные работы
rumc-accord-joint-works-desc = Общие стройки и ремонты: заметный доход от общей работы.
rumc-accord-arms-supply-name = Поставки оружия
rumc-accord-arms-supply-desc = Город-оружейник продаёт охране по льготной цене: вызовы третьих сторон заметно дешевле.

# The licences
rumc-license-weapon-name = Разрешение на оружие
rumc-license-weapon-desc = Право носить и применять оружие в городе.
rumc-license-trade-name = Торговая лицензия
rumc-license-trade-desc = Право торговать на городском рынке и через торговые платформы.
rumc-license-mining-name = Лицензия на добычу
rumc-license-mining-desc = Право добывать ресурсы на землях города.
rumc-license-night-work-name = Разрешение на ночную работу
rumc-license-night-work-desc = Право работать в ночную смену.
rumc-license-build-name = Разрешение на строительство
rumc-license-build-desc = Право строить вне размеченных зон землеустройства (на чужом участке строить всё равно нельзя).
rumc-license-card-permanent = Бессрочно, пока участок за владельцем.

# The tabs
rumc-government-tab-land = Земля

# The land
rumc-land-title = Землеустройство
rumc-land-hint = Мэр размечает зоны, где можно строить: жильё, торговля, производство. Люди приходят к консоли и просят участок (правый клик по консоли). Участок — квадрат 5 на 5 в зоне нужного вида; за него платят аренду со счёта каждый цикл, сверху идёт налог на землю.
rumc-land-tax = Налог на землю: { $value }% к аренде
rumc-land-tax-hint = Налог платит владелец участка вместе с арендой: чем он выше, тем дороже держать землю.
rumc-land-enforce-on = Строить вне зон без разрешения нельзя: ВКЛ
rumc-land-enforce-off = Строить можно где угодно: ВЫКЛ
rumc-land-enforce-hint = Когда включено, меню строительства закрыто за пределами зон (на карте, где стоит консоль) и на чужих участках. Исключение — «{ $permit }» (пошлина { $fee }, выдаётся на вкладке «Лицензии»).
rumc-land-tablet = Выдать землемерный планшет
rumc-land-tablet-hint = Стоя в середине будущей зоны, откройте правым кликом меню планшета: выберите размер и вид зоны. Метка стоит { $cost }, всего зон не больше { $max }; планшетов не больше двух.
rumc-land-income = Сейчас аренда даёт { $sum } за цикл ({ $time })
rumc-land-kinds = Виды зон
rumc-land-kind-line = { $name }: аренда участка { $rent } за цикл
rumc-land-zones = Зоны: { $used } из { $max }
rumc-land-zones-none = Зон нет.
rumc-land-zone-line = { $size }×{ $size }, угол X { $x }, Y { $y }; участков занято { $plots } из { $capacity }
rumc-land-zone-remove = Снять зону
rumc-land-requests = Заявки на участок
rumc-land-requests-hint = Участок выбирается сам: первая свободная клетка в зоне нужного вида. Грамота падает в руки мэру, он передаёт её владельцу.
rumc-land-requests-none = Заявок нет.
rumc-land-request-line = { $name } просит участок: { $kind }
rumc-land-request-nofree = В зонах этого вида нет свободных участков.
rumc-land-allot = Выделить участок
rumc-land-decline = Отказать
rumc-land-plots = Выданные участки
rumc-land-plots-none = Участков никто не держит.
rumc-land-plot-line = № { $id }: { $holder }, { $zone }
rumc-land-plot-place = { $place }; аренда { $rent } за цикл
rumc-land-plot-missed = Не заплатил аренду: { $count } раз(а)
rumc-land-plot-revoke = Забрать участок

# The land, in the world
rumc-land-zone-name = { $kind } №{ $id }
rumc-land-verb-mark = Разметить зону: { $kind } ({ $size }×{ $size })
rumc-land-verb-size = Размер зоны: { $size }×{ $size }
rumc-land-verb-unmark = Снять зону, в которой вы стоите
rumc-land-verb-request = Попросить участок: { $kind }
rumc-land-verb-zones = Где в городе можно строить
rumc-land-size-set = Следующая зона будет { $size }×{ $size }.
rumc-land-tablet-examine = Размер следующей метки: { $size }×{ $size }.
rumc-land-tablet-in-zone = Вы стоите в зоне: { $zone }.
rumc-land-no-grid = Зону можно разметить только на земле города.
rumc-land-too-many = Зон уже { $max }, больше не разметить.
rumc-land-no-money = В бюджете не хватает на разметку: { $cost }.
rumc-land-overlap = Здесь уже размечена зона: { $zone }.
rumc-land-marked = Размечена зона: { $zone } ({ $size }×{ $size }).
rumc-land-not-in-zone = Вы стоите вне зон.
rumc-land-unmarked = Зона снята: { $zone }.
rumc-land-request-refused = У вас уже есть заявка, или очередь переполнена.
rumc-land-request-has-plots = У вас уже { $max } участка — больше не дают.
rumc-land-request-sent = Заявка на участок ({ $kind }) подана мэру.
rumc-land-zones-empty = Мэр ещё не размечал зоны.
rumc-land-zones-line = { $zone }: угол X { $x }, Y { $y }, { $size }×{ $size }
rumc-land-card-type = Участок: { $zone }, угол X { $x }, Y { $y }
rumc-land-allotted = Вам выделен участок в зоне «{ $zone }», угол X { $x }, Y { $y }.
rumc-land-declined = Мэр отказал в участке.
rumc-land-taken-back = Участок № { $plot } у вас забрали.
rumc-land-rent-missed = Нечем заплатить аренду участка № { $plot } ({ $rent }). Ещё раз — и участок заберут.
rumc-land-denied-outside = Здесь строить нельзя: это вне зон города. Нужно разрешение мэра.
rumc-land-denied-plot = Это участок другого человека: { $holder }.

# The zones
rumc-zone-housing-name = Жильё
rumc-zone-housing-desc = Дома, бараки, общежития. Аренда небольшая.
rumc-zone-trade-name = Торговля
rumc-zone-trade-desc = Лавки и склады у площади. Самая дорогая аренда.
rumc-zone-production-name = Производство
rumc-zone-production-desc = Мастерские, плавильни, цеха. Аренда средняя.
ent-RUMCSurveyorTablet = землемерный планшет
    .desc = Командный планшет с чертежами города. Правый клик по нему — разметка зон; с планшетом в руке размеченные зоны видны на земле.
