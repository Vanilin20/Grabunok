extends Node
## Навігація між екранами й короткі сповіщення поверх будь-якого екрана.
## Екрани не знають один про одного: фазу гри міняє GameState, а Router показує відповідний екран.

const MENU_SCENE: String = "res://scenes/ui/main_menu.tscn"
const LOBBY_SCENE: String = "res://scenes/ui/lobby.tscn"
const HEIST_SCENE: String = "res://scenes/main/heist.tscn"
const RESULT_SCENE: String = "res://scenes/ui/result.tscn"
const JOIN_SCENE: String = "res://scenes/ui/join_room.tscn"
const SENSOR_TEST_SCENE: String = "res://scenes/ui/sensor_test.tscn"
const BENCHMARK_SCENE: String = "res://scenes/ui/benchmark.tscn"
const TOAST_TIME: float = 2.8

## Автотести вимикають перемикання екранів, щоб їхня сцена не замінювалась.
var auto_navigate: bool = true

var _toast_layer: CanvasLayer
var _toast_label: Label
var _toast_tween: Tween
var _reconnect_label: Label


func _ready() -> void:
	DisplayServer.screen_set_keep_on(true) # екран не гасне посеред справи
	_build_overlay()
	GameState.phase_changed.connect(_on_phase_changed)
	GameState.notice.connect(toast)
	GameState.rejected.connect(_on_rejected)
	Network.reconnecting.connect(_on_reconnecting)
	Network.reconnected.connect(_hide_reconnect)
	Network.connection_lost.connect(func(_reason: String) -> void: _hide_reconnect())


func go(scene_path: String) -> void:
	ScreenOrientation.set_landscape()
	get_tree().change_scene_to_file.call_deferred(scene_path)


func show_join() -> void:
	go(JOIN_SCENE)


func show_sensor_test() -> void:
	go(SENSOR_TEST_SCENE)


func show_benchmark() -> void:
	go(BENCHMARK_SCENE)


func show_menu() -> void:
	go(MENU_SCENE)


func toast(text: String) -> void:
	_toast_label.text = text
	_toast_label.visible = true
	if _toast_tween != null:
		_toast_tween.kill()
	_toast_label.modulate.a = 1.0
	_toast_tween = create_tween()
	_toast_tween.tween_interval(TOAST_TIME)
	_toast_tween.tween_property(_toast_label, "modulate:a", 0.0, 0.4)
	_toast_tween.tween_callback(_toast_label.hide)


func _on_phase_changed(phase: GameState.Phase) -> void:
	if not auto_navigate:
		return
	var path: String = _scene_for(phase)
	var current: Node = get_tree().current_scene
	if current != null and current.scene_file_path == path:
		return # уже на потрібному екрані
	go(path)


func _scene_for(phase: GameState.Phase) -> String:
	match phase:
		GameState.Phase.LOBBY:
			return LOBBY_SCENE
		GameState.Phase.HEIST:
			return HEIST_SCENE
		GameState.Phase.RESULT:
			return RESULT_SCENE
	return MENU_SCENE


func _on_rejected(reason: String) -> void:
	toast(reason)
	GameState.leave_room()


func _on_reconnecting(attempt: int, max_attempts: int) -> void:
	_reconnect_label.text = "ЗВ'ЯЗОК ВТРАЧЕНО\nперепідключення %d/%d…" % [attempt, max_attempts]
	_reconnect_label.visible = true


func _hide_reconnect() -> void:
	_reconnect_label.visible = false


func _build_overlay() -> void:
	_toast_layer = CanvasLayer.new()
	_toast_layer.layer = 100
	add_child(_toast_layer)

	_toast_label = _make_banner(Palette.LAMP)
	_toast_label.anchor_top = 1.0
	_toast_label.anchor_bottom = 1.0
	_toast_label.offset_top = -40.0
	_toast_label.offset_bottom = -14.0
	_toast_layer.add_child(_toast_label)

	_reconnect_label = _make_banner(Palette.DANGER)
	_reconnect_label.anchor_top = 0.5
	_reconnect_label.anchor_bottom = 0.5
	_reconnect_label.offset_top = -20.0
	_reconnect_label.offset_bottom = 20.0
	_reconnect_label.mouse_filter = Control.MOUSE_FILTER_STOP # під час перепідключення екран не реагує
	_toast_layer.add_child(_reconnect_label)


func _make_banner(color: Color) -> Label:
	var label := Label.new()
	label.anchor_left = 0.0
	label.anchor_right = 1.0
	label.offset_left = 40.0
	label.offset_right = -40.0
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_color_override("font_color", color)
	var box := StyleBoxFlat.new()
	box.bg_color = Color(Palette.NIGHT, 0.94)
	box.border_color = color
	box.set_border_width_all(1)
	box.set_content_margin_all(6.0)
	box.anti_aliasing = false
	label.add_theme_stylebox_override("normal", box)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.visible = false
	return label
