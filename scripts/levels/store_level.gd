extends Node2D
## Рівень «Магазин на розі» (вид зверху, 3/4). Будується з assets/levels/store.json і шарів
## assets/sprites/store/*, які генерує tools/art/gen_store.py.
##
## Шари глибини: підлога (фон) → стіни й предмети + персонажі (ігровий план) → стеля (передній план).
## Світло: ніч (CanvasModulate); вуличні ліхтарі й неон «24/7» світять крізь вітрини — стіни
## кидають тіні (LightOccluder2D), скло пропускає; холодильники, каса, монітор, лампа в підсобці;
## ліхтарики охоронця й Тіні з тінями від стелажів. Життя: дощ і бризки на вулиці, машини з фарами,
## пил у промені ліхтаря, мерехтіння неону, лампи в підсобці й одного холодильника, мигання камери.
##
## Етап 2 — концепт-кадр: охоронець ходить маршрутом, Тінь чекає за стелажем (без керування).
## Логіку гри (рух від хоста, тривога) підключаємо на етапі 3.

const LEVEL_PATH: String = "res://assets/levels/store.json"
const DIR_ROW: Dictionary = {"down": 0, "left": 1, "right": 2, "up": 3}
const CHAR_FRAME: Vector2i = Vector2i(24, 32)
const LIGHT_WORLD: int = 1
const LIGHT_SHADOW: int = 2
const NIGHT: Color = Color(0.42, 0.42, 0.62, 1)
const GUARD_SPEED: float = 26.0

@onready var _occluders: Node2D = %Occluders
@onready var _lights: Node2D = %Lights
@onready var _actors: Node2D = %Actors
@onready var _street_fx: Node2D = %StreetFx

var _data: Dictionary = {}
var _rng := RandomNumberGenerator.new()
var _time: float = 0.0
var _light_tex: GradientTexture2D
var _cone_tex: ImageTexture
var _unshaded: CanvasItemMaterial
var _flicker: Array[Dictionary] = []
var _guard: AnimatedSprite2D
var _guard_light: PointLight2D
var _guard_dust: CPUParticles2D
var _guard_path: Array[Vector2] = []
var _guard_target: int = 1
var _guard_pause: float = 0.0
var _shadow: AnimatedSprite2D
var _cam_led: Sprite2D
var _cars: Array[Dictionary] = []
var _car_wait: float = 1.0
var _frozen: bool = OS.get_cmdline_user_args().has("--still")


func _ready() -> void:
	_rng.randomize()
	_unshaded = CanvasItemMaterial.new()
	_unshaded.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
	_light_tex = _radial_texture()
	_cone_tex = _cone_texture(18.0)
	var file := FileAccess.open(LEVEL_PATH, FileAccess.READ)
	if file == null:
		push_error("[Store] Немає %s — запусти tools/art/gen_store.py" % LEVEL_PATH)
		return
	_data = JSON.parse_string(file.get_as_text())
	for layer_name: String in ["Floor", "Walls", "Objects"]:
		_chunk_layer(get_node(layer_name) as Sprite2D)
	_build_occluders()
	_build_lights()
	_build_actors()
	_build_street()
	if OS.get_cmdline_user_args().has("--focus-mask"):
		_make_focus_mask()


func _process(delta: float) -> void:
	_time += delta
	_update_flicker(delta)
	_update_guard(delta)
	_update_cars(delta)
	if _cam_led != null:
		_cam_led.visible = fmod(_time, 1.2) < 0.15


# --- Побудова --------------------------------------------------------------------

## Великий шар ділимо на шматки CHUNK×CHUNK: кожен шматок освітлюють лише ближні джерела.
## Причина — ліміт світла на один об'єкт (8 у Compatibility, 16 у Mobile): один спрайт на весь
## рівень «бачив» би лише перші 8–16 ламп. Заодно GPU не рахує далекі лампи для кожного пікселя.
const CHUNK: int = 96


func _chunk_layer(sprite: Sprite2D) -> void:
	var size: Vector2 = sprite.texture.get_size()
	for y: int in range(0, int(size.y), CHUNK):
		for x: int in range(0, int(size.x), CHUNK):
			var part := Sprite2D.new()
			part.texture = sprite.texture
			part.centered = false
			part.region_enabled = true
			part.region_rect = Rect2(x, y, mini(CHUNK, int(size.x) - x), mini(CHUNK, int(size.y) - y))
			part.position = Vector2(x, y)
			sprite.add_child(part)
	sprite.self_modulate.a = 0.0 # сам «батько» не малюється — малюють шматки


func _radial_texture() -> GradientTexture2D:
	var g := Gradient.new()
	g.offsets = PackedFloat32Array([0.0, 0.15, 0.5, 1.0])
	g.colors = PackedColorArray([Color(1, 1, 1, 1), Color(1, 1, 1, 0.7), Color(1, 1, 1, 0.2), Color(1, 1, 1, 0)])
	var t := GradientTexture2D.new()
	t.gradient = g
	t.width = 128
	t.height = 128
	t.fill = GradientTexture2D.FILL_RADIAL
	t.fill_from = Vector2(0.5, 0.5)
	t.fill_to = Vector2(0.5, 0.0)
	return t


## Конус ліхтарика: вершина в центрі текстури, промінь праворуч; кут розкриття ±half_angle.
func _cone_texture(half_angle: float) -> ImageTexture:
	var size: int = 256
	var image := Image.create(size, size, false, Image.FORMAT_RGBA8)
	var c := Vector2(size / 2.0, size / 2.0)
	for y: int in size:
		for x: int in size:
			var d := Vector2(x, y) - c
			var dist: float = d.length() / (size / 2.0)
			var edge: float = clampf(1.0 - absf(d.angle()) / deg_to_rad(half_angle), 0.0, 1.0)
			var a: float = 0.0
			if d.x > 0.0 and dist <= 1.0:
				# Різкіший край (ліхтар, а не лампа) і гаряче ядро біля джерела.
				# Біля самого ліхтаря світло ще не розійшлось: перші 10% променя приглушені —
				# інакше біля руки охоронця з'являється сліпуча пляма, яка спорить з персонажами.
				a = smoothstep(0.0, 0.25, edge) * pow(1.0 - dist, 0.8) * smoothstep(0.02, 0.14, dist)
			image.set_pixel(x, y, Color(1, 1, 1, a))
	return ImageTexture.create_from_image(image)


func _build_occluders() -> void:
	for poly: Array in _data.get("occluders", []):
		var occ := LightOccluder2D.new()
		var shape := OccluderPolygon2D.new()
		var points := PackedVector2Array()
		for p: Array in poly:
			points.append(Vector2(float(p[0]), float(p[1])))
		shape.polygon = points
		occ.occluder = shape
		_occluders.add_child(occ)


func _build_lights() -> void:
	for info: Dictionary in _data.get("lights", []):
		var light := PointLight2D.new()
		light.texture = _light_tex
		light.position = Vector2(float(info["x"]), float(info["y"]))
		light.color = Color(str(info["color"]))
		light.texture_scale = float(info["scale"])
		light.energy = float(info["energy"])
		# Висота світла над підлогою: з картами нормалей низьке світло дає лише ковзний відблиск на краях,
		# а лампа на висоті стелі освітлює підлогу — і рельєф цегли/полиць усе одно читається.
		light.height = 110.0
		light.range_item_cull_mask = LIGHT_WORLD
		light.shadow_enabled = bool(info.get("shadow", false))
		light.shadow_color = Color(0, 0, 0, 0.85)
		light.shadow_filter = Light2D.SHADOW_FILTER_NONE # піксельні краї тіней
		_lights.add_child(light)
		if bool(info.get("flicker", false)):
			_flicker.append({"node": light, "base": light.energy, "name": str(info["name"]), "left": 0.0})
	var cam: Variant = _data.get("camera")
	if cam is Array:
		_cam_led = Sprite2D.new()
		_cam_led.texture = load("res://assets/sprites/fx/blinker.png")
		_cam_led.material = _unshaded
		_cam_led.position = Vector2(float(cam[0]), float(cam[1]))
		_lights.add_child(_cam_led)


func _sheet(path: String, rows: Array[String], fps: float) -> SpriteFrames:
	var tex: Texture2D = load(path)
	var frames := SpriteFrames.new()
	frames.remove_animation("default")
	for dir: String in rows:
		var row: int = DIR_ROW[dir]
		for anim: Array in [["idle_", 0, 4, fps * 0.5], ["walk_", 4, 6, fps], ["alert_", 10, 2, fps * 0.4]]:
			var name: String = str(anim[0]) + dir
			frames.add_animation(name)
			frames.set_animation_speed(name, float(anim[3]))
			for i: int in int(anim[2]):
				var atlas := AtlasTexture.new()
				atlas.atlas = tex
				atlas.region = Rect2((int(anim[1]) + i) * CHAR_FRAME.x, row * CHAR_FRAME.y, CHAR_FRAME.x, CHAR_FRAME.y)
				frames.add_frame(name, atlas)
	return frames


func _build_actors() -> void:
	var spawns: Dictionary = _data.get("spawns", {})
	var dirs: Array[String] = ["down", "left", "right", "up"]
	# Тінь пригнулася за торцем стелажа, дивиться на охоронця (праворуч).
	_shadow = AnimatedSprite2D.new()
	_shadow.sprite_frames = _sheet("res://assets/sprites/chars/shadow.png", dirs, 6.0)
	_shadow.position = _vec(spawns.get("shadow", [200, 200]))
	_shadow.offset = Vector2(0, -12)
	# Тінь — на окремому шарі світла: лампи й холодильники її не освітлюють, вона чорний силует
	# на тлі скла. Освітити її може тільки ліхтар охоронця — тобто саме тоді, коли її помітили.
	_shadow.light_mask = LIGHT_SHADOW
	_shadow.play("alert_right")
	_shadow.frame = 0
	_actors.add_child(_shadow)
	# Ліхтарик Тіні прикритий долонею: вузьке тьмяне коло біля ніг — видно, куди ступати.
	var cover := PointLight2D.new()
	cover.texture = _light_tex
	cover.color = Palette.STEEL_3
	cover.texture_scale = 0.5
	cover.energy = 1.6
	cover.height = 60.0
	cover.position = Vector2(6, 2)
	_shadow.add_child(cover)
	# Охоронець з ліхтарем іде маршрутом.
	for p: Array in spawns.get("guard_path", []):
		_guard_path.append(_vec(p))
	_guard = AnimatedSprite2D.new()
	_guard.sprite_frames = _sheet("res://assets/sprites/chars/guard.png", dirs, 8.0)
	_guard.position = _vec(spawns.get("guard", [300, 200]))
	_guard.offset = Vector2(0, -12)
	_guard.play("walk_left")
	if _guard_path.size() > 1:
		_guard_target = 1 # стартує в середині маршруту й іде на захід — до проходу, де ховається Тінь
	_actors.add_child(_guard)
	_guard_light = PointLight2D.new()
	_guard_light.texture = _cone_tex
	_guard_light.color = Palette.LAMP_3 # тепле жовте світло ліхтаря — контраст до холодних холодильників
	_guard_light.energy = 4.0
	_guard_light.texture_scale = 1.2 # промінь ~150 px: дотягується до проходу, де Тінь
	_guard_light.height = 70.0
	_guard_light.range_item_cull_mask = LIGHT_WORLD | LIGHT_SHADOW
	_guard_light.shadow_enabled = true
	_guard_light.shadow_color = Color(0, 0, 0, 0.9)
	_guard_light.shadow_filter = Light2D.SHADOW_FILTER_NONE
	_guard.add_child(_guard_light)
	# Відсвіт ліхтаря на самого охоронця (світло відбивається від підлоги перед ним): світла сорочка
	# читається на темній підлозі — загроза видна з першого погляду.
	var spill := PointLight2D.new()
	spill.texture = _light_tex
	spill.color = Palette.LAMP_3
	spill.energy = 1.3
	spill.texture_scale = 0.4
	spill.height = 40.0
	spill.position = Vector2(0, -8)
	_guard.add_child(spill)
	# Пил у промені ліхтаря.
	_guard_dust = CPUParticles2D.new()
	_guard_dust.amount = 18
	_guard_dust.lifetime = 2.5
	_guard_dust.preprocess = 2.5
	_guard_dust.texture = load("res://assets/sprites/fx/pixel.png")
	_guard_dust.emission_shape = CPUParticles2D.EMISSION_SHAPE_RECTANGLE
	_guard_dust.emission_rect_extents = Vector2(28, 8)
	_guard_dust.gravity = Vector2(0, 2)
	_guard_dust.initial_velocity_min = 1.0
	_guard_dust.initial_velocity_max = 3.0
	_guard_dust.spread = 180.0
	_guard_dust.color = Color(Palette.LAMP_4, 0.55)
	_guard.add_child(_guard_dust)


func _build_street() -> void:
	var street: Array = _data.get("street_y", [320, 352])
	var width: float = float(_data["size"][0]) * float(_data["tile"])
	var y0: float = float(street[0])
	# Дощ над вулицею й провулками (у приміщенні дощу немає).
	for info: Array in [[Vector2(width / 2.0, y0 - 40.0), Vector2(width / 2.0, 1), 160], [Vector2(40.0, 0.0), Vector2(40.0, 1), 50], [Vector2(width - 40.0, 0.0), Vector2(40.0, 1), 50]]:
		var rain := CPUParticles2D.new()
		rain.position = info[0]
		rain.amount = int(info[2])
		rain.lifetime = 0.45
		rain.preprocess = 1.0
		rain.texture = load("res://assets/sprites/fx/rain_near.png")
		rain.emission_shape = CPUParticles2D.EMISSION_SHAPE_RECTANGLE
		rain.emission_rect_extents = info[1]
		rain.particle_flag_align_y = true
		rain.direction = Vector2(0.1, 1)
		rain.spread = 2.0
		rain.gravity = Vector2.ZERO
		rain.initial_velocity_min = 260.0
		rain.initial_velocity_max = 300.0
		rain.color = Color(1, 1, 1, 0.45)
		_street_fx.add_child(rain)
	var splash := CPUParticles2D.new()
	splash.position = Vector2(width / 2.0, y0 + 20.0)
	splash.amount = 30
	splash.lifetime = 0.25
	splash.texture = load("res://assets/sprites/fx/splash.png")
	splash.emission_shape = CPUParticles2D.EMISSION_SHAPE_RECTANGLE
	splash.emission_rect_extents = Vector2(width / 2.0, 20)
	splash.gravity = Vector2.ZERO
	splash.color = Color(1, 1, 1, 0.5)
	_street_fx.add_child(splash)


func _vec(p: Variant) -> Vector2:
	return Vector2(float(p[0]), float(p[1]))


## Службовий режим для tools/checks/check_focus.py: лишає видимими тільки персонажів (білим),
## щоб перевірка фокусу знала їхні точні силуети в тому самому кадрі запису.
func _make_focus_mask() -> void:
	for child: Node in get_children():
		if child is CanvasItem and child != _actors:
			(child as CanvasItem).visible = false
	var white := CanvasItemMaterial.new()
	white.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
	for actor: Node in _actors.get_children():
		var sprite := actor as CanvasItem
		sprite.material = white
		sprite.modulate = Color(30, 30, 30, 1) # пересвічуємо до білого
		for c: Node in actor.get_children():
			if c is CanvasItem:
				(c as CanvasItem).visible = false
	for n: Node in get_tree().root.find_children("*", "CanvasModulate", true, false):
		(n as CanvasModulate).color = Color.WHITE
	for n: Node in get_tree().root.find_children("*", "WorldEnvironment", true, false):
		n.queue_free()


# --- Оновлення -------------------------------------------------------------------

func _update_flicker(delta: float) -> void:
	for f: Dictionary in _flicker:
		var light: PointLight2D = f["node"]
		var left: float = f["left"]
		if left > 0.0:
			left -= delta
			light.energy = float(f["base"]) * (0.1 if _rng.randf() < 0.5 else 1.0)
		else:
			light.energy = float(f["base"])
			if _rng.randf() < delta * (0.8 if str(f["name"]) == "back_tube" else 0.2):
				left = _rng.randf_range(0.08, 0.5)
		f["left"] = left


func _update_guard(delta: float) -> void:
	if _guard == null or _guard_path.size() < 2:
		return
	if _frozen:
		delta = 0.0 # перевірки фокусу: кадр і маска мають збігатися піксель у піксель
	if _guard_pause > 0.0:
		_guard_pause -= delta
		# Зупинився й водить ліхтарем — шукає.
		_guard_light.rotation = _base_angle() + sin(_time * 1.6) * 0.6
		return
	var target: Vector2 = _guard_path[_guard_target]
	var to: Vector2 = target - _guard.position
	if to.length() < 1.0:
		_guard_target = (_guard_target + 1) % _guard_path.size()
		_guard_pause = 1.6
		_guard.play("alert_" + _dir_name(to if to.length() > 0.1 else Vector2.LEFT))
		return
	var step: Vector2 = to.normalized() * GUARD_SPEED * delta
	_guard.position += step if step.length() < to.length() else to
	var dir: String = _dir_name(to)
	if _guard.animation != "walk_" + dir:
		_guard.play("walk_" + dir)
	_guard_light.rotation = to.angle() + sin(_time * 3.0) * 0.06 # ліхтар гойдається в такт кроку
	_guard_light.position = to.normalized() * 6.0 + Vector2(0, -6)
	_guard_dust.position = to.normalized() * 40.0 + Vector2(0, -6)


func _base_angle() -> float:
	var target: Vector2 = _guard_path[_guard_target]
	return (target - _guard.position).angle()


func _dir_name(v: Vector2) -> String:
	if absf(v.x) > absf(v.y):
		return "right" if v.x > 0.0 else "left"
	return "down" if v.y > 0.0 else "up"


func _update_cars(delta: float) -> void:
	# Машини на вулиці: фари світять уперед і крізь вітрини на підлогу залу.
	_car_wait -= delta
	var width: float = float(_data["size"][0]) * float(_data["tile"])
	var street: Array = _data.get("street_y", [320, 352])
	if _car_wait <= 0.0:
		_car_wait = _rng.randf_range(3.0, 7.0)
		var dir: float = 1.0 if _rng.randf() < 0.5 else -1.0
		var car := Node2D.new()
		car.position = Vector2(-60.0 if dir > 0.0 else width + 60.0, float(street[0]) + (12.0 if dir > 0.0 else 30.0))
		var beam := PointLight2D.new()
		beam.texture = _cone_tex
		beam.color = Palette.LAMP_4
		beam.energy = 1.4
		beam.texture_scale = 0.9
		beam.rotation = 0.0 if dir > 0.0 else PI
		beam.height = 60.0
		beam.shadow_enabled = true
		beam.shadow_filter = Light2D.SHADOW_FILTER_NONE
		car.add_child(beam)
		var tail := PointLight2D.new()
		tail.texture = _light_tex
		tail.color = Palette.ALARM
		tail.energy = 0.8
		tail.texture_scale = 0.3
		tail.position = Vector2(-30.0 * dir, 0)
		car.add_child(tail)
		var body := Sprite2D.new()
		body.texture = load("res://assets/sprites/fx/car.png")
		body.material = _unshaded
		body.scale = Vector2(2, 2)
		body.flip_h = dir < 0.0
		body.position = Vector2(-14.0 * dir, 0)
		car.add_child(body)
		_street_fx.add_child(car)
		_cars.append({"node": car, "dir": dir, "speed": _rng.randf_range(90.0, 140.0)})
	for i: int in range(_cars.size() - 1, -1, -1):
		var c: Dictionary = _cars[i]
		var node: Node2D = c["node"]
		node.position.x += float(c["dir"]) * float(c["speed"]) * delta
		if node.position.x < -120.0 or node.position.x > width + 120.0:
			node.queue_free()
			_cars.remove_at(i)
