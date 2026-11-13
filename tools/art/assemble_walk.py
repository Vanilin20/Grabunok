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


def main() -> None:
    cid = sys.argv[1]
    keys = load_strip(f"Assets/Art/Characters/{cid}_keys.png", 3)
    pal = palette_of(keys)
    gpl = load_palette()
    front_key, back_key, side_key = keys
    if cid == "shadow":
        back_key = shadow_back(back_key, gpl)
        pal = sorted(set(pal) | {gpl["steel_1"][:3], gpl["steel_2"][:3]})

    walk = {d: load_strip(f"Assets/Art/Incoming/{cid}_walk_{d}_pixellab.png", 7)[1:] for d in ("front", "back", "side")}
    walk = {d: [snap(f, pal) for f in fr] for d, fr in walk.items()}

    front = symmetric_legs([with_head(f, front_key) for f in walk["front"]])
    side = [with_head(f, side_key) for f in walk["side"]]
    # Профіль: кадр 3 (рука злипається з тулубом, ноги одним стовпцем) — та сама фаза «прохід», що кадр 0.
    side[3] = side[0]

    # Спина: верх ключової пози + ноги з анфасу; руки махають у протифазі до ноги, що виходить уперед.
    swing = [(0, 0), (-1, 1), (-1, 1), (0, 0), (1, -1), (1, -1)]
    back = []
    for f, (ldy, rdy) in zip(front, swing):
        dy = bob(f) - bob(front_key)
        frame = region(f, LEG_SPLIT, CELL)
        upper = swing_arms(region(back_key, 0, LEG_SPLIT), ldy, rdy, 10, 21)
        frame.paste(upper, (0, dy), upper)
        back.append(frame)

    rows = [
        [front_key, breath(front_key)] + front,
        [k.transpose(Image.FLIP_LEFT_RIGHT) for k in [side_key, breath(side_key)] + side],
        [back_key, breath(back_key)] + back,
        [side_key, breath(side_key)] + side,
    ]
    sheet = Image.new("RGBA", (8 * CELL, 4 * CELL), (0, 0, 0, 0))
    for r, row in enumerate(rows):
        for c, fr in enumerate(row):
            sheet.paste(fr, (c * CELL, r * CELL))
    sheet.save(f"Assets/Art/Characters/{cid}_sheet.png")
    print(f"{cid}: palette {len(pal)}, sheet {sheet.size}")


if __name__ == "__main__":
    main()
