"""Експорт палітри: docs/art/palette.gpl (GIMP/Aseprite), docs/art/palette.png (зразки),
scripts/systems/palette.gd (константи для коду). Запуск: python tools/art/export_palette.py"""
import os
from PIL import Image, ImageDraw

import palette as pal

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")

with open(os.path.join(ROOT, "docs", "art", "palette.gpl"), "w", encoding="utf-8") as f:
	f.write("GIMP Palette\nName: Banda\nColumns: 8\n#\n")
	for (r, g, b), name in zip(pal.P, pal.NAMES):
		f.write(f"{r:3d} {g:3d} {b:3d}\t{name}\n")

# Зразки 8×4 по 16 px — зручно підвантажити як палітру в Aseprite.
img = Image.new("RGB", (8 * 16, 4 * 16))
d = ImageDraw.Draw(img)
for i, c in enumerate(pal.P):
	x, y = (i % 8) * 16, (i // 8) * 16
	d.rectangle([x, y, x + 15, y + 15], fill=c)
img.save(os.path.join(ROOT, "docs", "art", "palette.png"))

lines = [
	"class_name Palette",
	"extends RefCounted",
	"## Палітра гри — 32 кольори (docs/art/palette.gpl). Згенеровано tools/art/export_palette.py,",
	"## руками не правити: змінюємо tools/art/palette.py і перезапускаємо експорт.",
	"",
]
for (r, g, b), name, hexv in zip(pal.P, pal.NAMES, pal.HEX):
	lines.append(f'const {name.upper()} := Color("{hexv}")')
lines += [
	"",
	"# Ролі кольорів в інтерфейсі.",
	"const NIGHT := NIGHT_0",
	"const DEEP := NIGHT_1",
	"const DUSK := NIGHT_2",
	"const SLATE := NIGHT_4",
	"const STEEL := NIGHT_5",
	"const MUTED := STEEL_2 # другорядний текст: контраст 6:1 на NIGHT_1",
	"const TEXT := STEEL_3",
	"const LAMP := LAMP_3",
	"const NEON := NEON_2",
	"const CYAN := CYAN_2",
	"const DANGER := ALARM",
	"const SAFE := OK",
	"",
]
with open(os.path.join(ROOT, "scripts", "systems", "palette.gd"), "w", encoding="utf-8") as f:
	f.write("\n".join(lines))
print("ok")
