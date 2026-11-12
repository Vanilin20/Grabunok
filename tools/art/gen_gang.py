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
	"face": [10, 11, 29, 29, 29], # обличчя в холодному світлі екрана
	"strap": [0, 8, 9, 9, 9],
}
EMISSIVE = {"lens": 21, "phone": 29, "phone_edge": 28, "ember": 20, "face": 12}


class Figure:
	def __init__(self, scale: float = 1.0):
		self.scale = scale
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
		"""Затінення персонажа, що стоїть спиною до білборда (контрове світло) під рожевим неоном:
		- крайній піксель силуету — блакитний кант від білборда (вище плечей яскравіший);
		- другий піксель від краю — світлий тон матеріалу (світло обгинає край);
		- верхні грані голови й плечей — тонкий рожевий кант від вивіски;
		- середина — основний тон, складки й шви — темний, низ біля землі — найтемніший.
		Так кожен матеріал має 4–5 тонів, а силует читається на будь-якому тлі."""
		# Персонаж ближче до камери: карта матеріалів масштабується, а затінення рахується вже
		# на новій роздільності — контур і кант лишаються в 1 px, без «товстих» пікселів.
		FW, FH = round(48 * self.scale), round(76 * self.scale)
		if self.scale != 1.0:
			self.lab = self.lab.resize((FW, FH), Image.NEAREST)
			# Шви масштабуємо як маску й проріджуємо до лінії в 1 px — щоб лишились суцільними.
			mask = Image.new("L", (48, 76), 0)
			for x, y in self.seams:
				if 0 <= x < 48 and 0 <= y < 76:
					mask.putpixel((x, y), 255)
			big = mask.resize((FW, FH), Image.NEAREST).load()
			self.seams = [(x, y) for y in range(FH) for x in range(FW)
				if big[x, y] and not (x > 0 and big[x - 1, y] and y > 0 and big[x, y - 1])]
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
				edge_l, edge_r = not filled(x - 1, y), not filled(x + 1, y)
				# Внутрішня щілина (зліва й справа від неї знову фігура) — це тінь, не край силуету.
				if edge_l and any(filled(x - k, y) for k in range(2, 5)):
					edge_l = False
				if edge_r and any(filled(x + k, y) for k in range(2, 5)):
					edge_r = False
				near = not filled(x - 2, y) or not filled(x + 2, y)
				up = filled(x, y - 1)
				if up and name(x, y - 1) != n and name(x, y - 1) not in EMISSIVE:
					c = ramp[1] # тінь під іншим матеріалом (під коміром, під сумкою)
				if (x, y) in seams:
					c = ramp[1]
				elif (x + 1, y) in seams and not (edge_l or edge_r):
					c = ramp[3] # гребінь складки ловить світло — об'єм тканини
				if bottom - y < 3:
					c = ramp[0] if not (edge_l or edge_r) else c # контактна тінь біля землі
				if near and not (edge_l or edge_r):
					c = ramp[3]
				if n in ("hair", "skin", "cap", "face", "bag") and not (edge_l or edge_r):
					# Верх частини освітлений, низ у власній тіні — 3–4 тони на маленькій площі.
					part_y = [yy for yy in range(FH) if name(x, yy) == n]
					rel = (y - min(part_y)) / max(1, max(part_y) - min(part_y))
					c = ramp[3] if rel < 0.3 else (ramp[1] if rel > 0.7 else ramp[2])
					if n == "hair" and (x + y) % 3 == 0 and 0.3 <= rel <= 0.7:
						c = ramp[3] # пасма
				if edge_l or edge_r:
					# Блакитний контржур від білборда: яскравий на голові й плечах, тьмяний нижче.
					c = 28 if y < rim_y else (27 if y < rim_y + 12 else 26)
				if not up:
					if y < rim_y:
						c = 23 if (edge_l or edge_r) else 24 # тонкий рожевий кант зверху
					else:
						c = ramp[4]
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


LEADER_SCALE = 1.5 # стоїть найближче до камери


def leader(f: int) -> Image.Image:
	"""Ватажок: довгий плащ, кашкет, у правій руці ліхтарик (промінь — світло у сцені)."""
	g = Figure(LEADER_SCALE)
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
	g.rect((cx + 20, 42, cx + 27, 45), "metal")
	g.rect((cx + 27, 41, cx + 28, 46), "metal") # голівка ліхтарика ширша
	g.rect((cx + 29, 42, cx + 29, 45), "lens")
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


HACKER_SCALE = 1.4


def hacker(f: int) -> Image.Image:
	"""Дівчина-хакер у напівоберті ліворуч: телефон перед собою, обличчя й рука в холодному світлі екрана.
	Хвіст гойдається, час від часу піднімає погляд від екрана."""
	g = Figure(HACKER_SCALE)
	b = BREATH[f]
	w = WIND[f]
	look = GLANCE[f]
	cx = 24
	g.rect((cx - 5, 48, cx - 2, 69), "jeans")
	g.rect((cx + 1, 47, cx + 4, 69), "jeans")
	g.rect((cx - 7, 70, cx - 2, 71), "shoe")
	g.rect((cx + 1, 70, cx + 5, 71), "shoe")
	# Коротка куртка, звужена в талії.
	g.poly([(cx - 8, 23 - b), (cx + 8, 23 - b), (cx + 7, 40), (cx + 8, 48), (cx - 7, 48), (cx - 6, 40)], "jacket")
	g.ellipse((cx - 10, 21 - b, cx - 3, 28 - b), "jacket")
	g.ellipse((cx + 3, 21 - b, cx + 10, 28 - b), "jacket")
	# Права рука вздовж тіла, ліва зігнута — телефон перед грудьми, трохи вліво.
	g.poly([(cx + 7, 25 - b), (cx + 11, 25 - b), (cx + 11, 44), (cx + 7, 44)], "jacket")
	g.rect((cx + 8, 45, cx + 10, 47), "skin")
	g.poly([(cx - 10, 25 - b), (cx - 6, 25 - b), (cx - 7, 33), (cx - 13, 36), (cx - 14, 33)], "jacket")
	g.rect((cx - 15, 32, cx - 13, 34), "skin")
	g.rect((cx - 18, 29 - look // 2, cx - 15, 34 - look // 2), "phone")
	g.px(cx - 18, 29 - look // 2, "phone_edge")
	# Голова в напівоберті: видно щоку й кінчик носа ліворуч, підсвічені екраном.
	g.ellipse((cx - 5, 8 - b, cx + 5, 20 - b), "hair")
	g.rect((cx - 6, 13 - b - look, cx - 3, 18 - b - look), "face")
	g.px(cx - 7, 15 - b - look, "face")
	g.px(cx - 4, 19 - b, "skin") # шия
	g.rect((cx - 2, 11 - b, cx + 1, 14 - b), "hair")
	for i in range(18):
		x = cx + 2 + round(math.sin(i / 17 * 1.4) * (w + 1))
		half = 2 if i < 6 else (1 if i < 13 else 0)
		g.rect((x - half, 13 - b + i, x + (1 if i < 10 else 0), 13 - b + i), "hair")
	g.line_seam(cx - 6, 44, cx + 7, 44)
	g.line_seam(cx + 6, 27 - b, cx + 6, 43)
	g.line_seam(cx, 25 - b, cx, 43)
	return g.render(0.45)


def runner(f: int) -> Image.Image:
	"""Хлопець у худі сидить на парапеті боком (обличчям ліворуч, до міста): права нога зігнута,
	коліно підняте на парапет, лікоть на коліні, у пальцях цигарка; капюшон на голові.
	Друга нога звисає за парапет (її не видно). Сумка стоїть окремо праворуч."""
	g = Figure()
	b = BREATH[f]
	base = 75 # нижній край спрайта = верх парапету
	cx = 26
	# Сумка-дафл: циліндр з ручками й ременем, з проміжком від фігури.
	g.ellipse((cx + 9, base - 12, cx + 21, base), "bag")
	g.rect((cx + 11, base - 12, cx + 19, base - 1), "bag")
	g.line_seam(cx + 11, base - 6, cx + 19, base - 6)
	g.px(cx + 19, base - 6, "metal")
	for i in range(6):
		g.px(cx + 12 + i, base - 13 - (1 if 0 < i < 5 else 0), "strap")
	# Сідниці й стегно на парапеті, коліно підняте ліворуч-уперед.
	g.poly([(cx - 4, base - 1), (cx + 6, base - 1), (cx + 6, base - 8), (cx - 4, base - 9)], "pants")
	g.poly([(cx - 4, base - 9), (cx - 14, base - 22), (cx - 18, base - 19), (cx - 9, base - 5)], "pants") # стегно до коліна
	g.poly([(cx - 18, base - 19), (cx - 14, base - 22), (cx - 15, base - 1), (cx - 19, base - 1)], "pants") # гомілка вниз
	g.rect((cx - 21, base - 3, cx - 14, base - 1), "shoe")
	# Тулуб нахилений уперед (до коліна), капюшон.
	g.poly([(cx - 6, base - 10), (cx + 7, base - 9), (cx + 6, base - 30 - b), (cx - 8, base - 33 - b)], "hoodie")
	g.ellipse((cx - 10, base - 37 - b, cx + 2, base - 27 - b), "hoodie") # плече
	g.ellipse((cx - 13, base - 48 - b, cx + 1, base - 33 - b), "hoodie") # капюшон
	g.rect((cx - 13, base - 42 - b, cx - 11, base - 38 - b), "skin") # кінчик носа з-під капюшона
	# Рука: від плеча до коліна, кисть звисає над коліном з цигаркою.
	g.poly([(cx - 9, base - 32 - b), (cx - 5, base - 31 - b), (cx - 13, base - 21), (cx - 17, base - 23)], "hoodie")
	g.rect((cx - 19, base - 23, cx - 17, base - 20), "skin")
	g.px(cx - 20, base - 21, "metal")
	g.px(cx - 21, base - 21, "ember")
	g.line_seam(cx - 6, base - 46 - b, cx - 2, base - 34 - b) # край капюшона
	g.line_seam(cx - 8, base - 14, cx + 6, base - 13) # пояс худі
	g.line_seam(cx + 2, base - 28 - b, cx + 3, base - 12) # шов спини
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
	fw, fh = frames[0].size
	out = Image.new("RGBA", (fw * FRAMES, fh), (0, 0, 0, 0))
	for i, fr in enumerate(frames):
		out.alpha_composite(fr, (i * fw, 0))
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
	sheets = [Image.open(os.path.join(OUT, f"gang_{n}.png")) for n in ("leader", "hacker", "runner")]
	prev = Image.new("RGBA", (max(sh.width for sh in sheets) * 3, sum(sh.height for sh in sheets) * 3), pal.P[10] + (255,))
	y = 0
	for sh in sheets:
		prev.alpha_composite(sh.resize((sh.width * 3, sh.height * 3), Image.NEAREST), (0, y))
		y += sh.height * 3
	prev.save(os.path.join(os.path.dirname(__file__), "gang_preview.png"))
	print("ok")


if __name__ == "__main__":
	main()
