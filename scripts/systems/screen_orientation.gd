class_name ScreenOrientation
extends RefCounted
## Перемикання орієнтації. TZ: «Уся гра йде в горизонтальній орієнтації. Коли роль бере в руки
## предмет, гра просить повернути телефон вертикально».
## Базова роздільність теж повертається: 640×360 ↔ 360×640, щоб піксель лишався тим самим.

const LANDSCAPE_SIZE: Vector2i = Vector2i(640, 360)
const PORTRAIT_SIZE: Vector2i = Vector2i(360, 640)
const DESKTOP_SCALE: int = 2 # на ПК вікно імітує поворот телефона


static func set_portrait() -> void:
	_apply(DisplayServer.SCREEN_PORTRAIT, PORTRAIT_SIZE)


static func set_landscape() -> void:
	_apply(DisplayServer.SCREEN_LANDSCAPE, LANDSCAPE_SIZE)


static func is_portrait() -> bool:
	var tree := Engine.get_main_loop() as SceneTree
	return tree != null and tree.root.content_scale_size == PORTRAIT_SIZE


static func _apply(orientation: DisplayServer.ScreenOrientation, base_size: Vector2i) -> void:
	var tree := Engine.get_main_loop() as SceneTree
	if tree == null:
		return
	var window: Window = tree.root
	if window.content_scale_size == base_size:
		return
	window.content_scale_size = base_size
	if OS.has_feature("mobile"):
		DisplayServer.screen_set_orientation(orientation)
	elif DisplayServer.get_name() != "headless":
		window.size = base_size * DESKTOP_SCALE
