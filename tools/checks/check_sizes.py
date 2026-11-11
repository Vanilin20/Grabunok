"""Перевірка 6: розміри персонажів у базовій роздільності й силуети одним кольором.

Запуск: python tools/checks/check_sizes.py
Вимога: Тінь і охоронець ≥ 16 px заввишки (етап 3); для меню — кожен член банди ≥ 16 px.
Силуети (персонаж залитий одним кольором) лежать у tools/art/silhouettes/ — їх переглядаємо очима.
"""
import glob
import os
import sys

from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
SPRITES = {"assets/sprites/menu/gang_leader.png": 48, "assets/sprites/menu/gang_hacker.png": 48, "assets/sprites/menu/gang_runner.png": 48}
MIN_HEIGHT = 16


def main() -> int:
	bad = 0
	for rel, frame_w in SPRITES.items():
		img = Image.open(os.path.join(ROOT, rel)).convert("RGBA")
		heights = []
		for f in range(img.width // frame_w):
			box = img.crop((f * frame_w, 0, (f + 1) * frame_w, img.height)).getbbox()
			heights.append(box[3] - box[1] if box else 0)
		h = min(heights)
		ok = h >= MIN_HEIGHT
		bad += 0 if ok else 1
		print(f"[sizes] {'OK  ' if ok else 'FAIL'} {rel}: висота {min(heights)}–{max(heights)} px (мін. {MIN_HEIGHT})")
	sil = sorted(glob.glob(os.path.join(ROOT, "tools", "art", "silhouettes", "*.png")))
	print(f"[sizes] силуетів для перегляду: {len(sil)} у tools/art/silhouettes/")
	return 1 if bad else 0


if __name__ == "__main__":
	sys.exit(main())
