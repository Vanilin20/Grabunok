class_name Palette
extends RefCounted
## Палітра гри — 32 кольори (docs/art/palette.gpl). Згенеровано tools/art/export_palette.py,
## руками не правити: змінюємо tools/art/palette.py і перезапускаємо експорт.

const NIGHT_0 := Color("07060d")
const NIGHT_1 := Color("0e0c1a")
const NIGHT_2 := Color("171428")
const NIGHT_3 := Color("221d38")
const NIGHT_4 := Color("2f2849")
const NIGHT_5 := Color("40375e")
const NIGHT_6 := Color("574b78")
const NIGHT_7 := Color("786b98")
const BRICK_0 := Color("2a1717")
const BRICK_1 := Color("45221f")
const BRICK_2 := Color("63302a")
const BRICK_3 := Color("874539")
const BRICK_4 := Color("b0634a")
const STEEL_0 := Color("3a3f4f")
const STEEL_1 := Color("5a6175")
const STEEL_2 := Color("8b93a8")
const STEEL_3 := Color("c7cede")
const LAMP_0 := Color("4a2e1a")
const LAMP_1 := Color("8a5a24")
const LAMP_2 := Color("d8962f")
const LAMP_3 := Color("f7c95a")
const LAMP_4 := Color("fff2c4")
const NEON_0 := Color("5c1036")
const NEON_1 := Color("a3195b")
const NEON_2 := Color("ff3d7f")
const NEON_3 := Color("ffb3cf")
const CYAN_0 := Color("0f3a4d")
const CYAN_1 := Color("1a7f99")
const CYAN_2 := Color("3de0ff")
const CYAN_3 := Color("c2f6ff")
const ALARM := Color("e8433a")
const OK := Color("5cf08a")

# Ролі кольорів в інтерфейсі.
const NIGHT := NIGHT_0
const DEEP := NIGHT_1
const DUSK := NIGHT_2
const SLATE := NIGHT_4
const STEEL := NIGHT_5
const MUTED := STEEL_2 # другорядний текст: контраст 6:1 на NIGHT_1
const TEXT := STEEL_3
const LAMP := LAMP_3
const NEON := NEON_2
const CYAN := CYAN_2
const DANGER := ALARM
const SAFE := OK
