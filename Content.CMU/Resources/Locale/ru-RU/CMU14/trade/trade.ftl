ent-RUMCTradeParcel = посылка
    .desc = Запечатанная картонная посылка. Всё, что в неё упаковано, продаётся вместе с ней на торговой платформе. Чтобы достать вещи, посылку нужно вскрыть.
ent-RUMCTradePacker = упаковщик посылок
    .desc = Принимает картон и всё, что в него положат, и по команде запечатывает вещи в посылку. Один лист картона запечатывает восемь вещей. Нажмите на него пустой рукой, чтобы упаковать.
ent-RUMCTradePlatform = торговая платформа
    .desc = Посадочная площадка для торговых дронов. Вложите в неё контракты компаний (не больше девяти): они открывают то, что дроны покупают и продают. Посылку на площадке можно продать, а купленное дрон оставляет на ней. Дрон прилетает через десять секунд после вызова и только на площадку под открытым небом.
ent-RUMCTradeDrone = торговый дрон
    .desc = Торговый дрон. Прилетает за посылкой или привозит её.

au14-craft-recipe-tradepacker = Упаковщик посылок
au14-craft-recipe-tradeplatform = Торговая платформа
au14-craft-recipe-cardboard = Картон

rumc-trade-packer-cardboard-loaded = Вы загружаете картон. В упаковщике листов: { $count }.
rumc-trade-packer-cardboard-full = В упаковщик больше не помещается картон.
rumc-trade-packer-full = В упаковщике больше нет места.
rumc-trade-packer-cannot-take = Упаковщик не принимает { $item }.
rumc-trade-packer-item-loaded = Вы кладёте { $item } в упаковщик. Внутри вещей: { $count }.
rumc-trade-packer-empty = В упаковщике ничего нет.
rumc-trade-packer-no-cardboard = Не хватает картона: нужно листов { $need }, загружено { $have }.
rumc-trade-packer-started = Упаковщик начинает работу.
rumc-trade-packer-done = Упаковщик запечатывает посылку.
rumc-trade-packer-take-out = Достать всё из упаковщика
rumc-trade-packer-examine = В упаковщике картона: { $cardboard } л., вещей внутри: { $items }. На них нужно листов: { $need }.

rumc-trade-parcel-open = Вскрыть посылку
rumc-trade-parcel-opened = Вы вскрываете посылку.
rumc-trade-parcel-examine = В посылке вещей: { $count }.

rumc-trade-busy = Дрон уже вызван.
rumc-trade-no-sky = Платформа стоит не под открытым небом: дрон не сможет прилететь.
rumc-trade-no-sky-arrival = Платформа оказалась закрыта от неба, дрон не прилетел. Деньги возвращены.
rumc-trade-no-parcel = На платформе нет посылки.
rumc-trade-no-card = Нужна ваша ID-карта с банковским счётом.
rumc-trade-no-money = На вашей карте не хватает денег.
rumc-trade-called = Дрон вызван и прилетит через { $seconds } с.
rumc-trade-sold = Дрон забрал посылки: { $worth }, налог колонии { $tax }, продавцу { $payout }.
rumc-trade-delivered = Дрон оставил посылку.

rumc-trade-ui-title = Торговая платформа
rumc-trade-ui-sell-section = Продажа
rumc-trade-ui-sell-hint = Поставьте запечатанную посылку на платформу.
rumc-trade-ui-sell = Продать посылки
rumc-trade-ui-parcels = Посылок на платформе: { $count }
rumc-trade-ui-value = Стоимость: { $value }
rumc-trade-ui-tax = Налог колонии: { $percent }% ({ $tax })
rumc-trade-ui-payout = Вы получите: { $payout }
rumc-trade-ui-buy-section = Покупка
rumc-trade-ui-balance = На вашей карте: { $money }
rumc-trade-ui-no-card = Нет ID-карты с банковским счётом.
rumc-trade-ui-price = { $price }
rumc-trade-ui-buy = Купить
rumc-trade-ui-busy = Дрон уже вызван.
rumc-trade-ui-sky-open = Платформа под открытым небом.
rumc-trade-ui-sky-closed = Платформа закрыта от неба: дрон не прилетит.


rumc-trade-company-ores = Горнорудная компания «Тяжёлая Порода»

rumc-trade-contracts-full = В платформе больше нет места для контрактов.
rumc-trade-contract-present = Контракт с этой компанией уже лежит в платформе.
rumc-trade-contract-refused = Контракт не удаётся вложить.
rumc-trade-contract-installed = Вы вкладываете контракт компании «{ $company }» в платформу.
rumc-trade-nothing-wanted = Ни один из контрактов в платформе не покупает то, что лежит на ней.
rumc-trade-locked = Этот товар откроется на более высоком уровне контракта.
rumc-trade-platform-examine = Контрактов в платформе: { $count } из { $max }.
rumc-trade-contract-examine-next = До следующего уровня: { $next } очков.

rumc-trade-ui-contracts-section = Контракты
rumc-trade-ui-contracts-count = Контракты: { $count } из { $max }
rumc-trade-ui-no-contracts = Контрактов нет: платформа ничего не покупает и не продаёт.
rumc-trade-ui-contract-standing = Уровень { $level }, очков { $points } из { $next }
rumc-trade-ui-contract-standing-max = Уровень { $level } (максимум), очков { $points }
rumc-trade-ui-eject = Достать
rumc-trade-ui-needs-level = Нужен уровень { $level }

ent-RUMCTradeContractMetals = контракт на руды
    .desc = Контракт с горнорудной компанией на руды и кристаллы. Вложите его в торговую платформу, чтобы продавать компании руду и кристаллы, заказывать у неё готовые слитки по десять штук и кристаллы.
ent-RUMCTradeContractGems = контракт на самоцветы
    .desc = Контракт с горнорудной компанией на самоцветы. Вложите его в торговую платформу, чтобы продавать компании необработанные самоцветы и покупать их у неё.
ent-RUMCTradeContractFuel = контракт на топливо
    .desc = Контракт с горнорудной компанией на топливо. Вложите его в торговую платформу, чтобы продавать компании кокс и покупать его у неё.
ent-RUMCCrateTradeStart = ящик начала торговли
    .desc = Запечатанный ящик со всем для начала торговли: упаковщик посылок с картоном, торговая платформа и четыре контракта. Открывается монтировкой.

rumc-trade-company-label = { $company } — { $category }
rumc-trade-category-metals = руды и кристаллы
rumc-trade-category-gems = самоцветы
rumc-trade-category-fuel = топливо
rumc-trade-contract-metals-desc = Все металлические руды и кристаллы шахт, от самых частых до самых редких, и три сплава: латунь, оружейная сталь и пласталь. Компания покупает руду, а готовые слитки того же металла заказываются у неё партиями по десять штук. Слитки сплавов, у которых нет руды, и кристаллы покупаются и заказываются как есть.
rumc-trade-contract-gems-desc = Необработанные самоцветы шахт. Дрон платит за самоцвет десятую часть цены, по которой компания его продаёт.
rumc-trade-contract-fuel-desc = Кокс из шахт. Дрон платит за него десятую часть цены, по которой компания его продаёт.
rumc-trade-contract-examine = Компания: { $company }, категория: { $category }. Уровень { $level } из 3, очков торговли: { $points }.
rumc-trade-ui-contracts-hint = Чтобы вложить контракт, ударьте им по платформе.
rumc-trade-ui-category = { $name } ({ $count })
rumc-trade-ui-no-goods = Нечего заказывать: вложите контракт.

ent-RUMCTradeContractSheets = контракт на листы
    .desc = Контракт с горнорудной компанией на листы металла. Вложите его в торговую платформу, чтобы продавать компании листы и заказывать у неё листы по десять штук.
rumc-trade-category-sheets = листы
rumc-trade-contract-sheets-desc = Листы всех металлов и сплавов, от железа до урана. Лист прессуется из слитка, а слиток весит четыре руды: дрон платит за лист четыре цены руды, а заказать десять листов можно за десять слитков с надбавкой на прессовку.
