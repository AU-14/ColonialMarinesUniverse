# The tab
rumc-citizen-tab-trade = Торговля

# The mayor
rumc-land-sales-tax = Налог с продаж: { $value }% от цены
rumc-land-sales-tax-hint = С каждой продажи между горожанами (из рук в руки, на прилавке, с торгов) эта доля идёт в бюджет. Остальное получает продавец.

# The console of the people
rumc-trade-title = Продажа готовых товаров
rumc-trade-hint = Покупатель всегда платит из своего кармана и получает вещь в руки в тот же миг, так что обмануть друг друга нельзя. С каждой продажи { $tax }% идёт в бюджет города. Третий способ — торговый прилавок: у него своё окно.
rumc-trade-hand = Из рук в руки
rumc-trade-hand-hint = Возьмите товар в руку, выберите человека рядом с консолью и назначьте цену. Он подтвердит у своей консоли, и вещь перейдёт к нему.
rumc-trade-price-placeholder = Цена
rumc-trade-offer = Предложить товар из руки
rumc-trade-offers = Предложения
rumc-trade-offers-none = Предложений нет.
rumc-trade-offer-line = { $seller } продаёт { $buyer }: { $item } за { $sum }
rumc-trade-accept = Купить за { $sum }
rumc-trade-auctions = Торги вещами
rumc-trade-auction-hint = Возьмите вещь в руку и выставите её на торги. Вещь остаётся в консоли, пока идут торги. Побеждает самая высокая ставка, ставка сразу удерживается со счёта. Если ставок нет, вещь вернётся к вам.
rumc-trade-auction-start-placeholder = Стартовая цена
rumc-trade-auction-start = Выставить вещь из руки на торги
rumc-trade-auctions-none = Торгов вещами нет.
rumc-trade-auction-line = { $item } (продавец { $seller })
rumc-trade-auction-cancel = Забрать вещь обратно
rumc-trade-stall = Торговый прилавок
rumc-trade-stall-hint = Прилавок — стол, который держит торговец с лицензией. Положите на него вещь (кликните по нему вещью в руке), назначьте цену в окне прилавка, и любой покупатель купит товар сразу.
rumc-trade-offer-refused = Не получилось: нужна вещь в руке, у вас уже есть предложение, либо очередь полна.
rumc-trade-offered = Предложение отправлено: { $name }.
rumc-trade-offered-to-you = { $name } предлагает вам купить вещь. Откройте консоль горожан, вкладку «Торговля».
rumc-trade-both-here = Обе стороны должны стоять у консоли.
rumc-trade-item-gone = Вещь уже не в руках у продавца.
rumc-trade-no-account = У обеих сторон должны быть банковские счета.
rumc-trade-no-money = На счёте не хватает денег: { $sum }.
rumc-trade-sold = Продано: { $item } за { $sum }.
rumc-trade-bought = Куплено: { $item } за { $sum }.
rumc-trade-auction-refused = Не получилось: нужна вещь в руке и банковский счёт, либо торги переполнены.
rumc-trade-own-auction = Нельзя ставить на свои торги.
rumc-trade-auction-won = Вы выиграли торги: { $item } за { $sum }.

# The market stall
ent-RUMCMarketStall = торговый прилавок
    .desc = Стол для товара торговца. Нужна торговая лицензия: кликните прилавок вещью в руке, чтобы положить её, и назначьте цену в окне прилавка. Покупатель видит цены и покупает сразу.
rumc-stall-title = Торговый прилавок
rumc-stall-free = Прилавок свободен
rumc-stall-owner = Торгует: { $owner }
rumc-stall-tax = Налог с продаж: { $value }% (платит продавец из выручки)
rumc-stall-owner-hint = Назначьте цену каждой вещи, не больше { $max } вещей на прилавке. Цена ноль значит «не продаётся».
rumc-stall-free-hint = Чтобы занять прилавок, нужна торговая лицензия: кликните его вещью в руке.
rumc-stall-empty = На прилавке ничего нет.
rumc-stall-price = Цена: { $sum }
rumc-stall-not-for-sale = Не продаётся
rumc-stall-price-placeholder = Цена
rumc-stall-set-price = Назначить цену
rumc-stall-take = Забрать вещь обратно
rumc-stall-buy = Купить за { $sum }
rumc-stall-release = Освободить прилавок
rumc-stall-examine-free = Прилавок свободен.
rumc-stall-examine-owner = Торгует: { $owner }. Вещей на прилавке: { $count }.
rumc-stall-not-yours = Прилавок занят: торгует { $owner }.
rumc-stall-need-license = Чтобы торговать, нужна торговая лицензия.
rumc-stall-full = На прилавке нет места.
rumc-stall-cannot = Не получилось положить вещь на прилавок.
rumc-stall-placed = На прилавок положено: { $item }. Назначьте цену в окне прилавка.
rumc-stall-cannot-pay = Не удалось заплатить: нужно { $sum } на счёте, и у продавца должен быть счёт.
rumc-stall-bought = Куплено: { $item } за { $sum }.
rumc-stall-sold = Продано: { $item } за { $sum }.
