"""Перевірка 3: фокус кадру — найконтрастніша область.

Запуск: python tools/checks/check_focus.py <скрін.png>
Фокус меню — банда: маска з першого кадру спрайтів за координатами з menu_layout.json.

Основна метрика (за визначенням з ТЗ на перевірку: «різниця яскравості персонажа відносно оточення»):
контраст WCAG між середньою відносною яскравістю фігур і кільця 4 px навколо них.
Ту саму форму (маска + кільце) зсуваємо по всьому кадру з кроком 6 базових px і рахуємо те саме
для кожної області, що перекривається з бандою менш ніж наполовину. Мета: банда — максимум.
Довідково: середній локальний RMS-контраст (вікно 8×8 базових px) під маскою — показує,
наскільки «багатий» на світло й деталі сам фокус (силует одним кольором дає тут мало).
"""
import json
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
GANG = {"runner": "gang_runner.png", "leader": "gang_leader.png", "hacker": "gang_hacker.png"}
FRAME = (48, 76)


def luminance(rgb: np.ndarray) -> np.ndarray:
	c = rgb / 255.0
	lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
	return 0.2126 * lin[..., 0] + 0.7152 * lin[..., 1] + 0.0722 * lin[..., 2]


def main() -> int:
	shot = sys.argv[1]
	layout_path = os.path.join(ROOT, "assets", "sprites", "menu", "menu_layout.json")
	layout = json.load(open(layout_path, encoding="utf-8"))
	img = np.asarray(Image.open(shot).convert("RGB")).astype(np.float64)
	H, W = img.shape[:2]
	scale = H / 360.0
	vw = W / scale
	L = luminance(img)
	mask = np.zeros((H, W), bool)
	for key, file in GANG.items():
		sx, sy = layout["gang"][key]
		fw = layout.get("gang_frames", {}).get(key, FRAME)[0]
		spr = np.asarray(Image.open(os.path.join(ROOT, "assets", "sprites", "menu", file)).convert("RGBA"))[:, :fw]
		alpha = spr[..., 3] > 0
		big = np.kron(alpha, np.ones((int(scale), int(scale)), bool))
		x0 = int(round((sx - 432 + vw / 2) * scale))
		y0 = int(round(sy * scale))
		h, w = big.shape
		ys, xs = slice(max(y0, 0), min(y0 + h, H)), slice(max(x0, 0), min(x0 + w, W))
		mask[ys, xs] |= big[: ys.stop - ys.start, : xs.stop - xs.start]
	# Відділення силуету: фігури проти кільця навколо.
	ring = np.zeros_like(mask)
	r = int(4 * scale)
	for dy in range(-r, r + 1, max(1, r // 2)):
		for dx in range(-r, r + 1, max(1, r // 2)):
			ring |= np.roll(np.roll(mask, dy, 0), dx, 1)
	ring &= ~mask
	lf, lr = L[mask].mean(), L[ring].mean()
	sep = (max(lf, lr) + 0.05) / (min(lf, lr) + 0.05)
	ys, xs = np.nonzero(mask)
	ry, rx = np.nonzero(ring)
	step = max(1, int(6 * scale))
	best = (0.0, 0, 0)
	h_span, w_span = ys.max() - ys.min(), xs.max() - xs.min()
	y_lo, y_hi = -min(ys.min(), ry.min()), H - 1 - max(ys.max(), ry.max())
	x_lo, x_hi = -min(xs.min(), rx.min()), W - 1 - max(xs.max(), rx.max())
	for oy in range(y_lo, y_hi + 1, step):
		for ox in range(x_lo, x_hi + 1, step):
			if abs(oy) < h_span / 2 and abs(ox) < w_span / 2:
				continue # перекривається з бандою більш ніж наполовину
			a_in, a_ring = L[ys + oy, xs + ox].mean(), L[ry + oy, rx + ox].mean()
			v = (max(a_in, a_ring) + 0.05) / (min(a_in, a_ring) + 0.05)
			if v > best[0]:
				best = (v, ox, oy)
	# Довідково: локальний RMS-контраст під маскою.
	k = max(1, int(8 * scale))
	def box(a):
		# Середнє у вікні k×k через сумарну таблицю — без величезних масивів на 2400×1080.
		c = np.pad(np.cumsum(np.cumsum(a, 0), 1), ((1, 0), (1, 0)))
		y0 = np.clip(np.arange(H) - k // 2, 0, H)
		y1 = np.clip(np.arange(H) + k // 2 + 1, 0, H)
		x0 = np.clip(np.arange(W) - k // 2, 0, W)
		x1 = np.clip(np.arange(W) + k // 2 + 1, 0, W)
		area = (y1 - y0)[:, None] * (x1 - x0)[None, :]
		return (c[y1][:, x1] - c[y0][:, x1] - c[y1][:, x0] + c[y0][:, x0]) / area
	m = box(L)
	local = np.sqrt(np.maximum(box(L * L) - m * m, 0))
	print(f"[focus] {os.path.basename(shot)}: банда проти оточення {sep:.2f}:1 (фігури L={lf:.3f}, кільце L={lr:.3f})")
	print(f"[focus] найкраща інша область тієї ж форми: {best[0]:.2f}:1, зсув ({best[1] / scale:+.0f}, {best[2] / scale:+.0f}) базових px")
	print(f"[focus] довідково: локальний RMS під бандою {local[mask].mean():.4f}, середній по кадру {local.mean():.4f}")
	ok = sep >= best[0]
	print(f"[focus] {'OK' if ok else 'FAIL'}: банда {'найконтрастніша' if ok else 'НЕ найконтрастніша'} область кадру ({sep / max(best[0], 1e-9):.2f}× від найкращої іншої)")
	return 0 if ok else 1


if __name__ == "__main__":
	sys.exit(main())
