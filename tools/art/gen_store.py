"""Рівень «Магазин на розі» — вид зверху (3/4), тайли 16×16, нічне освітлення.

Запуск: python tools/art/gen_store.py
Результат:
  assets/sprites/store/tiles.png      — атлас тайлів 16×16 (усі тайли рівня, підписи — у store.json)
  assets/sprites/store/floor.png      — підлога (тайли, зібрані за картою)          + floor_n.png (нормалі)
  assets/sprites/store/walls.png      — стіни: верх (кришка) і лицьові грані          + walls_n.png
  assets/sprites/store/objects.png    — меблі й предмети (стелажі, холодильники, каса, сейф…) + objects_n.png
  assets/sprites/store/glow.png       — те, що світиться само (скло холодильників, екрани, табличка ВИХІД)
  assets/sprites/store/ceiling.png    — передній план: лампи на стелі, кабелі, балка (над персонажами)
  assets/levels/store.json            — карта, твердість клітинок, оклюдери, світло, точки персонажів

Стайлгайд: docs/art/styleguide.md. Світло з північного заходу (верх-ліво): верхні й ліві грані світліші.
Перспектива 3/4: у горизонтальних стін видно верх (4 px) і лицьову грань (12 px) на південь.
"""
from __future__ import annotations

import json
import math
import os
import random

from PIL import Image

import palette as pal
from pixel import BAYER4, Canvas, brick_wall, concrete, metal_panel

T = 16
COLS, ROWS = 50, 23 # 50 = кадр 20:9 (800 px); магазин — у центральних 40 клітинках
W, H = COLS * T, ROWS * T
ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
OUT = os.path.join(ROOT, "assets", "sprites", "store")
LEVEL = os.path.join(ROOT, "assets", "levels")

# Карта: # стіна, = вікно (скло), D двері (скло), . лінолеум залу, , бетон підсобки,
# s тротуар, r дорога, ~ дах/темрява за межами, d двері підсобки (відчинені), b задні двері,
# a — провулок обабіч магазину (мокрий асфальт; видно на широких екранах).
MAP = [
	"~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~",
	"~~~~~~######################################~~~~~~",
	"aaaaa~#....................#,,,,,,,,,,,,,,b#~aaaaa",
	"aaaaa~#....................#,,,,,,,,,,,,,,,#~aaaaa",
	"aaaaa~#....................#,,,,,,,,,,,,,,,#~aaaaa",
	"aaaaa~#....................#,,,,,,,,,,,,,,,#~aaaaa",
	"aaaaa~#....................#,,,,,,,,,,,,,,,#~aaaaa",
	"aaaaa~#....................#######d#########~aaaaa",
	"aaaaa~#.....................................#aaaaa",
	"aaaaa~#.....................................#aaaaa",
	"aaaaa~#.....................................#aaaaa",
	"aaaaa~#.....................................#aaaaa",
	"aaaaa~#.....................................#aaaaa",
	"aaaaa~#.....................................#aaaaa",
	"aaaaa~#.....................................#aaaaa",
	"aaaaa~#.....................................#aaaaa",
	"aaaaa~#.....................................#aaaaa",
	"~~~~~~##====####=====##DDD##=========#############",
	"ssssssssssssssssssssssssssssssssssssssssssssssssss",
	"ssssssssssssssssssssssssssssssssssssssssssssssssss",
	"rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr",
	"rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr",
	"rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr",
]
assert all(len(r) == COLS for r in MAP) and len(MAP) == ROWS

rng = random.Random(42)
data: dict = {"tile": T, "size": [COLS, ROWS], "solid": [], "occluders": [], "lights": [], "spawns": {},
	"tiles_atlas": {}, "camera": None, "windows": []}


def cell(x, y):
	return MAP[y][x] if 0 <= x < COLS and 0 <= y < ROWS else "~"


def is_wall(c):
	return c in "#=D"


# --- Тайли ------------------------------------------------------------------------------

def tile_lino(cv, ox, oy, variant):
	"""Лінолеум «шахівниця» в залі: дві тони, потерті шви, бруд по краях, рідкі тріщини."""
	# Дві близькі тони (не контрастна шахівниця): простір «вирізає» світло, а не альбедо.
	light = (ox // T + oy // T) % 2 == 0
	base, dark, lite = (3, 2, 4) if light else (2, 1, 3)
	cv.rect(ox, oy, T, T, base)
	for i in range(T):
		cv.put(ox + i, oy, dark if i % 5 else base) # шов
		cv.put(ox, oy + i, dark if i % 4 else base)
	cv.put(ox + 1, oy + 1, lite)
	r = random.Random(variant * 977 + ox * 13 + oy)
	for _ in range(r.randrange(2, 6)): # потертості
		cv.put(ox + r.randrange(2, T), oy + r.randrange(2, T), lite if r.random() < 0.5 else dark)
	if variant == 1: # тріщина
		x, y = ox + r.randrange(3, 12), oy + r.randrange(3, 12)
		for i in range(r.randrange(3, 7)):
			cv.put(x + i, y + (i // 2) * r.choice([1, -1]), dark)
	if variant == 2: # відколотий кут плитки
		cv.rect(ox + 11, oy + 11, 5, 5, dark)
		cv.hline(ox + 11, oy + 11, 5, 1)


def tile_concrete(cv, ox, oy, variant):
	r = random.Random(variant * 31 + ox * 7 + oy)
	concrete(cv, ox, oy, T, T, r, (2, 3, 4), speckle=0.09)
	cv.hline(ox, oy, T, 1)
	if variant == 1: # пляма мастила
		for y in range(5):
			for x in range(7):
				if (x - 3) ** 2 / 9 + (y - 2) ** 2 / 4 < 1:
					cv.put(ox + 4 + x, oy + 6 + y, 1)


def tile_sidewalk(cv, ox, oy, variant):
	"""Мокрий тротуар: плити з темними швами, холодний відблиск неба на верхньому краї.
	Тон темний: яскравість дає світло ліхтарів, а не альбедо (інакше під лампою — пласка пляма)."""
	r = random.Random(variant * 53 + ox + oy * 3)
	concrete(cv, ox, oy, T, T, r, (2, 3, 4), speckle=0.08)
	cv.hline(ox, oy, T, 1)
	cv.vline(ox, oy, T, 1)
	cv.hline(ox + 1, oy + 1, T - 2, 5)
	if variant == 1:
		cv.rect(ox + 3, oy + 6, 9, 4, 3) # калюжа (відблиск — у glow)


def tile_road(cv, ox, oy, variant):
	r = random.Random(variant * 71 + ox * 5 + oy)
	cv.rect(ox, oy, T, T, 1)
	for _ in range(10):
		cv.put(ox + r.randrange(T), oy + r.randrange(T), r.choice([0, 2]))
	if oy // T == 21 and (ox // T) % 3 != 2: # розмітка посередині дороги
		cv.rect(ox, oy + 7, T, 2, 15)
		cv.hline(ox, oy + 8, T, 14)


def tile_wall_top(cv, ox, oy):
	"""Кришка стіни, вигляд згори: темна, світлий край з північного заходу."""
	cv.rect(ox, oy, T, T, 1)
	cv.hline(ox, oy, T, 3)
	cv.vline(ox, oy, T, 2)
	for i in range(0, T, 4):
		cv.put(ox + i + 2, oy + 7, 2)


def tile_wall_face(cv, ox, oy, variant):
	"""Лицьова грань стіни (дивиться на південь): штукатурка 3 тони, плінтус, варіації."""
	r = random.Random(variant * 17 + ox)
	cv.rect(ox, oy, T, T, 4)
	for y in range(T):
		for x in range(T):
			if r.random() < 0.08:
				cv.put(ox + x, oy + y, r.choice([3, 5]))
	cv.hline(ox, oy, T, 6) # верхній край ловить світло
	cv.rect(ox, oy + 12, T, 4, 2) # плінтус
	cv.hline(ox, oy + 12, T, 13)
	cv.hline(ox, oy + 15, T, 0)
	if variant == 1: # розетка
		cv.rect(ox + 6, oy + 8, 3, 3, 15)
		cv.put(ox + 7, oy + 9, 0)
	if variant == 2: # подряпини від візків
		cv.hline(ox + 2, oy + 10, 9, 3)
		cv.hline(ox + 4, oy + 11, 7, 3)


def tile_window(cv, glow, ox, oy, variant):
	"""Вітрина: рама, темне скло з відблисками, краплі дощу на склі (у glow — холодний відсвіт)."""
	cv.rect(ox, oy, T, T, 0)
	cv.rect(ox, oy, T, 3, 14)
	cv.hline(ox, oy, T, 16)
	cv.rect(ox, oy + 13, T, 3, 13)
	cv.rect(ox, oy + 3, T, 10, 26)
	for i in range(8): # діагональний блік на склі
		cv.put(ox + 3 + i, oy + 11 - i, 27)
	r = random.Random(variant + ox)
	for _ in range(4):
		x, y = ox + r.randrange(1, T - 1), oy + r.randrange(4, 12)
		glow.put(x, y, 28) # краплі на склі ловлять неон
	if variant == 1:
		cv.vline(ox + T - 1, oy, T, 14) # стійка рами


TILE_FUNCS = ["lino0", "lino1", "lino2", "conc0", "conc1", "side0", "side1", "road0", "wall_top",
	"face0", "face1", "face2", "window0", "window1"]


def build_atlas():
	"""Атлас усіх тайлів — для стайлгайду й етапу 3 (TileMapLayer)."""
	atlas = Canvas(T * len(TILE_FUNCS), T)
	g = Canvas(T * len(TILE_FUNCS), T)
	for i, name in enumerate(TILE_FUNCS):
		ox = i * T
		if name.startswith("lino"):
			tile_lino(atlas, ox, 0, int(name[-1]))
		elif name.startswith("conc"):
			tile_concrete(atlas, ox, 0, int(name[-1]))
		elif name.startswith("side"):
			tile_sidewalk(atlas, ox, 0, int(name[-1]))
		elif name.startswith("road"):
			tile_road(atlas, ox, 0, 0)
		elif name == "wall_top":
			tile_wall_top(atlas, ox, 0)
		elif name.startswith("face"):
			tile_wall_face(atlas, ox, 0, int(name[-1]))
		elif name.startswith("window"):
			tile_window(atlas, g, ox, 0, int(name[-1]))
		data["tiles_atlas"][name] = i
	atlas.save(os.path.join(OUT, "tiles.png"))


# --- Шари рівня --------------------------------------------------------------------------

def build_floor():
	cv = Canvas(W, H)
	glow = Canvas(W, H)
	for y in range(ROWS):
		for x in range(COLS):
			c = cell(x, y)
			ox, oy = x * T, y * T
			if c in ".D":
				tile_lino(cv, ox, oy, rng.choice([0, 0, 0, 0, 1, 2]))
			elif c in ",db":
				tile_concrete(cv, ox, oy, rng.choice([0, 0, 0, 1]))
			elif c == "s":
				v = 1 if rng.random() < 0.15 else 0
				tile_sidewalk(cv, ox, oy, v)
				if v:
					for i in range(9): # калюжа на тротуарі відбиває неон вивіски над дверима
						if rng.random() < 0.6:
							glow.put(ox + 3 + i, oy + 7 + (i % 2), 23)
			elif c == "r":
				tile_road(cv, ox, oy, 0)
			elif c == "a":
				tile_sidewalk(cv, ox, oy, 1 if rng.random() < 0.1 else 0)
				cv.dither_rect(ox, oy, T, T, 1, 0.45) # провулок темніший за тротуар
			elif c == "~":
				cv.rect(ox, oy, T, T, 0)
	# Тінь від стін на підлогу: 3/4 перспектива — під північною гранню й вздовж західних стін.
	for y in range(ROWS):
		for x in range(COLS):
			if cell(x, y) in ".,d" and is_wall(cell(x, y - 1)):
				cv.dither_rect(x * T, y * T, T, 5, 0, 0.6)
			if cell(x, y) in ".,d" and is_wall(cell(x - 1, y)):
				cv.dither_rect(x * T, y * T, 3, T, 0, 0.45)
	# Килимок біля входу й мокрі сліди Тіні від задніх дверей до місця, де вона ховається.
	mat_x, mat_y = 22 * T, 16 * T
	cv.rect(mat_x, mat_y + 2, 3 * T, 12, 9)
	for i in range(0, 3 * T, 2):
		cv.vline(mat_x + i, mat_y + 3, 10, 8)
	path = [(42, 3), (40, 5), (37, 6), (35, 7), (34, 9), (30, 10), (26, 11), (22, 12), (19, 12), (15, 13), (13, 13)]
	for (x0, y0), (x1, y1) in zip(path, path[1:]):
		steps = int(math.hypot(x1 - x0, y1 - y0) * 2)
		for k in range(steps):
			t = k / steps
			px = int((x0 + (x1 - x0) * t) * T + 8)
			py = int((y0 + (y1 - y0) * t) * T + 8 + (3 if k % 2 else -3))
			cv.rect(px, py, 2, 3, 1)
			cv.put(px, py, 6) # мокрий відблиск
	return cv, glow


def build_walls():
	cv = Canvas(W, H)
	glow = Canvas(W, H)
	for y in range(ROWS):
		for x in range(COLS):
			c = cell(x, y)
			ox, oy = x * T, y * T
			if c == "#":
				tile_wall_top(cv, ox, oy)
				data["solid"].append([x, y])
			elif c == "=":
				tile_window(cv, glow, ox, oy, rng.randrange(2))
				data["solid"].append([x, y])
				data["windows"].append([x, y])
			elif c == "D":
				# Скляні двері: рама й скло, табличка «ВІДЧИНЕНО 24/7» світиться (цифри — у glow).
				tile_window(cv, glow, ox, oy, 0)
				cv.vline(ox + 8, oy, T, 14)
				data["windows"].append([x, y])
	# Південна зовнішня стіна: її лицьова грань дивиться на вулицю — малюємо цеглою на тротуарі.
	for x in range(COLS):
		if cell(x, 17) in "#=D" and cell(x, 18) == "s":
			ox, oy = x * T, 18 * T
			if cell(x, 17) == "#":
				brick_wall(cv, ox, oy, T, 10, random.Random(x), (8, 9, 10, 11))
				cv.hline(ox, oy, T, 12)
			else:
				cv.rect(ox, oy, T, 10, 0) # вітрина: скло до підлоги, нижня рама
				cv.rect(ox, oy, T, 3, 14)
				cv.hline(ox, oy, T, 16)
				cv.rect(ox, oy + 3, T, 7, 26)
			cv.hline(ox, oy + 10, T, 0)
			cv.dither_rect(ox, oy + 11, T, 3, 0, 0.5) # тінь стіни на тротуар
	# Лицьові грані горизонтальних стін (видно з півночі на південь): під кришкою, на клітинці підлоги.
	for y in range(ROWS):
		for x in range(COLS):
			if cell(x, y) == "#" and cell(x, y + 1) in ".,db":
				tile_wall_face(cv, x * T, (y + 1) * T - 4, rng.choice([0, 0, 0, 1, 2]))
	# Табличка ВИХІД над входом (зелений світний короб) і вивіска «24/7» над дверима зовні.
	ex, ey = 23 * T, 17 * T - 7
	cv.rect(ex - 1, ey - 1, 14, 7, 0)
	# Табличка ВИХІД: темний короб, світиться лише піктограма «людина біжить до дверей» —
	# маленький акцент, а не яскрава пляма, що спорить з фокусом.
	cv.rect(ex, ey, 12, 5, 1)
	for x, y in ((3, 0), (2, 1), (3, 1), (4, 1), (3, 2), (2, 3), (4, 3), (8, 0), (8, 1), (8, 2), (8, 3), (9, 0), (9, 3)):
		glow.put(ex + x, ey + y, 31)
	sx, sy = 21 * T + 4, 18 * T + 2
	cv.rect(sx - 1, sy - 1, 42, 9, 0)
	glow.rect(sx, sy, 40, 7, 22)
	# «24/7» піксельним шрифтом 3×5: вивіска має зміст, а не порожня яскрава смуга.
	glyphs = {"2": ["111", "001", "111", "100", "111"], "4": ["101", "101", "111", "001", "001"],
		"/": ["001", "001", "010", "100", "100"], "7": ["111", "001", "010", "010", "010"]}
	gx = sx + 11
	for ch in "24/7":
		for gy, row in enumerate(glyphs[ch]):
			for gi, bit in enumerate(row):
				if bit == "1":
					glow.put(gx + gi, sy + 1 + gy, 25)
		gx += 5
	data["lights"].append({"name": "sign_24", "x": sx + 20, "y": sy + 4, "color": pal.HEX[24], "scale": 3.4, "energy": 1.6, "shadow": True, "flicker": True})
	# Оклюдери стін: суцільні відрізки по рядках (вікна й двері світло пропускають — склo).
	for y in range(ROWS):
		x = 0
		while x < COLS:
			if cell(x, y) == "#":
				x0 = x
				while x < COLS and cell(x, y) == "#":
					x += 1
				data["occluders"].append([[x0 * T, y * T], [x * T, y * T], [x * T, y * T + T], [x0 * T, y * T + T]])
			else:
				x += 1
	return cv, glow


# --- Предмети -----------------------------------------------------------------------------

PRODUCT_COLORS = [(9, 10, 11), (17, 18, 19), (26, 27, 28), (22, 23, 24), (13, 14, 15), (18, 19, 20), (8, 10, 12)]


def shelf(cv, x, y, w, h, r):
	"""Стелаж-гондола у 3/4: видно кришку (полиці з товаром згори) і лицьову грань 12 px на південь
	з рядами товару спереду. Тінь на південний схід — від ключового світла з північного заходу."""
	px, py, pw, ph = x * T, y * T, w * T, h * T
	face = 12
	top_h = ph - face
	cv.dither_rect(px + 4, py + 4, pw, ph, 0, 0.75) # тінь на ПдС
	cv.rect(px - 1, py - 1, pw + 2, ph + 2, 0)
	# Кришка: товар на двох боках, хребет посередині.
	metal_panel(cv, px, py, pw, top_h, (0, 13, 14, 15), rivets=False)
	spine = px + pw // 2 - 1
	cv.rect(spine, py + 1, 2, top_h - 2, 1)
	for side_x in (px + 2, spine + 3):
		yy = py + 3
		while yy < py + top_h - 4:
			xx = side_x
			while xx < side_x + pw // 2 - 5:
				if r.random() < 0.82: # порожні місця — розкупили
					dark, mid, lite = r.choice(PRODUCT_COLORS)
					bw, bh = r.choice([(2, 3), (3, 2), (2, 2), (3, 3)])
					cv.rect(xx, yy, bw, bh, mid)
					cv.hline(xx, yy, bw, lite)
					cv.vline(xx + bw - 1, yy + 1, bh - 1, dark)
				xx += 3
			cv.hline(side_x - 1, yy + 3, pw // 2 - 3, 13)
			yy += 5
	cv.hline(px, py, pw, 16)
	cv.vline(px, py, top_h, 15)
	# Лицьова грань: 2 полиці з товаром фронтально, цінник, нижній цоколь.
	fy = py + top_h
	cv.rect(px, fy, pw, face, 14)
	cv.hline(px, fy, pw, 16)
	for row in (fy + 2, fy + 7):
		xx = px + 2
		while xx < px + pw - 3:
			dark, mid, lite = r.choice(PRODUCT_COLORS)
			cv.rect(xx, row, 3, 4, mid)
			cv.hline(xx, row, 3, lite)
			cv.vline(xx + 2, row + 1, 3, dark)
			xx += 4
		cv.hline(px + 1, row + 4, pw - 2, 13)
	cv.rect(px + pw // 2 - 3, fy + 1, 6, 2, 21) # цінник
	cv.rect(px, py + ph - 1, pw, 1, 0)
	data["occluders"].append([[px, py], [px + pw, py], [px + pw, py + ph], [px, py + ph]])
	for ty in range(y, y + h):
		for tx in range(x, x + w):
			data["solid"].append([tx, ty])


def fridge(cv, glow, x, y, h):
	"""Холодильники вздовж західної стіни: корпус, скляні двері на схід, усередині — пляшки,
	що світяться холодним світлом (glow). Кожна дверцята — окремий світлий прямокутник з рамою."""
	px, py, ph = x * T, y * T, h * T
	cv.rect(px, py, 2 * T, ph, 0)
	metal_panel(cv, px, py, 14, ph, (0, 13, 14, 15), rivets=False)
	r = random.Random(77)
	cv.dither_rect(px + 2 * T, py + 4, 4, ph, 0, 0.75) # тінь на схід
	for k in range(h):
		dy = py + k * T
		cv.rect(px + 14, dy + 1, 17, T - 2, 0)
		# Скло — 27 (не найяскравіше в кадрі), верхній край ловить світло лампи всередині.
		glow.rect(px + 15, dy + 2, 15, T - 4, 27)
		glow.hline(px + 15, dy + 2, 15, 28)
		empty = r.random() < 0.25
		for b in range(4): # пляшки в ряд; частина полиць розкуплена
			if empty and b > 1:
				continue
			bx = px + 17 + b * 3
			glow.rect(bx, dy + 5, 2, 6, [26, 22, 18, 17][(b + k) % 4])
			glow.put(bx, dy + 5, [28, 23, 19, 18][(b + k) % 4])
		cv.vline(px + 30, dy + 2, T - 4, 15) # ручка
		if k % 4 == 1: # одне джерело на чотири дверцята: менше ламп для GPU, світло однаково суцільне
			data["lights"].append({"name": f"fridge_{k}", "x": px + 28, "y": dy + 24, "color": pal.HEX[29], "scale": 1.5, "energy": 0.9, "shadow": False, "flicker": k == 5})
	cv.rect(px, py + ph, 2 * T, 10, 13) # торцева грань (3/4)
	cv.hline(px, py + ph, 2 * T, 15)
	cv.hline(px, py + ph + 9, 2 * T, 0)
	cv.rect(px + 4, py + ph + 3, 8, 3, 0) # вентиляційна решітка
	for i in range(0, 8, 2):
		cv.put(px + 4 + i, py + ph + 4, 14)
	data["occluders"].append([[px, py], [px + 2 * T, py], [px + 2 * T, py + ph], [px, py + ph]])
	for ty in range(y, y + h):
		data["solid"].append([x, ty])
		data["solid"].append([x + 1, ty])


def counter(cv, glow, x, y, w, h):
	"""Каса: дерев'яна стільниця (4 тони, волокна), касовий апарат з екраном, стійка з жуйками."""
	px, py, pw, ph = x * T, y * T, w * T, h * T
	cv.dither_rect(px + 4, py + 4, pw, ph, 0, 0.75)
	cv.rect(px - 1, py - 1, pw + 2, ph + 2, 0)
	for yy in range(py, py + ph):
		for xx in range(px, px + pw):
			c = 18 if (yy - py) % 5 else 17
			if (xx * 7 + yy * 3) % 23 == 0:
				c = 19
			cv.put(xx, yy, c)
	cv.hline(px, py, pw, 19)
	cv.vline(px, py, ph, 19)
	cv.rect(px, py + ph - 4, pw, 4, 17) # лицьова грань на південь
	cv.hline(px, py + ph - 4, pw, 0)
	# Касовий апарат.
	rx, ry = px + 10, py + 6
	metal_panel(cv, rx, ry, 18, 14, (0, 13, 14, 15), rivets=False)
	cv.rect(rx + 3, ry + 3, 12, 5, 0)
	glow.rect(rx + 4, ry + 4, 10, 3, 31)
	glow.hline(rx + 5, ry + 5, 6, 0)
	for i in range(3):
		for j in range(3):
			cv.put(rx + 4 + i * 2, ry + 10 + j, 16 if (i + j) % 2 else 15)
	data["lights"].append({"name": "register", "x": rx + 9, "y": ry + 5, "color": pal.HEX[31], "scale": 0.5, "energy": 0.8, "shadow": False, "flicker": False})
	# Лотерея й жуйки.
	for i in range(5):
		cv.rect(px + 40 + i * 5, py + 8, 4, 6, PRODUCT_COLORS[i][1])
		cv.hline(px + 40 + i * 5, py + 8, 4, PRODUCT_COLORS[i][2])
	data["occluders"].append([[px, py], [px + pw, py], [px + pw, py + ph], [px, py + ph]])
	for ty in range(y, y + h):
		for tx in range(x, x + w):
			data["solid"].append([tx, ty])


def safe(cv, x, y):
	"""Сейф у підсобці: масивний метал, заклепки, диск з насічками, тінь на підлогу."""
	px, py = x * T, y * T
	cv.dither_rect(px + 3, py + 3, 2 * T, 2 * T, 0, 0.7) # тінь на південний схід
	cv.rect(px - 1, py - 1, 2 * T + 2, 2 * T + 2, 0)
	metal_panel(cv, px, py, 2 * T, 2 * T, (0, 13, 14, 15))
	metal_panel(cv, px + 4, py + 4, 2 * T - 8, 2 * T - 10, (0, 13, 14, 15), rivets=False)
	cx, cy = px + T, py + T - 2
	for a in range(0, 360, 30):
		cv.put(cx + round(4 * math.cos(math.radians(a))), cy + round(4 * math.sin(math.radians(a))), 16)
	cv.rect(cx - 1, cy - 1, 3, 3, 13)
	cv.put(cx - 1, cy - 1, 16)
	cv.rect(px + 2 * T - 6, py + 2 * T - 9, 3, 6, 16) # ручка
	data["occluders"].append([[px, py], [px + 2 * T, py], [px + 2 * T, py + 2 * T], [px, py + 2 * T]])
	data["spawns"]["safe"] = [cx, cy]
	for ty in (y, y + 1):
		for tx in (x, x + 1):
			data["solid"].append([tx, ty])


def boxes(cv, x, y, r):
	"""Картонні коробки, складені в підсобці: 3 тони картону, скотч, кути."""
	for _ in range(5):
		bx = x * T + r.randrange(0, 2 * T)
		by = y * T + r.randrange(0, 2 * T)
		bw, bh = r.randrange(9, 15), r.randrange(8, 13)
		cv.rect(bx - 1, by - 1, bw + 2, bh + 2, 0)
		cv.rect(bx, by, bw, bh, 18)
		cv.hline(bx, by, bw, 19)
		cv.vline(bx, by, bh, 19)
		cv.rect(bx, by + bh - 2, bw, 2, 17)
		cv.vline(bx + bw // 2, by, bh - 2, 17) # скотч
	data["occluders"].append([[x * T, y * T], [x * T + 2 * T + 12, y * T], [x * T + 2 * T + 12, y * T + 2 * T + 10], [x * T, y * T + 2 * T + 10]])


def desk(cv, glow, x, y):
	"""Стіл охорони з монітором і лампою: тепле світло лампи, монітор з камерою (cyan)."""
	px, py = x * T, y * T
	cv.rect(px - 1, py - 1, 3 * T + 2, T + 2, 0)
	for yy in range(py, py + T):
		for xx in range(px, px + 3 * T):
			cv.put(xx, yy, 9 if (yy - py) % 4 else 8)
	cv.hline(px, py, 3 * T, 11)
	cv.rect(px, py + T - 3, 3 * T, 3, 8)
	metal_panel(cv, px + 4, py + 1, 14, 9, (0, 13, 14, 15), rivets=False)
	glow.rect(px + 6, py + 3, 10, 5, 27)
	glow.hline(px + 6, py + 4, 10, 28)
	glow.put(px + 9, py + 5, 30) # червона точка запису на картинці камери
	cv.rect(px + 30, py + 2, 6, 6, 13) # лампа
	glow.rect(px + 31, py + 3, 4, 4, 21)
	for i in range(3): # папери
		cv.rect(px + 20 + i * 2, py + 3 + i, 6, 7, 16)
		cv.hline(px + 21 + i * 2, py + 5 + i, 4, 14)
	data["lights"].append({"name": "desk_lamp", "x": px + 33, "y": py + 5, "color": pal.HEX[20], "scale": 2.2, "energy": 1.4, "shadow": True, "flicker": False})
	data["lights"].append({"name": "monitor", "x": px + 11, "y": py + 6, "color": pal.HEX[28], "scale": 0.6, "energy": 0.7, "shadow": False, "flicker": True})
	for tx in range(x, x + 3):
		data["solid"].append([tx, y])


def mop(cv, x, y):
	px, py = x * T + 4, y * T + 2
	cv.rect(px, py + 6, 10, 8, 14)
	cv.hline(px, py + 6, 10, 16)
	cv.rect(px + 1, py + 7, 8, 5, 26)
	cv.vline(px + 5, py - 6, 12, 18)
	cv.put(px + 5, py - 6, 19)


def atm(cv, glow, x, y):
	"""Банкомат біля східної стіни: корпус, екран (світиться), клавіатура, щілина для карток."""
	px, py = x * T, y * T
	cv.rect(px - 1, py - 1, T + 2, 2 * T + 2, 0)
	metal_panel(cv, px, py, T, 2 * T, (0, 13, 14, 15), rivets=False)
	cv.rect(px + 2, py + 4, 11, 8, 0)
	glow.rect(px + 3, py + 5, 9, 6, 27)
	glow.hline(px + 3, py + 5, 9, 28)
	glow.rect(px + 5, py + 7, 5, 1, 29)
	for i in range(3):
		for j in range(3):
			cv.put(px + 4 + i * 2, py + 15 + j * 2, 16)
	cv.hline(px + 3, py + 23, 9, 0)
	glow.put(px + 12, py + 23, 31)
	data["lights"].append({"name": "atm", "x": px - 6, "y": py + 10, "color": pal.HEX[28], "scale": 1.1, "energy": 0.9, "shadow": False, "flicker": False})
	data["occluders"].append([[px, py], [px + T, py], [px + T, py + 2 * T], [px, py + 2 * T]])
	data["solid"] += [[x, y], [x, y + 1]]


def freezer(cv, glow, x, y):
	"""Морозильна скриня з морозивом: скляна кришка зверху, всередині кольорові пачки, холодне світло."""
	px, py, pw, ph = x * T, y * T, 4 * T, 2 * T
	cv.rect(px - 1, py - 1, pw + 2, ph + 2, 0)
	metal_panel(cv, px, py, pw, ph, (0, 13, 14, 15), rivets=False)
	# Скло кришки темне (запотіле), світяться лише пачки під ним — не велика яскрава пляма.
	glow.rect(px + 3, py + 3, pw - 6, ph - 8, 26)
	glow.hline(px + 3, py + 3, pw - 6, 27)
	r = random.Random(9)
	for yy in range(py + 5, py + ph - 7, 4):
		for xx in range(px + 5, px + pw - 7, 4):
			c = r.choice(PRODUCT_COLORS)
			glow.rect(xx, yy, 3, 2, c[1])
			glow.put(xx, yy, c[2])
	glow.vline(px + pw // 2, py + 3, ph - 8, 27) # стик двох кришок
	cv.rect(px, py + ph - 4, pw, 4, 13)
	cv.hline(px, py + ph - 4, pw, 15)
	data["lights"].append({"name": "freezer", "x": px + pw // 2, "y": py + ph // 2, "color": pal.HEX[29], "scale": 1.6, "energy": 0.7, "shadow": False, "flicker": False})
	data["occluders"].append([[px, py], [px + pw, py], [px + pw, py + ph], [px, py + ph]])
	for ty in range(y, y + 2):
		for tx in range(x, x + 4):
			data["solid"].append([tx, ty])


def pallet(cv, x, y, r):
	"""Піддон з упаковками води: дерев'яний піддон, плівка з бліками, ціна-«лапка»."""
	px, py = x * T, y * T
	cv.rect(px - 1, py - 1, 2 * T + 2, 2 * T + 2, 0)
	for i in range(0, 2 * T, 4):
		cv.rect(px, py + i, 2 * T, 3, 18)
		cv.hline(px, py + i, 2 * T, 19)
	for j in range(2):
		for i in range(3):
			bx, by = px + 2 + i * 10, py + 3 + j * 13
			cv.rect(bx, by, 9, 11, 27)
			cv.hline(bx, by, 9, 29)
			cv.vline(bx, by, 11, 28)
			cv.rect(bx + 2, by + 4, 5, 3, 26)
	cv.rect(px + 2 * T - 6, py - 4, 7, 5, 21)
	cv.rect(px + 2 * T - 4, py - 2, 3, 1, 30)
	data["occluders"].append([[px, py], [px + 2 * T, py], [px + 2 * T, py + 2 * T], [px, py + 2 * T]])
	data["solid"] += [[x, y], [x + 1, y], [x, y + 1], [x + 1, y + 1]]


def build_objects():
	cv = Canvas(W, H)
	glow = Canvas(W, H)
	r = random.Random(5)
	fridge(cv, glow, 7, 4, 12)
	for sx in (12, 16, 20):
		shelf(cv, sx, 5, 2, 9, r)
	counter(cv, glow, 31, 13, 6, 2)
	safe(cv, 40, 3)
	boxes(cv, 29, 2, r)
	desk(cv, glow, 33, 3)
	mop(cv, 27, 6)
	atm(cv, glow, 43, 9)
	freezer(cv, glow, 30, 9)
	pallet(cv, 36, 11, r)
	# Стійка з журналами: металева рамка, обкладинки різних кольорів з «заголовком» і фото.
	bx, by = 25 * T, 15 * T + 2
	metal_panel(cv, bx - 2, by - 2, 26, 14, (0, 13, 14, 15), rivets=False)
	for i, col in enumerate((30, 27, 19)):
		x0 = bx + i * 8
		cv.rect(x0, by, 6, 9, col)
		cv.hline(x0 + 1, by + 1, 4, 16)
		cv.rect(x0 + 1, by + 3, 4, 4, 2)
		cv.put(x0 + 2, by + 4, 12)
	# Камера в кутку залу (спрайт з мигаючим світлодіодом — у сцені) — тут кронштейн.
	cam_x, cam_y = 27 * T - 4, 8 * T + 2
	cv.rect(cam_x, cam_y, 3, 6, 13)
	data["camera"] = [cam_x + 1, cam_y + 6]
	return cv, glow


def build_ceiling():
	"""Передній план над персонажами: лампи денного світла (вимкнені, одна в підсобці мерехтить),
	кабель-канал і вентиляційний короб. Найтемніший шар, рухається з найбільшим паралаксом."""
	cv = Canvas(W, H)
	glow = Canvas(W, H)
	for lx, ly in ((11, 10), (18, 10), (25, 10), (11, 15), (18, 15), (25, 15), (36, 11)):
		px, py = lx * T, ly * T
		# Світильник денного світла (вимкнений): металевий короб, грати, підвіси до стелі.
		cv.rect(px - 1, py - 1, 26, 7, 0)
		cv.rect(px, py, 24, 5, 13)
		cv.hline(px, py, 24, 15)
		for i in range(2, 22, 3):
			cv.vline(px + i, py + 1, 3, 2)
		cv.put(px + 3, py - 4, 13)
		cv.put(px + 20, py - 4, 13)
		cv.vline(px + 3, py - 3, 2, 1)
		cv.vline(px + 20, py - 3, 2, 1)
	# У підсобці лампа мерехтить (керує сцена; тут — трубка, що світиться).
	px, py = 36 * T, 5 * T
	cv.rect(px - 1, py - 1, 26, 6, 0)
	cv.rect(px, py, 24, 4, 2)
	glow.hline(px + 2, py + 2, 20, 29)
	data["lights"].append({"name": "back_tube", "x": px + 12, "y": py + 2, "color": pal.HEX[29], "scale": 2.6, "energy": 0.9, "shadow": True, "flicker": True})
	# Вентиляційний короб уздовж залу.
	for x in range(8 * T, 43 * T):
		cv.put(x, 7 * T + 9, 0)
		cv.put(x, 7 * T + 10, 2)
		cv.put(x, 7 * T + 11, 3)
		cv.put(x, 7 * T + 12, 0)
	for x in range(8 * T, 43 * T, 40):
		cv.rect(x, 7 * T + 8, 3, 6, 1)
	return cv, glow


def save(cv, name):
	cv.save(os.path.join(OUT, name))


def save_normal(name, strength=2.5):
	import numpy as np
	img = np.asarray(Image.open(os.path.join(OUT, name)).convert("RGBA")).astype(np.float32) / 255.0
	h = (0.3 * img[..., 0] + 0.59 * img[..., 1] + 0.11 * img[..., 2]) * img[..., 3]
	dx = np.zeros_like(h)
	dy = np.zeros_like(h)
	dx[:, 1:-1] = (h[:, 2:] - h[:, :-2]) * 0.5
	dy[1:-1, :] = (h[2:, :] - h[:-2, :]) * 0.5
	nx, ny, nz = -dx * strength, -dy * strength, np.ones_like(h)
	ln = np.sqrt(nx * nx + ny * ny + nz * nz)
	out = np.stack([(nx / ln) * 0.5 + 0.5, (-ny / ln) * 0.5 + 0.5, (nz / ln) * 0.5 + 0.5, img[..., 3]], axis=-1)
	Image.fromarray((out * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, name.replace(".png", "_n.png")))


def main():
	os.makedirs(OUT, exist_ok=True)
	os.makedirs(LEVEL, exist_ok=True)
	build_atlas()
	floor, floor_glow = build_floor()
	walls, walls_glow = build_walls()
	objects, obj_glow = build_objects()
	ceiling, ceil_glow = build_ceiling()
	glow = Canvas(W, H)
	for g in (floor_glow, walls_glow, obj_glow, ceil_glow):
		glow.paste(g, 0, 0)
	save(floor, "floor.png")
	save(walls, "walls.png")
	save(objects, "objects.png")
	save(glow, "glow.png")
	save(ceiling, "ceiling.png")
	for n in ("floor.png", "walls.png", "objects.png"):
		save_normal(n)
	# Ліхтарі вулиці за вікнами: світло проходить крізь скло на підлогу залу.
	for lx in (2, 11, 31, 39, 47):
		data["lights"].append({"name": f"street_{lx}", "x": lx * T + 8, "y": 21 * T + 4, "color": pal.HEX[20], "scale": 5.6, "energy": 1.7, "shadow": True, "flicker": False})
	data["spawns"]["shadow"] = [8 * T + 12, 12 * T + 10] # пригнулася в проході між стелажами, біля торця # Тінь пригнулася біля торця стелажа
	data["spawns"]["guard"] = [23 * T, 14 * T + 8] # охоронець іде залом на захід
	data["spawns"]["guard_path"] = [[29 * T, 14 * T + 8], [17 * T, 14 * T + 8]] # прохід під торцями стелажів
	data["street_y"] = [20 * T, 22 * T]
	with open(os.path.join(LEVEL, "store.json"), "w", encoding="utf-8") as f:
		json.dump(data, f, ensure_ascii=False)
	prev = Image.new("RGBA", (W, H), (0, 0, 0, 255))
	for n in ("floor.png", "walls.png", "objects.png", "glow.png", "ceiling.png"):
		prev.alpha_composite(Image.open(os.path.join(OUT, n)))
	prev.save(os.path.join(os.path.dirname(__file__), "store_preview.png"))
	print("ok", len(data["occluders"]), "occluders,", len(data["lights"]), "lights")


if __name__ == "__main__":
	main()
