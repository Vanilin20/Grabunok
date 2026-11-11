extends Control
## Замір FPS меню на самому телефоні (рендерер Mobile): 60 с з усіма ефектами,
## потім середній і мінімальний FPS та час кадру. Відкривається з «тесту датчиків».

const DURATION: float = 60.0
const WARMUP: float = 2.0

@onready var _stats: Label = %Stats
@onready var _back: Button = %BackButton

var _times: PackedFloat32Array = []
var _elapsed: float = 0.0
var _warmup: float = WARMUP
var _done: bool = false


func _ready() -> void:
	_back.pressed.connect(Router.show_sensor_test)
	Engine.max_fps = 0


func _exit_tree() -> void:
	Engine.max_fps = 0


func _process(delta: float) -> void:
	if _done:
		return
	if _warmup > 0.0:
		_warmup -= delta
		_stats.text = "замір почнеться за %d с" % ceili(_warmup)
		return
	_times.append(delta)
	_elapsed += delta
	_stats.text = "%d FPS · %d с з %d" % [Engine.get_frames_per_second(), int(_elapsed), int(DURATION)]
	if _elapsed >= DURATION:
		_done = true
		_report()


func _report() -> void:
	var total: float = 0.0
	for t: float in _times:
		total += t
	var sorted: PackedFloat32Array = _times.duplicate()
	sorted.sort()
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
	_stats.text = "FPS середній %.0f · мінімум %.0f\nкадр %.1f мс · p99 %.1f мс\n%s" % [
		_times.size() / total, min_fps, total / _times.size() * 1000.0,
		sorted[int(sorted.size() * 0.99)] * 1000.0, RenderingServer.get_current_rendering_method()]
	print("[bench] " + _stats.text.replace("\n", " | "))
