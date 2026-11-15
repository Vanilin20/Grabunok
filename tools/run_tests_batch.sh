#!/usr/bin/env bash
# EditMode-тести в batch-режимі НА ОКРЕМІЙ КОПІЇ проєкту — не конфліктує з відкритим редактором Unity
# (той тримає Temp/UnityLockfile у робочій теці). Копія: Assets, Packages, ProjectSettings, docs; Library лишається
# в копії між запусками, тож другий і подальші прогони швидкі.
# Запуск із кореня репо: bash tools/run_tests_batch.sh [фільтр_імені_тесту]
# Без -nographics: імпорт нормал-мап (Assets/Art/Menu/fg_n.png) у цьому режимі валить Unity «Stack overflow»; UNITY_NOGRAPHICS=1 повертає прапорець.
# Результат: Captures/tests_batch.xml, лог — $BATCH_DIR/unity.log. Код виходу = код Unity (0 — усе зелене).
set -uo pipefail
UNITY="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Unity.exe}"
SRC="$(pwd)"
BATCH_DIR="${BATCH_DIR:-${TEMP:-/tmp}/banda_batch}"
mkdir -p "$BATCH_DIR"
# robocopy: коди 0–7 — успіх.
for d in Assets Packages ProjectSettings docs; do
  robocopy "$(cygpath -w "$SRC/$d")" "$(cygpath -w "$BATCH_DIR/$d")" //MIR //NFL //NDL //NJH //NJS //NP > /dev/null
  rc=$?
  if [ $rc -ge 8 ]; then echo "robocopy $d: помилка $rc"; exit 1; fi
done
FILTER=()
[ $# -gt 0 ] && FILTER=(-testFilter "$1")
"$UNITY" -batchmode ${UNITY_NOGRAPHICS:+-nographics} -projectPath "$(cygpath -w "$BATCH_DIR")" \
  -runTests -testPlatform EditMode -testResults "$(cygpath -w "$SRC/Captures/tests_batch.xml")" \
  -logFile "$(cygpath -w "$BATCH_DIR/unity.log")" "${FILTER[@]}"
code=$?
echo "Unity завершився з кодом $code"
grep -E "error CS[0-9]+|warning CS[0-9]+" "$BATCH_DIR/unity.log" | sort -u | head -20
python -I - <<'PY'
import re
try:
    s=open("Captures/tests_batch.xml",encoding="utf-8").read()
    m=re.search(r'<test-run[^>]*total="(\d+)"[^>]*passed="(\d+)"[^>]*failed="(\d+)"',s)
    print("тести: всього %s, пройдено %s, провалено %s"%m.groups() if m else "результат не розібрано")
except FileNotFoundError:
    print("Captures/tests_batch.xml не створено")
PY
exit $code
