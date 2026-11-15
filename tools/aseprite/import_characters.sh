#!/usr/bin/env bash
# Нова генерація → .aseprite: шари з tools/art/assemble_walk.py → tools/aseprite/import_character.lua.
# УВАГА: перезаписує Assets/Art/Source/<id>.aseprite — ручні правки в ньому пропадуть. Запускати лише свідомо.
# Запуск із кореня репо: bash tools/aseprite/import_characters.sh [id ...]   (без аргументів — shadow guard)
set -euo pipefail
ASE="${ASEPRITE:-/d/SteamLibrary/steamapps/common/Aseprite/Aseprite.exe}"
TMP="tools/aseprite/_import"
ids=("$@")
[ ${#ids[@]} -eq 0 ] && ids=(shadow guard)
for id in "${ids[@]}"; do
  python -I tools/art/assemble_walk.py "$id" "$TMP"
  flash=""
  if [ -f "$TMP/${id}_flashlight.png" ]; then
    flash="$TMP/${id}_flashlight.png"
  fi
  "$ASE" -b \
    --script-param "body=$TMP/${id}_body.png" \
    --script-param "flash=$flash" \
    --script-param "palette=docs/art/palette.gpl" \
    --script-param "out=Assets/Art/Source/${id}.aseprite" \
    --script tools/aseprite/import_character.lua
done
