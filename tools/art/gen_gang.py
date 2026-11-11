"""Банда на даху (вид зі спини на три чверті) — три персонажі ~64–70 px, idle по 8 кадрів.

Запуск: python tools/art/gen_gang.py → assets/sprites/menu/gang_*.png (кадри в ряд) і *_silhouette.png.

Будова: фігура малюється «мітками» матеріалів (плащ, куртка, худі, шкіра, волосся, сумка, метал),
далі — автоматичне затінення за стайлгайдом:
  - ключове світло зліва-згори: лівий край на тон світліший, кут лівий-верхній ще світліший;
  - контржур від вивіски «БАНДА» попереду-праворуч: верхні й праві краї голови, плечей, рук — рожеві;
  - складки й шви — найтемніший тон рампи; без «подушки» (тон не залежить від відстані до центру);
  - контур NIGHT_0, щоб силует читався на будь-якому тлі.
Анімація (8 кадрів): дихання (плечі й голова на 1 px), поли плаща й хвіст на вітрі,
погляд убік (дівчина повертає голову), промінь ліхтарика гойдається (світло — у сцені).
"""
from __future__ import annotations

import math
import os

from PIL import Image, ImageDraw

import palette as pal

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "sprites", "menu")
FRAMES = 8
FW, FH = 48, 76 # кадр
BREATH = [0, 0, 1, 1, 1, 1, 0, 0]
WIND = [0, 1, 2, 3, 3, 2, 1, 0]
GLANCE = [0, 0, 0, 1, 2, 2, 1, 0] # дівчина: 0 — дивиться на вивіску, 2 — озирається праворуч

# Матеріали: рампа від темного до світлого (індекси палітри).
RAMPS = {
	"coat": [0, 0, 1, 2, 3],
	"jacket": [0, 0, 1, 26, 27],
	"hoodie": [0, 1, 2, 13, 14],
	"pants": [0, 0, 1, 2, 2],
	"jeans": [0, 0, 1, 26, 26],
	"shoe": [0, 0, 1, 2, 2],
	"skin": [9, 10, 11, 12, 12],
	"hair": [8, 9, 10, 11, 11],
	"cap": [0, 2, 3, 4, 5],
	"bag": [8, 9, 10, 11, 12],
	"metal": [0, 13, 15, 16, 16],
	"strap": [0, 8, 9, 9, 9],
}
EMISSIVE = {"lens": 21, "phone": 29, "phone_edge": 28, "ember": 20}


class Figure:
	def __init__(self):
		self.lab = Image.new("P", (FW, FH), 0)
		self.names = ["."]
		self.d = ImageDraw.Draw(self.lab)
		self.seams: list[tuple[int, int]] = []

	def _id(self, name: str) -> int:
		if name not in self.names:
			self.names.append(name)
		return self.names.index(name)

	def ellipse(self, box, name):
		self.d.ellipse(box, fill=self._id(name))

	def poly(self, pts, name):
		self.d.polygon([(round(x), round(y)) for x, y in pts], fill=self._id(name))

	def rect(self, box, name):
		self.d.rectangle(box, fill=self._id(name))

	def px(self, x, y, name):
		if 0 <= x < FW and 0 <= y < FH:
			self.lab.putpixel((x, y), self._id(name))

	def seam(self, pts):
		self.seams += [(int(x), int(y)) for x, y in pts]

	def line_seam(self, x0, y0, x1, y1):
		n = max(abs(x1 - x0), abs(y1 - y0)) + 1
		for i in range(n):
			t = i / max(1, n - 1)
			self.seams.append((round(x0 + (x1 - x0) * t), round(y0 + (y1 - y0) * t)))

	def render(self, rim_until: float = 0.55) -> Image.Image:
		lab = self.lab.load()
		name = lambda x, y: self.names[lab[x, y]] if 0 <= x < FW and 0 <= y < FH else "."
		filled = lambda x, y: name(x, y) != "."
		ys = [y for y in range(FH) for x in range(FW) if filled(x, y)]
		top, bottom = min(ys), max(ys)
		rim_y = top + (bottom - top) * rim_until
		img = Image.new("RGBA", (FW, FH), (0, 0, 0, 0))
		out = img.load()
		seams = set(self.seams)
		for y in range(FH):
			for x in range(FW):
				n = name(x, y)
				if n == ".":
					continue
				if n in EMISSIVE:
					out[x, y] = pal.P[EMISSIVE[n]] + (255,)
					continue
				ramp = RAMPS[n]
				c = ramp[2]
				up, left, right = filled(x, y - 1), filled(x - 1, y), filled(x + 1, y)
				# Межа між різними матеріалами — тінь на нижньому/правому.
				if up and name(x, y - 1) != n and name(x, y - 1) not in EMISSIVE:
					c = ramp[1]
				if not left:
					c = ramp[3] if not up else ramp[3 if y < rim_y else 2]
				elif not filled(x - 2, y) and y < rim_y:
					c = max(c, ramp[3]) if ramp[3] != ramp[2] else c
				if (x, y) in seams:
					c = ramp[1]
				if not right:
					c = 24 if y < rim_y else (23 if y < rim_y + 10 else ramp[1])
				if not up:
					c = 24 if y < rim_y else ramp[3]
					if not filled(x, y - 1) and not right and y < rim_y:
						c = 25 # найгарячіша точка контржуру
				out[x, y] = pal.P[c] + (255,)
		# Контур.
		src = img.copy().load()
		for y in range(FH):
			for x in range(FW):
				if src[x, y][3]:
					continue
				for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
					nx, ny = x + dx, y + dy
					if 0 <= nx < FW and 0 <= ny < FH and src[nx, ny][3]:
						out[x, y] = pal.P[0] + (255,)
						break
		return img


def leader(f: int) -> Image.Image:
	"""Ватажок: довгий плащ, кашкет, у правій руці ліхтарик (промінь — світло у сцені)."""
	g = Figure()
	b = BREATH[f]
	w = WIND[f]
	cx = 22
	# Ноги й черевики.
	g.rect((cx - 7, 58, cx - 3, 71), "pants")
	g.rect((cx + 2, 58, cx + 6, 71), "pants")
	g.rect((cx - 8, 72, cx - 2, 73), "shoe")
	g.rect((cx + 2, 72, cx + 8, 73), "shoe")
	# Плащ: плечі → талія → поли, права пола на вітрі.
	g.poly([(cx - 11, 22 - b), (cx + 11, 22 - b), (cx + 10, 40), (cx + 13 + w, 61), (cx + 9 + w // 2, 63),
		(cx - 12, 63), (cx - 13, 60), (cx - 10, 40)], "coat")
	g.ellipse((cx - 13, 20 - b, cx - 6, 27 - b), "coat") # ліве плече
	g.ellipse((cx + 6, 20 - b, cx + 13, 27 - b), "coat") # праве плече
	# Ліва рука вздовж тіла, права — вперед з ліхтариком.
	g.poly([(cx - 14, 25 - b), (cx - 10, 25 - b), (cx - 10, 46), (cx - 14, 46)], "coat")
	g.rect((cx - 14, 47, cx - 11, 49), "skin")
	g.poly([(cx + 10, 25 - b), (cx + 14, 25 - b), (cx + 17, 38), (cx + 21, 42), (cx + 19, 45), (cx + 13, 41)], "coat")
	g.rect((cx + 19, 42, cx + 21, 44), "skin")
	g.rect((cx + 21, 43, cx + 25, 45), "metal")
	g.px(cx + 26, 44, "lens")
	g.px(cx + 26, 43, "lens")
	# Комір, потилиця, щока (на три чверті видно краєчок обличчя праворуч).
	g.poly([(cx - 6, 18 - b), (cx + 6, 18 - b), (cx + 7, 23 - b), (cx - 7, 23 - b)], "coat")
	g.ellipse((cx - 6, 7 - b, cx + 6, 20 - b), "hair")
	g.rect((cx + 4, 13 - b, cx + 6, 17 - b), "skin")
	g.px(cx + 7, 15 - b, "skin")
	# Кашкет: тулія й козирок уперед-праворуч.
	g.ellipse((cx - 7, 4 - b, cx + 6, 11 - b), "cap")
	g.rect((cx + 3, 9 - b, cx + 9, 10 - b), "cap")
	# Шви: пояс, шліца, лінія рукава.
	g.line_seam(cx - 9, 40, cx + 9, 40)
	g.line_seam(cx, 44, cx, 62)
	g.line_seam(cx - 10, 27 - b, cx - 10, 45)
	g.line_seam(cx + 10, 27 - b, cx + 12, 39)
	g.line_seam(cx - 4, 21 - b, cx + 4, 21 - b) # комір
	return g.render(0.5)


def hacker(f: int) -> Image.Image:
	"""Дівчина з телефоном: коротка куртка, джинси, хвіст; телефон світиться в правій руці;
	час від часу озирається праворуч (видно профіль)."""
	g = Figure()
	b = BREATH[f]
	w = WIND[f]
	look = GLANCE[f]
	cx = 22
	g.rect((cx - 6, 46, cx - 2, 69), "jeans")
	g.rect((cx + 1, 46, cx + 5, 69), "jeans")
	g.rect((cx - 7, 70, cx - 2, 71), "shoe")
	g.rect((cx + 1, 70, cx + 6, 71), "shoe")
	# Куртка до стегон.
	g.poly([(cx - 9, 22 - b), (cx + 9, 22 - b), (cx + 9, 46), (cx - 9, 46)], "jacket")
	g.ellipse((cx - 11, 20 - b, cx - 4, 27 - b), "jacket")
	g.ellipse((cx + 4, 20 - b, cx + 11, 27 - b), "jacket")
	g.poly([(cx - 12, 24 - b), (cx - 8, 24 - b), (cx - 8, 44), (cx - 12, 44)], "jacket")
	g.rect((cx - 12, 45, cx - 9, 47), "skin")
	# Права рука зігнута — телефон перед грудьми, екран видно збоку.
	g.poly([(cx + 8, 24 - b), (cx + 12, 24 - b), (cx + 13, 34), (cx + 16, 36), (cx + 14, 39), (cx + 9, 36)], "jacket")
	g.rect((cx + 15, 35, cx + 16, 37), "skin")
	g.rect((cx + 16, 32, cx + 18, 36), "phone")
	g.px(cx + 18, 32, "phone_edge")
	# Голова: хвіст гойдається, профіль праворуч при погляді вбік.
	g.ellipse((cx - 5, 8 - b, cx + 5, 20 - b), "hair")
	if look:
		g.rect((cx + 3, 12 - b, cx + 5 + look, 17 - b), "skin")
		g.px(cx + 6 + look, 14 - b, "skin") # кінчик носа
	else:
		g.rect((cx + 4, 13 - b, cx + 5, 16 - b), "skin")
	g.rect((cx - 2, 11 - b, cx + 1, 14 - b), "hair") # гумка/основа хвоста
	for i in range(18):
		x = cx - 1 + round(math.sin(i / 17 * 1.4) * (w + 1))
		half = 2 if i < 6 else (1 if i < 13 else 0)
		g.rect((x - half, 13 - b + i, x + (1 if i < 10 else 0), 13 - b + i), "hair")
	g.line_seam(cx - 8, 44, cx + 8, 44)
	g.line_seam(cx - 8, 26 - b, cx - 8, 43)
	g.line_seam(cx, 24 - b, cx, 43) # блискавка/шов на спині
	return g.render(0.45)


def runner(f: int) -> Image.Image:
	"""Хлопець у худі з капюшоном: на плечі велика спортивна сумка, у лівій руці цигарка."""
	g = Figure()
	b = BREATH[f]
	cx = 22
	g.rect((cx - 7, 50, cx - 3, 71), "pants")
	g.rect((cx + 2, 50, cx + 6, 71), "pants")
	g.rect((cx - 8, 72, cx - 2, 73), "shoe")
	g.rect((cx + 2, 72, cx + 8, 73), "shoe")
	# Худі: широке, з кишенею-кенгуру внизу.
	g.poly([(cx - 11, 23 - b), (cx + 11, 23 - b), (cx + 11, 50), (cx - 11, 50)], "hoodie")
	g.ellipse((cx - 14, 21 - b, cx - 5, 29 - b), "hoodie")
	g.ellipse((cx + 5, 21 - b, cx + 14, 29 - b), "hoodie")
	g.poly([(cx - 15, 25 - b), (cx - 11, 25 - b), (cx - 11, 46), (cx - 15, 46)], "hoodie")
	g.rect((cx - 15, 47, cx - 12, 49), "skin")
	g.px(cx - 16, 49, "ember") # цигарка в пальцях
	g.px(cx - 17, 49, "metal")
	g.poly([(cx + 11, 25 - b), (cx + 15, 25 - b), (cx + 15, 44), (cx + 11, 44)], "hoodie")
	# Капюшон на голові.
	g.ellipse((cx - 8, 6 - b, cx + 8, 24 - b), "hoodie")
	g.rect((cx + 5, 14 - b, cx + 7, 19 - b), "skin")
	# Сумка: ремінь через ліве плече навскіс, сама сумка біля правого стегна.
	g.line_seam(cx - 9, 22 - b, cx + 12, 44)
	for i in range(22):
		g.px(cx - 9 + i, 22 - b + i, "strap")
		g.px(cx - 8 + i, 22 - b + i, "strap")
	g.ellipse((cx + 4, 40, cx + 24, 55), "bag")
	g.rect((cx + 8, 40, cx + 20, 54), "bag")
	g.line_seam(cx + 8, 46, cx + 20, 46) # блискавка сумки
	g.px(cx + 20, 46, "metal")
	g.line_seam(cx - 10, 50, cx + 10, 50)
	g.line_seam(cx - 6, 42, cx + 3, 42) # кишеня
	g.line_seam(cx, 8 - b, cx, 22 - b) # шов капюшона
	return g.render(0.45)


def silhouette(img: Image.Image) -> Image.Image:
	"""Персонаж, залитий одним кольором, — перевірка, що силует впізнається."""
	out = Image.new("RGBA", img.size, (0, 0, 0, 0))
	px, src = out.load(), img.load()
	for y in range(img.height):
		for x in range(img.width):
			if src[x, y][3]:
				px[x, y] = pal.P[0] + (255,)
	return out


def sheet(fn, name: str) -> None:
	frames = [fn(f) for f in range(FRAMES)]
	out = Image.new("RGBA", (FW * FRAMES, FH), (0, 0, 0, 0))
	for i, fr in enumerate(frames):
		out.alpha_composite(fr, (i * FW, 0))
	out.save(os.path.join(OUT, f"gang_{name}.png"))
	sil_dir = os.path.join(os.path.dirname(__file__), "silhouettes")
	os.makedirs(sil_dir, exist_ok=True)
	silhouette(frames[0]).save(os.path.join(sil_dir, f"gang_{name}_silhouette.png"))


def main() -> None:
	os.makedirs(OUT, exist_ok=True)
	sheet(leader, "leader")
	sheet(hacker, "hacker")
	sheet(runner, "runner")
	for old in ("coat", "phone", "hood"):
		path = os.path.join(OUT, f"gang_{old}.png")
		if os.path.exists(path):
			os.remove(path)
	# Превʼю ×4 на тлі освітленого парапету.
	prev = Image.new("RGBA", (FW * FRAMES * 4, FH * 3 * 4), pal.P[10] + (255,))
	for row, name in enumerate(["leader", "hacker", "runner"]):
		s = Image.open(os.path.join(OUT, f"gang_{name}.png")).resize((FW * FRAMES * 4, FH * 4), Image.NEAREST)
		prev.alpha_composite(s, (0, row * FH * 4))
	prev.save(os.path.join(os.path.dirname(__file__), "gang_preview.png"))
	print("ok")


if __name__ == "__main__":
	main()
