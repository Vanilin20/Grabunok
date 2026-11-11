extends Control
## Екран справи: показує екран своєї ролі й спільний HUD поверх.
## Сам нічого не рахує — ролі слухають GameState, а GameState отримує стан від хоста.

const ROLE_SCENES: Dictionary = {
	Roles.Id.HACKER: "res://scenes/roles/role_hacker.tscn",
	Roles.Id.SHADOW: "res://scenes/roles/role_shadow.tscn",
	Roles.Id.SAFECRACKER: "res://scenes/roles/role_safecracker.tscn",
}
const WAITING_SCENE: String = "res://scenes/roles/role_waiting.tscn"

@onready var _role_slot: Control = %RoleSlot
@onready var _hud: Control = %Hud

var _shown_role: int = -2


func _ready() -> void:
	GameState.role_changed.connect(_show_role)
	_show_role(GameState.my_role)


func _show_role(role: int) -> void:
	if role == _shown_role:
		return
	_shown_role = role
	for child: Node in _role_slot.get_children():
		child.queue_free()
	var path: String = ROLE_SCENES.get(role, WAITING_SCENE)
	var screen: Node = (load(path) as PackedScene).instantiate()
	_role_slot.add_child(screen)
	_hud.visible = ROLE_SCENES.has(role)
