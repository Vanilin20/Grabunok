class_name HeistSim
extends RefCounted
## Авторитетна симуляція справи. Живе ТІЛЬКИ на хості.
##
## ЗАГЛУШКА ЕТАПУ 1: «тестовий поверх» для перевірки мережі, ролей і синхронізації.
## Це не рівень «Магазин на розі» з MVP — геометрія, баланс і механіки тут тестові.
## Числа тривоги взяті з ТЗ лише за змістом (росте від помилок, 50% — охорона посилюється,
## 100% — провал), конкретні значення — тестові.

enum Outcome { NONE, SUCCESS, CAUGHT, TIMEOUT }

const DURATION: float = 600.0
const LEVEL_SIZE: Vector2 = Vector2(320, 180)
const SHADOW_SPEED: float = 55.0
const SHADOW_HALF: float = 3.0 # половина розміру хітбокса Тіні
const SHADOW_VIEW_RADIUS: float = 52.0 # «невелике коло навколо себе»
const HIDE_DISTANCE: float = 12.0
const TAKE_DISTANCE: float = 18.0
const CAMERA_RANGE: float = 34.0
const CAMERA_OFF_TIME: float = 6.0 # «вимикає камери на кілька секунд»
const GUARD_RANGE: float = 24.0
const GUARD_SPEED: float = 26.0
const GUARD_ALERT_SPEED_MULT: float = 1.5 # після 50% тривоги
const ALARM_CAMERA_PER_SEC: float = 30.0
const ALARM_GUARD_PER_SEC: float = 45.0
const ALARM_SAFE_MISTAKE: float = 10.0
const ALARM_ALERT_LEVEL: float = 50.0
const BUSY_WINDOW: float = 2.5
const COMBO_RANGE: int = 40 # цифри на диску сейфа: 0..39

var time_left: float = DURATION
var alarm: float = 0.0
var max_alarm: float = 0.0
var outcome: Outcome = Outcome.NONE

var walls: Array[Rect2] = [
	Rect2(0, 0, 320, 4), Rect2(0, 176, 320, 4), Rect2(0, 0, 4, 180), Rect2(316, 0, 4, 180),
	Rect2(200, 0, 4, 70), Rect2(200, 100, 4, 80), # перегородка з дверима посередині
]
var covers: Array[Rect2] = [Rect2(70, 92, 60, 12), Rect2(236, 120, 40, 12)] # стелажі, за якими можна сховатися
var door_rect: Rect2 = Rect2(200, 70, 4, 30)
var door_open: bool = false
var exit_rect: Rect2 = Rect2(4, 148, 26, 28)
var safe_pos: Vector2 = Vector2(290, 30)
var safe_open: bool = false
var safe_combo: Array[int] = []
var safe_mistakes: int = 0

var cameras: Array[Dictionary] = [
	{"pos": Vector2(110, 40), "off_left": 0.0},
	{"pos": Vector2(250, 70), "off_left": 0.0},
]

var guard_path: Array[Vector2] = [Vector2(40, 60), Vector2(180, 60), Vector2(180, 150), Vector2(60, 150)]
var guard_pos: Vector2 = Vector2(40, 60)
var _guard_target: int = 1

var shadow_pos: Vector2 = Vector2(16, 162)
var shadow_input: Vector2 = Vector2.ZERO
var shadow_hidden: bool = false
var shadow_carrying: bool = false
var shadow_seen: bool = false

var _last_action: Dictionary = {} # role -> мітка часу останньої дії
var _elapsed: float = 0.0


func _init() -> void:
	var rng := RandomNumberGenerator.new()
	rng.randomize()
	for i: int in 3:
		safe_combo.append(rng.randi_range(0, COMBO_RANGE - 1))


func is_finished() -> bool:
	return outcome != Outcome.NONE


func tick(delta: float) -> void:
	if is_finished():
		return
	_elapsed += delta
	time_left = maxf(time_left - delta, 0.0)
	for cam: Dictionary in cameras:
		cam["off_left"] = maxf(float(cam["off_left"]) - delta, 0.0)
	_move_guard(delta)
	_move_shadow(delta)
	_detect(delta)
	if alarm >= 100.0:
		outcome = Outcome.CAUGHT
	elif time_left <= 0.0:
		outcome = Outcome.TIMEOUT
	elif shadow_carrying and exit_rect.has_point(shadow_pos):
		outcome = Outcome.SUCCESS


# --- Дії від гравців (хост уже перевірив, що роль відповідає гравцю) -----

func set_shadow_input(direction: Vector2) -> void:
	shadow_input = direction.limit_length(1.0)
	if shadow_input.length() > 0.1:
		_mark_busy(Roles.Id.SHADOW)


func apply_action(role: int, action: String, args: Dictionary) -> void:
	if is_finished():
		return
	match role:
		Roles.Id.HACKER:
			_hacker_action(action, args)
		Roles.Id.SHADOW:
			_shadow_action(action)
		Roles.Id.SAFECRACKER:
			_safecracker_action(action, args)


func _hacker_action(action: String, args: Dictionary) -> void:
	match action:
		"camera_off":
			var index: int = int(args.get("index", -1))
			if index >= 0 and index < cameras.size():
				cameras[index]["off_left"] = CAMERA_OFF_TIME
				_mark_busy(Roles.Id.HACKER)
		"door_toggle":
			# Не закриваємо двері на Тіні — інакше вона застрягне в стіні.
			if door_open and door_rect.grow(SHADOW_HALF).has_point(shadow_pos):
				return
			door_open = not door_open
			_mark_busy(Roles.Id.HACKER)


func _shadow_action(action: String) -> void:
	match action:
		"hide":
			shadow_hidden = not shadow_hidden and _near_cover(shadow_pos)
			_mark_busy(Roles.Id.SHADOW)
		"take":
			if safe_open and not shadow_carrying and shadow_pos.distance_to(safe_pos) <= TAKE_DISTANCE:
				shadow_carrying = true
				_mark_busy(Roles.Id.SHADOW)


func _safecracker_action(action: String, args: Dictionary) -> void:
	if action != "safe_try" or safe_open:
		return
	_mark_busy(Roles.Id.SAFECRACKER)
	var numbers: Array = args.get("numbers", [])
	var correct: bool = numbers.size() == safe_combo.size()
	if correct:
		for i: int in safe_combo.size():
			if int(numbers[i]) != safe_combo[i]:
				correct = false
				break
	if correct:
		safe_open = true
	else:
		safe_mistakes += 1
		_add_alarm(ALARM_SAFE_MISTAKE)


# --- Симуляція ------------------------------------------------------------

func _move_guard(delta: float) -> void:
	var speed: float = GUARD_SPEED * (GUARD_ALERT_SPEED_MULT if alarm >= ALARM_ALERT_LEVEL else 1.0)
	var target: Vector2 = guard_path[_guard_target]
	guard_pos = guard_pos.move_toward(target, speed * delta)
	if guard_pos.distance_to(target) < 0.5:
		_guard_target = (_guard_target + 1) % guard_path.size()


func _move_shadow(delta: float) -> void:
	if shadow_hidden:
		if shadow_input.length() > 0.3:
			shadow_hidden = false # рух виводить зі схованки
		else:
			return
	var step: Vector2 = shadow_input * SHADOW_SPEED * delta
	# Рух по осях окремо — Тінь ковзає вздовж стін, а не прилипає.
	var next: Vector2 = shadow_pos + Vector2(step.x, 0.0)
	if not _blocked(next):
		shadow_pos = next
	next = shadow_pos + Vector2(0.0, step.y)
	if not _blocked(next):
		shadow_pos = next


func _blocked(pos: Vector2) -> bool:
	var box := Rect2(pos - Vector2.ONE * SHADOW_HALF, Vector2.ONE * SHADOW_HALF * 2.0)
	for rect: Rect2 in _solid_rects():
		if rect.intersects(box):
			return true
	return false


func _solid_rects() -> Array[Rect2]:
	var result: Array[Rect2] = walls.duplicate()
	result.append_array(covers)
	if not door_open:
		result.append(door_rect)
	return result


func _near_cover(pos: Vector2) -> bool:
	for rect: Rect2 in covers:
		if rect.grow(HIDE_DISTANCE).has_point(pos):
			return true
	return false


func _detect(delta: float) -> void:
	shadow_seen = false
	if shadow_hidden:
		return
	for cam: Dictionary in cameras:
		if float(cam["off_left"]) <= 0.0 and shadow_pos.distance_to(cam["pos"]) <= CAMERA_RANGE:
			shadow_seen = true
			_add_alarm(ALARM_CAMERA_PER_SEC * delta)
	if shadow_pos.distance_to(guard_pos) <= GUARD_RANGE:
		shadow_seen = true
		_add_alarm(ALARM_GUARD_PER_SEC * delta)


func _add_alarm(amount: float) -> void:
	alarm = clampf(alarm + amount, 0.0, 100.0)
	max_alarm = maxf(max_alarm, alarm)


func _mark_busy(role: int) -> void:
	_last_action[role] = _elapsed


# --- Що бачить кожна роль -------------------------------------------------

## Статичні дані, які роль отримує один раз на початку справи.
func static_view_for(role: int) -> Dictionary:
	match role:
		Roles.Id.HACKER:
			var cam_positions: Array[Vector2] = []
			for cam: Dictionary in cameras:
				cam_positions.append(cam["pos"])
			return {
				"size": LEVEL_SIZE, "walls": walls, "covers": covers, "door": door_rect,
				"cams": cam_positions, "cam_range": CAMERA_RANGE, "cam_off_time": CAMERA_OFF_TIME, "safe": safe_pos, "exit": exit_rect,
			}
		Roles.Id.SHADOW:
			return {"view_radius": SHADOW_VIEW_RADIUS}
		Roles.Id.SAFECRACKER:
			# Комбінацію Зломщик «відчуває» вібрацією на своєму телефоні — її бачить лише ця роль.
			return {"combo": safe_combo, "range": COMBO_RANGE}
	return {}


## Дані, які роль отримує щотіку.
func view_for(role: int) -> Dictionary:
	match role:
		Roles.Id.HACKER:
			var cams: Array[float] = []
			for cam: Dictionary in cameras:
				cams.append(cam["off_left"])
			return {"cams_off": cams, "door_open": door_open, "guard": guard_pos, "alert": alarm >= ALARM_ALERT_LEVEL}
		Roles.Id.SHADOW:
			return _shadow_view()
		Roles.Id.SAFECRACKER:
			return {"open": safe_open, "mistakes": safe_mistakes}
	return {}


## Тінь отримує лише те, що в колі навколо неї.
func _shadow_view() -> Dictionary:
	var r: float = SHADOW_VIEW_RADIUS
	var area := Rect2(shadow_pos - Vector2.ONE * r, Vector2.ONE * r * 2.0)
	var near_walls: Array[Rect2] = []
	for rect: Rect2 in walls:
		if rect.intersects(area):
			near_walls.append(rect.intersection(area))
	var near_covers: Array[Rect2] = []
	for rect: Rect2 in covers:
		if rect.intersects(area):
			near_covers.append(rect)
	var cams: Array[Vector2] = []
	for cam: Dictionary in cameras:
		if shadow_pos.distance_to(cam["pos"]) <= r:
			cams.append(cam["pos"])
	var view: Dictionary = {
		"p": shadow_pos, "hid": shadow_hidden, "carry": shadow_carrying, "seen": shadow_seen,
		"walls": near_walls, "covers": near_covers, "cams": cams,
		"can_hide": _near_cover(shadow_pos),
		"can_take": safe_open and not shadow_carrying and shadow_pos.distance_to(safe_pos) <= TAKE_DISTANCE,
	}
	if shadow_pos.distance_to(guard_pos) <= r:
		view["guard"] = guard_pos
	if door_rect.intersects(area):
		view["door"] = door_rect
		view["door_open"] = door_open
	if shadow_pos.distance_to(safe_pos) <= r:
		view["safe"] = safe_pos
		view["safe_open"] = safe_open
	if exit_rect.intersects(area):
		view["exit"] = exit_rect
	return view


## Стан гравця для спільної панелі: busy / waiting / danger.
func status_for(role: int) -> String:
	if role == Roles.Id.SHADOW and shadow_seen:
		return "danger"
	if _last_action.has(role) and _elapsed - float(_last_action[role]) <= BUSY_WINDOW:
		return "busy"
	return "waiting"


func result_summary() -> Dictionary:
	return {
		"outcome": outcome,
		"max_alarm": roundi(max_alarm),
		"time_used": roundi(DURATION - time_left),
		"loot": shadow_carrying and outcome == Outcome.SUCCESS,
		"safe_mistakes": safe_mistakes,
	}
