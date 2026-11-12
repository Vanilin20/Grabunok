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
SPRITES = {"assets/sprites/menu/gang_leader.png": 72, "assets/sprites/menu/gang_hacker.png": 67, "assets/sprites/menu/gang_runner.png": 48}
MIN_HEIGHT = 16


def topdown_heights(rel: str) -> list[int]:
	"""Вид зверху: аркуш 12×4 кадри по 24×32 — висота фігури в кожному кадрі."""
	img = Image.open(os.path.join(ROOT, rel)).convert("RGBA")
	hs = []
	for r in range(4):
		for c in range(12):
			box = img.crop((c * 24, r * 32, (c + 1) * 24, (r + 1) * 32)).getbbox()
			hs.append(box[3] - box[1] if box else 0)
	return hs


def main() -> int:
	bad = 0
	for rel in ("assets/sprites/chars/shadow.png", "assets/sprites/chars/guard.png"):
		if os.path.exists(os.path.join(ROOT, rel)):
			hs = topdown_heights(rel)
			ok = min(hs) >= MIN_HEIGHT
			bad += 0 if ok else 1
			print(f"[sizes] {'OK  ' if ok else 'FAIL'} {rel}: висота {min(hs)}–{max(hs)} px (мін. {MIN_HEIGHT}), кадрів 48 (4 напрямки × idle 4, ходьба 6, тривога 2)")
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
