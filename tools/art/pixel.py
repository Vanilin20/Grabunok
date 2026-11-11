"""Невеликий набір інструментів для піксель-арту кодом: полотно з палітрою, дизеринг, матеріали.

Усі кольори — індекси палітри (tools/art/palette.py), тому арт завжди лишається в палітрі гри.
"""
from __future__ import annotations

import random

from PIL import Image

import palette as pal

BAYER4 = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


class Canvas:
	"""Полотно з прозорим фоном. Малює індексами палітри; ox/oy — зсув системи координат."""

	def __init__(self, w: int, h: int):
		self.w, self.h = w, h
		self.img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
		self.px = self.img.load()
		self.ox = 0
		self.oy = 0

	def put(self, x: int, y: int, c: int | None) -> None:
		x += self.ox
		y += self.oy
		if c is None or not (0 <= x < self.w and 0 <= y < self.h):
			return
		self.px[x, y] = pal.P[c] + (255,)

	def clear(self, x: int, y: int) -> None:
		x += self.ox
		y += self.oy
		if 0 <= x < self.w and 0 <= y < self.h:
			self.px[x, y] = (0, 0, 0, 0)

	def get(self, x: int, y: int):
		x += self.ox
		y += self.oy
		if 0 <= x < self.w and 0 <= y < self.h:
			return self.px[x, y]
		return (0, 0, 0, 0)

	def filled(self, x: int, y: int) -> bool:
		return self.get(x, y)[3] > 0

	def rect(self, x: int, y: int, w: int, h: int, c: int) -> None:
		for yy in range(y, y + h):
			for xx in range(x, x + w):
				self.put(xx, yy, c)

	def hline(self, x: int, y: int, w: int, c: int) -> None:
		self.rect(x, y, w, 1, c)

	def vline(self, x: int, y: int, h: int, c: int) -> None:
		self.rect(x, y, 1, h, c)

	def dither_rect(self, x: int, y: int, w: int, h: int, c: int, level: float) -> None:
		"""Покрити прямокутник кольором з щільністю level (0..1) за матрицею Байєра."""
		for yy in range(y, y + h):
			for xx in range(x, x + w):
				if BAYER4[(yy + self.oy) % 4][(xx + self.ox) % 4] < level * 16:
					self.put(xx, yy, c)

	def vgradient(self, x: int, y: int, w: int, h: int, ramp: list[int]) -> None:
		"""Вертикальний градієнт смугами з дизерингом на межах (без плавних переходів)."""
		n = len(ramp)
		for yy in range(y, y + h):
			t = (yy - y) / max(1, h - 1) * (n - 1)
			i = int(t)
			frac = t - i
			for xx in range(x, x + w):
				up = BAYER4[(yy + self.oy) % 4][(xx + self.ox) % 4] < frac * 16
				self.put(xx, yy, ramp[min(i + (1 if up else 0), n - 1)])

	def outline(self, c: int) -> None:
		"""Обвести непрозорі пікселі контуром (4-зв'язність) — для силуетів спрайтів."""
		src = self.img.copy().load()
		for y in range(self.h):
			for x in range(self.w):
				if src[x, y][3] > 0:
					continue
				for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
					nx, ny = x + dx, y + dy
					if 0 <= nx < self.w and 0 <= ny < self.h and src[nx, ny][3] > 0:
						self.px[x, y] = pal.P[c] + (255,)
						break

	def paste(self, other: "Canvas", x: int, y: int) -> None:
		self.img.alpha_composite(other.img, (x + self.ox, y + self.oy))

	def save(self, path: str) -> None:
		self.img.save(path)


# --- Матеріали ----------------------------------------------------------------

def brick_wall(cv: Canvas, x: int, y: int, w: int, h: int, rng: random.Random,
		tones: tuple[int, int, int, int] = (8, 9, 10, 11), brick_w: int = 6, brick_h: int = 3,
		lit_left: bool = True) -> None:
	"""Цегляна кладка: розчин, 3 тони цегли з випадковими відхиленнями, світлий верхній край цеглини.
	tones = (розчин/тінь, темна цегла, основна цегла, світла грань)."""
	mortar, dark, base, light = tones
	cv.rect(x, y, w, h, mortar)
	row = 0
	for by in range(y, y + h, brick_h + 1):
		offset = (brick_w + 1) // 2 if row % 2 else 0
		bx = x - offset
		while bx < x + w:
			roll = rng.random()
			tone = dark if roll < 0.22 else base
			x0, x1 = max(bx, x), min(bx + brick_w, x + w)
			y1 = min(by + brick_h, y + h)
			for yy in range(by, y1):
				for xx in range(x0, x1):
					cv.put(xx, yy, tone)
			# Світла верхня грань цеглини (світло падає згори-зліва).
			if tone == base and roll > 0.45:
				for xx in range(x0, max(x0, x1 - 1)):
					cv.put(xx, by, light)
			if lit_left and x0 == bx and roll > 0.3:
				cv.put(x0, by + 1 if by + 1 < y1 else by, light)
			bx += brick_w + 1
		row += 1


def concrete(cv: Canvas, x: int, y: int, w: int, h: int, rng: random.Random,
		tones: tuple[int, int, int] = (13, 14, 15), speckle: float = 0.06) -> None:
	"""Бетон: основний тон, плями темнішого (вологі), рідкі світлі вкраплення."""
	dark, base, light = tones
	cv.rect(x, y, w, h, base)
	for yy in range(y, y + h):
		for xx in range(x, x + w):
			r = rng.random()
			if r < speckle:
				cv.put(xx, yy, dark)
			elif r > 1 - speckle * 0.4:
				cv.put(xx, yy, light)


def metal_panel(cv: Canvas, x: int, y: int, w: int, h: int,
		tones: tuple[int, int, int, int] = (0, 13, 14, 15), rivets: bool = True) -> None:
	"""Металева пластина: контур, основа, світлий лівий/верхній скіс, темний правий/нижній, заклепки."""
	outline, dark, base, light = tones
	cv.rect(x, y, w, h, outline)
	cv.rect(x + 1, y + 1, w - 2, h - 2, base)
	cv.hline(x + 1, y + 1, w - 2, light)
	cv.vline(x + 1, y + 1, h - 2, light)
	cv.hline(x + 1, y + h - 2, w - 2, dark)
	cv.vline(x + w - 2, y + 1, h - 2, dark)
	if rivets and w > 10 and h > 10:
		for rx, ry in ((x + 3, y + 3), (x + w - 4, y + 3), (x + 3, y + h - 4), (x + w - 4, y + h - 4)):
			cv.put(rx, ry, light)
			cv.put(rx + 1, ry + 1, dark)
