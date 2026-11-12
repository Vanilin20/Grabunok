"""Приклади «так / ні» для стайлгайду → docs/art/styleguide_examples.png (×4, з підписами).

Запуск: python tools/art/gen_styleguide_examples.py
"""
import os

from PIL import Image, ImageDraw, ImageFont

import palette as pal
from pixel import BAYER4, Canvas

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
FONT = os.path.join(os.path.dirname(__file__), "..", "fonts", "press_start_2p.ttf")
CELL = 40


def face_bad(cv, ox, oy):
	cv.rect(ox + 12, oy + 8, 16, 18, 10)
	cv.rect(ox + 10, oy + 4, 20, 6, 2)


def face_good(cv, ox, oy):
	cv.rect(ox + 12, oy + 8, 16, 18, 10)
	cv.rect(ox + 12, oy + 8, 16, 2, 9) # тінь під козирком
	cv.rect(ox + 10, oy + 4, 20, 6, 2)
	cv.hline(ox + 10, oy + 4, 20, 4)
	cv.put(ox + 16, oy + 13, 16)
	cv.put(ox + 23, oy + 13, 16)
	cv.vline(ox + 12, oy + 10, 14, 11) # світла щока з північного заходу
	cv.hline(ox + 14, oy + 25, 12, 9) # щелепа
	cv.vline(ox + 27, oy + 10, 15, 9)


def body_bad(cv, ox, oy):
	cv.rect(ox + 10, oy + 6, 20, 28, 1)


def body_good(cv, ox, oy):
	cv.rect(ox + 10, oy + 6, 20, 28, 2)
	cv.vline(ox + 10, oy + 6, 28, 4)
	cv.hline(ox + 10, oy + 6, 20, 4)
	cv.vline(ox + 29, oy + 6, 28, 1)
	cv.vline(ox + 20, oy + 8, 18, 1) # застібка
	cv.hline(ox + 11, oy + 24, 18, 9) # пояс
	cv.vline(ox + 13, oy + 9, 14, 1) # шов рукава
	cv.vline(ox + 26, oy + 9, 14, 1)
	for i in range(10):
		cv.put(ox + 12 + i, oy + 8 + i, 10) # ремінь навскіс
	cv.put(ox + 15, oy + 12, 21) # відблиск жетона


def fog_bad(cv, ox, oy):
	for y in range(36):
		for x in range(36):
			cv.put(ox + 2 + x, oy + 2 + y, 4 if (x + y) % 2 else 1)


def fog_good(cv, ox, oy):
	cv.vgradient(ox + 2, oy + 2, 36, 36, [1, 2, 3, 4])


def bright_bad(cv, ox, oy):
	cv.rect(ox + 4, oy + 8, 32, 22, 28)


def bright_good(cv, ox, oy):
	cv.rect(ox + 4, oy + 8, 32, 22, 27)
	cv.rect(ox + 6, oy + 10, 28, 18, 28)
	cv.rect(ox + 16, oy + 11, 8, 15, 26) # силует телефона
	cv.rect(ox + 17, oy + 12, 6, 11, 29)
	for k in (4, 7):
		cv.vline(ox + 16 - k, oy + 14, 7, 26)
		cv.vline(ox + 23 + k, oy + 14, 7, 26)


def light_bad(cv, ox, oy):
	cv.rect(ox + 12, oy + 12, 16, 16, 14)


def light_good(cv, ox, oy):
	cv.dither_rect(ox + 15, oy + 15, 16, 16, 0, 0.7) # тінь на південний схід
	cv.rect(ox + 12, oy + 12, 16, 16, 14)
	cv.hline(ox + 12, oy + 12, 16, 16)
	cv.vline(ox + 12, oy + 12, 16, 15)
	cv.hline(ox + 12, oy + 27, 16, 13)
	cv.vline(ox + 27, oy + 12, 16, 13)


ROWS = [("обличчя", face_bad, face_good), ("тіло", body_bad, body_good), ("туман", fog_bad, fog_good),
	("яскраве", bright_bad, bright_good), ("світло", light_bad, light_good)]


def main():
	scale = 4
	label_w = 90
	w = label_w + 2 * (CELL + 8)
	h = len(ROWS) * (CELL + 6) + 14
	cv = Canvas(w, h)
	cv.rect(0, 0, w, h, 1)
	for i, (_name, bad, good) in enumerate(ROWS):
		y = 14 + i * (CELL + 6)
		for j, fn in enumerate((bad, good)):
			x = label_w + j * (CELL + 8)
			cv.rect(x, y, CELL, CELL, 3)
			fn(cv, x, y)
	img = cv.img.resize((w * scale, h * scale), Image.NEAREST)
	d = ImageDraw.Draw(img)
	font = ImageFont.truetype(FONT, 16)
	d.text((label_w * scale + 40, 6), "НІ", font=font, fill=pal.P[30])
	d.text(((label_w + CELL + 8) * scale + 40, 6), "ТАК", font=font, fill=pal.P[31])
	for i, (name, _b, _g) in enumerate(ROWS):
		d.text((10, (14 + i * (CELL + 6)) * scale + 70), name.upper(), font=font, fill=pal.P[16])
	img.save(os.path.join(ROOT, "docs", "art", "styleguide_examples.png"))
	print("ok")


if __name__ == "__main__":
	main()
