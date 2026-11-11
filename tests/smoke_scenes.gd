extends Node
## Димовий тест: завантажує й запускає кожну сцену кілька кадрів (без мережі).
## Запуск: godot --headless --path . res://tests/smoke_scenes.tscn

const SCENES: Array[String] = [
	"res://scenes/ui/main_menu.tscn",
	"res://scenes/ui/join_room.tscn",
	"res://scenes/ui/lobby.tscn",
	"res://scenes/ui/sensor_test.tscn",
	"res://scenes/ui/result.tscn",
	"res://scenes/main/heist.tscn",
	"res://scenes/roles/role_hacker.tscn",
	"res://scenes/roles/role_shadow.tscn",
	"res://scenes/roles/role_safecracker.tscn",
	"res://scenes/roles/role_waiting.tscn",
]


func _ready() -> void:
	var failed: int = 0
	for path: String in SCENES:
		var packed := load(path) as PackedScene
		if packed == null:
			push_error("[smoke] не завантажилась: %s" % path)
			failed += 1
			continue
		var node: Node = packed.instantiate()
		add_child(node)
		for i: int in 5:
			await get_tree().process_frame
		node.queue_free()
		await get_tree().process_frame
		print("[smoke] ok: %s" % path)
	print("[smoke] ГОТОВО, помилок завантаження: %d" % failed)
	get_tree().quit(1 if failed > 0 else 0)
