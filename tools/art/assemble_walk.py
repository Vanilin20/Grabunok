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


def main() -> None:
    cid = sys.argv[1]
    keys = load_strip(f"Assets/Art/Characters/{cid}_keys.png", 3)
    pal = palette_of(keys)
    front_key, back_key, side_key = keys

    walk = {d: load_strip(f"Assets/Art/Incoming/{cid}_walk_{d}_pixellab.png", 7)[1:] for d in ("front", "back", "side")}
    walk = {d: [snap(f, pal) for f in fr] for d, fr in walk.items()}

    front = [with_head(f, front_key) for f in walk["front"]]
    side = [with_head(f, side_key) for f in walk["side"]]

    back = []
    for f in front:
        dy = bob(f) - bob(front_key)
        frame = region(f, LEG_SPLIT, CELL)
        upper = region(back_key, 0, LEG_SPLIT)
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
    print(f"{cid}: палітра {len(pal)} кольорів, стрічка {sheet.size}")


if __name__ == "__main__":
    main()
