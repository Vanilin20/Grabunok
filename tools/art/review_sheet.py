"""Телефонні версії (320×180) і аркуш набору кадрів рецензії. Запуск: python -I tools/art/review_sheet.py <prefix> <out_sheet>"""
import sys
from PIL import Image

NAMES = ["1_start", "2_aisle", "3_hall_right", "4_counter", "5_backroom"]


def main() -> None:
    prefix, out = sys.argv[1], sys.argv[2]
    ims = [Image.open(f"{prefix}_{n}.png").convert("RGB") for n in NAMES]
    for n, im in zip(NAMES, ims):
        # 1280×720 = 2× від 640×360; «як на телефоні» — 640×360 зменшений до 50% = 320×180.
        im.resize((320, 180), Image.NEAREST).save(f"{prefix}_{n}_phone_half.png")
    sheet = Image.new("RGB", (3 * 640, 2 * 360), (10, 10, 16))
    for i, im in enumerate(ims):
        sheet.paste(im.resize((640, 360), Image.NEAREST), ((i % 3) * 640, (i // 3) * 360))
    sheet.save(out)


if __name__ == "__main__":
    main()
