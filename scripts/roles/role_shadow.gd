extends Control
## Екран Тіні: вид зверху з обмеженою видимістю, джойстик ліворуч, «взяти» і «сховатися» праворуч.
## ЗАГЛУШКА ЕТАПУ 1 за візуалом: світ намальований кодом. Перевіряємо синхронізацію руху:
## клієнт шле лише напрямок джойстика, хост рахує позицію, клієнт згладжує отримане.

const HUD_HEIGHT: float = 30.0
const WORLD_SCALE: float = 2.0 # 1 одиниця рівня = 2 пікселі екрана
const INPUT_SEND_RATE: float = 15.0
const SMOOTHING: float = 14.0 # швидкість наздоганяння позиції з хоста

@onready var _joystick: TouchStick = %Joystick
@onready var _hide_button: Button = %HideButton
@onready var _take_button: Button = %TakeButton

var _view: Dictionary = {}
var _view_radius: float = 50.0
var _server_pos: Vector2 = Vector2.ZERO
var _shown_pos: Vector2 = Vector2.ZERO
var _has_pos: bool = false
var _send_accum: float = 0.0
var _time: float = 0.0


func _ready() -> void:
	GameState.heist_static_received.connect(_on_static)
	GameState.heist_state_received.connect(_on_state)
	_on_static(GameState.last_static)
	_hide_button.pressed.connect(GameState.request_action.bind("hide", {}))
	_take_button.pressed.connect(GameState.request_action.bind("take", {}))
	_hide_button.disabled = true
	_take_button.disabled = true


func _process(delta: float) -> void:
	_time += delta
	_send_accum += delta
	if _send_accum >= 1.0 / INPUT_SEND_RATE:
		_send_accum = 0.0
		GameState.send_move_input(_joystick.direction)
	# Експоненційне згладжування: плавно навіть при 15 оновленнях/с і нерівній мережі.
	_shown_pos = _shown_pos.lerp(_server_pos, 1.0 - exp(-SMOOTHING * delta))
	queue_redraw()


func _on_static(data: Dictionary) -> void:
	_view_radius = float(data.get("view_radius", _view_radius))


func _on_state(_shared: Dictionary, view: Dictionary) -> void:
	_view = view
	if view.has("p"):
		_server_pos = view["p"]
		if not _has_pos:
			_shown_pos = _server_pos
			_has_pos = true
	var hidden: bool = bool(view.get("hid", false))
	_hide_button.disabled = not (hidden or bool(view.get("can_hide", false)))
	_hide_button.text = "ВИЙТИ" if hidden else "СХОВАТИСЬ"
	_take_button.disabled = not bool(view.get("can_take", false))
	_take_button.text = "ЗДОБИЧ Є" if bool(view.get("carry", false)) else "ВЗЯТИ"


func _center() -> Vector2:
	return Vector2(size.x / 2.0, HUD_HEIGHT + (size.y - HUD_HEIGHT) / 2.0).round()


func _to_screen(p: Vector2) -> Vector2:
	return (_center() + (p - _shown_pos) * WORLD_SCALE).round()


func _rect_to_screen(r: Rect2) -> Rect2:
	return Rect2(_to_screen(r.position), r.size * WORLD_SCALE)


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color("050409"))
	if not _has_pos:
		return
	var center: Vector2 = _center()
	var radius: float = _view_radius * WORLD_SCALE
	# Підлога в колі видимості.
	draw_circle(center, radius, Palette.DEEP)
	if _view.has("exit"):
		var exit_rect: Rect2 = _rect_to_screen(_view["exit"])
		draw_rect(exit_rect, Color(Palette.SAFE, 0.15))
		draw_rect(exit_rect, Color(Palette.SAFE, 0.6), false, 1.0)
	for wall: Rect2 in _view.get("walls", []):
		draw_rect(_rect_to_screen(wall), Palette.STEEL)
	for cover: Rect2 in _view.get("covers", []):
		draw_rect(_rect_to_screen(cover), Palette.SLATE)
		draw_rect(_rect_to_screen(cover), Palette.STEEL, false, 1.0)
	if _view.has("door"):
		var door: Rect2 = _rect_to_screen(_view["door"])
		draw_rect(door, Color(Palette.LAMP, 0.25) if bool(_view.get("door_open", false)) else Palette.LAMP)
	if _view.has("safe"):
		var safe: Vector2 = _to_screen(_view["safe"])
		var open: bool = bool(_view.get("safe_open", false))
		draw_rect(Rect2(safe - Vector2(8, 8), Vector2(16, 16)), Palette.LAMP if open else Palette.STEEL)
		draw_rect(Rect2(safe - Vector2(8, 8), Vector2(16, 16)), Palette.NIGHT, false, 1.0)
	# Камеру видно, але не видно, чи вона ввімкнена — це знає тільки Хакер.
	for cam: Vector2 in _view.get("cams", []):
		var c: Vector2 = _to_screen(cam)
		draw_rect(Rect2(c - Vector2(4, 3), Vector2(8, 6)), Palette.MUTED)
	if _view.has("guard"):
		var g: Vector2 = _to_screen(_view["guard"])
		draw_circle(g, 7.0, Palette.DANGER)
		draw_circle(g, 7.0, Palette.NIGHT, false, 1.0)
	# Темрява за межами кола: кільце з кількома сходинками замість плавного градієнта.
	for i: int in 4:
		draw_arc(center, radius - 6.0 + i * 3.0, 0.0, TAU, 64, Color("050409", 0.35 + i * 0.2), 3.0)
	draw_arc(center, radius + 400.0, 0.0, TAU, 64, Color("050409"), 800.0)
	# Сама Тінь.
	var hidden: bool = bool(_view.get("hid", false))
	var seen: bool = bool(_view.get("seen", false))
	var body_color: Color = Roles.color(Roles.Id.SHADOW)
	if hidden:
		body_color = Color(body_color, 0.4)
	draw_circle(center, 6.0, body_color)
	if bool(_view.get("carry", false)):
		draw_rect(Rect2(center + Vector2(-3, -12), Vector2(6, 5)), Palette.LAMP)
	if seen and fmod(_time, 0.3) < 0.15:
		draw_arc(center, 11.0, 0.0, TAU, 24, Palette.DANGER, 2.0)
