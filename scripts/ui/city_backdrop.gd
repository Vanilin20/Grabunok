extends Node2D
## Нічне місто з даху — фон меню, лобі й підсумку.
##
## Статичні шари (небо, будинки, дах) лежать у сцені. Живі деталі розставляються тут
## за assets/sprites/menu/menu_layout.json, який генерує tools/art/gen_menu.py разом з артом,
## тож координати вогнів завжди збігаються з малюнком.
##
## Живе: хмари пливуть, прожектори гуляють небом, мигають вогні антен, по естакаді ходить потяг,
## у вікнах мерехтять телевізори, неон «БАНДА» зрідка збоїть (і калюжа збоїть разом з ним),
## лампа над дверима гуде, банда дихає, у дівчини світиться телефон, у хлопця тліє цигарка,
## з вентиляції йде пара, на даху — бризки дощу.

const LAYOUT_PATH: String = "res://assets/sprites/menu/menu_layout.json"
const CANVAS_SIZE: Vector2 = Vector2(864, 360)
const CLOUD_SPEED: float = 3.0 # px/с
const TRAIN_SPEED: float = 64.0
const GANG_FRAME: Vector2i = Vector2i(30, 52)
const GANG_FPS: Array[float] = [3.0, 3.6, 2.6]
const LIGHT_LAYER_WORLD: int = 1 # світло для міста й даху
const LIGHT_LAYER_GANG: int = 2 # банда: її освітлюють лише лампа, телефон і цигарка

@onready var _world: Node2D = %World
@onready var _clouds: Node2D = %Clouds
@onready var _sky_fx: Node2D = %SkyFx
@onready var _far_fx: Node2D = %FarFx
@onready var _train: Node2D = %Train
@onready var _mid_fx: Node2D = %MidFx
@onready var _letters_root: Node2D = %SignLetters
@onready var _roof_glow: Sprite2D = %RoofGlow
@onready var _gang_root: Node2D = %Gang
@onready var _roof_fx: Node2D = %RoofFx

var _rng := RandomNumberGenerator.new()
var _time: float = 0.0
var _light_texture: GradientTexture2D
var _unshaded: CanvasItemMaterial
var _additive: CanvasItemMaterial

var _blinkers: Array[Dictionary] = [] # {node, phase, period}
var _tvs: Array[Dictionary] = [] # {node, timer}
var _letters: Array[Dictionary] = [] # {node, glitch, chance}
var _flicker_lights: Array[Dictionary] = [] # {node, base, kind}
var _sign_light: PointLight2D
var _bounce_light: PointLight2D # рожевий відсвіт калюжі на парапет і дах навколо банди
var _sign_halo: Sprite2D # м'який ореол неону в повітрі (адитивний)
var _beams: Array[Dictionary] = []
var _ember_light: PointLight2D
var _train_wait: float = 2.0
var _train_dir: float = 1.0


func _ready() -> void:
	_rng.randomize()
	_light_texture = _make_light_texture()
	_unshaded = CanvasItemMaterial.new()
	_unshaded.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
	_additive = CanvasItemMaterial.new()
	_additive.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	_additive.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
	var layout: Dictionary = _load_layout()
	_build_searchlights()
	_build_blinkers(layout.get("blinkers", []))
	_build_tvs(layout.get("tvs", []))
	_build_letters(layout.get("letters", []))
	_build_lights(layout.get("lights", []))
	_build_gang(layout.get("gang", {}))
	_build_steam(layout.get("steam", []))
	_train.position.y = float(layout.get("viaduct_y", 198)) - CANVAS_SIZE.y / 2.0
	_train.visible = false


func _process(delta: float) -> void:
	_time += delta
	_update_clouds(delta)
	_update_beams()
	_update_blinkers()
	_update_tvs(delta)
	_update_letters(delta)
	_update_lights(delta)
	_update_train(delta)


# --- Побудова ------------------------------------------------------------------

func _load_layout() -> Dictionary:
	var file := FileAccess.open(LAYOUT_PATH, FileAccess.READ)
	if file == null:
		push_error("[CityBackdrop] Немає %s — запусти tools/art/gen_menu.py" % LAYOUT_PATH)
		return {}
	var data: Variant = JSON.parse_string(file.get_as_text())
	return data if data is Dictionary else {}


## Координати з генератора (полотно 864×360) → локальні координати World (центр полотна = 0,0).
func _to_world(p: Array) -> Vector2:
	return Vector2(float(p[0]), float(p[1])) - CANVAS_SIZE / 2.0


func _make_light_texture() -> GradientTexture2D:
	var gradient := Gradient.new()
	gradient.offsets = PackedFloat32Array([0.0, 0.12, 0.45, 1.0])
	gradient.colors = PackedColorArray([Color(1, 1, 1, 1), Color(1, 1, 1, 0.65), Color(1, 1, 1, 0.18), Color(1, 1, 1, 0)])
	var texture := GradientTexture2D.new()
	texture.gradient = gradient
	texture.width = 128
	texture.height = 128
	texture.fill = GradientTexture2D.FILL_RADIAL
	texture.fill_from = Vector2(0.5, 0.5)
	texture.fill_to = Vector2(0.5, 0.0)
	return texture


func _make_light(pos: Vector2, color: Color, scale: float, energy: float) -> PointLight2D:
	var light := PointLight2D.new()
	light.texture = _light_texture
	light.color = color
	light.texture_scale = scale
	light.energy = energy
	light.position = pos
	light.range_item_cull_mask = LIGHT_LAYER_WORLD | LIGHT_LAYER_GANG
	return light


func _build_searchlights() -> void:
	# Два прожектори з-за міста: адитивні промені, що повільно гуляють небом.
	for i: int in 2:
		var beam := Polygon2D.new()
		beam.material = _additive
		beam.polygon = PackedVector2Array([Vector2(-2, 0), Vector2(2, 0), Vector2(26, -330), Vector2(-26, -330)])
		var c := Color(Palette.NIGHT_6, 0.22)
		beam.vertex_colors = PackedColorArray([c, c, Color(c, 0.0), Color(c, 0.0)])
		beam.position = Vector2(-250.0 + i * 430.0, 70.0)
		_sky_fx.add_child(beam)
		_beams.append({"node": beam, "base": -0.35 + i * 0.6, "speed": 0.13 + i * 0.05, "amp": 0.38})


func _build_blinkers(points: Array) -> void:
	var texture: Texture2D = load("res://assets/sprites/fx/blinker.png")
	for p: Array in points:
		var s := Sprite2D.new()
		s.texture = texture
		s.material = _unshaded
		s.position = _to_world(p)
		s.visible = false
		_far_fx.add_child(s)
		_blinkers.append({"node": s, "phase": _rng.randf() * 3.0, "period": _rng.randf_range(1.4, 2.6)})


func _build_tvs(rects: Array) -> void:
	var texture: Texture2D = load("res://assets/sprites/fx/pixel.png")
	for r: Array in rects:
		var s := Sprite2D.new()
		s.texture = texture
		s.centered = false
		s.material = _additive
		s.position = _to_world(r)
		s.scale = Vector2(float(r[2]), float(r[3]))
		_mid_fx.add_child(s)
		_tvs.append({"node": s, "timer": 0.0})


func _build_letters(letters: Array) -> void:
	for i: int in letters.size():
		var info: Dictionary = letters[i]
		var s := Sprite2D.new()
		s.texture = load("res://assets/sprites/menu/" + str(info["file"]))
		s.centered = false
		s.material = _unshaded
		s.position = _to_world([info["x"], info["y"]])
		_letters_root.add_child(s)
		# «Д» (4-та літера) — стара трубка, збоїть частіше за інші.
		_letters.append({"node": s, "glitch": 0.0, "chance": 0.5 if i == 3 else 0.06})


func _build_lights(lights: Array) -> void:
	for info: Dictionary in lights:
		var color := Color(str(info["color"]))
		var light := _make_light(_to_world([info["x"], info["y"]]), color, float(info["scale"]), 1.0)
		light.shadow_item_cull_mask = LIGHT_LAYER_GANG
		match str(info["name"]):
			"sign":
				# Вивіска світить на будинок, парапет і дах, але не на банду: зі спини вони — силуети.
				light.energy = 1.7
				light.texture_scale = 5.0
				light.range_item_cull_mask = LIGHT_LAYER_WORLD
				_sign_light = light
				_sign_halo = Sprite2D.new()
				_sign_halo.texture = _light_texture
				_sign_halo.material = _additive
				_sign_halo.modulate = Color(color, 0.28)
				_sign_halo.scale = Vector2(2.4, 0.9)
				_sign_halo.position = light.position
				_world.add_child(_sign_halo)
				_bounce_light = _make_light(_to_world([432, 312]), color, 2.6, 1.1)
				_bounce_light.range_item_cull_mask = LIGHT_LAYER_WORLD
				# Банда стоїть між калюжею й парапетом — її тіні лягають на освітлений парапет.
				_bounce_light.shadow_enabled = true
				_bounce_light.shadow_color = Color(0, 0, 0, 0.6)
				_bounce_light.shadow_filter = Light2D.SHADOW_FILTER_NONE
				_bounce_light.shadow_item_cull_mask = LIGHT_LAYER_GANG
				_world.add_child(_bounce_light)
			"bulb":
				light.energy = 2.4
				light.texture_scale = 7.0
				light.shadow_enabled = true
				light.shadow_color = Color(0, 0, 0, 0.75)
				light.shadow_filter = Light2D.SHADOW_FILTER_NONE # тіні з рівними піксельними краями
				_flicker_lights.append({"node": light, "base": light.energy, "kind": "buzz"})
			"bar":
				light.energy = 0.9
				light.texture_scale = 1.2
				_flicker_lights.append({"node": light, "base": light.energy, "kind": "neon"})
		_world.add_child(light)


func _build_gang(gang: Dictionary) -> void:
	var order: Array[String] = ["hood", "coat", "phone"]
	for i: int in order.size():
		var key: String = order[i]
		if not gang.has(key):
			continue
		var sprite := AnimatedSprite2D.new()
		sprite.sprite_frames = _sheet_frames("res://assets/sprites/menu/gang_%s.png" % key, GANG_FPS[i])
		sprite.centered = false
		sprite.position = _to_world(gang[key])
		sprite.light_mask = LIGHT_LAYER_GANG
		sprite.play("idle")
		sprite.frame = _rng.randi_range(0, 5) # не дихають синхронно
		_gang_root.add_child(sprite)
		var occluder := LightOccluder2D.new()
		var poly := OccluderPolygon2D.new()
		poly.polygon = PackedVector2Array([Vector2(8, 10), Vector2(22, 10), Vector2(22, 48), Vector2(8, 48)])
		occluder.occluder = poly
		occluder.occluder_light_mask = LIGHT_LAYER_GANG
		sprite.add_child(occluder)
		if key == "phone":
			# Телефон у руці: холодне світло на руку й підлогу.
			var phone_light := _make_light(Vector2(22, 25), Palette.CYAN_2, 0.6, 2.0)
			sprite.add_child(phone_light)
		elif key == "hood":
			_ember_light = _make_light(Vector2(26, 40), Palette.LAMP_2, 0.12, 0.8)
			sprite.add_child(_ember_light)
			sprite.add_child(_make_smoke(Vector2(26, 39)))


func _sheet_frames(path: String, fps: float) -> SpriteFrames:
	var sheet: Texture2D = load(path)
	var frames := SpriteFrames.new()
	frames.add_animation("idle")
	frames.set_animation_speed("idle", fps)
	frames.set_animation_loop("idle", true)
	var count: int = sheet.get_width() / GANG_FRAME.x
	for i: int in count:
		var atlas := AtlasTexture.new()
		atlas.atlas = sheet
		atlas.region = Rect2(i * GANG_FRAME.x, 0, GANG_FRAME.x, GANG_FRAME.y)
		frames.add_frame("idle", atlas)
	if frames.has_animation("default"):
		frames.remove_animation("default")
	return frames


func _make_smoke(pos: Vector2) -> CPUParticles2D:
	var smoke := CPUParticles2D.new()
	smoke.position = pos
	smoke.amount = 10
	smoke.lifetime = 2.6
	smoke.preprocess = 2.0
	smoke.texture = load("res://assets/sprites/fx/puff.png")
	smoke.direction = Vector2(0.3, -1)
	smoke.spread = 12.0
	smoke.gravity = Vector2(3, -6)
	smoke.initial_velocity_min = 4.0
	smoke.initial_velocity_max = 7.0
	smoke.scale_amount_min = 0.5
	smoke.scale_amount_max = 1.0
	smoke.color_ramp = _fade_ramp(Color(Palette.NIGHT_7, 0.5))
	return smoke


func _build_steam(points: Array) -> void:
	for p: Array in points:
		var steam := CPUParticles2D.new()
		steam.position = _to_world(p)
		steam.amount = 26
		steam.lifetime = 3.2
		steam.preprocess = 3.0
		steam.texture = load("res://assets/sprites/fx/puff.png")
		steam.emission_shape = CPUParticles2D.EMISSION_SHAPE_RECTANGLE
		steam.emission_rect_extents = Vector2(4, 1)
		steam.direction = Vector2(0.15, -1)
		steam.spread = 18.0
		steam.gravity = Vector2(4, -10)
		steam.initial_velocity_min = 8.0
		steam.initial_velocity_max = 14.0
		steam.scale_amount_min = 1.0
		steam.scale_amount_max = 2.0
		steam.color_ramp = _fade_ramp(Color(Palette.STEEL_2, 0.45))
		_roof_fx.add_child(steam)


func _fade_ramp(color: Color) -> Gradient:
	var ramp := Gradient.new()
	ramp.offsets = PackedFloat32Array([0.0, 0.15, 1.0])
	ramp.colors = PackedColorArray([Color(color, 0.0), color, Color(color, 0.0)])
	return ramp


# --- Оновлення -----------------------------------------------------------------

func _update_clouds(delta: float) -> void:
	_clouds.position.x -= CLOUD_SPEED * delta
	if _clouds.position.x <= -CANVAS_SIZE.x:
		_clouds.position.x += CANVAS_SIZE.x
	# Піксельна сітка: хмари рухаються цілими пікселями.
	for child: Node2D in _clouds.get_children():
		child.position.x = roundf(child.position.x)


func _update_beams() -> void:
	for beam: Dictionary in _beams:
		(beam["node"] as Polygon2D).rotation = float(beam["base"]) + sin(_time * float(beam["speed"])) * float(beam["amp"])


func _update_blinkers() -> void:
	for b: Dictionary in _blinkers:
		var t: float = fmod(_time + float(b["phase"]), float(b["period"]))
		(b["node"] as Sprite2D).visible = t < 0.18


func _update_tvs(delta: float) -> void:
	for tv: Dictionary in _tvs:
		tv["timer"] = float(tv["timer"]) - delta
		if float(tv["timer"]) <= 0.0:
			tv["timer"] = _rng.randf_range(0.08, 0.6)
			var shades: Array[Color] = [Palette.CYAN_1, Palette.CYAN_2, Palette.CYAN_0, Palette.STEEL_2]
			var c: Color = shades[_rng.randi_range(0, shades.size() - 1)]
			(tv["node"] as Sprite2D).modulate = Color(c, _rng.randf_range(0.15, 0.5))


func _update_letters(delta: float) -> void:
	var lit: float = 0.0
	for letter: Dictionary in _letters:
		var node: Sprite2D = letter["node"]
		var glitch: float = letter["glitch"]
		if glitch > 0.0:
			glitch -= delta
			node.modulate.a = 0.2 if _rng.randf() < 0.55 else 1.0
		else:
			node.modulate.a = 1.0
			if _rng.randf() < delta * float(letter["chance"]):
				glitch = _rng.randf_range(0.1, 0.5)
		letter["glitch"] = glitch
		lit += node.modulate.a
	var level: float = lit / maxf(1.0, float(_letters.size()))
	# Відображення в калюжі й світло вивіски збоять разом з літерами.
	_roof_glow.modulate.a = lerpf(0.6, 1.0, level)
	if _sign_light != null:
		_sign_light.energy = 1.7 * (0.55 + 0.45 * level)
	if _bounce_light != null:
		_bounce_light.energy = 1.1 * (0.5 + 0.5 * level)
	if _sign_halo != null:
		_sign_halo.modulate.a = 0.28 * level


func _update_lights(delta: float) -> void:
	for info: Dictionary in _flicker_lights:
		var light: PointLight2D = info["node"]
		var base: float = info["base"]
		if str(info["kind"]) == "buzz":
			# Лампа розжарювання гуде: дрібне тремтіння і рідкісне «підморгування».
			light.energy = base * (0.94 + 0.06 * sin(_time * 50.0)) * (0.4 if _rng.randf() < delta * 0.15 else 1.0)
		else:
			light.energy = base * (0.15 if _rng.randf() < delta * 1.2 else 1.0)
	if _ember_light != null:
		# Жар цигарки: повільні затяжки.
		_ember_light.energy = 0.5 + 0.5 * maxf(0.0, sin(_time * 0.9)) ** 4


func _update_train(delta: float) -> void:
	if not _train.visible:
		_train_wait -= delta
		if _train_wait <= 0.0:
			_train_dir = 1.0 if _rng.randf() < 0.5 else -1.0
			_train.scale.x = _train_dir
			_train.position.x = -CANVAS_SIZE.x / 2.0 - 220.0 if _train_dir > 0.0 else CANVAS_SIZE.x / 2.0 + 220.0
			_train.visible = true
		return
	_train.position.x += TRAIN_SPEED * _train_dir * delta
	if absf(_train.position.x) > CANVAS_SIZE.x / 2.0 + 230.0:
		_train.visible = false
		_train_wait = _rng.randf_range(5.0, 12.0)
