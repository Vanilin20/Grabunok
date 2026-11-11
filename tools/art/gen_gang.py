"""Банда на даху (вид зі спини) — три персонажі з idle-анімацією по 6 кадрів.

Запуск: python tools/art/gen_gang.py → assets/sprites/menu/gang_*.png (кадри в ряд).

Персонаж будується з «міток» (тіло, волосся, кепка, шкіра, ноги, взуття), а потім
автоматично затінюється за правилами стайлгайду:
  - контур NIGHT_0, щоб силует читався на будь-якому фоні;
  - ключове світло зліва-згори: лівий край тіла — холодний відсвіт (NIGHT_6/7);
  - контржур від неонової вивіски попереду: верхні краї голови й плечей — рожеві;
  - складки й шви — на тон темніше основи (без «подушкового» затінення від центру).
Анімація: дихання (плечі й голова на 1 px вгору), вторинний рух — поли плаща, хвіст, капюшон.
"""
from __future__ import annotations

import os

from PIL import Image

import palette as pal

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "sprites", "menu")
FRAMES = 6
BREATH = [0, 0, 1, 1, 1, 0] # зсув верхньої частини тіла вгору
FLUTTER = [0, 1, 2, 3, 2, 1] # поли плаща на вітрі
SWAY = [0, 1, 1, 0, -1, -1] # хвіст / капюшон

# Мітки → базовий колір.
BASE = {"r": 10, "b": 3, "j": 4, "L": 2, "k": 1, "c": 4, "h": 9, "s": 10, "t": 5, "p": 28}


def blank(w: int, h: int) -> list[list[str]]:
	return [["." for _ in range(w)] for _ in range(h)]


def span(g, y: int, x0: int, x1: int, label: str) -> None:
	if 0 <= y < len(g):
		for x in range(max(0, x0), min(len(g[0]), x1 + 1)):
			g[y][x] = label


def shade(g, seams: list[tuple[int, int]], dark_seams: list[tuple[int, int]] = (), rim_rows: int = 14) -> Image.Image:
	h, w = len(g), len(g[0])
	top_y = min(y for y in range(h) if any(c != "." for c in g[y]))
	img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
	px = img.load()

	def filled(x, y):
		return 0 <= x < w and 0 <= y < h and g[y][x] != "."

	for y in range(h):
		for x in range(w):
			lab = g[y][x]
			if lab == ".":
				continue
			c = BASE[lab]
			if lab == "p":
				pass # екран телефона — світиться сам
			elif not filled(x, y - 1) and lab in "bjchstr" and y - top_y < rim_rows:
				c = 23 # контржур неону згори
			elif not filled(x - 1, y):
				c = 7 if lab in "bjct" else 6 # ключове світло зліва
			elif not filled(x - 2, y) and lab in "bjt":
				c = 5
			elif not filled(x + 1, y) and lab in "bjLt":
				c = 1 # тіньовий бік
			px[x, y] = pal.P[c] + (255,)
	for x, y in seams:
		if filled(x, y) and px[x, y][:3] == pal.P[BASE[g[y][x]]]:
			px[x, y] = pal.P[2 if g[y][x] in "bjt" else 1] + (255,)
	for x, y in dark_seams:
		if filled(x, y):
			px[x, y] = pal.P[1] + (255,)
	# Контур.
	src = img.copy().load()
	for y in range(h):
		for x in range(w):
			if src[x, y][3]:
				continue
			for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
				nx, ny = x + dx, y + dy
				if 0 <= nx < w and 0 <= ny < h and src[nx, ny][3] and g[ny][nx] != "p":
					px[x, y] = pal.P[0] + (255,)
					break
	return img


def coat_frame(f: int) -> Image.Image:
	"""Високий у плащі й кашкеті, руки в кишенях."""
	w, h = 30, 52
	g = blank(w, h)
	cx = 13
	up = BREATH[f]
	fl = FLUTTER[f]
	top = 2 - up
	# Кашкет: тулія і козирок (видно краї збоку).
	for i, hw in enumerate([2, 3, 4]):
		span(g, top + i, cx - hw, cx + hw, "c")
	span(g, top + 3, cx - 5, cx + 4, "c")
	# Потилиця й шия.
	for i, hw in enumerate([4, 4, 3, 3]):
		span(g, top + 4 + i, cx - hw, cx + hw - 1, "h")
	span(g, top + 8, cx - 2, cx + 1, "s")
	# Комір і плечі.
	span(g, top + 9, cx - 4, cx + 3, "b")
	span(g, top + 10, cx - 7, cx + 6, "b")
	for y in range(top + 11, 30):
		span(g, y, cx - 8, cx + 7, "b")
	# Поли плаща розходяться донизу, права пола на вітрі.
	for i, y in enumerate(range(30, 42)):
		left = cx - 8 - i // 4
		right = cx + 7 + i // 4 + (fl * i) // 11
		span(g, y, left, right, "b")
	# Ноги й черевики.
	for y in range(42, 49):
		span(g, y, cx - 5, cx - 2, "L")
		span(g, y, cx + 1, cx + 4, "L")
	span(g, 49, cx - 6, cx - 2, "k")
	span(g, 49, cx + 1, cx + 5, "k")
	seams = []
	for y in range(top + 12, 31):
		seams += [(cx - 6, y), (cx + 5, y)] # руки вздовж тіла
	for y in range(top + 13, 42):
		seams.append((cx, y)) # центральний шов-шліца
	dark = [(x, 27) for x in range(cx - 7, cx + 7)] # пояс
	return shade(g, seams, dark)


def phone_frame(f: int) -> Image.Image:
	"""Струнка з хвостом, коротка куртка, джинси; праворуч у руці світиться телефон."""
	w, h = 30, 52
	g = blank(w, h)
	cx = 12
	up = BREATH[f]
	sw = SWAY[f]
	top = 8 - up
	# Голова: волосся гладко зачесане, хвіст починається з потилиці.
	for i, hw in enumerate([2, 3, 3, 3, 3, 2]):
		span(g, top + i, cx - hw, cx + hw - 1, "h")
	span(g, top + 6, cx - 1, cx, "s") # шия
	# Куртка до стегон, вузькі плечі.
	span(g, top + 7, cx - 4, cx + 3, "j")
	for y in range(top + 8, 31):
		span(g, y, cx - 5, cx + 4, "j")
	span(g, 31, cx - 4, cx + 3, "j")
	for i in range(9): # хвіст: 2 px завширшки, гойдається, звужується
		x = cx + (sw * i) // 5
		span(g, top + 3 + i, x - (1 if i < 6 else 0), x, "r")
	# Права рука зігнута вперед — тримає телефон.
	span(g, 25, cx + 5, cx + 7, "j")
	span(g, 26, cx + 7, cx + 8, "s")
	span(g, 24, cx + 8, cx + 10, "p")
	span(g, 25, cx + 9, cx + 10, "p")
	# Джинси з просвітом між ногами, кеди.
	for y in range(32, 45):
		span(g, y, cx - 4, cx - 1, "L")
		span(g, y, cx + 1, cx + 3, "L")
	span(g, 45, cx - 5, cx - 1, "k")
	span(g, 45, cx + 1, cx + 4, "k")
	seams = [(cx - 4, y) for y in range(top + 11, 30)] + [(cx + 3, y) for y in range(top + 11, 25)]
	dark = [(x, 30) for x in range(cx - 4, cx + 4)]
	return shade(g, seams, dark, rim_rows=10)


def hood_frame(f: int) -> Image.Image:
	"""Сидить на парапеті в худі з капюшоном, звісивши ноги назовні; збоку цигарка."""
	w, h = 30, 52
	g = blank(w, h)
	cx = 14
	up = BREATH[f]
	sw = SWAY[f] if f in (2, 3) else 0
	top = 24 - up
	# Згорблені плечі: два горби обабіч капюшона.
	for i, hw in enumerate([5, 7, 8, 9]):
		span(g, top + 4 + i, cx - hw, cx + hw - 1, "t")
	# Капюшон опущений вперед — нижче лінії плечей видно лише його верх.
	for i, hw in enumerate([2, 3, 4, 4, 4, 4]):
		span(g, top + i, cx - hw + sw, cx + hw - 1 + sw, "t")
	# Спина звужується до поясу, сидить на покритті парапету.
	for i, y in enumerate(range(top + 8, 47)):
		hw = 9 if i < 6 else (8 if i < 14 else 7)
		span(g, y, cx - hw, cx + hw - 1, "t")
	span(g, 47, cx - 8, cx + 7, "t")
	# Права рука з цигаркою відставлена вбік.
	span(g, 39, cx + 7, cx + 10, "t")
	span(g, 40, cx + 10, cx + 11, "s")
	seams = [(cx + sw, y) for y in range(top + 1, top + 5)] + [(cx - 7, y) for y in range(top + 9, 45)] + [(cx + 6, y) for y in range(top + 9, 39)]
	seams += [(x, top + 4) for x in range(cx - 4 + sw, cx + 4 + sw)] # край капюшона на плечах
	img = shade(g, seams, rim_rows=8)
	img.putpixel((cx + 12, 40), pal.P[20] + (255,)) # жар цигарки (пульсує окремим світлом у сцені)
	return img


def sheet(fn, name: str) -> None:
	frames = [fn(f) for f in range(FRAMES)]
	w, h = frames[0].size
	out = Image.new("RGBA", (w * FRAMES, h), (0, 0, 0, 0))
	for i, fr in enumerate(frames):
		out.alpha_composite(fr, (i * w, 0))
	out.save(os.path.join(OUT, name))


def main() -> None:
	os.makedirs(OUT, exist_ok=True)
	sheet(coat_frame, "gang_coat.png")
	sheet(phone_frame, "gang_phone.png")
	sheet(hood_frame, "gang_hood.png")
	# Превʼю: кадри ×8 на тлі заграви.
	prev = Image.new("RGBA", (30 * FRAMES * 4, 52 * 3 * 4), pal.P[5] + (255,))
	for row, name in enumerate(["gang_coat.png", "gang_phone.png", "gang_hood.png"]):
		s = Image.open(os.path.join(OUT, name)).resize((30 * FRAMES * 4, 52 * 4), Image.NEAREST)
		prev.alpha_composite(s, (0, row * 52 * 4))
	prev.save(os.path.join(os.path.dirname(__file__), "gang_preview.png"))
	print("ok")


if __name__ == "__main__":
	main()
