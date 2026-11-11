extends Control
## Екран Зломщика: замок крупним планом; вертикальний режим для сейфа з вібрацією (TZ: «Зломщик»).
## Диск крутиться поворотом телефона (гіроскоп) або пальцем. Кожна поділка — легкий клац вібрацією,
## потрібна цифра — сильний поштовх. Комбінацію знає лише телефон Зломщика; перевіряє хост.
## ЗАГЛУШКА ЕТАПУ 1 за візуалом (диск намальований кодом) і балансом.

const DIAL_RADIUS: float = 118.0
const GYRO_GAIN: float = 1.0 # 1 = диск повертається рівно на кут повороту телефона
const TICK_VIBRATE_MS: int = 8
const TICK_VIBRATE_AMP: float = 0.25
const HIT_VIBRATE_MS: int = 70
const HIT_VIBRATE_AMP: float = 1.0
const FAIL_VIBRATE_MS: int = 300

@onready var _landscape: Control = %Landscape
@onready var _portrait: Control = %Portrait
@onready var _open_button: Button = %OpenButton
@onready var _status_label: Label = %StatusLabel
@onready var _lock_button: Button = %LockButton
@onready var _reset_button: Button = %ResetButton
@onready var _back_button: Button = %BackButton

var _combo: Array = []
var _range: int = 40
var _angle: float = 0.0 # радіани, 0 = цифра 0 угорі
var _last_number: int = 0
var _entered: Array[int] = []
var _safe_open: bool = false
var _mistakes: int = 0
var _drag_index: int = -1
var _drag_angle: float = 0.0
var _hit_flash: float = 0.0
var _fail_flash: float = 0.0
var _has_haptics: bool = false


func _ready() -> void:
	_has_haptics = OS.has_feature("mobile")
	GameState.heist_static_received.connect(_on_static)
	GameState.heist_state_received.connect(_on_state)
	_on_static(GameState.last_static)
	_open_button.pressed.connect(_enter_portrait)
	_back_button.pressed.connect(_enter_landscape)
	_lock_button.pressed.connect(_on_lock)
	_reset_button.pressed.connect(_on_reset)
	_enter_landscape()


func _exit_tree() -> void:
	ScreenOrientation.set_landscape()


func _on_static(data: Dictionary) -> void:
	_combo = data.get("combo", [])
	_range = int(data.get("range", 40))


func _on_state(_shared: Dictionary, view: Dictionary) -> void:
	var was_open: bool = _safe_open
	_safe_open = bool(view.get("open", false))
	var mistakes: int = int(view.get("mistakes", 0))
	if mistakes > _mistakes:
		_fail_flash = 0.6
		_vibrate(FAIL_VIBRATE_MS, HIT_VIBRATE_AMP)
	_mistakes = mistakes
	if _safe_open and not was_open:
		_vibrate(HIT_VIBRATE_MS * 3, HIT_VIBRATE_AMP)
		_enter_landscape()
	_update_landscape()


func _enter_portrait() -> void:
	if _safe_open:
		return
	ScreenOrientation.set_portrait()
	_landscape.visible = false
	_portrait.visible = true
	_last_number = _current_number()


func _enter_landscape() -> void:
	ScreenOrientation.set_landscape()
	_portrait.visible = false
	_landscape.visible = true
	_update_landscape()


func _update_landscape() -> void:
	_open_button.disabled = _safe_open
	_open_button.text = "СЕЙФ ВІДКРИТО" if _safe_open else "ВЗЯТИСЯ ЗА СЕЙФ"
	_status_label.text = "Тінь може брати здобич" if _safe_open else ("помилок: %d" % _mistakes if _mistakes > 0 else "")


func _process(delta: float) -> void:
	_hit_flash = maxf(_hit_flash - delta, 0.0)
	_fail_flash = maxf(_fail_flash - delta, 0.0)
	if not _portrait.visible:
		return
	if _drag_index == -1:
		var gyro: Vector3 = Input.get_gyroscope()
		_angle -= gyro.z * GYRO_GAIN * delta
	_check_ticks()
	queue_redraw()


func _gui_input(event: InputEvent) -> void:
	if not _portrait.visible:
		return
	var center: Vector2 = _dial_center()
	if event is InputEventScreenTouch:
		var touch := event as InputEventScreenTouch
		if touch.pressed and _drag_index == -1 and touch.position.distance_to(center) <= DIAL_RADIUS + 20.0:
			_drag_index = touch.index
			_drag_angle = (touch.position - center).angle()
			accept_event()
		elif not touch.pressed and touch.index == _drag_index:
			_drag_index = -1
	elif event is InputEventScreenDrag:
		var drag := event as InputEventScreenDrag
		if drag.index == _drag_index:
			var a: float = (drag.position - center).angle()
			_angle += wrapf(a - _drag_angle, -PI, PI)
			_drag_angle = a
			accept_event()


func _current_number() -> int:
	var step: float = TAU / float(_range)
	# Мітка зверху показує цифру, що проти неї: диск крутиться за годинниковою — цифри йдуть назад.
	return posmod(roundi(-_angle / step), _range)


func _check_ticks() -> void:
	var number: int = _current_number()
	if number == _last_number:
		return
	_last_number = number
	var step_index: int = _entered.size()
	if step_index < _combo.size() and number == int(_combo[step_index]):
		_hit_flash = 0.25
		_vibrate(HIT_VIBRATE_MS, HIT_VIBRATE_AMP)
	else:
		_vibrate(TICK_VIBRATE_MS, TICK_VIBRATE_AMP)


func _on_lock() -> void:
	if _entered.size() >= 3:
		return
	_entered.append(_current_number())
	if _entered.size() == 3:
		GameState.request_action("safe_try", {"numbers": _entered.duplicate()})
		_entered.clear()
	queue_redraw()


func _on_reset() -> void:
	_entered.clear()
	queue_redraw()


func _vibrate(duration_ms: int, amplitude: float) -> void:
	if _has_haptics:
		Input.vibrate_handheld(duration_ms, amplitude)


func _dial_center() -> Vector2:
	return Vector2(size.x / 2.0, size.y * 0.47).round()


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Palette.NIGHT)
	if not _portrait.visible:
		_draw_safe_icon(Vector2(size.x / 2.0, size.y * 0.42).round())
		return
	var center: Vector2 = _dial_center()
	var fail: bool = _fail_flash > 0.0
	# Корпус і диск.
	draw_circle(center, DIAL_RADIUS + 14.0, Palette.DEEP)
	draw_arc(center, DIAL_RADIUS + 14.0, 0.0, TAU, 96, Palette.DANGER if fail else Palette.SLATE, 2.0)
	draw_circle(center, DIAL_RADIUS, Palette.DUSK)
	draw_arc(center, DIAL_RADIUS, 0.0, TAU, 96, Palette.STEEL, 1.0)
	var font: Font = get_theme_default_font()
	var step: float = TAU / float(_range)
	for i: int in _range:
		var a: float = _angle + i * step - PI / 2.0
		var dir := Vector2(cos(a), sin(a))
		var long_tick: bool = i % 5 == 0
		var inner: float = DIAL_RADIUS - (16.0 if long_tick else 8.0)
		draw_line(center + dir * inner, center + dir * (DIAL_RADIUS - 2.0), Palette.TEXT if long_tick else Palette.MUTED, 1.0)
		if i % 10 == 0:
			var label_pos: Vector2 = center + dir * (DIAL_RADIUS - 30.0)
			draw_string(font, (label_pos + Vector2(-8, 4)).round(), str(i), HORIZONTAL_ALIGNMENT_CENTER, 16, 8, Palette.TEXT)
	draw_circle(center, 22.0, Palette.SLATE)
	draw_circle(center, 22.0, Palette.STEEL, false, 1.0)
	# Мітка зверху.
	var top: Vector2 = center - Vector2(0, DIAL_RADIUS + 14.0)
	draw_colored_polygon(PackedVector2Array([top + Vector2(-6, -8), top + Vector2(6, -8), top + Vector2(0, 4)]), Palette.LAMP)
	# Без вібрації (ПК) потрібну цифру показує спалах — тільки для тестів, на телефоні його нема.
	if _hit_flash > 0.0 and not _has_haptics:
		draw_arc(center, DIAL_RADIUS + 6.0, 0.0, TAU, 96, Color(Palette.LAMP, _hit_flash * 4.0), 3.0)
	# Поточна цифра й введені.
	draw_string(font, Vector2(0, center.y - DIAL_RADIUS - 40.0), "%02d" % _current_number(), HORIZONTAL_ALIGNMENT_CENTER, size.x, 24, Palette.LAMP)
	var slots_y: float = center.y + DIAL_RADIUS + 34.0
	for i: int in 3:
		var slot := Rect2(Vector2(size.x / 2.0 - 66.0 + i * 46.0, slots_y), Vector2(40, 26))
		draw_rect(slot, Palette.DEEP)
		draw_rect(slot, Palette.DANGER if fail else (Palette.LAMP if i == _entered.size() else Palette.SLATE), false, 1.0)
		if i < _entered.size():
			draw_string(font, slot.position + Vector2(0, 17), "%02d" % _entered[i], HORIZONTAL_ALIGNMENT_CENTER, slot.size.x, 8, Palette.TEXT)


func _draw_safe_icon(c: Vector2) -> void:
	var body := Rect2(c - Vector2(46, 40), Vector2(92, 80))
	draw_rect(body, Palette.DUSK)
	draw_rect(body, Palette.LAMP if _safe_open else Palette.STEEL, false, 2.0)
	draw_rect(Rect2(body.position + Vector2(8, 8), body.size - Vector2(16, 16)), Palette.SLATE, false, 1.0)
	draw_circle(c, 16.0, Palette.DEEP)
	draw_arc(c, 16.0, 0.0, TAU, 32, Palette.LAMP if _safe_open else Palette.STEEL, 1.0)
	draw_line(c, c + Vector2(0, -12), Palette.TEXT, 2.0)
