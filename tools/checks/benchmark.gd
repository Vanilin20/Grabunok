extends SceneTree
## Перевірка 5: продуктивність. Сцена з усіма ефектами крутиться 60 с у реальному часі.
## Запуск: godot --path . -s tools/checks/benchmark.gd -- --scene=res://scenes/ui/main_menu.tscn [--seconds=60]
## Друкує середній і мінімальний FPS (мінімум — по секундних вікнах), середній і 99-й перцентиль
## часу кадру. Ціль: середній 60, мінімум ≥ 50 на рендерері Mobile на середньому Android.
## УВАГА: на ПК числа показують лише відносне навантаження — на телефоні міряти окремо.

var _times: PackedFloat32Array = []
var _scene_path: String = "res://scenes/ui/main_menu.tscn"
var _seconds: float = 60.0
var _elapsed: float = 0.0
var _warmup: float = 2.0


func _initialize() -> void:
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--scene="):
			_scene_path = arg.trim_prefix("--scene=")
		elif arg.begins_with("--seconds="):
			_seconds = arg.trim_prefix("--seconds=").to_float()
	var router: Node = root.get_node_or_null("Router")
	if router != null:
		router.set("auto_navigate", false)
	root.add_child((load(_scene_path) as PackedScene).instantiate())
	Engine.max_fps = 0
	process_frame.connect(_on_frame)


func _on_frame() -> void:
	var dt: float = get_root().get_process_delta_time()
	if _warmup > 0.0:
		_warmup -= dt
		return
	_times.append(dt)
	_elapsed += dt
	if _elapsed >= _seconds:
		_report()
		quit()


func _report() -> void:
	var total: float = 0.0
	for t: float in _times:
		total += t
	var sorted: PackedFloat32Array = _times.duplicate()
	sorted.sort()
	var p99: float = sorted[int(sorted.size() * 0.99)]
	# Мінімальний FPS по вікнах в 1 секунду.
	var min_fps: float = INF
	var acc: float = 0.0
	var frames: int = 0
	for t: float in _times:
		acc += t
		frames += 1
		if acc >= 1.0:
			min_fps = minf(min_fps, frames / acc)
			acc = 0.0
			frames = 0
	print("[bench] %s, %.0f с, кадрів %d, рендер %s / %s" % [_scene_path, _elapsed, _times.size(),
		RenderingServer.get_current_rendering_method(), RenderingServer.get_video_adapter_name()])
	print("[bench] FPS середній %.1f, мінімальний (1-с вікна) %.1f; кадр середній %.2f мс, p99 %.2f мс" % [
		_times.size() / total, min_fps, total / _times.size() * 1000.0, p99 * 1000.0])
