class_name TouchStick
extends Control
## Віртуальний джойстик для лівого великого пальця. Працює з мультитачем:
## запам'ятовує свій палець і не реагує на дотики правої руки до кнопок.
## На ПК керується мишею (emulate_touch_from_mouse) і клавішами WASD/стрілками.

signal direction_changed(direction: Vector2)

@export var radius: float = 34.0
@export var dead_zone: float = 0.18

var direction: Vector2 = Vector2.ZERO

var _touch_index: int = -1
var _origin: Vector2 = Vector2.ZERO
var _knob: Vector2 = Vector2.ZERO


func _ready() -> void:
	_origin = size / 2.0


func _gui_input(event: InputEvent) -> void:
	if event is InputEventScreenTouch:
		var touch := event as InputEventScreenTouch
		if touch.pressed and _touch_index == -1:
			_touch_index = touch.index
			_origin = touch.position # джойстик з'являється там, де торкнувся палець
			_update_knob(touch.position)
			accept_event()
		elif not touch.pressed and touch.index == _touch_index:
			_touch_index = -1
			_origin = size / 2.0
			_update_knob(_origin)
			accept_event()
	elif event is InputEventScreenDrag:
		var drag := event as InputEventScreenDrag
		if drag.index == _touch_index:
			_update_knob(drag.position)
			accept_event()


func _process(_delta: float) -> void:
	if _touch_index != -1:
		return
	# Клавіатура для тестів на ПК.
	var keys := Vector2(
		float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT)),
		float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP)))
	var target: Vector2 = keys.normalized()
	if target != direction:
		_knob = _origin + target * radius
		_set_direction(target)


func _update_knob(pos: Vector2) -> void:
	var offset: Vector2 = (pos - _origin).limit_length(radius)
	_knob = _origin + offset
	var dir: Vector2 = offset / radius
	_set_direction(Vector2.ZERO if dir.length() < dead_zone else dir)


func _set_direction(dir: Vector2) -> void:
	queue_redraw()
	if dir.is_equal_approx(direction):
		return
	direction = dir
	direction_changed.emit(direction)


func _draw() -> void:
	var active: bool = _touch_index != -1 or direction != Vector2.ZERO
	var ring_color: Color = Color(Palette.TEXT, 0.35 if active else 0.15)
	draw_arc(_origin, radius, 0.0, TAU, 32, ring_color, 1.0, false)
	draw_arc(_origin, radius + 3.0, 0.0, TAU, 32, Color(Palette.NIGHT, 0.6), 1.0, false)
	var knob: Vector2 = _knob if active else _origin
	draw_circle(knob, 12.0, Color(Palette.TEXT, 0.5 if active else 0.2))
	draw_circle(knob, 12.0, Color(Palette.NIGHT, 0.8), false, 1.0)
