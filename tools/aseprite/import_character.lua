-- Збирає .aseprite персонажа з шарів-стрічок (кадри 32×32 у рядок: вниз ×8, вгору ×8, профіль-ліворуч ×8).
-- Шари: body (обов'язково), flashlight (необов'язково — ліхтарик у руці охоронця).
-- Теги: idle_down 1–2, walk_down 3–8, idle_up 9–10, walk_up 11–16, idle_side 17–18, walk_side 19–24.
-- Палітра проєкту підключається до файлу (колірний режим RGB — пікселі не змінюються).
--
-- Запуск:
--   Aseprite.exe -b --script-param body=<body.png> --script-param flash=<flash.png> \
--     --script-param palette=docs/art/palette.gpl --script-param out=Assets/Art/Source/<id>.aseprite \
--     --script tools/aseprite/import_character.lua

local CELL = 32
local IDLE_SEC = 1 / 1.4 -- як у CharacterSpriteAnimator (BreathFps)
local WALK_SEC = 1 / 10  -- _walkFps

local p = app.params
if not p.body or not p.out or not p.palette then
  error("потрібні параметри body, out, palette")
end

local body = Image{ fromFile = p.body }
local frames = body.width // CELL
if frames ~= 24 or body.height ~= CELL then
  error("body: очікується 24 кадри 32×32 у рядок, отримано " .. body.width .. "×" .. body.height)
end

local spr = Sprite(CELL, CELL, ColorMode.RGB)
spr:setPalette(Palette{ fromFile = p.palette })

local bodyLayer = spr.layers[1]
bodyLayer.name = "body"
local flash = nil
local flashLayer = nil
if p.flash and p.flash ~= "" then
  flash = Image{ fromFile = p.flash }
  flashLayer = spr:newLayer()
  flashLayer.name = "flashlight"
end

for i = 2, frames do
  spr:newEmptyFrame()
end

local function cut(src, index)
  local img = Image(CELL, CELL, ColorMode.RGB)
  img:drawImage(src, Point(-(index - 1) * CELL, 0))
  return img
end

for i = 1, frames do
  spr:newCel(bodyLayer, i, cut(body, i), Point(0, 0))
  if flash then
    spr:newCel(flashLayer, i, cut(flash, i), Point(0, 0))
  end
  local inRow = (i - 1) % 8
  spr.frames[i].duration = inRow < 2 and IDLE_SEC or WALK_SEC
end

local function tag(from, to, name)
  local t = spr:newTag(from, to)
  t.name = name
end

tag(1, 2, "idle_down")
tag(3, 8, "walk_down")
tag(9, 10, "idle_up")
tag(11, 16, "walk_up")
tag(17, 18, "idle_side")
tag(19, 24, "walk_side")

spr:saveAs(p.out)
print("збережено " .. p.out .. ": " .. frames .. " кадрів, шарів " .. #spr.layers)
