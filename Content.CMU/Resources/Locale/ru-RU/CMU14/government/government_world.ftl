# The tab and the console
rumc-citizen-tab-world = Мировой заказ
ent-RUMCLogisticsConsole = логистическая консоль
    .desc = Логистическая консоль. Здесь главы выбирают зону посадки и город на карте мира, вызывают торговый шатл, заказывают товары городов и отправляют шатл с тем, что колонисты на него положили.

# The logistics console
rumc-foreman-title = Логистическая консоль: торговый шатл
rumc-foreman-subtitle = Закупки идут на бюджет колонии, продажи делают колонисты. С выручки 70% идёт в бюджет, 30% тем, кто положил товар.
rumc-foreman-shuttle = Шатл
rumc-foreman-phase-idle = Шатл не вызван
rumc-foreman-phase-calling = Шатл летит на зону посадки
rumc-foreman-phase-landed = Шатл на зоне: разгружайте и грузите
rumc-foreman-phase-departing = Шатл улетает
rumc-foreman-phase-away = Шатл в пути к городу
rumc-foreman-phase-left = До конца этапа { $time }
rumc-foreman-picked = Зона посадки: { $zone }. Город: { $city }.
rumc-foreman-call = Вызвать шатл
rumc-foreman-depart = Отправить шатл сейчас
rumc-foreman-hint = Шатл прилетает с тем, что вы заказали, стоит на зоне 5 минут и улетает с тем, что колонисты положили на борт (товар кладётся на консоль шатла). Пока он на зоне, она занята: другие шатлы там сесть не смогут. Улететь он не может, пока на борту есть люди. Оплата приходит, когда он долетит до города.
rumc-foreman-zones = Зоны посадки
rumc-foreman-zones-hint = Свободные зоны колонии. Зона, где уже стоит шатл, занята.
rumc-foreman-zones-none = На этой карте нет зон посадки.
rumc-foreman-zone-busy = { $name } (занята)
rumc-foreman-cities = Города карты мира
rumc-foreman-cities-none = Городов на карте нет.
rumc-foreman-city-line = { $name }: { $caps }
rumc-foreman-city-crisis = Беда в городе: { $crisis }
rumc-foreman-goods = Товары: { $city }
rumc-foreman-goods-hint = Покупка идёт по цене города (с небольшим разбросом), продажа по текущей цене: чем больше город уже принял такого товара, тем дешевле он платит дальше, цена со временем возвращается.
rumc-foreman-pick-city = Сначала выберите город.
rumc-foreman-lots = { $value } парт.
rumc-foreman-good-line = Город платит сейчас: { $sell } за штуку ({ $percent }% от полной цены). Партия: { $lot } шт.
rumc-foreman-buy = Купить { $amount } шт. по { $price }: { $cost }
rumc-foreman-not-sold = Этот город такое не продаёт.
rumc-foreman-cart = Заказы на ближайший рейс
rumc-foreman-cart-none = Заказов нет.
rumc-foreman-cart-line = { $good }: { $amount } шт., платил { $payer }, { $cost }
rumc-foreman-cart-cancel = Отменить заказ
rumc-foreman-orders = Заказы городов
rumc-foreman-orders-hint = Города сами просят товары и платят за них больше. Заказ выполняется, когда шатл везёт этот товар именно в этот город.
rumc-foreman-orders-none = Заказов городов нет.
rumc-foreman-order-line = { $city }: { $good }, { $filled } из { $amount }, надбавка к цене { $percent }%
rumc-foreman-order-left = Осталось { $time }
rumc-foreman-shipment = На борту шатла на продажу
rumc-foreman-shipment-hint = Колонисты кладут товары на консоль шатла или грузят в любые ящики на борту: всё, что лежит в ящиках, тоже уйдёт на продажу (деньги за ящики идут колонии). Что осталось лежать на полу, улетит и пропадёт без оплаты.
rumc-foreman-shipment-none = Ничего не положено.
rumc-foreman-shipment-line = { $good }: { $amount } шт. ({ $owner })
rumc-foreman-expected = Ожидаемая выручка: { $sum }

# Why not, and what is told
rumc-world-fail-zone = Выберите свободную зону посадки.
rumc-world-fail-load = Не удалось подготовить шатл.
rumc-world-fail-flight = Шатл не смог вылететь. Заказы возвращены.
rumc-world-fail-passengers = На борту шатла люди: пусть выйдут.
rumc-world-intake-closed = Шатл сейчас не принимает груз.
rumc-world-intake-no-account = Нужен банковский счёт, чтобы получить свою долю.
rumc-world-intake-taken = Принято: { $amount } шт. ({ $good }), город платит сейчас { $price } за штуку.
rumc-world-paid = Город { $city } заплатил за ваш товар: { $sum }.
rumc-world-payer-colony = колония
rumc-world-order-refused = Заказ сейчас невозможен: шатл занят рейсом или корзина полна.
rumc-world-order-no-money = Не хватает денег на счёте: { $sum }.

# The console of the people
rumc-world-title = Мировой заказ
rumc-world-hint = Заказывайте товары городов на свои деньги. Они приедут на ближайшем рейсе торгового шатла (его вызывают главы), забрать их можно, когда шатл стоит на зоне. В цене небольшая наценка за доставку.
rumc-world-city = Шатл идёт в город: { $city }
rumc-world-goods = Что можно заказать
rumc-world-buy = Заказать { $amount } шт. по { $price }: { $cost }
rumc-world-orders = Ваши заказы
rumc-world-orders-none = Заказов нет.
rumc-world-order-line = { $good }: { $amount } шт., { $cost }

# The goods
rumc-good-steel-name = Стальные листы
rumc-good-wood-name = Доски
rumc-good-glass-name = Стекло
rumc-good-cloth-name = Ткань
rumc-good-plastic-name = Пластик
rumc-good-iron-ore-name = Железная руда
rumc-good-copper-ore-name = Медная руда
rumc-good-iron-ingot-name = Железные слитки
rumc-good-copper-ingot-name = Медные слитки
rumc-good-silver-ingot-name = Серебряные слитки
rumc-good-titanium-ingot-name = Титановые слитки
rumc-good-weapon-steel-ingot-name = Слитки оружейной стали
rumc-good-plasteel-ingot-name = Слитки пластила
rumc-world-crate-owner = Ящики шаттла
