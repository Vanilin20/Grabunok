"""Генератор фону головного меню: нічне місто під дощем (піксель-арт 640×360).

Запуск з кореня репозиторію:  python tools/art/gen_menu_city.py
Результат: assets/sprites/menu/city_far.png (небо й далекі будинки)
           assets/sprites/menu/city_near.png (ближні будинки й вулиця, прозоре небо)

Фон генерується кодом, щоб його можна було відтворити й правити числами.
Кольори — лише з робочої палітри (scripts/systems/palette.gd + відтінки нижче).
Світло падає з одного напрямку: місяць/заграва зліва-згори, тому світлий край будинків — лівий.
"""
from __future__ import annotations

import os
import random

from PIL import Image

W, H = 864, 360 # 864 = ширина до 2.4:1; центральні 640 px — базовий кадр
OX = 0 # зсув малювання по x (центральна частина малюється в координатах 640-кадру)
CENTER_OFFSET = (W - 640) // 2
SEED = 7

# --- Палітра (тимчасова, див. Palette) ---------------------------------------
NIGHT = (10, 9, 18)
SKY = [(10, 9, 18), (14, 11, 26), (19, 14, 34), (27, 17, 43), (40, 21, 54), (58, 26, 66)]
HAZE_BODY = (31, 20, 46)
HAZE_RIM = (40, 26, 56)
FAR_BODY = (19, 15, 31)
FAR_RIM = (30, 24, 46)
FAR_WIN = [(52, 42, 70), (86, 66, 52), (40, 70, 86)]
MID_BODY = (14, 12, 23)
MID_RIM = (30, 27, 46)
WIN_OFF = (22, 19, 36)
WIN_WARM = (242, 193, 78)
WIN_WARM_DIM = (168, 118, 52)
WIN_CYAN = (61, 224, 255)
WIN_CYAN_DIM = (36, 112, 140)
NEON_PINK = (255, 61, 127)
NEON_PINK_HALO = (96, 26, 60)
NEON_CYAN = (61, 224, 255)
NEON_CYAN_HALO = (22, 70, 92)
ROAD = (12, 10, 20)
ROAD_LINE = (40, 36, 56)
CURB = (30, 27, 46)
CURB_TOP = (46, 42, 66)
LAMP_POST = (26, 23, 40)
LAMP_HEAD = (255, 236, 170)

HORIZON = 250 # де закінчується небо за ближніми будинками
STREET_Y = 300 # верхній край тротуару


def put(img: Image.Image, x: int, y: int, c: tuple) -> None:
	x += OX
	if 0 <= x < W and 0 <= y < H:
		img.putpixel((x, y), c + (255,) if len(c) == 3 else c)


def rect(img: Image.Image, x: int, y: int, w: int, h: int, c: tuple) -> None:
	for yy in range(y, y + h):
		for xx in range(x, x + w):
			put(img, xx, yy, c)


def sky(img: Image.Image) -> None:
	# Смуги неба з упорядкованим дизерингом на межах — без плавних градієнтів.
	bayer = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
	top, bottom = 0, HORIZON
	rect(img, 0, HORIZON, W, H - HORIZON, SKY[-1])
	n = len(SKY)
	for y in range(top, bottom):
		t = (y - top) / (bottom - top)
		t = t ** 1.7 # більше темного зверху, заграва лише біля обрію
		f = t * (n - 1)
		i = int(f)
		frac = f - i
		for x in range(W):
			c = SKY[min(i + (1 if frac * 16 > bayer[y % 4][x % 4] else 0), n - 1)]
			put(img, x, y, c)
	# Низькі хмари, підсвічені містом знизу: рвані смуги з горбистим верхом і дизерингом по краях.
	import math
	crng = random.Random(SEED + 5)
	for band_y, thickness, color, alpha_rows in [(120, 7, (24, 16, 38), 2), (158, 10, (33, 19, 47), 3), (196, 12, (46, 24, 60), 3)]:
		phase = crng.uniform(0, 6.28)
		x = -20
		while x < W:
			length = crng.randrange(60, 170)
			for xx in range(x, x + length):
				t = (xx - x) / length
				envelope = math.sin(t * math.pi) # хмара тонша до країв
				bump = math.sin(xx * 0.09 + phase) * 2 + math.sin(xx * 0.23 + phase * 2) * 1.2
				top = band_y - int(thickness * envelope * 0.6 + bump)
				bottom = band_y + int(thickness * envelope * 0.4)
				for yy in range(top, bottom):
					inner = min(yy - top, bottom - 1 - yy)
					if inner >= alpha_rows or bayer[yy % 4][xx % 4] < (inner + 1) * 16 // (alpha_rows + 1):
						put(img, xx, yy, color)
			x += length + crng.randrange(20, 80)
	# Рідкі зорі (за хмарами їх майже не видно — місто світиться).
	rng = random.Random(SEED + 1)
	for _ in range(26):
		x, y = rng.randrange(W), rng.randrange(4, 110)
		put(img, x, y, (58, 52, 84) if rng.random() < 0.8 else (110, 102, 140))


def far_buildings(img: Image.Image) -> None:
	# Два далекі плани: найдальший тоне в заграві, другий — темніший і з рідкими вікнами.
	for layer, (body, rim, base, hmin, hmax, win_chance) in enumerate([
		(HAZE_BODY, HAZE_RIM, HORIZON - 6, 30, 90, 0.0),
		(FAR_BODY, FAR_RIM, HORIZON + 12, 36, 104, 0.11),
	]):
		rng = random.Random(SEED + 2 + layer * 13)
		x = -8 + layer * 9
		while x < W:
			w = rng.randrange(14, 40)
			h = rng.randrange(hmin, hmax)
			top = base - h
			rect(img, x, top, w, H - top, body)
			# Уступ на даху у частини будинків — силует не «коробковий».
			if rng.random() < 0.4 and w > 20:
				sw = rng.randrange(6, w - 8)
				sx = x + rng.randrange(2, w - sw - 1)
				rect(img, sx, top - rng.randrange(4, 12), sw, 12, body)
			for yy in range(top, H):
				put(img, x, yy, rim)
			if rng.random() < 0.3:
				ax = x + rng.randrange(3, max(4, w - 3))
				ah = rng.randrange(6, 18)
				for yy in range(top - ah, top):
					put(img, ax, yy, body)
				if layer == 1 and rng.random() < 0.7:
					put(img, ax, top - ah, NEON_PINK) # червоний вогник на антені
			if win_chance > 0:
				for wy in range(top + 4, HORIZON + 14, 4):
					for wx in range(x + 3, x + w - 2, 3):
						if rng.random() < win_chance:
							put(img, wx, wy, rng.choice(FAR_WIN))
			x += w + rng.randrange(-3, 4)


def mid_building(img: Image.Image, rng: random.Random, x: int, w: int, top: int, accent: str) -> None:
	rect(img, x, top, w, STREET_Y - top, MID_BODY)
	for yy in range(top, STREET_Y):
		put(img, x, yy, MID_RIM)
		put(img, x + 1, yy, MID_RIM if yy % 2 == 0 else MID_BODY) # м'який перехід краю, без «подушки»
	for xx in range(x, x + w):
		put(img, xx, top, MID_RIM)
	# Карниз.
	rect(img, x - 1, top + 3, w + 2, 2, MID_RIM)
	# Вікна 3×4, двома рядами з рамою.
	cols = max(1, (w - 6) // 7)
	pad = (w - cols * 7 + 3) // 2
	for wy in range(top + 10, STREET_Y - 18, 11):
		for ci in range(cols):
			wx = x + pad + ci * 7
			roll = rng.random()
			if roll < 0.11:
				body, edge = WIN_WARM, WIN_WARM_DIM
			elif roll < 0.18:
				body, edge = WIN_WARM_DIM, WIN_OFF # світло десь углибині кімнати
			elif roll < 0.22 and accent == "cyan":
				body, edge = WIN_CYAN, WIN_CYAN_DIM
			else:
				body, edge = WIN_OFF, WIN_OFF
			rect(img, wx, wy, 4, 5, body)
			if body != WIN_OFF:
				# Нижній рядок вікна темніший — штора/підвіконня, і силует людини в деяких.
				rect(img, wx, wy + 4, 4, 1, edge)
				if rng.random() < 0.25:
					put(img, wx + 1, wy + 2, edge)
					put(img, wx + 1, wy + 3, edge)
					put(img, wx + 2, wy + 3, edge)


def neon_sign_vertical(img: Image.Image, x: int, y: int, h: int, color: tuple, halo: tuple) -> None:
	# Вертикальна вивіска: рамка й «літери» короткими штрихами.
	for yy in range(y - 2, y + h + 2):
		for xx in range(x - 2, x + 9):
			put(img, xx, yy, halo)
	rect(img, x, y, 7, h, MID_BODY)
	for yy in range(y, y + h):
		put(img, x, yy, color)
		put(img, x + 6, yy, color)
	for xx in range(x, x + 7):
		put(img, xx, y, color)
		put(img, xx, y + h - 1, color)
	for gy in range(y + 3, y + h - 4, 7):
		rect(img, x + 2, gy, 3, 1, color)
		put(img, x + 2, gy + 1, color)
		put(img, x + 4, gy + 2, color)
		rect(img, x + 2, gy + 3, 3, 1, color)


def neon_sign_horizontal(img: Image.Image, x: int, y: int, w: int, color: tuple, halo: tuple) -> None:
	for yy in range(y - 2, y + 9):
		for xx in range(x - 2, x + w + 2):
			put(img, xx, yy, halo)
	rect(img, x, y, w, 7, MID_BODY)
	for xx in range(x, x + w):
		put(img, xx, y, color)
		put(img, xx, y + 6, color)
	for yy in range(y, y + 7):
		put(img, x, yy, color)
		put(img, x + w - 1, yy, color)
	# Стрілка-покажчик праворуч, як у старих вивісках барів.
	for i in range(3):
		put(img, x + w - 6 + i, y + 2 + i if i < 2 else y + 3, color)
	for xx in range(x + 3, x + w - 7, 2):
		put(img, xx, y + 3, color)


def near_layer(img: Image.Image) -> None:
	global OX
	rng = random.Random(SEED + 3)
	OX = 0
	# Тротуар і бордюр.
	rect(img, 0, STREET_Y, W, 8, CURB)
	rect(img, 0, STREET_Y, W, 1, CURB_TOP)
	for xx in range(0, W, 24):
		put(img, xx, STREET_Y + 1, ROAD)
		put(img, xx, STREET_Y + 2, ROAD)
	rect(img, 0, STREET_Y + 8, W, H - STREET_Y - 8, ROAD)
	rect(img, 0, STREET_Y + 8, W, 1, (20, 17, 30))
	# Розмітка.
	for xx in range(10, W, 40):
		rect(img, xx, 334, 18, 1, ROAD_LINE)
	# Крайні будинки для широких екранів (за межами базових 640 px).
	edge_rng = random.Random(SEED + 9)
	for x, w, top, accent in [(0, 60, 104, "warm"), (58, 58, 140, "cyan"), (CENTER_OFFSET + 636, 60, 128, "warm"), (CENTER_OFFSET + 694, 60, 96, "cyan")]:
		mid_building(img, edge_rng, x, w, top, accent)
	lamp_post(img, 40)
	lamp_post(img, CENTER_OFFSET + 700)
	OX = CENTER_OFFSET
	# Будинки ближнього плану: високі по краях, нижчі в центрі, щоб лишити місце назві й небу.
	layout = [
		(-4, 70, 120, "warm"), (64, 44, 176, "cyan"), (106, 52, 196, "warm"),
		(416, 48, 188, "warm"), (462, 70, 150, "cyan"), (530, 40, 118, "warm"), (568, 76, 92, "cyan"),
	]
	for x, w, top, accent in layout:
		mid_building(img, rng, x, w, top, accent)
	# Пожежні драбини на одному з фасадів (силует).
	for fy in range(200, STREET_Y - 10, 22):
		rect(img, 474, fy, 30, 1, MID_RIM)
		for xx in range(474, 504, 3):
			put(img, xx, fy - 1, MID_RIM)
		for i in range(18):
			put(img, 478 + i, fy + 2 + i, MID_RIM)
	# Неонові вивіски.
	neon_sign_vertical(img, 96, 150, 48, NEON_PINK, NEON_PINK_HALO)
	neon_sign_horizontal(img, 430, 262, 34, NEON_CYAN, NEON_CYAN_HALO)
	neon_sign_vertical(img, 556, 132, 34, NEON_CYAN, NEON_CYAN_HALO)
	# Магазинчик на розі ліворуч: вітрина, навіс, світло на тротуар.
	rect(img, 6, 262, 58, 38, (34, 24, 22))
	rect(img, 9, 268, 34, 30, (150, 104, 48))
	rect(img, 9, 268, 34, 2, (198, 146, 66)) # лампа під стелею
	for sy in (277, 286, 295):
		rect(img, 9, sy, 34, 1, (82, 56, 30)) # полиці
		for bx in range(11, 42, 4):
			if (bx + sy) % 3:
				rect(img, bx, sy - 3, 2, 3, (106, 72, 38))
	rect(img, 47, 268, 13, 32, (24, 18, 18)) # двері
	rect(img, 49, 271, 9, 12, (96, 66, 34))
	for i, ax in enumerate(range(4, 66, 6)):
		rect(img, ax, 256, 6, 5, (122, 34, 58) if i % 2 == 0 else (52, 18, 34)) # смугастий навіс
	rect(img, 4, 261, 62, 1, (30, 14, 24))
	for yy in range(STREET_Y, STREET_Y + 8):
		for xx in range(6, 64):
			if (xx + yy) % 2 == 0:
				put(img, xx, yy, (78, 58, 40))
	reflections(img)
	lamp_post(img, 186)
	lamp_post(img, 380)
	OX = 0


def reflections(img: Image.Image) -> None:
	# Мокрий асфальт: кожне джерело світла дає стовпчик з горизонтальних штрихів,
	# який рветься й розширюється ближче до глядача (брижі від дощу).
	rng = random.Random(SEED + 4)
	sources = [(99, 7, NEON_PINK_HALO, NEON_PINK), (430, 34, NEON_CYAN_HALO, NEON_CYAN),
		(556, 7, NEON_CYAN_HALO, NEON_CYAN), (14, 42, (78, 54, 28), (170, 118, 52)),
		(194, 3, (96, 84, 54), LAMP_HEAD), (388, 3, (96, 84, 54), LAMP_HEAD)]
	for sx, sw, dim, bright in sources:
		cx = sx + sw / 2
		for y in range(STREET_Y + 9, H, 2):
			depth = (y - STREET_Y - 9) / (H - STREET_Y - 9)
			if rng.random() < 0.15 + depth * 0.45:
				continue # розрив у відблиску
			half = sw / 2 * (0.7 + depth * 0.6) * rng.uniform(0.4, 1.0)
			jitter = rng.uniform(-1.5, 1.5) * (1 + depth * 2)
			x0, x1 = int(cx - half + jitter), int(cx + half + jitter)
			for x in range(x0, max(x0 + 1, x1)):
				put(img, x, y, bright if depth < 0.25 and abs(x - cx) < sw * 0.2 + 1 else dim)


def lamp_post(img: Image.Image, x: int) -> None:
	# Стовп ліхтаря; саме світло — PointLight2D у сцені, тут лише пляма на тротуарі.
	bayer = [[0, 2], [3, 1]]
	for yy in range(STREET_Y, STREET_Y + 8):
		spread = 10 + (yy - STREET_Y) * 2
		for xx in range(x + 9 - spread, x + 9 + spread):
			if bayer[yy % 2][xx % 2] < 2 - abs(xx - x - 9) / spread * 2:
				put(img, xx, yy, (70, 62, 66) if yy == STREET_Y else (52, 46, 58))
	rect(img, x, 210, 2, STREET_Y - 210, LAMP_POST)
	rect(img, x - 1, STREET_Y - 5, 4, 5, LAMP_POST)
	rect(img, x - 1, 226, 4, 2, LAMP_POST)
	rect(img, x, 208, 9, 2, LAMP_POST)
	rect(img, x + 5, 210, 8, 3, LAMP_POST)
	rect(img, x + 6, 213, 6, 1, LAMP_HEAD)
	put(img, x + 5, 210, MID_RIM) # блік зліва — світло з того ж боку, що й на будинках


def main() -> None:
	root = os.path.join(os.path.dirname(__file__), "..", "..")
	out_dir = os.path.join(root, "assets", "sprites", "menu")
	os.makedirs(out_dir, exist_ok=True)
	far = Image.new("RGBA", (W, H), (0, 0, 0, 0))
	sky(far)
	far_buildings(far)
	far.save(os.path.join(out_dir, "city_far.png"))
	near = Image.new("RGBA", (W, H), (0, 0, 0, 0))
	near_layer(near)
	near.save(os.path.join(out_dir, "city_near.png"))
	preview = far.copy()
	preview.alpha_composite(near)
	preview.save(os.path.join(os.path.dirname(__file__), "menu_city_preview.png"))  # поза ресурсами гри (tools/ має .gdignore)
	print("ok:", out_dir)


if __name__ == "__main__":
	main()
