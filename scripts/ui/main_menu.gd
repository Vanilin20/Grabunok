extends Control
## Головне меню: ім'я гравця, створити кімнату, приєднатися, тест датчиків.

@onready var _name_edit: LineEdit = %NameEdit
@onready var _host_button: Button = %HostButton
@onready var _join_button: Button = %JoinButton
@onready var _sensors_button: Button = %SensorsButton
@onready var _version_label: Label = %VersionLabel


func _ready() -> void:
	_name_edit.text = GameState.my_name
	_name_edit.text_changed.connect(_on_name_changed)
	_host_button.pressed.connect(_on_host_pressed)
	_join_button.pressed.connect(_on_join_pressed)
	_sensors_button.pressed.connect(Router.show_sensor_test)
	Network.hosting_failed.connect(Router.toast)
	var version: String = ProjectSettings.get_setting("application/config/version", "0.0.0")
	_version_label.text = "v%s · етап 1" % version
	_update_buttons()


func _on_name_changed(_text: String) -> void:
	_update_buttons()


func _update_buttons() -> void:
	var has_name: bool = not _name_edit.text.strip_edges().is_empty()
	_host_button.disabled = not has_name
	_join_button.disabled = not has_name


func _commit_name() -> void:
	GameState.set_my_name(_name_edit.text)


func _on_host_pressed() -> void:
	_commit_name()
	Network.host_room() # далі GameState переведе в лобі, а Router покаже екран лобі


func _on_join_pressed() -> void:
	_commit_name()
	Router.show_join()
