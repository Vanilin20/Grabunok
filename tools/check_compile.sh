#!/usr/bin/env bash
# Перевірка компіляції: шукає помилки й попередження C# в останній компіляції Unity (Editor.log).
# Запуск: bash tools/check_compile.sh   (після refresh/компіляції в редакторі)
# Код виходу 0 — чисто, 1 — є помилки або попередження.
LOG="${LOCALAPPDATA}/Unity/Editor/Editor.log"
if [ ! -f "$LOG" ]; then
  echo "Немає $LOG — редактор Unity не запускався?"
  exit 1
fi
# Беремо лише частину логу після останнього запиту компіляції.
START=$(grep -n "Requested script compilation\|Starting: .*bee_backend\|- Starting script compilation" "$LOG" | tail -1 | cut -d: -f1)
START=${START:-1}
ISSUES=$(tail -n +"$START" "$LOG" | grep -E "Assets/.*\.cs\([0-9]+,[0-9]+\): (error|warning) CS[0-9]+" | sort -u)
if [ -n "$ISSUES" ]; then
  echo "$ISSUES"
  echo "КОМПІЛЯЦІЯ: є проблеми ($(echo "$ISSUES" | wc -l))"
  exit 1
fi
echo "КОМПІЛЯЦІЯ: 0 помилок, 0 попереджень"
exit 0
