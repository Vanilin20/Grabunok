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
	plate(3, 1, 1, 1, 0, None).save(os.path.join(OUT, "button_disabled.png"))
	# Панель: темний метал, світлий верхній-лівий скіс, заклепки.
	p = Canvas(S, S)
	p.rect(0, 0, S, S, 0)
	p.rect(1, 1, S - 2, S - 2, 4)
	p.hline(1, 1, S - 2, 5)
	p.vline(1, 1, S - 2, 5)
	p.rect(2, 2, S - 4, S - 4, 1)
	for x, y in ((3, 3), (S - 5, 3), (3, S - 5), (S - 5, S - 5)):
		p.put(x, y, 15)
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
