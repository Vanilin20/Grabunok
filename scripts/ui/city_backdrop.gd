extends Node2D
## Нічне місто з даху — фон меню, лобі й підсумку. Еталон якості (style frame) для всієї гри.
##
## Статичні шари лежать у сцені; живі деталі розставляються тут за
## assets/sprites/menu/menu_layout.json (його генерує tools/art/gen_menu.py разом з артом).
##
## Світло: CanvasModulate — ніч; неон «БАНДА» світить на каркас, будинок і наш дах; відсвіт
## мокрої плитки під вивіскою; лампа над дверима; ліхтарик ватажка (з тінями від банди);
## телефон хакерки; цигарка. Неон — HDR, тому світиться через WorldEnvironment glow.
## Життя: дощ і бризки, хмари, прожектор, гелікоптер, вогні антен, потяг, машини внизу,
## пара з вулиці й вентиляції, ворона на дроті, телевізор у вікні, збої неону,
## блискавка раз на 20–30 с. Паралакс шарів — від нахилу телефона (на ПК — від миші).

signal lightning_struck(delay_to_thunder: float)

const LAYOUT_PATH: String = "res://assets/sprites/menu/menu_layout.json"
const CANVAS_SIZE: Vector2 = Vector2(864, 360)
const CLOUD_SPEED: float = 3.0
const TRAIN_SPEED: float = 64.0
const GANG_FRAME: Vector2i = Vector2i(48, 76)
const GANG_ORDER: Array[String] = ["runner", "leader", "hacker"]
const GANG_FPS: Array[float] = [3.2, 2.8, 3.6]
const LIGHT_LAYER_WORLD: int = 1 # місто й дах
const LIGHT_LAYER_GANG: int = 2 # банда: її освітлюють лише близькі джерела (лампа, ліхтарик, телефон)
const NEON_HDR: float = 1.25 # неон яскравіший за білий → його підхоплює glow
## Максимальний зсув кожного плану при паралаксі (px): що ближче, то більше.
const PARALLAX: Dictionary = {"Sky": 0.0, "FarLayer": 1.0, "MidLayer": 2.0, "RoofLayer": 4.0, "FgLayer": 7.0}
## Ліхтарик світить уперед — на дах перед бандою й на парапет (вгору по екрану, праворуч).
const FLASH_ANGLE: float = -55.0
const NIGHT_COLOR: Color = Color(0.56, 0.54, 0.74, 1)

@onready var _world: Node2D = %World
@onready var _night: CanvasModulate = %Night
@onready var _clouds: Node2D = %Clouds
@onready var _sky_fx: Node2D = %SkyFx
@onready var _far_fx: Node2D = %FarFx
@onready var _mid_layer: Node2D = %MidLayer
@onready var _train: Node2D = %Train
@onready var _street_fx: Node2D = %StreetFx
@onready var _mid_fx: Node2D = %MidFx
@onready var _letters_root: Node2D = %SignLetters
@onready var _roof_layer: Node2D = %RoofLayer
@onready var _roof_glow: Sprite2D = %RoofGlow
@onready var _roof_fx: Node2D = %RoofFx
@onready var _gang_root: Node2D = %Gang

var _rng := RandomNumberGenerator.new()
var _time: float = 0.0
var _light_texture: GradientTexture2D
var _cone_texture: ImageTexture
var _unshaded: CanvasItemMaterial
var _additive: CanvasItemMaterial
var _layout: Dictionary = {}

var _blinkers: Array[Dictionary] = []
var _tvs: Array[Dictionary] = []
var _letters: Array[Dictionary] = []
var _flicker_lights: Array[Dictionary] = []
var _sign_light: PointLight2D
var _bounce_light: PointLight2D
var _sign_halo: Sprite2D
var _beam: Polygon2D
var _ember_light: PointLight2D
var _flashlight: PointLight2D
var _flash_beam: Polygon2D
var _crow: AnimatedSprite2D
var _heli: AnimatedSprite2D
var _heli_beam: Polygon2D
var _bolt: Sprite2D
var _cars: Array[Dictionary] = []
var _car_timer: float = 0.5
var _train_wait: float = 2.0
var _train_dir: float = 1.0
var _heli_wait: float = 6.0
var _lightning_wait: float = 0.0
var _lightning_t: float = -1.0
var _parallax: Vector2 = Vector2.ZERO
var _layers: Dictionary = {} # Node2D -> базова позиція
var _still_camera: bool = OS.get_cmdline_args().has("--write-movie") or OS.get_cmdline_user_args().has("--still")


func _ready() -> void:
	_rng.randomize()
	_light_texture = _make_light_texture()
	_cone_texture = _make_cone_texture()
	_unshaded = CanvasItemMaterial.new()
	_unshaded.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
	_additive = CanvasItemMaterial.new()
	_additive.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	_additive.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
	_layout = _load_layout()
	_lightning_wait = _rng.randf_range(20.0, 30.0)
	for layer_name: String in PARALLAX:
		var layer: Node2D = _world.get_node(layer_name)
		_layers[layer] = layer.position
	_build_sky()
	_build_blinkers(_layout.get("blinkers", []))
	_build_street(_layout.get("street", {}))
	_build_tvs(_layout.get("tvs", []))
	_build_letters(_layout.get("letters", []))
	_build_lights(_layout.get("lights", []))
	_build_gang(_layout.get("gang", {}))
	_build_steam(_layout.get("steam", []))
	_build_crow(_layout.get("crow", []))
	_train.position.y = float(_layout.get("viaduct_y", 198)) - CANVAS_SIZE.y / 2.0
	_train.visible = false
	_build_sound()


func _process(delta: float) -> void:
	_time += delta
	_update_parallax(delta)
	_update_clouds(delta)
	_update_beam()
	_update_blinkers()
	_update_tvs(delta)
	_update_letters(delta)
	_update_lights(delta)
	_update_train(delta)
	_update_cars(delta)
	_update_heli(delta)
	_update_crow(delta)
	_update_lightning(delta)


# --- Допоміжне -------------------------------------------------------------------

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


## Текстура конуса ліхтарика: вершина в центрі, промінь праворуч, кут ~40°, спад з відстанню.
func _make_cone_texture() -> ImageTexture:
	var size: int = 128
	var image := Image.create(size, size, false, Image.FORMAT_RGBA8)
	var c := Vector2(size / 2.0, size / 2.0)
	for y: int in size:
		for x: int in size:
			var d := Vector2(x, y) - c
			var dist: float = d.length() / (size / 2.0)
			var ang: float = absf(d.angle())
			var edge: float = clampf(1.0 - ang / deg_to_rad(20.0), 0.0, 1.0)
			var a: float = 0.0
			if d.x > 0.0 and dist <= 1.0:
				a = pow(edge, 0.6) * pow(1.0 - dist, 1.2)
			image.set_pixel(x, y, Color(1, 1, 1, a))
	return ImageTexture.create_from_image(image)


func _make_light(pos: Vector2, color: Color, scale: float, energy: float) -> PointLight2D:
	var light := PointLight2D.new()
	light.texture = _light_texture
	light.color = color
	light.texture_scale = scale
	light.energy = energy
	light.position = pos
	light.height = 24.0 # для карт нормалей: світло «над» площиною, ковзає по цеглі
	light.range_item_cull_mask = LIGHT_LAYER_WORLD | LIGHT_LAYER_GANG
	return light


func _beam_polygon(length: float, half_width: float, color: Color) -> Polygon2D:
	var beam := Polygon2D.new()
	beam.material = _additive
	beam.polygon = PackedVector2Array([Vector2(-1.5, 0), Vector2(1.5, 0), Vector2(half_width, length), Vector2(-half_width, length)])
	beam.vertex_colors = PackedColorArray([color, color, Color(color, 0.0), Color(color, 0.0)])
	return beam


func _fade_ramp(color: Color) -> Gradient:
	var ramp := Gradient.new()
	ramp.offsets = PackedFloat32Array([0.0, 0.15, 1.0])
	ramp.colors = PackedColorArray([Color(color, 0.0), color, Color(color, 0.0)])
	return ramp


func _sheet_frames(path: String, frame: Vector2i, fps: float) -> SpriteFrames:
	var sheet: Texture2D = load(path)
	var frames := SpriteFrames.new()
	frames.add_animation("idle")
	frames.set_animation_speed("idle", fps)
	frames.set_animation_loop("idle", true)
	for i: int in sheet.get_width() / frame.x:
		var atlas := AtlasTexture.new()
		atlas.atlas = sheet
		atlas.region = Rect2(i * frame.x, 0, frame.x, frame.y)
		frames.add_frame("idle", atlas)
	frames.remove_animation("default")
	return frames


# --- Побудова --------------------------------------------------------------------

func _build_sky() -> void:
	# Прожектор з-за міста: адитивний промінь повільно гуляє небом.
	_beam = _beam_polygon(-330.0, 26.0, Color(Palette.NIGHT_6, 0.22))
	_beam.position = Vector2(230.0, 70.0)
	_sky_fx.add_child(_beam)
	# Гелікоптер удалині з прожектором униз; пролітає час від часу.
	_heli = AnimatedSprite2D.new()
	_heli.sprite_frames = _sheet_frames("res://assets/sprites/fx/heli.png", Vector2i(14, 6), 4.0)
	_heli.material = _unshaded
	_heli.play("idle")
	_heli.visible = false
	_sky_fx.add_child(_heli)
	_heli_beam = _beam_polygon(150.0, 16.0, Color(Palette.STEEL_3, 0.12))
	_heli_beam.position = Vector2(7, 5)
	_heli.add_child(_heli_beam)
	# Блискавка — спрайт у небі на кілька кадрів.
	_bolt = Sprite2D.new()
	_bolt.texture = load("res://assets/sprites/fx/bolt.png")
	_bolt.material = _additive
	_bolt.centered = false
	_bolt.visible = false
	_sky_fx.add_child(_bolt)


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


func _build_street(street: Dictionary) -> void:
	if street.has("y_steam"):
		# Пара з люка внизу на вулиці — видно в прогалині між будинками.
		var steam := _make_steam(_to_world(street["y_steam"]), 30, Vector2(10, 1), 1.0, 3.0, 0.3)
		_street_fx.add_child(steam)


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
		var light := _make_light(s.position + s.scale / 2.0, Palette.CYAN_2, 0.25, 0.6)
		light.range_item_cull_mask = LIGHT_LAYER_WORLD
		_mid_fx.add_child(light)
		_tvs.append({"node": s, "light": light, "timer": 0.0})


func _build_letters(letters: Array) -> void:
	for i: int in letters.size():
		var info: Dictionary = letters[i]
		var s := Sprite2D.new()
		s.texture = load("res://assets/sprites/menu/" + str(info["file"]))
		s.centered = false
		s.material = _unshaded
		s.self_modulate = Color(NEON_HDR, NEON_HDR, NEON_HDR, 1.0)
		s.position = _to_world([info["x"], info["y"]])
		_letters_root.add_child(s)
		# «Д» (4-та літера) — стара трубка, збоїть частіше за інші.
		_letters.append({"node": s, "glitch": 0.0, "chance": 0.35 if i == 3 else 0.03})


func _build_lights(lights: Array) -> void:
	for info: Dictionary in lights:
		var color := Color(str(info["color"]))
		var pos: Vector2 = _to_world([info["x"], info["y"]])
		match str(info["name"]):
			"sign":
				# Неон світить на каркас і будинок; банду не заливає — зі спини вони силуети з рожевим кантом.
				_sign_light = _make_light(pos, color, 5.0, 2.0)
				_sign_light.height = 60.0
				_sign_light.range_item_cull_mask = LIGHT_LAYER_WORLD
				_mid_layer.add_child(_sign_light)
				_sign_halo = Sprite2D.new()
				_sign_halo.texture = _light_texture
				_sign_halo.material = _additive
				_sign_halo.modulate = Color(color, 0.18)
				_sign_halo.scale = Vector2(2.4, 0.9)
				_sign_halo.position = pos
				_mid_layer.add_child(_sign_halo)
				# Рожевий відсвіт на мокрому даху й парапеті нашої будівлі.
				_bounce_light = _make_light(_to_world([380, 330]), color, 3.4, 1.0)
				_bounce_light.range_item_cull_mask = LIGHT_LAYER_WORLD
				_roof_layer.add_child(_bounce_light)
			"bulb":
				var bulb := _make_light(pos, color, 6.0, 2.2)
				bulb.range_item_cull_mask = LIGHT_LAYER_WORLD
				_roof_layer.add_child(bulb)
				_flicker_lights.append({"node": bulb, "base": bulb.energy, "kind": "buzz"})
			"billboard":
				# Холодне світло реклами за бандою: контрове світло на банду й відсвіт на парапет.
				var box := _make_light(pos, color, 3.2, 1.4)
				box.height = 40.0
				_mid_layer.add_child(box)
			"bar":
				var bar := _make_light(pos, color, 1.3, 1.0)
				bar.range_item_cull_mask = LIGHT_LAYER_WORLD
				_mid_layer.add_child(bar)
				_flicker_lights.append({"node": bar, "base": bar.energy, "kind": "neon"})


func _build_gang(gang: Dictionary) -> void:
	for i: int in GANG_ORDER.size():
		var key: String = GANG_ORDER[i]
		if not gang.has(key):
			continue
		var sprite := AnimatedSprite2D.new()
		sprite.sprite_frames = _sheet_frames("res://assets/sprites/menu/gang_%s.png" % key, GANG_FRAME, GANG_FPS[i])
		sprite.centered = false
		sprite.position = _to_world(gang[key])
		sprite.light_mask = LIGHT_LAYER_GANG
		sprite.play("idle")
		sprite.frame = _rng.randi_range(0, 7) # не дихають синхронно
		_gang_root.add_child(sprite)
		var occluder := LightOccluder2D.new()
		var poly := OccluderPolygon2D.new()
		poly.polygon = PackedVector2Array([Vector2(13, 22), Vector2(33, 22), Vector2(31, 72), Vector2(15, 72)])
		occluder.occluder = poly
		occluder.occluder_light_mask = LIGHT_LAYER_GANG
		sprite.add_child(occluder)
		match key:
			"leader":
				# Ліхтарик: конус униз-праворуч на дах, з тінями від банди. Найяскравіша пляма біля фокусу.
				_flashlight = PointLight2D.new()
				_flashlight.texture = _cone_texture
				_flashlight.color = Palette.LAMP_4
				_flashlight.energy = 3.2
				_flashlight.texture_scale = 1.6
				_flashlight.height = 70.0
				_flashlight.position = Vector2(48, 44)
				_flashlight.rotation = deg_to_rad(FLASH_ANGLE)
				_flashlight.range_item_cull_mask = LIGHT_LAYER_WORLD | LIGHT_LAYER_GANG
				_flashlight.shadow_enabled = true
				_flashlight.shadow_color = Color(0, 0, 0, 0.7)
				_flashlight.shadow_filter = Light2D.SHADOW_FILTER_NONE
				_flashlight.shadow_item_cull_mask = LIGHT_LAYER_GANG
				sprite.add_child(_flashlight)
				_flash_beam = _beam_polygon(64.0, 16.0, Color(Palette.LAMP_4, 0.16))
				_flash_beam.position = Vector2(48, 44)
				_flash_beam.rotation = deg_to_rad(FLASH_ANGLE - 90.0)
				sprite.add_child(_flash_beam)
			"hacker":
				var phone_light := _make_light(Vector2(39, 34), Palette.CYAN_2, 0.7, 2.0)
				sprite.add_child(phone_light)
			"runner":
				_ember_light = _make_light(Vector2(6, 49), Palette.LAMP_2, 0.14, 0.8)
				sprite.add_child(_ember_light)
				sprite.add_child(_make_steam(Vector2(6, 48), 10, Vector2(0, 0), 0.5, 2.6, 0.5))


func _make_steam(pos: Vector2, amount: int, extents: Vector2, scale_min: float, lifetime: float, alpha: float) -> CPUParticles2D:
	var steam := CPUParticles2D.new()
	steam.position = pos
	steam.amount = amount
	steam.lifetime = lifetime
	steam.preprocess = lifetime
	steam.texture = load("res://assets/sprites/fx/puff.png")
	if extents != Vector2.ZERO:
		steam.emission_shape = CPUParticles2D.EMISSION_SHAPE_RECTANGLE
		steam.emission_rect_extents = extents
	steam.direction = Vector2(0.2, -1)
	steam.spread = 15.0
	steam.gravity = Vector2(4, -8)
	steam.initial_velocity_min = 5.0
	steam.initial_velocity_max = 12.0
	steam.scale_amount_min = scale_min
	steam.scale_amount_max = scale_min * 2.0
	steam.color_ramp = _fade_ramp(Color(Palette.STEEL_2, alpha))
	return steam


func _build_steam(points: Array) -> void:
	for p: Array in points:
		_roof_fx.add_child(_make_steam(_to_world(p), 26, Vector2(4, 1), 1.0, 3.2, 0.45))


func _build_crow(pos: Array) -> void:
	if pos.is_empty():
		return
	_crow = AnimatedSprite2D.new()
	_crow.sprite_frames = _sheet_frames("res://assets/sprites/fx/crow.png", Vector2i(9, 8), 1.0)
	_crow.centered = false
	_crow.position = _to_world(pos)
	_crow.pause()
	_roof_layer.add_child(_crow)


func _build_sound() -> void:
	# У безголовому режимі (автотести, CI) звуковий драйвер Dummy не обробляє відтворення,
	# і петлі, що грають у момент виходу, лишаються «витеклими» (ERROR у перевірці перед комітом).
	# Звук там однаково нікому не чутно — не вмикаємо.
	if DisplayServer.get_name() == "headless":
		return
	# Фон: дощ і далеке місто (петлі), гудіння неону; грім — після спалаху блискавки із затримкою.
	for info: Array in [["res://assets/audio/rain_loop.ogg", -4.0], ["res://assets/audio/city_loop.ogg", -8.0], ["res://assets/audio/neon_hum.ogg", -22.0]]:
		var player := AudioStreamPlayer.new()
		player.stream = load(info[0])
		player.volume_db = info[1]
		player.bus = &"Ambience"
		player.autoplay = true
		add_child(player)
	var thunder := AudioStreamPlayer.new()
	thunder.stream = load("res://assets/audio/thunder.ogg")
	thunder.bus = &"SFX"
	thunder.volume_db = -3.0
	add_child(thunder)
	lightning_struck.connect(func(delay: float) -> void:
		get_tree().create_timer(delay).timeout.connect(thunder.play))


# --- Оновлення -------------------------------------------------------------------

func _update_parallax(delta: float) -> void:
	var target := Vector2.ZERO
	if _still_camera:
		pass # запис кадрів для перевірок і скріншотів: шари на місці, координати збігаються з макетом
	elif OS.has_feature("mobile"):
		# Нахил телефона: гравітація в координатах пристрою, нормована до 1g.
		var g: Vector3 = Input.get_gravity()
		if g.length() > 0.1:
			target = Vector2(clampf(g.x / 4.0, -1.0, 1.0), clampf(-g.y / 9.8 - 0.6, -1.0, 1.0))
	else:
		var view: Vector2 = get_viewport().get_visible_rect().size
		target = (get_viewport().get_mouse_position() / view - Vector2(0.5, 0.5)) * 2.0
		target = target.clamp(Vector2(-1, -1), Vector2(1, 1))
	_parallax = _parallax.lerp(target, 1.0 - exp(-3.0 * delta))
	for layer: Node2D in _layers:
		var amount: float = PARALLAX[str(layer.name)]
		# Цілі пікселі: піксель-арт не «пливе» між пікселями.
		layer.position = (_layers[layer] - _parallax * amount).round()


func _update_clouds(delta: float) -> void:
	_clouds.position.x -= CLOUD_SPEED * delta
	if _clouds.position.x <= -CANVAS_SIZE.x:
		_clouds.position.x += CANVAS_SIZE.x


func _update_beam() -> void:
	_beam.rotation = -0.1 + sin(_time * 0.13) * 0.4


func _update_blinkers() -> void:
	for b: Dictionary in _blinkers:
		(b["node"] as Sprite2D).visible = fmod(_time + float(b["phase"]), float(b["period"])) < 0.18


func _update_tvs(delta: float) -> void:
	for tv: Dictionary in _tvs:
		tv["timer"] = float(tv["timer"]) - delta
		if float(tv["timer"]) <= 0.0:
			tv["timer"] = _rng.randf_range(0.08, 0.6)
			var shades: Array[Color] = [Palette.CYAN_1, Palette.CYAN_2, Palette.CYAN_0, Palette.STEEL_2]
			var c: Color = shades[_rng.randi_range(0, shades.size() - 1)]
			var a: float = _rng.randf_range(0.15, 0.5)
			(tv["node"] as Sprite2D).modulate = Color(c, a)
			(tv["light"] as PointLight2D).energy = a * 1.6


func _update_letters(delta: float) -> void:
	var lit: float = 0.0
	for letter: Dictionary in _letters:
		var node: Sprite2D = letter["node"]
		var glitch: float = letter["glitch"]
		if glitch > 0.0:
			glitch -= delta
			node.modulate.a = 0.15 if _rng.randf() < 0.55 else 1.0
		else:
			node.modulate.a = 1.0
			if _rng.randf() < delta * float(letter["chance"]):
				glitch = _rng.randf_range(0.1, 0.5)
		letter["glitch"] = glitch
		lit += node.modulate.a
	var level: float = lit / maxf(1.0, float(_letters.size()))
	# Світло вивіски, відсвіт на даху й рожевий відблиск у калюжі збоять разом з літерами.
	_roof_glow.modulate.a = lerpf(0.7, 1.0, level)
	if _sign_light != null:
		_sign_light.energy = 2.0 * (0.55 + 0.45 * level)
		_bounce_light.energy = 1.0 * (0.5 + 0.5 * level)
		_sign_halo.modulate.a = 0.18 * level


func _update_lights(delta: float) -> void:
	for info: Dictionary in _flicker_lights:
		var light: PointLight2D = info["node"]
		var base: float = info["base"]
		if str(info["kind"]) == "buzz":
			light.energy = base * (0.94 + 0.06 * sin(_time * 50.0)) * (0.4 if _rng.randf() < delta * 0.15 else 1.0)
		else:
			light.energy = base * (0.15 if _rng.randf() < delta * 1.2 else 1.0)
	if _ember_light != null:
		_ember_light.energy = 0.5 + 0.7 * maxf(0.0, sin(_time * 0.9)) ** 4 # повільні затяжки
	if _flashlight != null:
		# Ліхтарик у руці ледь гуляє — рука живої людини.
		var sway: float = deg_to_rad(FLASH_ANGLE + sin(_time * 0.7) * 4.0 + sin(_time * 1.9) * 1.5)
		_flashlight.rotation = sway
		_flash_beam.rotation = sway - PI / 2.0


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


func _update_cars(delta: float) -> void:
	_car_timer -= delta
	var lanes: Array = _layout.get("street", {}).get("lanes", [])
	if _car_timer <= 0.0 and not lanes.is_empty():
		_car_timer = _rng.randf_range(0.8, 2.5)
		var lane: int = _rng.randi_range(0, lanes.size() - 1)
		var car := Sprite2D.new()
		car.texture = load("res://assets/sprites/fx/car.png")
		car.material = _unshaded
		var dir: float = 1.0 if lane == 0 else -1.0
		car.flip_h = dir < 0.0
		car.position = Vector2(-CANVAS_SIZE.x / 2.0 - 20.0 if dir > 0.0 else CANVAS_SIZE.x / 2.0 + 20.0, float(lanes[lane]) - CANVAS_SIZE.y / 2.0)
		_street_fx.add_child(car)
		_cars.append({"node": car, "dir": dir, "speed": _rng.randf_range(50.0, 90.0)})
	for i: int in range(_cars.size() - 1, -1, -1):
		var car: Dictionary = _cars[i]
		var node: Sprite2D = car["node"]
		node.position.x += float(car["dir"]) * float(car["speed"]) * delta
		if absf(node.position.x) > CANVAS_SIZE.x / 2.0 + 30.0:
			node.queue_free()
			_cars.remove_at(i)


func _update_heli(delta: float) -> void:
	if not _heli.visible:
		_heli_wait -= delta
		if _heli_wait <= 0.0:
			_heli.position = Vector2(-CANVAS_SIZE.x / 2.0 - 20.0, float(_layout.get("heli_y", 64)) - CANVAS_SIZE.y / 2.0)
			_heli.visible = true
		return
	_heli.position.x += 14.0 * delta
	_heli.position.y += sin(_time * 0.8) * 2.0 * delta
	_heli_beam.rotation = sin(_time * 0.5) * 0.35
	if _heli.position.x > CANVAS_SIZE.x / 2.0 + 20.0:
		_heli.visible = false
		_heli_wait = _rng.randf_range(25.0, 45.0)


func _update_crow(delta: float) -> void:
	if _crow == null:
		return
	# Ворона здебільшого сидить, зрідка повертає голову, кліпає чи струшується.
	if _rng.randf() < delta * 0.6:
		_crow.frame = _rng.randi_range(0, 3) if _crow.frame == 0 else 0


func _update_lightning(delta: float) -> void:
	if _lightning_t < 0.0:
		_lightning_wait -= delta
		if _lightning_wait <= 0.0:
			_lightning_t = 0.0
			_bolt.position = Vector2(_rng.randf_range(-380.0, 300.0), -180.0)
			lightning_struck.emit(_rng.randf_range(1.0, 3.0))
		return
	# Подвійний спалах, потім повільне згасання (R10 у refs.md).
	_lightning_t += delta
	var t: float = _lightning_t
	var flash: float = 0.0
	if t < 0.06 or (t > 0.12 and t < 0.2):
		flash = 1.0
	elif t >= 0.2:
		flash = maxf(0.0, 1.0 - (t - 0.2) / 0.6)
	_bolt.visible = t < 0.2
	_night.color = NIGHT_COLOR.lerp(Color(1.05, 1.05, 1.25), flash * 0.75)
	if t > 0.8:
		_lightning_t = -1.0
		_night.color = NIGHT_COLOR
		_lightning_wait = _rng.randf_range(20.0, 30.0)
