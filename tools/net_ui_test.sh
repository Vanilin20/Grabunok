#!/usr/bin/env bash
# Перевірка інтерфейсу мережі на справжній збірці (Builds/NetBot/Banda.exe): два вікна гри, кероване автоводієм UiDrive
# (хост-Хакер і клієнт-Тінь) + бот-Зломщик. Проходить меню → вибір кімнати → лобі → справа → обрив і повернення → хост вийшов,
# знімає скріншоти в Captures/net_ui/ і виводить OK/ПРОБЛЕМА по кроках. Wi-Fi-режим на 127.0.0.1, без інтернету.
# Запуск із кореня репо після збірки плеєра (Banda → Мережа → Зібрати бота для тестів): bash tools/net_ui_test.sh
set -uo pipefail
EXE="Builds/NetBot/Banda.exe"
OUT="Captures/net_ui"
[ -f "$EXE" ] || { echo "Немає $EXE"; exit 1; }
mkdir -p "$OUT"; rm -f "$OUT"/*.png "$OUT"/*.log "$OUT"/*.code
W=(-screen-fullscreen 0 -screen-width 960 -screen-height 540 -popupwindow)
run() { local name="$1"; shift; "$EXE" "$@" -logFile "$(cygpath -w "$(pwd)/$OUT/$name.log")" > /dev/null 2>&1; echo $? > "$OUT/$name.code"; }
run host "${W[@]}" -uidrive host -shotdir "$(cygpath -w "$(pwd)/$OUT")" & H=$!
sleep 4
run safe -batchmode -nographics -bot safe -botcode 4242 & S=$!
run client "${W[@]}" -uidrive client -shotdir "$(cygpath -w "$(pwd)/$OUT")" & C=$!
wait $H $S $C
fail=0
for r in host client safe; do
  code=$(cat "$OUT/$r.code" 2>/dev/null || echo 99)
  echo "=== $r (код $code) ==="
  grep -a "\[uidrive:\|\[bot:" "$OUT/$r.log" | sed 's/^.*\[\(uidrive\|bot\)/[\1/' | awk '!seen[$0]++'
  [ "$code" != "0" ] && fail=1
done
ls "$OUT"/*.png 2>/dev/null | sed 's|.*/||'
[ $fail = 0 ] && echo "ІНТЕРФЕЙС МЕРЕЖІ: OK" || echo "ІНТЕРФЕЙС МЕРЕЖІ: є проблеми"
exit $fail
