#!/usr/bin/env bash
# Мережеві тести Банди: три процеси збірки (host / shadow / safe) на справжньому NGO + UnityTransport, 127.0.0.1, без інтернету.
# Кожен бот друкує PASS/FAIL по перевірках (docs/network_options.md, «Як відтворити мережеві тести») і виходить з кодом 0/1.
# Запуск із кореня репо: bash tools/net_test.sh [-b]
#   -b  спершу зібрати плеєр (Banda → Мережа → Зібрати бота) через Unity у batch-режимі на копії проєкту
#       (потрібно ~3 ГБ вільної памʼяті; з відкритим редактором простіше зібрати його меню Banda/Мережа/Зібрати бота для тестів).
# Збірка: Builds/NetBot/Banda.exe. Логи: Captures/net_bots/{host,shadow,safe}.log. Код виходу 0 — усі три PASS.
set -uo pipefail
EXE="Builds/NetBot/Banda.exe"
PORT="${BOT_PORT:-7777}"
OUT="Captures/net_bots"
if [ "${1:-}" = "-b" ]; then
  UNITY="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Unity.exe}"
  BATCH_DIR="${BATCH_DIR:-${TEMP:-/tmp}/banda_batch}"
  for d in Assets Packages ProjectSettings docs; do
    robocopy "$(cygpath -w "$(pwd)/$d")" "$(cygpath -w "$BATCH_DIR/$d")" //MIR //NFL //NDL //NJH //NJS //NP > /dev/null
    [ $? -ge 8 ] && { echo "robocopy $d: помилка"; exit 1; }
  done
  "$UNITY" -batchmode -projectPath "$(cygpath -w "$BATCH_DIR")" -executeMethod Banda.EditorTools.NetBotBuild.Build \
    -logFile "$(cygpath -w "$BATCH_DIR/build.log")" -quit
  [ $? -ne 0 ] && { echo "збірка не вдалась, див. $BATCH_DIR/build.log"; exit 1; }
  rm -rf Builds/NetBot && mkdir -p Builds && cp -r "$BATCH_DIR/Builds/NetBot" Builds/NetBot
fi
[ -f "$EXE" ] || { echo "Немає $EXE: зберіть плеєр (bash tools/net_test.sh -b або меню Banda/Мережа/Зібрати бота для тестів)"; exit 1; }
mkdir -p "$OUT"; rm -f "$OUT"/*.log
run_bot() { "$EXE" -batchmode -nographics -bot "$1" -botport "$PORT" -logFile "$(cygpath -w "$(pwd)/$OUT/$1.log")" > /dev/null 2>&1; echo $? > "$OUT/$1.code"; }
rm -f "$OUT"/*.code
run_bot host & H=$!
sleep 3
run_bot shadow & S=$!
run_bot safe & F=$!
wait $H $S $F
fail=0
for r in host shadow safe; do
  code=$(cat "$OUT/$r.code" 2>/dev/null || echo 99)
  echo "=== $r (код $code) ==="
  grep -a "\[bot:" "$OUT/$r.log" | sed 's/^.*\[bot:/[bot:/' | awk '!seen[$0]++'
  [ "$code" != "0" ] && fail=1
done
[ $fail = 0 ] && echo "МЕРЕЖА 127.0.0.1: PASS" || echo "МЕРЕЖА 127.0.0.1: FAIL"
exit $fail
