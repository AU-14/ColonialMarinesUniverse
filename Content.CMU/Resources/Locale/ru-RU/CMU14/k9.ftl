# Сущности K9
ent-AU14MobK9 = Синтетическая собака К9
  .desc = Синтетическая собака производства «Интердайн». Стандартная модель без специального снаряжения

ent-AU14MobK9GhostRole = Синтетическая собака К9
  .desc = Синтетическая собака производства «Интердайн». Стандартная модель без специального снаряжения
  .suffix = Ghost Role

ent-AU14MobK9GhostRoleGOVFOR = Синтетическая собака К9
  .desc = Синтетическая собака производства «Интердайн». Стандартная модель без специального снаряжения
  .suffix = GOVFOR, Ghost Role

ent-AU14MobK9MP = К9 военной полиции
  .desc = Синтетическая собака производства «Интердайн» в шлейке военной полиции.

ent-AU14MobK9MPGhostRole = К9 военной полиции
  .desc = Синтетическая собака производства «Интердайн» в шлейке военной полиции.
  .suffix = MP, Ghost Role

ent-AU14MobK9MPGhostRoleGOVFOR = К9 военной полиции
  .desc = Синтетическая собака производства «Интердайн» в шлейке военной полиции.
  .suffix = MP, GOVFOR

ent-AU14MobK9BagMedic = К9 спасательная
  .desc = Синтетическая собака производства «Интердайн» в шлейке для переноса медицинских средств

ent-AU14MobK9BagMedicGhostRole = К9 спасательная
  .desc = Синтетическая собака производства «Интердайн» в шлейке для переноса медицинских средств
  .suffix = S&R, Ghost Role

ent-AU14MobK9BagMedicGhostRoleGOVFOR = К9 спасательная
  .desc = Синтетическая собака производства «Интердайн» в шлейке для переноса медицинских средств
  .suffix = S&R, GOVFOR

# Действия K9
ent-RMCActionK9ArmGrab = Хват за руку
  .desc = Вцепиться челюстями в руку цели, удерживая и замедляя её. Повторное применение сбивает цель с ног.

ent-RMCActionK9TrackMaster = Поиск хозяина
  .desc = Вычислить направление и дистанцию до вашего хозяина по его биосигналу.

ent-RMCActionK9RequestMaster = Запрос привязки
  .desc = Запросить у находящегося рядом морпеха привязку: доступы и поиск. Команды кинолога при этом не выдаются.

# Действия Кинолога
ent-RMCActionK9Tame = Приручить (Авторизация)
  .desc = Отправить служебному K9 запрос на регистрацию вас в качестве его кинолога.

ent-RMCActionK9SicEm = Фас!
  .desc = Указать цель для атаки. Все привязанные собаки получают бонус к урону против этой цели.

ent-RMCActionK9Evacuate = Унести!
  .desc = Приказать собакам эвакуировать раненого бойца. Собаки смогут тащить его без штрафа к скорости и с ускорением.

ent-RMCActionK9GoodBoy = Хороший мальчик
  .desc = Погладить и подбодрить верного синтетического напарника.

rmc-action-k9-arm-grab = Хват за руку
rmc-action-k9-arm-grab-desc = Вцепиться челюстями в руку цели, удерживая и замедляя её. Повторное применение сбивает цель с ног.

rmc-action-k9-track-master = Поиск хозяина
rmc-action-k9-track-master-desc = Вычислить направление и дистанцию до вашего хозяина по его биосигналу.

rmc-action-k9-request-master = Запрос привязки
rmc-action-k9-request-master-desc = Запросить у находящегося рядом морпеха привязку: доступы и поиск. Команды кинолога при этом не выдаются.

rmc-action-k9-tame = Приручить (Авторизация)
rmc-action-k9-tame-desc = Отправить служебному K9 запрос на регистрацию вас в качестве его кинолога.

rmc-action-k9-sic-em = Фас!
rmc-action-k9-sic-em-desc = Указать цель для атаки. Все привязанные собаки получают бонус к урону против этой цели.

rmc-action-k9-evacuate = Унести!
rmc-action-k9-evacuate-desc = Приказать собакам эвакуировать раненого бойца. Собаки смогут тащить его без штрафа к скорости и с ускорением.

rmc-action-k9-good-boy = Хороший мальчик
rmc-action-k9-good-boy-desc = Погладить и подбодрить верного синтетического напарника.

# Сообщения Хвата
rmc-k9-arm-grab-out-of-range = Цель находится слишком далеко!
rmc-k9-arm-grab-success-self = Вы мёртвой хваткой вцепляетесь в руку {$target}!
rmc-k9-arm-grab-success-target = {$dog} вцепляется челюстями в вашу руку! Сопротивляйтесь, чтобы вырваться!
rmc-k9-arm-grab-success-others = {$dog} вцепляется челюстями в руку {$target}!
rmc-k9-arm-grab-trip-self = Вы резко дёргаете руку {$target}, сбивая цель с ног!
rmc-k9-arm-grab-trip-target = {$dog} резко дёргает вас за руку и сбивает с ног!
rmc-k9-arm-grab-trip-others = {$dog} дёргает {$target} за руку и сбивает с ног!
rmc-k9-arm-grab-escape-attempt = Вы пытаетесь вырвать руку из стальных челюстей собаки...
rmc-k9-arm-grab-escaped-self = Вам удаётся вырвать руку из пасти собаки!
rmc-k9-arm-grab-escaped-dog = {$target} вырывает руку из вашей пасти!
rmc-k9-arm-grab-broken-damage = От сильного урона собака разжимает челюсти!
rmc-k9-arm-grab-xeno = Челюсти не могут надёжно зафиксировать ксеноморфа!

# Поиск хозяина
rmc-k9-track-master-none = У вас нет назначенного хозяина. Подойдите к морпеху и используйте «Запрос привязки».
rmc-k9-track-master-lost = Сенсоры не могут уловить сигнал вашего хозяина.
rmc-k9-track-master-result = Сенсоры пеленгуют хозяина: направление — {$direction}, дистанция ~{$distance}м.
rmc-k9-request-master-already-bound = У вас уже есть хозяин!
rmc-k9-request-master-not-marine = Привязать можно только к морпеху.
rmc-k9-request-master-title = Запрос K9
rmc-k9-request-master-prompt = Служебный синтетик {$dog} запрашивает привязку к вам. Вы получите статус хозяина: собака возьмёт ваши доступы и сможет вас найти. Команды кинолога не выдаются. Принять?
rmc-k9-request-master-sent = Запрос отправлен бойцу {$target}.

# Приручение
rmc-k9-tame-not-dog = Эта цель не является служебным K9!
rmc-k9-tame-already-bound = Этот K9 уже привязан к кинологу.
rmc-k9-tame-dialog-title = Назначение кинолога
rmc-k9-tame-dialog-prompt = Боец {$handler} запрашивает авторизацию в качестве вашего кинолога. Текущая привязка к морпеху будет снята. Подтвердить протокол?
rmc-k9-tame-request-sent = Запрос на синхронизацию отправлен K9 {$dog}.

# Связь
rmc-k9-bind-success-master = Протокол синхронизирован! Вы стали кинологом {$dog}.
rmc-k9-bind-success-dog = Протокол синхронизирован! Боец {$master} назначен вашим кинологом.
rmc-k9-bind-success-handler = Протокол кинолога синхронизирован! Вы стали кинологом {$dog}. Команды «Фас» и «Унести» активны.
rmc-k9-bind-success-dog-handler = Протокол синхронизирован! Боец {$master} назначен вашим кинологом.
rmc-k9-bind-success-marine = Привязка установлена. {$dog} получил ваши доступы и может вас найти. Команды кинолога недоступны.
rmc-k9-bind-success-dog-marine = Привязка установлена. Боец {$master} назначен вашим хозяином. Доступы синхронизированы.
rmc-k9-bind-replaced-old = Привязка с {$dog} разорвана: собака перешла к кинологу {$master}.

# Протокол защиты
rmc-k9-protector-rage-trigger = [color=red]{$dog} фиксирует угрозу кинологу и активирует боевой защитный протокол![/color]

# Фас
rmc-k9-sic-em-shout = Кинолог {$handler} указывает цель: «ФАС на {$target}!»
rmc-k9-sic-em-dog-order = [color=orange]Приказ кинолога: АТАКОВАТЬ {$target}![/color]

# Эвакуация
rmc-k9-evacuate-shout = Кинолог {$handler} командует собакам: «УНЕСТИ {$target}!»
rmc-k9-evacuate-dog-order = [color=cyan]Приказ кинолога: ЭВАКУИРОВАТЬ {$target}![/color]

# Хороший мальчик / Подзарядка
rmc-k9-good-boy-popup = {$user} подбадривает {$dog}. {$dog} радостно виляет металлическим хвостом и издаёт довольный бип-буп!

# Сенсоры
rmc-k9-senses-alert-growl = [color=red]{$dog} фиксирует вибрацию сенсорами движения и глухо рычит в темноту![/color]
rmc-k9-senses-alert-master = [color=red]Сенсоры вашего K9 {$dog} обнаружили движение противника поблизости![/color]

# Направления
rmc-k9-direction-north = север
rmc-k9-direction-northeast = северо-восток
rmc-k9-direction-east = восток
rmc-k9-direction-southeast = юго-восток
rmc-k9-direction-south = юг
rmc-k9-direction-southwest = юго-запад
rmc-k9-direction-west = запад
rmc-k9-direction-northwest = северо-запад
