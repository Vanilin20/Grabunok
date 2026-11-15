#!/usr/bin/env bash
# Експорт персонажів із Assets/Art/Source/<id>.aseprite у стрічки Unity
# (Assets/Art/Characters/<id>_sheet.png, <id>_halo.png).
# Запуск із кореня репо: bash tools/aseprite/export_characters.sh [id ...]   (без аргументів — shadow guard)
set -euo pipefail
ASE="${ASEPRITE:-/d/SteamLibrary/steamapps/common/Aseprite/Aseprite.exe}"
ids=("$@")
[ ${#ids[@]} -eq 0 ] && ids=(shadow guard)
for id in "${ids[@]}"; do
  "$ASE" -b \
    --script-param "src=Assets/Art/Source/${id}.aseprite" \
    --script-param "sheet=Assets/Art/Characters/${id}_sheet.png" \
    --script-param "halo=Assets/Art/Characters/${id}_halo.png" \
    --script tools/aseprite/export_character.lua
done
