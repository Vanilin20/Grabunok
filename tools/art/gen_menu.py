"""Генератор головного меню: банда на даху дивиться на нічне місто під дощем.

Запуск з кореня репо:  python tools/art/gen_menu.py
Результат — шари в assets/sprites/menu/ і menu_layout.json з координатами живих деталей
(вогні антен, телевізори у вікнах, світло неону), які розставляє scripts/ui/city_backdrop.gd.

Шари (від далекого до ближнього):
  sky        — небо з загравою міста (не реагує на світло: unshaded)
  clouds     — низькі хмари, безшовні по горизонталі (повільно пливуть)
  far        — далекі хмарочоси в серпанку (unshaded)
  viaduct    — естакада, по якій їде потяг (затінюється)
  mid / mid_glow       — будинки через дорогу; *_glow — вікна й неон, що світяться самі (unshaded)
  sign_frame / sign_letter_N — вивіска «БАНДА» на даху: каркас і окремі літери (для мерехтіння)
  roof / roof_glow     — наш дах: парапет, плитка, калюжі, будка з лампою
  fg         — передній план упритул до камери: труба й кабель

Світло з одного напрямку: ключове — холодна заграва зліва-згори (світлі ліві грані),
неон дає лише кольорові відблиски. Ширина 864 = до 2.4:1; центральні 640 px — базовий кадр.
"""
from __future__ import annotations

import json
import os
import random

from PIL import Image, ImageDraw, ImageFont

import palette as pal
from pixel import BAYER4, Canvas, brick_wall, concrete, metal_panel

W, H = 864, 360
CX = (W - 640) // 2 # зсув базового кадру 640 у широкому полотні
ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
OUT = os.path.join(ROOT, "assets", "sprites", "menu")
FONT = os.path.join(os.path.dirname(__file__), "..", "fonts", "press_start_2p.ttf")

PARAPET_TOP = 276
ROOF_Y = 300
layout: dict = {"size": [W, H], "blinkers": [], "tvs": [], "lights": [], "steam": [], "letters": []}


def base(cv: Canvas) -> Canvas:
	"""Перемкнути полотно на координати базового кадру 640×360."""
	cv.ox = CX
	return cv


# --- Небо й хмари ---------------------------------------------------------------

def make_sky() -> Canvas:
	cv = Canvas(W, H)
	# Згори майже чорне, донизу — фіолетова заграва міста, біля обрію рожевий відсвіт неону.
	cv.vgradient(0, 0, W, 150, [0, 1, 1, 2])
	cv.vgradient(0, 150, W, 110, [2, 3, 4, 5])
	cv.vgradient(0, 260, W, 100, [5, 22, 22])
	return cv


def make_clouds(rng: random.Random) -> Canvas:
	"""Хмари безшовні по x: малюємо з перенесенням через край."""
	cv = Canvas(W, H)
	import math
	bands = [(70, 9, (2, 3)), (118, 12, (3, 4)), (168, 14, (4, 5)), (214, 12, (5, 6))]
	for band_y, thick, (body, lit) in bands:
		x = rng.randrange(0, 80)
		while x < W + 40:
			length = rng.randrange(90, 220)
			phase = rng.uniform(0, 6.28)
			for i in range(length):
				t = i / length
				env = math.sin(t * math.pi) ** 0.7
				bump = math.sin(i * 0.11 + phase) * 2.5 + math.sin(i * 0.31 + phase) * 1.2
				top = int(band_y - thick * env * 0.7 - bump * env)
				bottom = int(band_y + thick * env * 0.35)
				xx = (x + i) % W
				for yy in range(top, bottom):
					inner_top = yy - top
					inner_bot = bottom - 1 - yy
					if min(inner_top, inner_bot) == 0 and BAYER4[yy % 4][xx % 4] > 7:
						continue # рвані краї
					# Низ хмари підсвічений містом знизу — світліший тон.
					cv.px[xx, yy] = pal.P[lit if inner_bot <= 1 else body] + (255,)
			x += length + rng.randrange(30, 120)
	return cv


# --- Далеке місто -----------------------------------------------------------------

def make_far(rng: random.Random) -> Canvas:
	cv = Canvas(W, H)
	for layer, (body, rim, base_y, hmin, hmax, win) in enumerate([
		(4, 5, 236, 40, 120, 0.0), # найдальший план тоне в заграві
		(3, 4, 252, 30, 150, 0.08),
	]):
		x = -10 + layer * 13
		while x < W:
			w = rng.randrange(14, 38)
			h = rng.randrange(hmin, hmax)
			top = base_y - h
			cv.rect(x, top, w, H - top, body)
			cv.vline(x, top, H - top, rim)
			# Уступи й шпилі — різноманітний силует.
			if rng.random() < 0.45 and w > 18:
				sw = rng.randrange(6, w - 8)
				sx = x + rng.randrange(2, w - sw - 2)
				sh = rng.randrange(6, 20)
				cv.rect(sx, top - sh, sw, sh, body)
				cv.vline(sx, top - sh, sh, rim)
				top -= sh
			if rng.random() < 0.35:
				ax = x + w // 2
				ah = rng.randrange(8, 26)
				cv.vline(ax, top - ah, ah, body)
				if layer == 1:
					layout["blinkers"].append([ax, top - ah])
			if win:
				for wy in range(top + 4, base_y + 20, 4):
					for wx in range(x + 2, x + w - 2, 3):
						if rng.random() < win:
							cv.put(wx, wy, rng.choice([18, 18, 27, 6]))
			x += w + rng.randrange(-3, 3)
	return cv


def make_viaduct(rng: random.Random) -> tuple[Canvas, Canvas]:
	"""Естакада з арками; по ній ходить потяг (окремий спрайт). Повертає (естакада, потяг)."""
	cv = Canvas(W, H)
	deck_y = 214
	cv.rect(0, deck_y, W, 5, 4)
	cv.hline(0, deck_y, W, 6)
	cv.hline(0, deck_y + 4, W, 2)
	for x in range(0, W, 28):
		cv.rect(x + 22, deck_y + 5, 6, H - deck_y, 3)
		cv.vline(x + 22, deck_y + 5, H - deck_y, 5)
		# Арка між опорами.
		for i in range(22):
			depth = int(6 * (1 - ((i - 11) / 11) ** 2))
			for yy in range(deck_y + 5, deck_y + 5 + 6 - depth):
				cv.put(x + i, yy, 3)
	# Рейки й контактна мережа.
	cv.hline(0, deck_y - 1, W, 2)
	for x in range(6, W, 40):
		cv.vline(x, deck_y - 14, 13, 3)
		cv.hline(x - 3, deck_y - 14, 7, 3)
	cv.hline(0, deck_y - 12, W, 2)

	train = Canvas(210, 18)
	for car in range(3):
		cx = car * 70
		metal_panel(train, cx, 2, 66, 13, (0, 13, 14, 15), rivets=False)
		train.hline(cx + 1, 2, 64, 15)
		for wx in range(cx + 5, cx + 62, 8):
			train.rect(wx, 5, 5, 4, 1)
		train.rect(cx + 66, 6, 4, 3, 0) # зчіпка
	train.rect(2, 13, 206, 2, 0)
	return cv, train


def make_train_glow() -> Canvas:
	glow = Canvas(210, 18)
	for car in range(3):
		cx = car * 70
		for wx in range(cx + 5, cx + 62, 8):
			glow.rect(wx, 5, 5, 4, 20)
			glow.hline(wx, 8, 5, 19)
	glow.rect(0, 6, 2, 3, 21) # фара
	return glow


# --- Будинки через дорогу ---------------------------------------------------------

def window(cv: Canvas, glow: Canvas, x: int, y: int, w: int, h: int, state: str, rng: random.Random) -> None:
	"""Вікно з рамою, підвіконням і перемичкою. state: dark / warm / dim / tv / blind."""
	cv.rect(x - 1, y - 2, w + 2, 1, 12) # світла перемичка (верх освітлений)
	cv.rect(x - 1, y - 1, w + 2, h + 2, 0) # глибина віконного прорізу
	cv.rect(x, y, w, h, 2)
	# Відблиск на темному склі — діагональ.
	for i in range(min(w, h) - 1):
		cv.put(x + w - 2 - i, y + 1 + i, 4)
	cv.vline(x + w // 2, y, h, 1) # імпост
	cv.hline(x - 1, y + h + 1, w + 2, 15) # підвіконня
	cv.hline(x - 1, y + h + 2, w + 2, 0)
	if state == "dark":
		return
	if state == "tv":
		glow.rect(x, y, w, h, 26)
		glow.rect(x + 1, y + h - 3, w - 2, 2, 27)
		layout["tvs"].append([x + CX, y, w, h])
		return
	if state == "blind":
		glow.rect(x, y, w, h, 18)
		for yy in range(y, y + h, 2):
			glow.hline(x, yy, w, 19)
		return
	core, edge = (20, 19) if state == "warm" else (19, 18)
	glow.rect(x, y, w, h, core)
	glow.vline(x, y, h, edge) # штори з боків
	glow.vline(x + w - 1, y, h, edge)
	glow.hline(x, y + h - 1, w, edge)
	glow.vline(x + w // 2, y, h, 18)
	if rng.random() < 0.3: # силует людини в кімнаті
		px = x + rng.randrange(1, max(2, w - 3))
		glow.rect(px, y + h - 5, 2, 5, 17)
		glow.rect(px, y + h - 6, 2, 1, 17)


def building(cv: Canvas, glow: Canvas, rng: random.Random, x: int, top: int, w: int,
		material: str, win_w: int = 5, win_h: int = 7, floor_h: int = 16, lit: float = 0.3,
		haze: float = 0.0, balconies: bool = False) -> None:
	bottom = PARAPET_TOP + 4
	if material == "brick":
		brick_wall(cv, x, top, w, bottom - top, rng, (8, 9, 10, 11))
	elif material == "redbrick":
		brick_wall(cv, x, top, w, bottom - top, rng, (9, 10, 11, 12))
	else: # штукатурка/бетон
		concrete(cv, x, top, w, bottom - top, rng, (4, 5, 6), speckle=0.08)
	# Об'єм: світла ліва грань, тінь справа, затемнення до низу (вологий бруд).
	cv.vline(x, top, bottom - top, 7)
	cv.vline(x + 1, top, bottom - top, 6 if material == "plaster" else 12)
	cv.rect(x + w - 3, top, 3, bottom - top, 1)
	cv.vline(x + w - 4, top, bottom - top, 2)
	# Карниз і парапет даху.
	metal_panel(cv, x - 2, top - 4, w + 4, 5, (0, 13, 14, 15), rivets=False)
	for xx in range(x - 2, x + w + 2):
		if rng.random() < 0.4:
			cv.put(xx, top + 1, 0) # краплі тіні під карнизом
	# Міжповерхові пояси.
	for fy in range(top + floor_h, bottom, floor_h):
		cv.hline(x + 2, fy, w - 5, 14 if material == "plaster" else 12)
		cv.hline(x + 2, fy + 1, w - 5, 1)
	# Вікна по поверхах.
	cols = max(1, (w - 8) // (win_w + 5))
	gap = (w - 4 - cols * win_w) / (cols + 1)
	for fy in range(top + 5, bottom - win_h - 2, floor_h):
		for c in range(cols):
			wx = int(x + 2 + gap * (c + 1) + c * win_w)
			roll = rng.random()
			state = "dark"
			if roll < lit * 0.55:
				state = "warm"
			elif roll < lit * 0.85:
				state = "dim"
			elif roll < lit:
				state = rng.choice(["tv", "blind"])
			window(cv, glow, wx, fy, win_w, win_h, state, rng)
			if balconies and (c % 2 == 0):
				# Балкон: плита й ґратки — об'єм перед фасадом.
				cv.hline(wx - 2, fy + win_h + 2, win_w + 4, 15)
				cv.hline(wx - 2, fy + win_h + 3, win_w + 4, 0)
				cv.hline(wx - 2, fy + win_h - 2, win_w + 4, 14)
				for xx in range(wx - 2, wx + win_w + 3, 2):
					cv.vline(xx, fy + win_h - 2, 4, 13)
	# Серпанок для дальших будинків і темрява біля землі (світло від неба падає згори).
	if haze > 0:
		cv.dither_rect(x, top - 4, w, bottom - top + 4, 4, haze)
	for i, yy in enumerate(range(bottom - 60, bottom)):
		cv.dither_rect(x, yy, w, 1, 1, i / 60 * 0.75)


def roof_clutter(cv: Canvas, rng: random.Random, x: int, top: int, w: int) -> None:
	"""Дахові дрібниці: кондиціонер, вентиляційний гриб, антени — живий силует."""
	if w > 40:
		ax = x + rng.randrange(4, w - 22)
		metal_panel(cv, ax, top - 12, 16, 9, (0, 13, 14, 15))
		cv.rect(ax + 4, top - 10, 7, 5, 13)
		for i in range(0, 7, 2):
			cv.vline(ax + 4 + i, top - 10, 5, 0) # решітка вентилятора
	vx = x + rng.randrange(2, max(3, w - 8))
	cv.rect(vx, top - 7, 3, 4, 14)
	cv.rect(vx - 1, top - 8, 5, 2, 15)
	if rng.random() < 0.6:
		mx = x + w - rng.randrange(6, 12)
		mh = rng.randrange(14, 30)
		cv.vline(mx, top - 4 - mh, mh, 14)
		cv.hline(mx - 3, top - 4 - mh + 4, 7, 14)
		layout["blinkers"].append([mx + CX, top - 4 - mh])


def make_mid(rng: random.Random) -> tuple[Canvas, Canvas]:
	cv = base(Canvas(W, H))
	glow = base(Canvas(W, H))
	# (x, верх, ширина, матеріал, частка світлих вікон, серпанок, (ширина, висота вікна, поверх), балкони)
	spec = [
		(-CX - 4, 132, 92, "redbrick", 0.22, 0.25, (5, 8, 16), False),
		(-26, 108, 90, "brick", 0.2, 0.0, (4, 9, 15), False),
		(64, 160, 78, "plaster", 0.16, 0.3, (6, 6, 14), True),
		# Прогалина 142..214 — видно естакаду й потяг.
		(214, 118, 214, "brick", 0.2, 0.0, (5, 8, 17), False), # будинок з вивіскою
		(428, 146, 96, "plaster", 0.08, 0.35, (6, 6, 14), True), # за меню — далі й темніший
		(524, 96, 96, "redbrick", 0.2, 0.0, (4, 10, 18), False),
		(620, 128, 140, "brick", 0.22, 0.2, (7, 7, 16), False),
	]
	for x, top, w, mat, lit, haze, (ww, wh, fh), balc in spec:
		building(cv, glow, rng, x, top, w, mat, win_w=ww, win_h=wh, floor_h=fh, lit=lit, haze=haze, balconies=balc)
		roof_clutter(cv, rng, x, top - 4, w)
	# Пожежні драбини на правому будинку — металевий силует поверх цегли.
	for fy in range(130, PARAPET_TOP, 24):
		cv.hline(530, fy, 34, 14)
		cv.hline(530, fy + 1, 34, 0)
		for xx in range(530, 564, 3):
			cv.vline(xx, fy - 6, 6, 13)
		cv.hline(530, fy - 6, 34, 14)
		for i in range(20):
			cv.put(536 + i, fy + 2 + i, 14)
			cv.put(537 + i, fy + 2 + i, 0)
	# Вертикальна неонова вивіска «БАР» на правому будинку (блакитна).
	vertical_sign(cv, glow, 150, 196, ["Б", "А", "Р"], neon=(26, 27, 28, 29), name="bar")
	return cv, glow


def vertical_sign(cv: Canvas, glow: Canvas, x: int, y: int, letters: list[str], neon: tuple[int, int, int, int], name: str) -> None:
	font = ImageFont.truetype(FONT, 8)
	h = len(letters) * 11 + 6
	metal_panel(cv, x, y, 14, h, (0, 1, 2, 4), rivets=False)
	cv.rect(x + 14, y + 4, 3, 2, 13) # кронштейн
	cv.rect(x + 14, y + h - 6, 3, 2, 13)
	halo, edge, core, hot = neon
	for i, ch in enumerate(letters):
		m = Image.new("L", (10, 10), 0)
		ImageDraw.Draw(m).text((1, 1), ch, font=font, fill=255)
		mp = m.load()
		for yy in range(10):
			for xx in range(10):
				if mp[xx, yy] > 128:
					glow.put(x + 2 + xx, y + 3 + i * 11 + yy, core)
	# Рамка-трубка навколо.
	for yy in range(y + 1, y + h - 1):
		glow.put(x + 1, yy, edge)
		glow.put(x + 12, yy, edge)
	glow.hline(x + 1, y + 1, 12, edge)
	glow.hline(x + 1, y + h - 2, 12, edge)
	layout["lights"].append({"name": name, "x": x + 7 + CX, "y": y + h // 2, "color": pal.HEX[core], "scale": 0.7, "flicker": True})


# --- Вивіска «БАНДА» -------------------------------------------------------------

def make_sign() -> tuple[Canvas, list[tuple[Canvas, int, int]]]:
	"""Великий неон на каркасі над центральним будинком. Літери окремо — кожна мерехтить сама."""
	frame = base(Canvas(W, H))
	font = ImageFont.truetype(FONT, 16)
	text = "БАНДА"
	scale = 2
	letter_w = 16 * scale
	gap = 6
	total = len(text) * letter_w + (len(text) - 1) * gap
	sx = 320 - total // 2
	sy = 34
	# Каркас: два ряди балок з розкосами від даху будинку (y=114) до вивіски.
	box = (sx - 10, sy - 8, total + 20, letter_w + 16)
	metal_panel(frame, *box, (0, 1, 2, 4))
	for i in range(box[0] + 4, box[0] + box[2] - 4, 6):
		frame.vline(i, box[1] + 2, box[3] - 4, 1) # ребра коробу
	for px in range(box[0] + 6, box[0] + box[2], 30):
		frame.vline(px, box[1] + box[3], 114 - box[1] - box[3], 14)
		frame.vline(px + 1, box[1] + box[3], 114 - box[1] - box[3], 0)
	for yy in (box[1] + box[3] + 6, 106):
		frame.hline(box[0] + 6, yy, box[2] - 10, 14)
	for px in range(box[0] + 6, box[0] + box[2] - 30, 30):
		for i in range(26):
			frame.put(px + 2 + i, box[1] + box[3] + 7 + i * (106 - box[1] - box[3] - 7) // 26, 13)
	letters: list[tuple[Canvas, int, int]] = []
	for i, ch in enumerate(text):
		m = Image.new("L", (18, 18), 0)
		ImageDraw.Draw(m).text((1, 1), ch, font=font, fill=255)
		m = m.resize((36, 36), Image.NEAREST)
		mp = m.load()
		lc = Canvas(48, 48)
		# Трубка: ядро (світле) всередині штриха, край — насичений рожевий; ореол — 2 кільця.
		mask = {(xx, yy) for yy in range(36) for xx in range(36) if mp[xx, yy] > 128}
		def near(p, r):
			return any((p[0] + dx, p[1] + dy) in mask for dx in range(-r, r + 1) for dy in range(-r, r + 1) if abs(dx) + abs(dy) <= r)
		for yy in range(-6, 42):
			for xx in range(-6, 42):
				p = (xx, yy)
				if p in mask:
					edge = any((xx + dx, yy + dy) not in mask for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
					lc.put(xx + 6, yy + 6, 24 if edge else 25)
				elif near(p, 1):
					lc.put(xx + 6, yy + 6, 23)
				elif near(p, 3) and BAYER4[yy % 4][xx % 4] < 6:
					lc.put(xx + 6, yy + 6, 22)
		lx = sx + i * (letter_w + gap) - 6 + CX
		letters.append((lc, lx, sy - 6 - 2))
		layout["letters"].append({"file": f"sign_letter_{i}.png", "x": lx, "y": sy - 8})
	layout["lights"].append({"name": "sign", "x": 320 + CX, "y": sy + 18, "color": pal.HEX[24], "scale": 3.2, "flicker": False})
	return frame, letters


# --- Наш дах -----------------------------------------------------------------------

def make_roof(rng: random.Random, letters) -> tuple[Canvas, Canvas]:
	cv = Canvas(W, H)
	glow = Canvas(W, H)
	# Парапет: цегла, бетонне покриття з виносом, тінь під ним.
	brick_wall(cv, 0, PARAPET_TOP + 5, W, ROOF_Y - PARAPET_TOP - 5, rng, (8, 9, 10, 11))
	cv.rect(0, PARAPET_TOP, W, 5, 14)
	cv.hline(0, PARAPET_TOP, W, 16)
	cv.hline(0, PARAPET_TOP + 1, W, 15)
	cv.hline(0, PARAPET_TOP + 4, W, 13)
	cv.hline(0, PARAPET_TOP + 5, W, 0)
	cv.dither_rect(0, PARAPET_TOP + 6, W, 2, 0, 0.5)
	for x in range(0, W, 32):
		cv.vline(x, PARAPET_TOP, 5, 13) # шви між плитами покриття
		cv.put(x + 1, PARAPET_TOP + 1, 16)
	for x in range(0, W):
		if rng.random() < 0.08:
			cv.vline(x, PARAPET_TOP + 6, rng.randrange(2, 6), 9) # патьоки від дощу
	# Дах — мокра бетонна плитка 16×16: темна, з холодним відблиском неба на верхніх краях.
	for ty in range(ROOF_Y, H, 16):
		for tx in range(0, W, 16):
			concrete(cv, tx, ty, 16, 16, rng, (2, 3, 4), speckle=0.07)
			cv.hline(tx, ty, 16, 1)
			cv.vline(tx, ty, 16, 1)
			cv.hline(tx + 1, ty + 1, 14, 5)
			cv.vline(tx + 1, ty + 1, 6, 4)
			if rng.random() < 0.18:
				cx, cy = tx + rng.randrange(3, 12), ty + rng.randrange(3, 12)
				for i in range(rng.randrange(3, 7)):
					cv.put(cx + i, cy + (i // 2) * rng.choice([1, -1]), 1)
	for i in range(8):
		cv.dither_rect(0, ROOF_Y + i, W, 1, 0, 0.9 - i * 0.11) # тінь від парапету
	# Велика калюжа під вивіскою: перевернуте, стиснуте й рване відображення «БАНДА».
	reflect_sign(cv, glow, letters, rng)
	for px, py, pw, ph, refl in [(40, 344, 54, 10, "sky"), (548, 322, 40, 6, "lamp"), (460, 350, 60, 8, "sky")]:
		puddle(cv, glow, px + CX, py, pw, ph, refl, rng)
	# Будка виходу на дах праворуч.
	hx, hy, hw = 604 + CX, 222, 150
	hb = ROOF_Y + 26
	brick_wall(cv, hx, hy, hw, hb - hy, rng, (8, 9, 10, 11))
	cv.vline(hx, hy, hb - hy, 12)
	cv.vline(hx + 1, hy, hb - hy, 11)
	for i, yy in enumerate(range(hb - 30, hb)):
		cv.dither_rect(hx, yy, hw, 1, 8, i / 30 * 0.7)
	metal_panel(cv, hx - 3, hy - 6, hw + 6, 7, (0, 13, 14, 15), rivets=False)
	cv.dither_rect(hx, hy + 1, hw, 3, 0, 0.6)
	# Двері: глибокий проріз, металеве полотно з рельєфом, віконце зі світлом.
	dx, dtop = hx + 16, hy + 26
	cv.rect(dx - 3, dtop - 3, 28, hb - dtop + 3, 0)
	cv.hline(dx - 3, dtop - 3, 28, 12)
	metal_panel(cv, dx, dtop, 22, hb - dtop, (0, 13, 14, 15))
	metal_panel(cv, dx + 3, dtop + 20, 16, 18, (13, 13, 14, 15), rivets=False)
	cv.rect(dx + 17, dtop + 26, 2, 5, 16) # ручка
	cv.rect(dx + 4, dtop + 4, 14, 10, 0)
	glow.rect(dx + 5, dtop + 5, 12, 8, 18)
	glow.hline(dx + 5, dtop + 5, 12, 19)
	glow.vline(dx + 5, dtop + 5, 8, 19)
	# Електрощиток і труба на стіні будки.
	metal_panel(cv, hx + 50, hy + 34, 14, 18, (0, 13, 14, 15))
	glow.put(hx + 53, hy + 38, 31) # зелений світлодіод
	cv.rect(hx + 72, hy - 2, 4, hb - hy + 2, 14)
	cv.vline(hx + 72, hy - 2, hb - hy + 2, 15)
	cv.vline(hx + 75, hy - 2, hb - hy + 2, 0)
	# Лампа над дверима: кронштейн, плафон-«тарілка», гаряча нитка.
	lx, ly = dx + 11, hy + 12
	cv.hline(lx - 7, ly - 5, 9, 14)
	cv.vline(lx - 7, ly - 5, 7, 14)
	cv.rect(lx - 5, ly, 11, 2, 13)
	cv.hline(lx - 4, ly - 1, 9, 15)
	glow.hline(lx - 3, ly + 2, 7, 20)
	glow.hline(lx - 1, ly + 3, 3, 21)
	layout["lights"].append({"name": "bulb", "x": lx, "y": ly + 3, "color": pal.HEX[20], "scale": 2.6, "flicker": True, "shadow": True})
	# Вентиляційний «гриб» з парою біля банди.
	vx, vy = 396 + CX, 304
	metal_panel(cv, vx, vy - 14, 12, 16, (0, 13, 14, 15), rivets=False)
	cv.rect(vx - 3, vy - 18, 18, 4, 14)
	cv.hline(vx - 3, vy - 18, 18, 16)
	cv.hline(vx - 3, vy - 15, 18, 0)
	cv.dither_rect(vx - 2, vy + 2, 18, 3, 0, 0.6) # тінь під ним
	layout["steam"].append([vx + 6, vy - 20])
	return cv, glow


def reflect_sign(cv: Canvas, glow: Canvas, letters, rng: random.Random) -> None:
	"""Калюжа під вивіскою з відображенням літер — найсильніший «живий» акцент даху."""
	import math
	x0, y0, pw, ph = 214 + CX, 318, 220, 22
	for yy in range(ph):
		k = (yy - ph / 2) / (ph / 2)
		half = int(pw / 2 * math.sqrt(max(0.0, 1 - k * k)) * (0.85 + 0.15 * math.sin(yy * 1.7)))
		for xx in range(-half, half):
			cv.put(x0 + pw // 2 + xx, y0 + yy, 0 if abs(xx) < half - 1 else 2)
	sign = Image.new("RGBA", (W, H), (0, 0, 0, 0))
	for lc, lx, ly in letters:
		sign.alpha_composite(lc.img, (lx, ly))
	sp = sign.load()
	sign_top, sign_bottom = 26, 74
	for yy in range(ph):
		k = (yy - ph / 2) / (ph / 2)
		half = int(pw / 2 * math.sqrt(max(0.0, 1 - k * k)) * (0.85 + 0.15 * math.sin(yy * 1.7)))
		if yy % 2 == 1:
			continue # брижі від дощу: кожен другий рядок порожній
		src_y = sign_bottom - int(yy / ph * (sign_bottom - sign_top)) # перевернуто
		shift = rng.randrange(-1, 2)
		for xx in range(-half + 1, half - 1):
			sx = x0 + pw // 2 + xx + shift
			c = sp[sx, src_y]
			if c[3] > 0:
				glow.px[x0 + pw // 2 + xx, y0 + yy] = c


def puddle(cv: Canvas, glow: Canvas, x: int, y: int, w: int, h: int, refl: str, rng: random.Random) -> None:
	import math
	for yy in range(h):
		half = int(w / 2 * math.sqrt(max(0.0, 1 - ((yy - h / 2) / (h / 2)) ** 2)))
		for xx in range(-half, half):
			cv.put(x + w // 2 + xx, y + yy, 1)
			if yy % 2 == 0 and rng.random() < 0.55 and abs(xx) < half - 2:
				if refl == "lamp":
					glow.put(x + w // 2 + xx, y + yy, 20 if rng.random() < 0.3 else 18)
				elif rng.random() < 0.35:
					cv.put(x + w // 2 + xx, y + yy, 5) # відблиск хмар
		if half > 1:
			cv.put(x + w // 2 - half, y + yy, 4)


# --- Передній план -----------------------------------------------------------------

def make_fg(rng: random.Random) -> Canvas:
	"""Впритул до камери: дерев'яний бак для води на сталевих ногах (зліва, обрізаний кадром)
	і кабель, що провисає через кадр. Майже силует — найтемніший план, лише кант світла."""
	import math
	cv = Canvas(W, H)
	tx, tw, ty, th = CX - 64, 150, 34, 150 # бак: ліва частина за межами базового кадру
	# Ноги й розкоси.
	for lx in (tx + 14, tx + tw - 18):
		cv.rect(lx, ty + th, 6, H - ty - th, 1)
		cv.vline(lx, ty + th, H - ty - th, 5)
	for i in range(0, H - ty - th - 4):
		cv.put(tx + 20 + i * (tw - 40) // (H - ty - th), ty + th + i, 2)
	cv.rect(tx + 4, ty + th - 2, tw - 8, 6, 1)
	cv.hline(tx + 4, ty + th - 2, tw - 8, 5)
	# Корпус з вертикальних дощок: кожна дошка — світла ліва грань, основа, темний стик.
	for x in range(tx, tx + tw):
		t = (x - tx) / tw
		bulge = int(3 * math.sin(math.pi * t))
		stave = (x - tx) % 6
		light_side = t < 0.25
		for y in range(ty - bulge, ty + th):
			if stave == 0:
				c = 8
			elif stave == 1:
				c = 11 if light_side else 10
			elif stave == 5:
				c = 9 if light_side else 8
			else:
				c = 10 if light_side else 9
			if rng.random() < 0.03:
				c = 8 # сучки й плями
			cv.put(x, y, c)
	# Рожевий кант від вивіски на правому краї бака.
	for y in range(ty, ty + th):
		cv.put(tx + tw - 1, y, 23)
		cv.put(tx + tw - 2, y, 9)
	for hy in (ty + 18, ty + 66, ty + 114):
		for x in range(tx - 1, tx + tw + 1):
			t = (x - tx) / tw
			cv.put(x, hy, 16 if t < 0.12 else (15 if t < 0.4 else 14))
			cv.put(x, hy + 1, 13)
			cv.put(x, hy + 2, 0)
		cv.put(tx + tw // 3, hy + 1, 16) # болт-стяжка
	# Конусний дах бака.
	for i in range(30):
		hw = tw // 2 + 4 - int(i * (tw // 2 + 4) / 30)
		cv.hline(tx + tw // 2 - hw, ty - 4 - i, hw * 2, 2 if i % 3 else 3)
		cv.put(tx + tw // 2 - hw, ty - 4 - i, 6)
	# Темніє донизу — передній план найтемніший.
	for i, y in enumerate(range(ty + 40, ty + th)):
		cv.dither_rect(tx, y, tw, 1, 0, i / (th - 40) * 0.55)
	# Кабель від бака до будки з провисанням (ланцюгова лінія).
	x0, y0, x1, y1 = tx + tw, ty + 30, 604 + CX + 20, 220
	for x in range(x0, x1):
		t = (x - x0) / (x1 - x0)
		y = y0 + (y1 - y0) * t + 44 * math.sin(math.pi * t)
		cv.put(x, int(y), 0)
		cv.put(x, int(y) + 1, 1)
	return cv


def make_fx() -> None:
	"""Дрібні текстури для частинок і вогників."""
	fx = os.path.join(ROOT, "assets", "sprites", "fx")
	b = Canvas(3, 3)
	for x, y in ((1, 0), (0, 1), (2, 1), (1, 2)):
		b.put(x, y, 23)
	b.put(1, 1, 25)
	b.save(os.path.join(fx, "blinker.png"))
	sp = Canvas(5, 3) # бризок краплі: «корона»
	for x, y in ((0, 2), (4, 2), (1, 1), (3, 1), (2, 0)):
		sp.put(x, y, 15)
	sp.put(2, 2, 16)
	sp.save(os.path.join(fx, "splash.png"))
	pf = Canvas(6, 6) # клубок пари: щільний центр, дизерингові краї
	for y in range(6):
		for x in range(6):
			d = abs(x - 2.5) + abs(y - 2.5)
			if d < 2:
				pf.put(x, y, 16)
			elif d < 3.5 and BAYER4[y % 4][x % 4] < 9:
				pf.put(x, y, 15)
	pf.save(os.path.join(fx, "puff.png"))
	px1 = Canvas(1, 1)
	px1.put(0, 0, 16)
	px1.save(os.path.join(fx, "pixel.png"))


def save(cv: Canvas, name: str) -> None:
	cv.save(os.path.join(OUT, name))


def main() -> None:
	os.makedirs(OUT, exist_ok=True)
	rng = random.Random(11)
	save(make_sky(), "sky.png")
	save(make_clouds(random.Random(3)), "clouds.png")
	save(make_far(random.Random(5)), "far.png")
	viaduct, train = make_viaduct(random.Random(7))
	save(viaduct, "viaduct.png")
	save(train, "train.png")
	save(make_train_glow(), "train_glow.png")
	mid, mid_glow = make_mid(random.Random(9))
	save(mid, "mid.png")
	save(mid_glow, "mid_glow.png")
	frame, letters = make_sign()
	save(frame, "sign_frame.png")
	for i, (lc, _x, _y) in enumerate(letters):
		save(lc, f"sign_letter_{i}.png")
	roof, roof_glow = make_roof(random.Random(13), letters)
	save(roof, "roof.png")
	save(roof_glow, "roof_glow.png")
	save(make_fg(rng), "fg.png")
	layout["viaduct_y"] = 214 - 16
	layout["gang"] = {"hood": [196 + CX, 276 - 47], "coat": [244 + CX, 326 - 49], "phone": [272 + CX, 324 - 45]}
	make_fx()
	with open(os.path.join(OUT, "menu_layout.json"), "w", encoding="utf-8") as f:
		json.dump(layout, f, ensure_ascii=False, indent=1)
	preview(letters)
	print("ok")


def preview(letters) -> None:
	"""Плаский попередній перегляд без освітлення Godot — лише для швидких ітерацій."""
	names = ["sky", "clouds", "far", "viaduct", "mid", "mid_glow", "sign_frame", "roof", "roof_glow", "fg"]
	img = Image.new("RGBA", (W, H), (0, 0, 0, 255))
	for n in names:
		layer = Image.open(os.path.join(OUT, n + ".png"))
		img.alpha_composite(layer)
		if n == "sign_frame":
			for lc, x, y in letters:
				img.alpha_composite(lc.img, (x, y))
	img.save(os.path.join(os.path.dirname(__file__), "menu_preview.png"))


if __name__ == "__main__":
	main()
