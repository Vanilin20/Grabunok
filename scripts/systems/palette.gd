class_name Palette
extends RefCounted
## Робоча палітра інтерфейсу й тестових екранів.
## ТИМЧАСОВА: фінальна палітра гри ще не затверджена (відкрите питання ТЗ «Розмір тайла і палітра»).
## Усі кольори UI беруться тільки звідси, щоб потім замінити палітру в одному місці.

const NIGHT := Color("0a0912") # фон, найглибша тінь
const DEEP := Color("14121f") # панелі
const DUSK := Color("1e1b2e") # кнопки
const SLATE := Color("2b2740") # рамки, натиснуті кнопки
const STEEL := Color("3a3552") # рамки активних елементів
const MUTED := Color("736e8c") # другорядний текст
const TEXT := Color("e8e4f0") # основний текст
const LAMP := Color("f2c14e") # тепле світло ліхтарів, акцент
const NEON := Color("ff3d7f") # неон вивісок
const CYAN := Color("3de0ff") # екрани, техніка
const DANGER := Color("ff4a3d") # тривога
const SAFE := Color("5cf08a") # усе гаразд
