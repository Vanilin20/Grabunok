"""Піксельні текстури інтерфейсу (9-slice) для теми assets/ui/theme.tres.

Запуск: python tools/art/gen_ui.py → assets/ui/*.png
Кнопка, що працює, — «неонова трубка» бурштинового кольору з ореолом: видно, що на неї можна тиснути.
Вимкнена — без трубки й ореолу, тьмяна пластина. Панель — темний метал із заклепками.
"""
import os

from PIL import Image

import palette as pal
from pixel import BAYER4, Canvas

OUT = os.path.join(os.path.dirname(__file__), "ui")  # джерела; у тему їх вшиває tools/ui/build_theme.gd
S = 24 # розмір текстури; 9-slice по 8 px (2 px з них — зовнішній ореол)


def plate(tube, inner, fill, top, bottom, halo) -> Canvas:
	cv = Canvas(S, S)
	if halo is not None:
		for y in range(S):
			for x in range(S):
				edge = min(x, y, S - 1 - x, S - 1 - y)
				if edge == 1:
					cv.put(x, y, halo)
	cv.rect(2, 2, S - 4, S - 4, 0) # контур
	cv.rect(3, 3, S - 6, S - 6, tube) # трубка/рамка
	cv.rect(4, 4, S - 8, S - 8, inner)
	cv.rect(5, 5, S - 10, S - 10, fill)
	cv.hline(5, 5, S - 10, top) # світлий верх
	cv.rect(5, S - 7, S - 10, 2, bottom) # тінь знизу — об'єм
	return cv


def main() -> None:
	os.makedirs(OUT, exist_ok=True)
	plate(19, 17, 2, 3, 1, 17).save(os.path.join(OUT, "button_normal.png"))
	plate(20, 18, 3, 4, 2, 18).save(os.path.join(OUT, "button_hover.png"))
	plate(21, 19, 17, 18, 17, 19).save(os.path.join(OUT, "button_pressed.png"))
	# Вимкнена: без світла, зі штрихуванням — «заблоковано», видно навіть на маленькому екрані.
	dis = plate(3, 1, 1, 1, 0, None)
	for y in range(5, S - 5):
		for x in range(5, S - 5):
			if (x + y) % 4 == 0:
				dis.put(x, y, 2)
	dis.save(os.path.join(OUT, "button_disabled.png"))
	# Другорядна, але активна: бірюзова трубка без ореолу (головні — бурштинові з ореолом).
	plate(27, 26, 1, 3, 0, None).save(os.path.join(OUT, "button_secondary.png"))
	plate(28, 27, 2, 4, 1, 26).save(os.path.join(OUT, "button_secondary_hover.png"))
	# Панель: масивна металева рамка (3 px: світлий скіс зверху-зліва, тінь знизу-справа),
	# заклепки, рожевий відблиск неону на верхньому ребрі, всередині — темне скло.
	p = Canvas(S, S)
	p.rect(0, 0, S, S, 0)
	p.rect(1, 1, S - 2, S - 2, 14)
	p.hline(1, 1, S - 2, 23) # відблиск вивіски на верхньому ребрі
	p.hline(1, 2, S - 2, 15)
	p.vline(1, 2, S - 3, 27) # бірюзовий відсвіт білборда на лівому ребрі
	p.hline(2, S - 2, S - 3, 13)
	p.vline(S - 2, 2, S - 3, 13)
	p.rect(3, 3, S - 6, S - 6, 0)
	p.rect(4, 4, S - 8, S - 8, 1)
	p.hline(4, 4, S - 8, 2)
	for x, y in ((2, 2), (S - 4, 2), (2, S - 4), (S - 4, S - 4)):
		p.put(x, y, 16)
		p.put(x + 1, y + 1, 13)
	p.save(os.path.join(OUT, "panel.png"))
	# Поле вводу: утоплене (тінь зверху-зліва), фокус — блакитна рамка.
	for name, border, inner in (("edit_normal.png", 4, 0), ("edit_focus.png", 28, 26)):
		e = Canvas(S, S)
		e.rect(0, 0, S, S, 0)
		e.rect(1, 1, S - 2, S - 2, border)
		e.rect(2, 2, S - 4, S - 4, inner)
		e.rect(3, 3, S - 6, S - 6, 1)
		e.hline(3, 3, S - 6, 0)
		e.vline(3, 3, S - 6, 0)
		e.save(os.path.join(OUT, name))
	print("ok")


if __name__ == "__main__":
	main()
