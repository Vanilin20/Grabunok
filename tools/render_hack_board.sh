#!/usr/bin/env bash
# Рендер вікна головоломки Хакера в PNG (піксель-арт без текстів) на копії проєкту — не конфліктує з відкритим редактором.
# Запуск із кореня репо: bash tools/render_hack_board.sh   Результат: Captures/hack_board/*.png (3× збільшення, Nearest).
set -uo pipefail
UNITY="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Unity.exe}"
SRC="$(pwd)"
BATCH_DIR="${BATCH_DIR:-/d/Temp/banda_batch}"
mkdir -p "$BATCH_DIR"
for d in Assets Packages ProjectSettings docs; do
  robocopy "$(cygpath -w "$SRC/$d")" "$(cygpath -w "$BATCH_DIR/$d")" //MIR //NFL //NDL //NJH //NJS //NP > /dev/null
  [ $? -ge 8 ] && { echo "robocopy $d: помилка"; exit 1; }
done
"$UNITY" -batchmode -projectPath "$(cygpath -w "$BATCH_DIR")" -executeMethod Banda.EditorTools.HackBoardRender.RenderAll \
  -logFile "$(cygpath -w "$BATCH_DIR/render.log")" -quit
code=$?
grep -E "error CS[0-9]+" "$BATCH_DIR/render.log" | sort -u | head
mkdir -p Captures/hack_board && cp -f "$BATCH_DIR"/Captures/hack_board/*.png Captures/hack_board/ 2>/dev/null
echo "Unity: код $code"; ls Captures/hack_board
exit $code
