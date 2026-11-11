extends Control
## Стартовий екран. Зараз лише підтверджує, що проєкт запускається.
## Головне меню й лобі з'являться на етапі 1 (технічний прототип).

@onready var _version_label: Label = %VersionLabel


func _ready() -> void:
	var version: String = ProjectSettings.get_setting("application/config/version", "0.0.0")
	_version_label.text = "v%s · Godot %s" % [version, Engine.get_version_info().string]
	print("[Boot] Проєкт запущено: %s" % _version_label.text)
