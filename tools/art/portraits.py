"""Портрети ролей для лобі: 36×36 (рамка 2 px + поле 32×32), три стани на роль.

Вихід: Assets/Art/UI/portrait_<role>_{normal,selected,locked}.png
  normal   — сталева рамка з заклепками, тло в кольорі ролі, персонаж 1:1 з rim-світлом праворуч;
  selected — рамка світиться кольором ролі (неон), тло світліше;
  locked   — темний силует, приглушена рамка, навісний замок.
Хакер: обличчя з ключової пози (зелена пляма від екрана) перемальовано — шкіра, тінь під капюшоном,
блиск очей, ніс; світло екрана лишилось слабким бірюзовим знизу (підборіддя).
Зломщик: персонажа ще немає — у полі його інструмент, сейф (Assets/Art/Heist/safe_closed.png).
Водій: персонажа немає й роль закрита до етапу 4 — лише силует (контур Тіні) із замком.
Запуск: python -I tools/art/portraits.py
"""
from PIL import Image

CELL = 32
SIZE = 36


def palette() -> dict:
    pal = {}
    for line in open("docs/art/palette.gpl", encoding="utf-8"):
        parts = line.split()
        if len(parts) == 4 and parts[0].isdigit():
            pal[parts[3]] = tuple(int(v) for v in parts[:3]) + (255,)
    return pal


P = palette()


def key(cid: str, index: int) -> Image.Image:
    strip = Image.open(f"Assets/Art/Characters/{cid}_keys.png").convert("RGBA")
    return strip.crop((index * CELL, 0, index * CELL + CELL, CELL))


def hacker_face(img: Image.Image) -> Image.Image:
    """Обличчя Хакера: шкіра в тіні капюшона, очі з блиском, ніс, слабке бірюзове світло екрана знизу."""
    out = img.copy()
    px = out.load()
    rows = {
        8: ["night_2", "night_3", "night_3", "night_3", "night_3", "night_2"],
        9: ["night_2", "brick_1", "brick_1", "brick_1", "brick_1", "night_2"],
        10: ["night_3", "brick_1", "brick_2", "brick_1", "brick_1", "night_3"],
        11: ["brick_1", "steel_3", "brick_3", "brick_2", "steel_3", "brick_1"],
        12: ["brick_2", "brick_2", "brick_3", "brick_1", "brick_2", "brick_1"],
        13: ["brick_1", "cyan_0", "night_2", "night_2", "cyan_0", "brick_1"],
        14: ["night_2", "cyan_0", "cyan_1", "cyan_1", "cyan_0", "night_2"],
    }
    for y, names in rows.items():
        for i, name in enumerate(names):
            px[13 + i, y] = P[name]
    return out


def silhouette(img: Image.Image, fill: str, edge: str) -> Image.Image:
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    src = img.load()
    px = out.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            if src[x, y][3] < 128:
                continue
            border = any(
                not (0 <= x + dx < w and 0 <= y + dy < h) or src[x + dx, y + dy][3] < 128
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
            )
            px[x, y] = P[edge] if border else P[fill]
    return out


def rim(img: Image.Image, color: str) -> Image.Image:
    """Rim-світло: перший непрозорий піксель праворуч у кожному рядку — колір ролі (світло з-за спини праворуч)."""
    out = img.copy()
    px = out.load()
    for y in range(CELL):
        for x in range(CELL - 1, -1, -1):
            if px[x, y][3] >= 128:
                if x > 0 and px[x - 1, y][3] >= 128:
                    px[x - 1, y] = P[color]  # сам контур лишається темним, світиться піксель під ним
                break
    return out


def background(ramp: list, lit: bool) -> Image.Image:
    """Тло поля 32×32: від кольору ролі вгорі до ночі внизу, пляма світла за персонажем, тінь під ногами."""
    bg = Image.new("RGBA", (CELL, CELL), P["night_1"])
    px = bg.load()
    top, mid = (ramp[1], ramp[0]) if lit else (ramp[0], "night_2")
    for y in range(CELL):
        for x in range(CELL):
            if y < 10:
                name = top if (x + y) % 2 == 0 or y < 6 else mid  # дизеринг переходу
            elif y < 20:
                name = mid if (x + y) % 2 == 0 or y < 15 else "night_1"
            else:
                name = "night_1"
            px[x, y] = P[name]
    for x in range(9, 23):
        px[x, 30] = P["night_0"]  # тінь під ногами
    for x in range(11, 21):
        px[x, 31] = P["night_0"]
    return bg


def frame(img: Image.Image, state: str, ramp: list) -> Image.Image:
    out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    out.paste(img, (2, 2))
    px = out.load()
    for i in range(SIZE):
        for a, b in ((i, 0), (i, SIZE - 1), (0, i), (SIZE - 1, i)):
            px[a, b] = P["night_0"]
    if state == "selected":
        inner_tl, inner_br, rivet = P[ramp[2]], P[ramp[1]], P["lamp_4"]
    elif state == "locked":
        inner_tl, inner_br, rivet = P["night_3"], P["night_2"], P["night_4"]
    else:
        inner_tl, inner_br, rivet = P["steel_2"], P["steel_0"], P["steel_3"]
    for i in range(1, SIZE - 1):
        px[i, 1] = inner_tl
        px[1, i] = inner_tl
        px[i, SIZE - 2] = inner_br
        px[SIZE - 2, i] = inner_br
    for x, y in ((1, 1), (SIZE - 2, 1), (1, SIZE - 2), (SIZE - 2, SIZE - 2)):
        px[x, y] = rivet  # заклепки по кутах
    return out


def lock(img: Image.Image) -> None:
    """Навісний замок 7×8 у правому нижньому куті поля."""
    px = img.load()
    x0, y0 = 25, 22
    for x in range(x0 + 1, x0 + 6):
        px[x, y0] = P["steel_1"]
    for y in range(y0, y0 + 4):
        px[x0 + 1, y] = P["steel_2"]
        px[x0 + 5, y] = P["steel_1"]
    for y in range(y0 + 3, y0 + 8):
        for x in range(x0, x0 + 7):
            px[x, y] = P["lamp_1"] if (x == x0 or y == y0 + 3) else P["lamp_0"]
    px[x0 + 3, y0 + 5] = P["night_0"]
    px[x0 + 3, y0 + 6] = P["night_0"]


ROLES = {
    # роль: (джерело, ramp тла й рамки, колір rim)
    "hacker": (lambda: hacker_face(key("hacker", 0)), ["cyan_0", "cyan_1", "cyan_2"], "cyan_1"),
    "shadow": (lambda: key("shadow", 0), ["night_4", "night_6", "night_7"], "night_7"),
    "safecracker": (None, ["lamp_0", "lamp_1", "lamp_3"], "lamp_2"),
    "distractor": (lambda: key("distractor", 0), ["neon_0", "neon_1", "neon_2"], "neon_2"),
    "driver": (lambda: key("shadow", 0), ["cyan_0", "cyan_1", "ok"], "ok"),
}


def figure(role: str) -> Image.Image:
    src = ROLES[role][0]
    if src is None:
        safe = Image.open("Assets/Art/Heist/safe_closed.png").convert("RGBA")
        out = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
        out.paste(safe, (6, 10), safe)
        return out
    return src()


def main() -> None:
    for role, (_, ramp, rim_color) in ROLES.items():
        fig = figure(role)
        for state in ("normal", "selected", "locked"):
            if state == "locked" or role == "driver":
                body = silhouette(fig, "night_2", "night_4")
                bg = background(["night_2", "night_3", "night_4"], False)
            else:
                body = rim(fig, rim_color)
                bg = background(ramp, state == "selected")
            bg.alpha_composite(body)
            if state == "locked" or role == "driver":
                lock(bg)
            frame(bg, state, ramp).save(f"Assets/Art/UI/portrait_{role}_{state}.png")
    print("portraits ok")


if __name__ == "__main__":
    main()
