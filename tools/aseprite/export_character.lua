-- Експорт персонажа з .aseprite у стрічки для Unity (CharacterSpriteAnimator).
--   <id>_sheet.png — 8×4 кадри по 32 px: рядки вниз, праворуч (дзеркало профілю), вгору, ліворуч;
--                    стовпці: 2 кадри idle-тегу (спокій, вдих) + 6 кадрів walk-тегу.
--   <id>_halo.png  — маска силуету (білий там, де альфа ≥ 128) для rim з боку джерела.
-- Кадри беруться за тегами, тож порядок кадрів у файлі може змінюватись — важливі теги.
-- Видимі шари зводяться (drawSprite), прихованих у стрічці немає.
--
-- Запуск:
--   Aseprite.exe -b --script-param src=Assets/Art/Source/<id>.aseprite \
--     --script-param sheet=Assets/Art/Characters/<id>_sheet.png --script-param halo=Assets/Art/Characters/<id>_halo.png \
--     --script tools/aseprite/export_character.lua

local p = app.params
local spr = app.open(p.src)
if not spr then
  error("не відкрився " .. tostring(p.src))
end

local W, H = spr.width, spr.height

local function tagFrames(name)
  for _, t in ipairs(spr.tags) do
    if t.name == name then
      local list = {}
      for f = t.fromFrame.frameNumber, t.toFrame.frameNumber do
        list[#list + 1] = f
      end
      return list
    end
  end
  error("немає тегу " .. name)
end

local function rowFrames(dir)
  local idle = tagFrames("idle_" .. dir)
  local walk = tagFrames("walk_" .. dir)
  if #idle ~= 2 or #walk ~= 6 then
    error(dir .. ": потрібно idle = 2 кадри (спокій, вдих) і walk = 6 кадрів, є " .. #idle .. " і " .. #walk)
  end
  local list = {}
  for _, f in ipairs(idle) do list[#list + 1] = f end
  for _, f in ipairs(walk) do list[#list + 1] = f end
  return list
end

local function frameImage(f)
  local img = Image(W, H, ColorMode.RGB)
  img:drawSprite(spr, f, Point(0, 0))
  return img
end

local function mirrored(img)
  local out = Image(W, H, ColorMode.RGB)
  for y = 0, H - 1 do
    for x = 0, W - 1 do
      out:drawPixel(W - 1 - x, y, img:getPixel(x, y))
    end
  end
  return out
end

local rows = {
  { frames = rowFrames("down"), mirror = false },
  { frames = rowFrames("side"), mirror = true }, -- праворуч = дзеркало профілю
  { frames = rowFrames("up"), mirror = false },
  { frames = rowFrames("side"), mirror = false },
}

local sheet = Image(8 * W, 4 * H, ColorMode.RGB)
for r, row in ipairs(rows) do
  for c, f in ipairs(row.frames) do
    local img = frameImage(f)
    if row.mirror then
      img = mirrored(img)
    end
    sheet:drawImage(img, Point((c - 1) * W, (r - 1) * H))
  end
end

sheet:saveAs(p.sheet)

if p.halo and p.halo ~= "" then
  local halo = Image(sheet.width, sheet.height, ColorMode.RGB)
  local white = app.pixelColor.rgba(255, 255, 255, 255)
  for y = 0, sheet.height - 1 do
    for x = 0, sheet.width - 1 do
      if app.pixelColor.rgbaA(sheet:getPixel(x, y)) >= 128 then
        halo:drawPixel(x, y, white)
      end
    end
  end
  halo:saveAs(p.halo)
end

print("експорт " .. p.src .. " → " .. p.sheet)
