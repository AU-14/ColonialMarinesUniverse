cmu-atm-card-slot-occupied = В банкомате уже есть карта.
cmu-atm-no-card = У вас нет ID-карты, которую можно вставить.
cmu-atm-take-card-verb = Забрать карту
cmu-atm-take-cash-verb = Забрать наличные
cmu-atm-take-card-start = Вы начинаете вытаскивать карту из банкомата...
cmu-atm-take-card-start-others = {CAPITALIZE(THE($user))} начинает вытаскивать карту из банкомата!
cmu-atm-boot-title = КОЛОНИАЛЬНЫЕ ФИНАНСОВЫЕ СИСТЕМЫ W-Y
cmu-atm-boot-bios = BIOS 2.7 (C) 2179 W-Y CORP.
cmu-atm-boot-memory = ПАМЯТЬ 640K
cmu-atm-boot-keypad = КЛАВИАТУРА
cmu-atm-boot-reader = СЧИТЫВАТЕЛЬ КАРТ
cmu-atm-boot-dispenser = ВЫДАЧА НАЛИЧНЫХ
cmu-atm-boot-uplink = СВЯЗЬ С КАЗНАЧЕЙСТВОМ ООН
cmu-atm-boot-ok = ОК
cmu-atm-boot-loading = ЗАГРУЗКА ТЕРМИНАЛА...
cmu-atm-out-of-order = НЕ РАБОТАЕТ
cmu-atm-out-of-order-sorry = ВОСПОЛЬЗУЙТЕСЬ ДРУГИМ БАНКОМАТОМ
cmu-atm-hint-confirm = OK = подтвердить   X = назад
cmu-atm-hint-continue = Нажмите OK для продолжения.
cmu-atm-nav-title = Банкомат колонии
cmu-atm-nav-pin = Ваша карта №{ $account } — PIN { $pin }
cmu-atm-nav-no-card = У вас нет собственной карты
cmu-atm-nav-pin-unknown = Чтение вашей карты...
cmu-atm-nav-pop-out = Отдельное окно

# ATM screens and transaction messages
cmu-atm-busy = Кто-то уже использует этот банкомат.
cmu-atm-tampered = Внимание: обнаружены следы вмешательства в электронику.
cmu-atm-normal = Банкомат работает исправно.
cmu-atm-unknown = Неизвестно
cmu-atm-invalid-amount = Введите корректную сумму.
cmu-atm-insufficient-funds = Недостаточно средств.
cmu-atm-insufficient-hand = Недостаточно наличных в руках.
cmu-atm-invalid-account = Введите корректный пятизначный номер счёта.
cmu-atm-account-missing = Счёт не найден.
cmu-atm-insufficient-cash = Недостаточно наличных.
cmu-atm-own-account = Нельзя перевести деньги на собственный счёт.
cmu-atm-timeout = Время сеанса истекло.
cmu-atm-take-cash-first = Сначала заберите наличные.
cmu-atm-invalid-pin = Неверный формат PIN. Введите все { $digits } цифры.
cmu-atm-incorrect-pin = Неверный PIN. Попытка { $attempt }/{ $maximum }.
cmu-atm-withdraw-confirm = Снять ${ $amount }? После налога вы получите ${ $net }.
cmu-atm-dispensed = Выдано ${ $amount }. Баланс: ${ $balance }.
cmu-atm-deposited = Внесено ${ $amount }. Баланс: ${ $balance }.
cmu-atm-recipient = Получатель: { $name }. Введите сумму:
cmu-atm-remote-confirm = Внести ${ $amount } на счёт №{ $account }?
cmu-atm-remote-done = Внесено ${ $amount } на счёт №{ $account }.
cmu-atm-transfer-confirm = Перевести ${ $amount } на счёт №{ $account }?
cmu-atm-transfer-done = Переведено ${ $amount }. Баланс: ${ $balance }.
cmu-atm-welcome =
    ФИНАНСОВЫЙ ТЕРМИНАЛ КОЛОНИИ
    v2.7  КАЗНА ООН
    { "" }
    1) ВНЕСЕНИЕ НА ЧУЖОЙ СЧЁТ
    { "" }
    Вставьте ID-карту для доступа к счёту.
cmu-atm-pin-prompt = ВВЕДИТЕ PIN:
cmu-atm-locked =
    { "** КАРТА ЗАБЛОКИРОВАНА **" }
    Слишком много неверных попыток. Попробуйте позже.
cmu-atm-main-menu =
    Здравствуйте, { $name }.
    { "" }
    1) СНЯТИЕ
    2) ВНЕСЕНИЕ
    3) ПЕРЕВОД
    4) ВНЕСЕНИЕ НА ЧУЖОЙ СЧЁТ
    5) ИСТОРИЯ
    6) ВЫХОД
cmu-atm-withdraw-prompt =
    СНЯТИЕ
    Введите сумму:
cmu-atm-deposit-prompt =
    ВНЕСЕНИЕ
    Введите сумму:
cmu-atm-remote-prompt =
    ВНЕСЕНИЕ НА ЧУЖОЙ СЧЁТ
    Номер счёта получателя:
cmu-atm-transfer-prompt =
    ПЕРЕВОД
    Номер счёта получателя:
cmu-atm-history-title = ИСТОРИЯ СЧЁТА
cmu-atm-history-empty = Операций пока нет.
cmu-atm-history-back = OK = назад
cmu-atm-history-withdrawal = -${ $amount } СНЯТИЕ
cmu-atm-history-deposit = +${ $amount } ВНЕСЕНИЕ
cmu-atm-history-cash = +${ $amount } НАЛИЧНЫЕ
cmu-atm-history-out = -${ $amount } НА №{ $account }
cmu-atm-history-in = +${ $amount } ОТ №{ $account }
cmu-atm-history-returned = +${ $amount } ВОЗВРАТ
cmu-atm-history-purchase = -${ $amount } ПОКУПКА
cmu-atm-fault-fatal = СБОЙ { $byte }: ОШИБКА CRC РЕЕСТРА
cmu-atm-fault-segv = SEGV ПО АДРЕСУ { $address } В TXN_CORE
cmu-atm-fault-halted = TXN_CORE ОСТАНОВЛЕНО
cmu-atm-fault-watchdog = СБРОС WATCHDOG ... НЕ УДАЛСЯ
cmu-atm-fault-uplink = СВЯЗЬ ПОТЕРЯНА, ПОВТОР { $attempt }/3
cmu-atm-fault-dispenser = ОШИБКА ШИНЫ ВЫДАЧИ { $byte }
cmu-atm-fault-auth = ТАБЛИЦА ДОСТУПА ПОВРЕЖДЕНА: { $address }
cmu-atm-fault-kernel = ПАНИКА ЯДРА: НЕТ СИНХРОНИЗАЦИИ
cmu-atm-fault-stack = СТЕК ПОВРЕЖДЁН, ПРЕРЫВАНИЕ
cmu-atm-fault-trap = НЕОБРАБОТАННОЕ ИСКЛЮЧЕНИЕ { $byte }: { $address }
cmu-atm-fault-garbled = ?? ???? ?????? ??? ??

# RUMC: налог на посылки, проданные через торговые платформы
admin-console-trade-tax-section = -- Налог на продажу посылок --
admin-console-current-trade-tax = Текущий налог на продажу посылок: { $percent }%
admin-console-trade-tax-description = Берётся из суммы, которую дрон платит за посылку, проданную на торговой платформе. Эта доля уходит в бюджет колонии, остальное получает продавец.
admin-console-trade-tax-announcement = Налог колонии на продажу посылок установлен в { $percent }%. Эта доля от каждой посылки, проданной на торговой платформе, уходит колонии.
