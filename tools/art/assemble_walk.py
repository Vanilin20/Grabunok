"""Збирання стрічки персонажа 32 px (8 стовпців × 4 рядки) з ключових поз і ходьби PixelLab.

Вхід:
  Assets/Art/Characters/<id>_keys.png            — ключові пози, зведені до палітри (кадри 0 front, 1 back, 2 side — дивиться ліворуч)
  Assets/Art/Incoming/<id>_walk_<dir>_pixellab.png — 7 кадрів (0 — вхідна поза, 1..6 — ходьба), dir = front/back/side
Вихід:
  Assets/Art/Characters/<id>_sheet.png — рядки: вниз, праворуч, вгору, ліворуч; стовпці: idle, вдих, 6 кадрів ходьби

Обробка:
  - колір кожного пікселя → найближчий з кольорів ключових поз (палітра персонажа, без нових тонів);
  - голова (маска, очі) з ключової пози вставляється на кожен кадр зі зсувом, що найкраще збігається:
    PixelLab губить маску в частині кадрів;
  - спина: верх із ключової пози + ноги з ходьби анфас (у генерації ззаду ноги майже не рухаються);
  - праворуч = дзеркало профілю.
Запуск: python -I tools/art/assemble_walk.py shadow
"""
import sys
from PIL import Image

CELL = 32
HEAD_ROWS = 15   # рядки 0..14 — капюшон і обличчя
LEG_SPLIT = 22   # з цього рядка — ноги


def load_strip(path: str, count: int) -> list:
    im = Image.open(path).convert("RGBA")
    return [im.crop((i * CELL, 0, i * CELL + CELL, CELL)) for i in range(count)]


def palette_of(frames: list) -> list:
    cols = set()
    for f in frames:
        for p in f.get_flattened_data():
            if p[3] >= 128:
                cols.add(p[:3])
    return sorted(cols)


def snap(frame: Image.Image, pal: list) -> Image.Image:
    out = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    px = out.load()
    src = frame.load()
    for y in range(CELL):
        for x in range(CELL):
            r, g, b, a = src[x, y]
            if a < 128:
                continue
            best = min(pal, key=lambda c: (c[0] - r) ** 2 * 3 + (c[1] - g) ** 2 * 4 + (c[2] - b) ** 2 * 2)
            px[x, y] = best + (255,)
    return out


def region(frame: Image.Image, top: int, bottom: int) -> Image.Image:
    part = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    part.paste(frame.crop((0, top, CELL, bottom)), (0, top))
    return part


def best_offset(key_head: Image.Image, frame: Image.Image) -> tuple:
    """Зсув голови ключової пози, за якого вона найкраще лягає на силует кадру."""
    k = key_head.load()
    f = frame.load()
    best, best_score = (0, 0), None
    for dy in range(-2, 3):
        for dx in range(-2, 3):
            score = 0
            for y in range(HEAD_ROWS):
                for x in range(CELL):
                    ka = k[x, y][3] >= 128
                    fx, fy = x + dx, y + dy
                    fa = 0 <= fx < CELL and 0 <= fy < CELL and f[fx, fy][3] >= 128
                    score += ka != fa
            score += abs(dx) + abs(dy)  # за рівності — менший зсув
            if best_score is None or score < best_score:
                best, best_score = (dx, dy), score
    return best


def with_head(frame: Image.Image, key: Image.Image) -> Image.Image:
    head = region(key, 0, HEAD_ROWS)
    dx, dy = best_offset(head, frame)
    out = frame.copy()
    # Прибираємо стару голову кадру, щоб не лишилось «привидів» поза новою.
    px = out.load()
    # Плечі (останні 3 рядки зони голови) лишаються з кадру — там рух рук.
    for y in range(0, max(0, HEAD_ROWS + dy - 3)):
        for x in range(CELL):
            px[x, y] = (0, 0, 0, 0)
    out.paste(head, (dx, dy), head)
    return out


def breath(key: Image.Image) -> Image.Image:
    """Вдих: плечі й голова на 1 px вгору, ноги на місці."""
    out = region(key, LEG_SPLIT - 5, CELL)  # рядок стику береться двічі — без щілини
    upper = region(key, 0, LEG_SPLIT - 4)
    out.paste(upper, (0, -1), upper)
    return out


def bob(frame: Image.Image) -> int:
    return frame.getbbox()[1]


def load_palette() -> dict:
    """Кольори docs/art/palette.gpl за назвою."""
    pal = {}
    for line in open("docs/art/palette.gpl", encoding="utf-8"):
        parts = line.split()
        if len(parts) == 4 and parts[0].isdigit():
            pal[parts[3]] = tuple(int(v) for v in parts[:3]) + (255,)
    return pal


def put(img: Image.Image, pixels: list, color: tuple) -> None:
    px = img.load()
    for x, y in pixels:
        if 0 <= x < CELL and 0 <= y < CELL:
            px[x, y] = color


def outline(img: Image.Image, pixels: list, color: tuple) -> None:
    """Контур навколо доданої деталі — лише там, де зараз прозоро."""
    px = img.load()
    own = set(pixels)
    for x, y in pixels:
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if (nx, ny) not in own and 0 <= nx < CELL and 0 <= ny < CELL and px[nx, ny][3] < 128:
                px[nx, ny] = color


def symmetric_legs(frames: list) -> list:
    """Анфас: кадри 4–6 = кадри 1–3 з дзеркальними ногами — кроки обох ніг однакові."""
    half = frames[:3]
    out = list(half)
    for f in half:
        legs = region(f, LEG_SPLIT + 1, CELL).transpose(Image.FLIP_LEFT_RIGHT)
        frame = region(f, 0, LEG_SPLIT + 1)
        frame.paste(legs, (0, 0), legs)
        out.append(frame)
    return out


def swing_arms(frame: Image.Image, left_dy: int, right_dy: int, left_max_x: int, right_min_x: int) -> Image.Image:
    """Мах руками: кисть і передпліччя (рядки 19–23) зсуваються на dy, плече лишається."""
    out = frame.copy()
    src = frame.load()
    px = out.load()
    for x in range(CELL):
        dy = left_dy if x <= left_max_x else right_dy if x >= right_min_x else 0
        if dy == 0:
            continue
        for y in range(19, 25):
            sy = y - dy
            px[x, y] = src[x, sy] if 19 <= sy < 25 else ((0, 0, 0, 0) if dy < 0 else src[x, 19])
    return out


def shadow_back(key: Image.Image, pal: dict) -> Image.Image:
    """Тінь ззаду: складки й шов капюшона, rim зверху-зліва, ломик з рюкзака за силуетом."""
    out = key.copy()
    # Rim від верхнього лівого світла: перший піксель усередині контуру.
    put(out, [(14, 4), (12, 5), (11, 6), (11, 7), (11, 8), (10, 9), (10, 10)], pal["night_6"])
    put(out, [(13, 5), (12, 6), (12, 7), (15, 4)], pal["night_7"])
    # Освітлена ліва частка капюшона.
    put(out, [(13, 6), (13, 7), (14, 6), (12, 8), (13, 8), (11, 9), (12, 9), (11, 10)], pal["night_5"])
    # Шов посередині: темна лінія, праворуч від неї — затінений бік.
    put(out, [(16, 5), (16, 6), (16, 7), (16, 8), (16, 9), (16, 10), (16, 11)], pal["night_2"])
    put(out, [(17, 6), (17, 7), (17, 8), (18, 9), (18, 10), (19, 8), (19, 9), (19, 10), (19, 11), (20, 9), (20, 10), (20, 11)], pal["night_3"])
    # Складка внизу капюшона, де він лягає на плечі.
    put(out, [(13, 11), (14, 12), (18, 12)], pal["night_2"])
    # Ломик із рюкзака, стирчить над правим плечем.
    bar = [(19, 15), (20, 14), (21, 13), (22, 12), (23, 11), (23, 10)]
    hook = [(24, 9), (25, 10)]
    put(out, bar, pal["steel_1"])
    put(out, [(22, 12), (23, 11), (23, 10)], pal["steel_2"])
    put(out, hook, pal["steel_1"])
    outline(out, bar + hook, pal["night_0"])
    return out


def fit_to(src: Image.Image, ref: Image.Image, pal: list) -> Image.Image:
    """Чужу генерацію (інший розмір) — у клітинку 32 з габаритами ref: більшість кольорів у вікні, зведення до палітри."""
    src = src.crop(src.getbbox())
    box = ref.getbbox()
    tw, th = box[2] - box[0], box[3] - box[1]
    sw, sh = src.size
    out = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    px = out.load()
    sp = src.load()
    for ty in range(th):
        for tx in range(tw):
            x0 = tx * sw // tw
            x1 = max(x0 + 1, (tx + 1) * sw // tw)
            y0 = ty * sh // th
            y1 = max(y0 + 1, (ty + 1) * sh // th)
            cells = [sp[x, y] for x in range(x0, x1) for y in range(y0, y1)]
            opaque = [c[:3] for c in cells if c[3] >= 128]
            if len(opaque) * 2 < len(cells):
                continue
            r, g, b = max(set(opaque), key=opaque.count)
            best = min(pal, key=lambda c: (c[0] - r) ** 2 * 3 + (c[1] - g) ** 2 * 4 + (c[2] - b) ** 2 * 2)
            px[box[0] + tx, box[1] + ty] = best + (255,)
    return out


def hand(frame: Image.Image, xs: range, ys: range) -> tuple:
    """Кисть: найнижчий непрозорий піксель у смузі руки (за рівності — крайній)."""
    px = frame.load()
    best = None
    for y in ys:
        for x in xs:
            if px[x, y][3] >= 128:
                best = (x, y)
    return best


def flashlight(frame: Image.Image, direction: str, gpl: dict) -> tuple:
    """Ліхтарик у правій руці. Повертає точку лінзи (x, y) у пікселях кадру — туди ставиться Light2D."""
    out = frame
    if direction == "down":      # права рука — ліворуч на екрані, світить униз
        h = hand(out, range(4, 9), range(16, 25))
        body, lens = [(h[0], h[1] + 1)], (h[0], h[1] + 2)
    elif direction == "up":      # права рука — праворуч на екрані, лінза від глядача, її не видно
        h = hand(out, range(23, 28), range(16, 25))
        body, lens = [(h[0], h[1] + 1)], (h[0], h[1] - 2)
    else:                        # "left": профіль, світить уперед
        h = hand(out, range(4, 12), range(16, 25))
        body, lens = [(h[0] - 1, h[1])], (h[0] - 2, h[1])
    put(out, body, gpl["steel_1"])
    if direction != "up":
        put(out, [lens], gpl["lamp_3"])
    outline(out, body + ([lens] if direction != "up" else []), gpl["night_0"])
    return lens


def rim_right(frame: Image.Image, color: tuple) -> Image.Image:
    """Піксель під правим контуром у кожному рядку — колір rim (контур лишається темним)."""
    out = frame.copy()
    px = out.load()
    for y in range(CELL):
        for x in range(CELL - 1, 0, -1):
            if px[x, y][3] >= 128:
                if px[x - 1, y][3] >= 128:
                    px[x - 1, y] = color
                break
    return out


CONFIG = {
    # keys: індекси front/back/side у <id>_keys.png (side дивиться ліворуч); back_src — окрема генерація виду ззаду.
    "shadow": {"keys": (0, 1, 2), "back_src": None, "side_replace": {3: 0}, "flashlight": False, "rim": "cyan_1"},
    "guard": {"keys": (0, None, 7), "back_src": "Assets/Art/Incoming/guard_back_spritecook.png", "side_replace": {}, "flashlight": True},
}


def main() -> None:
    cid = sys.argv[1]
    cfg = CONFIG[cid]
    strip = Image.open(f"Assets/Art/Characters/{cid}_keys.png").convert("RGBA")
    all_keys = load_strip(f"Assets/Art/Characters/{cid}_keys.png", strip.width // CELL)
    pal = palette_of(all_keys)
    gpl = load_palette()
    fi, bi, si = cfg["keys"]
    front_key, side_key = all_keys[fi], all_keys[si]
    if cfg["back_src"]:
        back_key = fit_to(Image.open(cfg["back_src"]).convert("RGBA"), front_key, pal)
    else:
        back_key = all_keys[bi]
    if cid == "shadow":
        back_key = shadow_back(back_key, gpl)
    pal = sorted(set(pal) | {gpl[n][:3] for n in ("steel_1", "steel_2", "lamp_3")})

    walk = {d: load_strip(f"Assets/Art/Incoming/{cid}_walk_{d}_pixellab.png", 7)[1:] for d in ("front", "side")}
    walk = {d: [snap(f, pal) for f in fr] for d, fr in walk.items()}

    front = symmetric_legs([with_head(f, front_key) for f in walk["front"]])
    side = [with_head(f, side_key) for f in walk["side"]]
    for bad, good in cfg["side_replace"].items():
        side[bad] = side[good]

    # Спина: верх виду ззаду + ноги з анфасу; руки махають у протифазі до ноги, що виходить уперед.
    swing = [(0, 0), (-1, 1), (-1, 1), (0, 0), (1, -1), (1, -1)]
    back = []
    for f, (ldy, rdy) in zip(front, swing):
        dy = bob(f) - bob(front_key)
        frame = region(f, LEG_SPLIT, CELL)
        upper = swing_arms(region(back_key, 0, LEG_SPLIT), ldy, rdy, 10, 21)
        frame.paste(upper, (0, dy), upper)
        back.append(frame)

    down = [front_key, breath(front_key)] + front
    up = [back_key, breath(back_key)] + back
    left = [side_key, breath(side_key)] + [f.copy() for f in side]
    if cfg["flashlight"]:
        lenses = {}
        for name, row in (("down", down), ("up", up), ("left", left)):
            row[:] = [f.copy() for f in row]
            lenses[name] = [flashlight(f, name, gpl) for f in row]
        # Опора Light2D: середня точка лінзи, у пікселях від центру кадру (Y угору, як у Unity).
        for name, pts in lenses.items():
            ax = sum(p[0] for p in pts) / len(pts) + 0.5 - CELL / 2
            ay = CELL / 2 - (sum(p[1] for p in pts) / len(pts) + 0.5)
            print(f"lens {name}: ({ax:.1f}, {ay:.1f})")
    right = [f.transpose(Image.FLIP_LEFT_RIGHT) for f in left]

    rows = [down, right, up, left]
    if cfg.get("rim"):
        # Rim-світло праворуч (вітрини й неон за спиною): Тінь Unlit, тож кант запікається в спрайт.
        rows = [[rim_right(f, gpl[cfg["rim"]]) for f in row] for row in rows]
    sheet = Image.new("RGBA", (8 * CELL, 4 * CELL), (0, 0, 0, 0))
    for r, row in enumerate(rows):
        for c, fr in enumerate(row):
            sheet.paste(fr, (c * CELL, r * CELL))
    sheet.save(f"Assets/Art/Characters/{cid}_sheet.png")
    print(f"{cid}: palette {len(pal)}, sheet {sheet.size}")


if __name__ == "__main__":
    main()
