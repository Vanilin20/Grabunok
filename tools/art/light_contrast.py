"""Контраст «пляма лампи / темрява між плямами» на зафіксованому наборі кадрів (docs/levels/review_frames.md).
Ділянки — чиста підлога в кадрі 1280×720, однакові для всіх ітерацій. Яскравість — середня luma (0–255).
Запуск: python -I tools/art/light_contrast.py <тека_або_префікс>   (напр. Captures/unity/shadow_set)"""
import sys
from PIL import Image

# (кадр, (x0, y0, x1, y1), опис)
PATCHES = [
    ("4_counter", (170, 218, 230, 238), "пляма: прохід між стелажами під лампою"),
    ("4_counter", (860, 230, 920, 260), "темрява: підлога праворуч від стелажа C"),
    ("2_aisle", (1080, 290, 1160, 330), "пляма: підлога під лампою в куті залу"),
    ("2_aisle", (265, 525, 310, 565), "темрява: лівий прохід між стелажем і стіною"),
]


def luma(path: str, box: tuple) -> float:
    im = Image.open(path).convert("RGB").crop(box)
    px = list(im.get_flattened_data())
    return sum(0.2126 * r + 0.7152 * g + 0.0722 * b for r, g, b in px) / len(px)


def main() -> None:
    prefix = sys.argv[1]
    for frame, box, label in PATCHES:
        print(f"{frame:10s} {label}: {luma(f'{prefix}_{frame}.png', box):.1f}")


if __name__ == "__main__":
    main()
