ent-CMUBasketballHoopLeft = левое баскетбольное кольцо
    .desc = Приносит очки левой команде, когда мяч проходит сквозь него. Подключите его к табло мультитулом.
ent-CMUBasketballHoopRight = правое баскетбольное кольцо
    .desc = Приносит очки правой команде, когда мяч проходит сквозь него. Подключите его к табло мультитулом.
ent-CMUBasketballScoreboard = баскетбольное табло
    .desc = Показывает счёт подключённых к нему колец. ПКМ — назвать команды или выбрать счёт для победы.
ent-CMUBasketballReset = кнопка сброса табло
    .desc = Обнуляет подключённые к ней табло и начинает новую игру.

# Названия команд по умолчанию. Название игрока всегда стоит в кавычках после слова «команда»,
# поэтому фразы ниже согласуются с любым названием.
cmu-basketball-side-left = Левые
cmu-basketball-side-right = Правые

cmu-basketball-shot = Команда «{$team}» забивает {$points} { $points ->
    [one] очко
    [few] очка
    [many] очков
   *[other] очков
}!
cmu-basketball-dunk = Слэм-данк! Команда «{$team}» получает {$points} { $points ->
    [one] очко
    [few] очка
    [many] очков
   *[other] очков
}!
cmu-basketball-win = Команда «{$team}» побеждает со счётом {$score}:{$otherScore}!
cmu-basketball-miss = Мяч отскакивает от кольца!

cmu-basketball-reset = Счёт обнулён.
cmu-basketball-reset-cooldown = Кнопка сброса перезаряжается.
cmu-basketball-reset-unlinked = Кнопка сброса не подключена к табло.

# {$side} — "left" или "right".
cmu-basketball-verb-name = Назвать { $side ->
    [left] левую
   *[right] правую
} команду
cmu-basketball-verb-target = Задать счёт для победы
cmu-basketball-locked = Идёт игра. Нажмите кнопку сброса, чтобы сменить команды или счёт для победы.

cmu-basketball-name-title = Название команды
cmu-basketball-name-prompt = Введите название { $side ->
    [left] левой
   *[right] правой
} команды. Оставьте поле пустым, чтобы вернуть название по умолчанию.
cmu-basketball-name-set = { $side ->
    [left] Левая
   *[right] Правая
} команда теперь называется «{$name}».

cmu-basketball-target-title = Счёт для победы
cmu-basketball-target-score = До {$score} { $score ->
    [one] очка
   *[other] очков
}
cmu-basketball-target-none = Без ограничения
cmu-basketball-target-set = Счёт для победы: {$target}.

cmu-basketball-scoreboard-examine = [color=yellow]{$left}[/color] {$leftScore} - {$rightScore} [color=yellow]{$right}[/color]
cmu-basketball-scoreboard-examine-target = Счёт для победы: {$target}.
cmu-basketball-scoreboard-examine-winner = [color=green]Победила команда «{$team}»![/color] Нажмите кнопку сброса, чтобы сыграть снова.

cmu-signal-port-name-basketball-score = Счёт
cmu-signal-port-description-basketball-score-source = Передаёт заброшенные мячи этого кольца на табло.
cmu-signal-port-description-basketball-score-sink = Добавляет очки подключённого кольца его команде.
cmu-signal-port-name-basketball-reset = Сброс
cmu-signal-port-description-basketball-reset-source = Обнуляет подключённые табло при нажатии.
cmu-signal-port-description-basketball-reset-sink = Обнуляет счёт обеих команд.
