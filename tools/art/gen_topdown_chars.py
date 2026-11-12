"""Персонажі для виду зверху (3/4): Тінь і охоронець. Кадр 24×32, персонаж ~14×24 px.

Запуск: python tools/art/gen_topdown_chars.py → assets/sprites/chars/{shadow,guard}.png + силуети.

Аркуш: рядки — напрямки (0 вниз, 1 вліво, 2 вправо, 3 вгору), стовпці — кадри:
  0–3  idle (дихання: плечі й голова на 1 px; вторинний рух — край куртки/пояс)
  4–9  ходьба, 6 кадрів (контакт → прохід → контакт; руки в протифазі; тіло підстрибує на 1 px)
  10–11 тривога/підозра (охоронець — підняв ліхтар і озирається; Тінь — пригнулася, завмерла)
Затінення за стайлгайдом: світло з північного заходу (зверху-зліва) → ліві/верхні краї світліші,
праві/нижні — темніші; контур NIGHT_0; обличчя — не пляма: у Тіні балаклава з прорізом і блиском очей,
в охоронця тінь під козирком, очі, щелепа, жетон.
"""
from __future__ import annotations

import os

from PIL import Image, ImageDraw

import palette as pal

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "sprites", "chars")
SIL = os.path.join(os.path.dirname(__file__), "silhouettes")
FW, FH = 24, 32
DIRS = ["down", "left", "right", "up"]

# Рампи (від тіні до світла) для кожного матеріалу.
RAMPS = {
	"shadow": {
		"eyedim": [15, 15, 15, 15, 15], "pants": [0, 1, 2, 3, 3],
		"suit": [0, 1, 2, 3, 5], "mask": [0, 1, 2, 3, 4], "skin": [9, 10, 11, 12, 12],
		"gear": [0, 13, 14, 15, 16], "strap": [0, 8, 9, 10, 10], "boot": [0, 0, 1, 2, 2], "glove": [0, 1, 2, 3, 3],
		"eye": [16, 16, 16, 16, 16], "accent": [13, 14, 15, 16, 16],
	},
	"guard": {
		# Світло-сіра сорочка охорони: на темній підлозі охоронець читається одразу — загроза видна.
		"suit": [13, 14, 15, 16, 16], "pants": [0, 1, 2, 3, 3], "mask": [0, 1, 2, 3, 4], "skin": [8, 9, 10, 11, 12],
		"gear": [0, 13, 14, 15, 16], "strap": [0, 1, 2, 3, 3], "boot": [0, 0, 1, 2, 2], "glove": [0, 1, 2, 3, 3],
		"eye": [16, 16, 16, 16, 16], "eyedim": [15, 15, 15, 15, 15], "accent": [17, 18, 19, 20, 21], "cap": [0, 26, 13, 14, 14],
	},
}
FLAT = {"eye", "lens"} # кольори без затінення
LENS = 21


class Sprite:
	def __init__(self, kind: str):
		self.kind = kind
		self.lab = Image.new("P", (FW, FH), 0)
		self.names = ["."]
		self.d = ImageDraw.Draw(self.lab)
		self.seams: set = set()
		self.lights: set = set()

	def _id(self, n):
		if n not in self.names:
			self.names.append(n)
		return self.names.index(n)

	def ellipse(self, box, n):
		self.d.ellipse(box, fill=self._id(n))

	def rect(self, box, n):
		self.d.rectangle(box, fill=self._id(n))

	def poly(self, pts, n):
		self.d.polygon([(round(x), round(y)) for x, y in pts], fill=self._id(n))

	def px(self, x, y, n):
		if 0 <= x < FW and 0 <= y < FH:
			self.lab.putpixel((x, y), self._id(n))

	def seam(self, *pts):
		self.seams.update(pts)

	def highlight(self, *pts):
		self.lights.update(pts)

	def render(self) -> Image.Image:
		lab = self.lab.load()
		ramps = RAMPS[self.kind]
		name = lambda x, y: self.names[lab[x, y]] if 0 <= x < FW and 0 <= y < FH else "."
		img = Image.new("RGBA", (FW, FH), (0, 0, 0, 0))
		out = img.load()
		for y in range(FH):
			for x in range(FW):
				n = name(x, y)
				if n == ".":
					continue
				if n == "lens":
					out[x, y] = pal.P[LENS] + (255,)
					continue
				r = ramps[n]
				c = r[2]
				if name(x - 1, y) == "." or name(x, y - 1) == ".":
					c = r[3] # ключове світло з північного заходу
				if name(x - 1, y) == "." and name(x, y - 1) == ".":
					c = r[4]
				if name(x + 1, y) == "." or name(x, y + 1) == ".":
					c = r[1]
				if name(x, y - 1) not in (".", n) and n not in ("eye",):
					c = r[1] # тінь під іншим матеріалом (під головою, під поясом)
				if (x, y) in self.seams:
					c = r[1]
				if (x, y) in self.lights:
					c = r[4]
				out[x, y] = pal.P[c] + (255,)
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


# --- Пози ---------------------------------------------------------------------------

WALK_LEG = [(3, -2), (1, 0), (-1, 2), (-2, 3), (0, 1), (2, -1)] # (ліва, права): широкий крок, ноги розходяться на 3–5 px
WALK_BOB = [0, 1, 0, 0, 1, 0]
IDLE_BREATH = [0, 0, 1, 1]


def pose(frame: int):
	"""→ (bob, breath, leg_l, leg_r, arm_l, arm_r, alert)"""
	if frame < 4:
		return 0, IDLE_BREATH[frame], 0, 0, 0, 0, False
	if frame < 10:
		i = frame - 4
		l, r = WALK_LEG[i]
		return WALK_BOB[i], 0, l, r, -l, -r, False
	return 0, frame - 10, 0, 0, 0, 0, True


def draw_body(s: Sprite, d: str, frame: int, guard: bool) -> None:
	bob, br, ll, lr, al, ar, alert = pose(frame)
	crouch = 2 if (alert and not guard) else 0
	cx = 12
	top = 4 + bob + crouch # верх голови
	head_h = 8
	sh_y = top + head_h - br # плечі
	hip_y = 21 + bob
	foot_y = 28
	side = d in ("left", "right")
	flip = d == "left"

	def X(x):
		return (2 * cx - x) if flip else x

	# Ноги: у видах спереду/ззаду — дві поруч, крок = зсув по вертикалі; збоку — ножиці.
	legs = "pants" if guard else "suit"
	if not side:
		for k, step in ((-1, ll), (1, lr)):
			lx = cx + k * 2 - (1 if k < 0 else 0)
			s.rect((lx, hip_y, lx + 1, foot_y - max(0, step)), legs)
			s.rect((lx, foot_y - max(0, step) + 1, lx + 1, foot_y - max(0, step) + 2), "boot")
	else:
		for step, shade in ((ll, legs), (lr, legs)):
			fx = cx + step
			s.poly([(X(cx - 1), hip_y), (X(cx + 1), hip_y), (X(fx + 1), foot_y), (X(fx - 1), foot_y)], shade)
			s.rect((min(X(fx - 1), X(fx + 2)), foot_y + 1, max(X(fx - 1), X(fx + 2)), foot_y + 2), "boot")
	# Тулуб: охоронець ширший у плечах — силуети героїв розрізняються навіть залиті одним кольором.
	w = (6 if guard else 4) if not side else (5 if guard else 4)
	sway = (1 if bob else 0) if not side else 0 # хитання тулуба в кроці
	s.poly([(cx - w + sway, sh_y), (cx + w - 1 + sway, sh_y), (cx + w - 2, hip_y + 1), (cx - w + 1, hip_y + 1)], "suit")
	s.ellipse((cx - w - 1, sh_y - 1, cx - w + 3, sh_y + 3), "suit")
	s.ellipse((cx + w - 4, sh_y - 1, cx + w, sh_y + 3), "suit")
	# Пояс зі спорядженням.
	s.rect((cx - w + 1, hip_y - 2, cx + w - 2, hip_y - 2), "strap")
	# Руки: гойдаються в протифазі з ногами.
	if not side:
		for k, sw in ((-1, al), (1, ar)):
			ax = cx + k * (w + 1) - (1 if k < 0 else 0)
			s.rect((ax, sh_y + 1 + max(0, sw), ax + 1, hip_y - 1 + sw), "suit")
			s.rect((ax, hip_y + sw, ax + 1, hip_y + 1 + sw), "glove")
	else:
		s.poly([(X(cx), sh_y + 1), (X(cx + 2), sh_y + 1), (X(cx + 2 + al), hip_y), (X(cx + al), hip_y)], "suit")
		s.rect((min(X(cx + al), X(cx + 2 + al)), hip_y + 1, max(X(cx + al), X(cx + 2 + al)), hip_y + 2), "glove")
	# Голова.
	hx0, hx1 = (cx - 4, cx + 3) if guard else (cx - 3, cx + 2) # у Тіні голова вужча — силует відрізняється
	s.ellipse((hx0, top, hx1, top + head_h), "mask" if not guard else "skin")
	if guard:
		draw_guard_head(s, d, top, cx, X, alert)
		# Ліхтар у правій руці (світло — PointLight2D у сцені).
		if d == "down":
			s.rect((cx + w + 1, hip_y - 4, cx + w + 2, hip_y + 1), "gear") # ліхтар у кулаці, виступає з силуету
			s.rect((cx + w + 1, hip_y + 2, cx + w + 2, hip_y + 2), "lens")
		elif side:
			s.rect((min(X(cx + 3), X(cx + 6)), hip_y - 4 - (3 if alert else 0), max(X(cx + 3), X(cx + 6)), hip_y - 3 - (3 if alert else 0)), "gear")
			s.px(X(cx + 7), hip_y - 4 - (3 if alert else 0), "lens")
		# Жетон на грудях — відблиск.
		if d == "down":
			s.px(cx - 3, sh_y + 3, "accent")
			s.highlight((cx - 3, sh_y + 3))
	else:
		draw_shadow_head(s, d, top, cx, X)
		# Сумка-рюкзак: видно ззаду й збоку.
		if d == "up":
			s.rect((cx - 3, sh_y + 2, cx + 2, sh_y + 8), "strap")
			s.seam((cx - 3, sh_y + 5), (cx - 2, sh_y + 5), (cx - 1, sh_y + 5), (cx, sh_y + 5), (cx + 1, sh_y + 5), (cx + 2, sh_y + 5))
		elif side:
			s.rect((min(X(cx - 7), X(cx - 4)), sh_y + 1, max(X(cx - 7), X(cx - 4)), sh_y + 8), "strap") # рюкзак виступає за спину
			s.highlight((X(cx - 7), sh_y + 1))
		else:
			for i in range(5): # ремінь навскіс через груди
				s.px(cx - 3 + i, sh_y + 1 + i, "strap")
	# Складки: лінія застібки/хребта й шов рукава — тіло не суцільна маса.
	for y in range(sh_y + 2, hip_y - 2):
		s.seam((cx if not side else X(cx - 1), y))
	if not side:
		s.highlight((cx - w + 1, sh_y + 1)) # світло на лівому плечі


def draw_shadow_head(s: Sprite, d: str, top: int, cx: int, X) -> None:
	"""Балаклава: темна голова з прорізом для очей; очі — 2 світлі пікселі (блиск)."""
	if d == "down":
		s.rect((cx - 3, top + 3, cx + 2, top + 4), "skin") # проріз
		s.px(cx - 2, top + 3, "eye")
		s.px(cx + 1, top + 3, "eye")
		s.seam((cx - 3, top + 5), (cx + 2, top + 5))
	elif d in ("left", "right"):
		s.rect((min(X(cx - 4), X(cx - 2)), top + 3, max(X(cx - 4), X(cx - 2)), top + 4), "skin")
		s.px(X(cx - 3), top + 3, "eye")
		s.seam((X(cx), top + 1), (X(cx), top + 2), (X(cx), top + 3))
	else:
		s.seam((cx - 1, top + 2), (cx - 1, top + 3), (cx - 1, top + 4), (cx - 1, top + 5)) # шов балаклави на потилиці


def draw_guard_head(s: Sprite, d: str, top: int, cx: int, X, alert: bool) -> None:
	"""Кашкет з козирком; під козирком тінь, очі, лінія щелепи; погляд убік під час тривоги."""
	s.ellipse((cx - 4, top - 1, cx + 3, top + 4), "cap")
	if d == "down":
		s.rect((cx - 5, top + 3, cx + 4, top + 4), "cap") # козирок ширший за голову — виступає в силуеті
		s.rect((cx - 3, top + 5, cx + 2, top + 5), "mask") # тінь під козирком
		look = 1 if alert else 0
		s.px(cx - 2 + look, top + 5, "eyedim")
		s.px(cx + 1 + look, top + 5, "eyedim")
		s.highlight((cx - 1 + look, top + 6)) # кінчик носа ловить світло
		s.seam((cx - 2, top + 8), (cx + 1, top + 8)) # кути щелепи
		s.px(cx, top - 1, "accent") # кокарда
	elif d in ("left", "right"):
		s.rect((min(X(cx - 6), X(cx - 3)), top + 2, max(X(cx - 6), X(cx - 3)), top + 3), "cap")
		s.px(X(cx - 3), top + 5, "eye")
		s.seam((X(cx - 2), top + 7), (X(cx - 1), top + 7), (X(cx), top + 7))
	else:
		s.rect((cx - 3, top + 4, cx + 2, top + 7), "skin") # потилиця
		s.seam((cx - 3, top + 7), (cx + 2, top + 7))


def frame(kind: str, d: str, f: int) -> Image.Image:
	s = Sprite(kind)
	draw_body(s, d, f, kind == "guard")
	return s.render()


def main() -> None:
	os.makedirs(OUT, exist_ok=True)
	os.makedirs(SIL, exist_ok=True)
	for kind in ("shadow", "guard"):
		sheet = Image.new("RGBA", (FW * 12, FH * 4), (0, 0, 0, 0))
		for r, d in enumerate(DIRS):
			for f in range(12):
				sheet.alpha_composite(frame(kind, d, f), (f * FW, r * FH))
		sheet.save(os.path.join(OUT, f"{kind}.png"))
		# Силует (кадр «вниз, idle 0»), залитий одним кольором.
		img = frame(kind, "down", 0)
		sil = Image.new("RGBA", img.size, (0, 0, 0, 0))
		sp, ip = sil.load(), img.load()
		for y in range(FH):
			for x in range(FW):
				if ip[x, y][3]:
					sp[x, y] = pal.P[0] + (255,)
		sil.save(os.path.join(SIL, f"{kind}_silhouette.png"))
	# Превʼю ×6 на тлі підлоги магазину.
	prev = Image.new("RGBA", (FW * 12 * 6, FH * 8 * 6), pal.P[5] + (255,))
	for i, kind in enumerate(("shadow", "guard")):
		sh = Image.open(os.path.join(OUT, f"{kind}.png")).resize((FW * 12 * 6, FH * 4 * 6), Image.NEAREST)
		prev.alpha_composite(sh, (0, i * FH * 4 * 6))
	prev.save(os.path.join(os.path.dirname(__file__), "topdown_preview.png"))
	print("ok")


if __name__ == "__main__":
	main()
