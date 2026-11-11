extends Node
## Попередній перегляд диска Зломщика у вертикальному режимі (без мережі) — для рендеру й перевірки на ПК.

func _ready() -> void:
	GameState.last_static = {"combo": [12, 30, 5], "range": 40}
	var screen: Node = (load("res://scenes/roles/role_safecracker.tscn") as PackedScene).instantiate()
	add_child(screen)
	await get_tree().process_frame
	screen.call("_enter_portrait")
	get_window().size = Vector2i(1280, 720) # для запису в PNG: вікно не повертаємо, весь кадр видно з полями
	screen.set("_angle", 0.6)
	screen.set("_entered", [12] as Array[int])
