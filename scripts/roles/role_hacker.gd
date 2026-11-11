extends Control
## Екран Хакера: схема поверху, камери, двері, охорона (TZ: «Хакер»).
## ЗАГЛУШКА ЕТАПУ 1 за візуалом і механікою: схема намальована кодом, двері відкриваються
## одним тапом (міні-гра з дротами — етап 2). Перевіряємо мережу: тап → дія на хост → стан назад.

const HUD_HEIGHT: float = 30.0
const MARGIN: float = 10.0
const TAP_RADIUS: float = 18.0

var _static: Dictionary = {}
var _view: Dictionary = {}
var _scale: float = 1.0
var _offset: Vector2 = Vector2.ZERO
var _time: float = 0.0


func _ready() -> void:
	GameState.heist_static_received.connect(_on_static)
	GameState.heist_state_received.connect(_on_state)
	_on_static(GameState.last_static)
	resized.connect(_fit)


func _process(delta: float) -> void:
	_time += delta
	queue_redraw()


func _on_static(data: Dictionary) -> void:
	_static = data
	_fit()


func _on_state(_shared: Dictionary, view: Dictionary) -> void:
	_view = view


func _fit() -> void:
	if not _static.has("size"):
		return
	var level_size: Vector2 = _static["size"]
	var area := Vector2(size.x - MARGIN * 2.0, size.y - HUD_HEIGHT - MARGIN * 2.0)
	_scale = minf(area.x / level_size.x, area.y / level_size.y)
	_offset = Vector2(MARGIN, HUD_HEIGHT + MARGIN) + (area - level_size * _scale) / 2.0
	_offset = _offset.round()


func _to_screen(p: Vector2) -> Vector2:
	return _offset + p * _scale


func _rect_to_screen(r: Rect2) -> Rect2:
	return Rect2(_to_screen(r.position), r.size * _scale)


func _gui_input(event: InputEvent) -> void:
	if not (event is InputEventScreenTouch and (event as InputEventScreenTouch).pressed):
		return
	var pos: Vector2 = (event as InputEventScreenTouch).position
	var cams: Array = _static.get("cams", [])
	for i: int in cams.size():
		if pos.distance_to(_to_screen(cams[i])) <= TAP_RADIUS:
			GameState.request_action("camera_off", {"index": i})
			accept_event()
			return
	if _static.has("door"):
		var door: Rect2 = _rect_to_screen(_static["door"]).grow(TAP_RADIUS * 0.6)
		if door.has_point(pos):
			GameState.request_action("door_toggle")
			accept_event()


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Palette.NIGHT)
	if not _static.has("size"):
		return
	var cyan_dim := Color(Palette.CYAN, 0.18)
	# Сітка креслення.
	var level_rect: Rect2 = _rect_to_screen(Rect2(Vector2.ZERO, _static["size"]))
	var step: float = 20.0 * _scale
	var x: float = level_rect.position.x
	while x <= level_rect.end.x:
		draw_line(Vector2(roundf(x), level_rect.position.y), Vector2(roundf(x), level_rect.end.y), Color(Palette.CYAN, 0.06))
		x += step
	var y: float = level_rect.position.y
	while y <= level_rect.end.y:
		draw_line(Vector2(level_rect.position.x, roundf(y)), Vector2(level_rect.end.x, roundf(y)), Color(Palette.CYAN, 0.06))
		y += step
	# Вихід.
	var exit_rect: Rect2 = _rect_to_screen(_static["exit"])
	draw_rect(exit_rect, Color(Palette.SAFE, 0.12))
	draw_rect(exit_rect, Color(Palette.SAFE, 0.5), false, 1.0)
	# Стіни й стелажі.
	for wall: Rect2 in _static.get("walls", []):
		draw_rect(_rect_to_screen(wall), Palette.CYAN)
	for cover: Rect2 in _static.get("covers", []):
		draw_rect(_rect_to_screen(cover), cyan_dim)
		draw_rect(_rect_to_screen(cover), Color(Palette.CYAN, 0.5), false, 1.0)
	# Двері: зачинені — суцільні, відчинені — пунктир.
	var door: Rect2 = _rect_to_screen(_static["door"])
	if bool(_view.get("door_open", false)):
		draw_rect(door, Color(Palette.SAFE, 0.25))
		draw_rect(door, Palette.SAFE, false, 1.0)
	else:
		draw_rect(door, Palette.LAMP)
	# Сейф.
	var safe: Vector2 = _to_screen(_static["safe"])
	draw_rect(Rect2(safe - Vector2(6, 6), Vector2(12, 12)), Palette.LAMP, false, 1.0)
	draw_circle(safe, 2.0, Palette.LAMP)
	# Камери: увімкнені — червоне коло огляду, вимкнені — сірі з дугою відліку.
	var cams: Array = _static.get("cams", [])
	var cams_off: Array = _view.get("cams_off", [])
	var cam_range: float = float(_static.get("cam_range", 30.0)) * _scale
	for i: int in cams.size():
		var p: Vector2 = _to_screen(cams[i])
		var off_left: float = float(cams_off[i]) if i < cams_off.size() else 0.0
		if off_left > 0.0:
			draw_arc(p, cam_range, 0.0, TAU, 48, Color(Palette.MUTED, 0.25), 1.0)
			draw_arc(p, 9.0, -PI / 2.0, -PI / 2.0 + TAU * off_left / float(_static.get("cam_off_time", 6.0)), 24, Palette.CYAN, 2.0)
			draw_circle(p, 4.0, Palette.MUTED)
		else:
			draw_circle(p, cam_range, Color(Palette.DANGER, 0.08))
			draw_arc(p, cam_range, 0.0, TAU, 48, Color(Palette.DANGER, 0.45), 1.0)
			draw_circle(p, 4.0, Palette.DANGER if fmod(_time, 1.0) < 0.6 else Color(Palette.DANGER, 0.5))
	# Охоронець.
	if _view.has("guard"):
		var g: Vector2 = _to_screen(_view["guard"])
		var alert: bool = bool(_view.get("alert", false))
		draw_circle(g, 5.0, Palette.DANGER if alert else Palette.TEXT)
		draw_circle(g, 5.0, Palette.NIGHT, false, 1.0)
