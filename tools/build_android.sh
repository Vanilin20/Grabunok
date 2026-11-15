#!/usr/bin/env bash
# Збірка Android APK в batch-режимі НА КОПІЇ проєкту (Unity-редактор має бути закритий або хоча б не тримати проєкт).
# Копія лежить на D: (Library для Android велика, перше імпортування довге — 10–30 хв, далі швидко).
# Запуск із кореня репо: bash tools/build_android.sh
# Результат: Builds/Android/Banda.apk, лог — $BATCH_DIR/android_build.log.
set -uo pipefail
UNITY="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Unity.exe}"
SRC="$(pwd)"
BATCH_DIR="${ANDROID_BATCH_DIR:-/d/Temp/banda_android}"
mkdir -p "$BATCH_DIR"
for d in Assets Packages ProjectSettings docs; do
  robocopy "$(cygpath -w "$SRC/$d")" "$(cygpath -w "$BATCH_DIR/$d")" //MIR //NFL //NDL //NJH //NJS //NP > /dev/null
  [ $? -ge 8 ] && { echo "robocopy $d: помилка"; exit 1; }
done
"$UNITY" -batchmode -projectPath "$(cygpath -w "$BATCH_DIR")" -buildTarget Android \
  -executeMethod Banda.EditorTools.AndroidBuild.Build -logFile "$(cygpath -w "$BATCH_DIR/android_build.log")" -quit
code=$?
echo "Unity завершився з кодом $code"
grep -a "error CS[0-9]*\|AndroidBuild:\|BuildFailedException\|FAILED\|Exception:" "$BATCH_DIR/android_build.log" | sort -u | head -15
if [ -f "$BATCH_DIR/Builds/Android/Banda.apk" ]; then
  mkdir -p Builds/Android && cp "$BATCH_DIR/Builds/Android/Banda.apk" Builds/Android/Banda.apk
  ls -la Builds/Android/Banda.apk
fi
exit $code
