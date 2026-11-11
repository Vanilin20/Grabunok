extends Node
## Попередній перегляд екрана ролі без інших телефонів: хост сам собі, одразу старт справи.
## Для рендеру в PNG і швидкої перевірки на ПК:
##   godot --path . res://tests/preview_role.tscn -- --role=1   (0 Хакер, 1 Тінь, 2 Зломщик)
## Опційно --lobby: зупинитися в лобі, не починаючи справу.

func _ready() -> void:
	var role: int = Roles.Id.HACKER
	var stay_in_lobby: bool = false
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--role="):
			role = arg.trim_prefix("--role=").to_int()
		elif arg == "--lobby":
			stay_in_lobby = true
	if GameState.my_name.is_empty():
		GameState.my_name = "Іван"
	Network.host_room()
	if stay_in_lobby:
		return
	GameState.request_pick_role(role)
	GameState.start_heist()
