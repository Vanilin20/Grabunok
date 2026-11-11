"""Перевірка 1: кожен непрозорий піксель усіх PNG в assets/ належить палітрі docs/art/palette.gpl.

Запуск з кореня репо: python tools/checks/check_palette.py
Код виходу 1, якщо хоч один піксель поза палітрою. Прозорість (альфа) не перевіряється — лише колір.
"""
import os
import sys

from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")


def load_palette() -> set:
	colors = set()
	with open(os.path.join(ROOT, "docs", "art", "palette.gpl"), encoding="utf-8") as f:
		for line in f:
			parts = line.split()
			if len(parts) >= 3 and all(p.isdigit() for p in parts[:3]):
				colors.add(tuple(int(p) for p in parts[:3]))
	return colors


def main() -> int:
	palette = load_palette()
	bad_files = 0
	checked = 0
	for base, _dirs, files in os.walk(os.path.join(ROOT, "assets")):
		for name in sorted(files):
			if not name.lower().endswith(".png") or name.endswith("_n.png"):  # *_n.png — карти нормалей, не колір
				continue
			path = os.path.join(base, name)
			img = Image.open(path).convert("RGBA")
			off = {}
			for r, g, b, a in img.get_flattened_data() if hasattr(img, "get_flattened_data") else img.getdata():
				if a > 0 and (r, g, b) not in palette:
					off[(r, g, b)] = off.get((r, g, b), 0) + 1
			checked += 1
			rel = os.path.relpath(path, ROOT)
			if off:
				bad_files += 1
				top = sorted(off.items(), key=lambda kv: -kv[1])[:3]
				print(f"FAIL {rel}: {sum(off.values())} px поза палітрою, напр. {top}")
	print(f"Палітра ({len(palette)} кольорів): перевірено {checked} PNG, з помилками {bad_files}")
	return 1 if bad_files else 0


if __name__ == "__main__":
	sys.exit(main())
