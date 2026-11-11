extends Control
## Лобі: код кімнати, список банди, вибір ролі, старт (тільки хост).
## Ролі з пізніших етапів показані заблокованими з номером етапу.

@onready var _code_label: Label = %CodeLabel
@onready var _ip_label: Label = %IpLabel
@onready var _players_box: VBoxContainer = %PlayersBox
@onready var _roles_box: VBoxContainer = %RolesBox
@onready var _start_button: Button = %StartButton
@onready var _leave_button: Button = %LeaveButton
@onready var _hint_label: Label = %HintLabel

var _role_buttons: Dictionary = {} # role -> Button


func _ready() -> void:
	_leave_button.pressed.connect(GameState.leave_room)
	_start_button.pressed.connect(GameState.start_heist)
	GameState.players_changed.connect(_refresh)
	GameState.role_changed.connect(func(_role: int) -> void: _refresh())
	_build_role_buttons()
	if Network.is_host():
		var ips: PackedStringArray = Network.get_local_ips()
		_ip_label.text = "IP: %s" % (", ".join(ips) if not ips.is_empty() else "немає Wi-Fi")
	else:
		_ip_label.text = ""
	_start_button.visible = Network.is_host()
	_refresh()


func _build_role_buttons() -> void:
	for role: int in Roles.ALL:
		var button := Button.new()
		button.toggle_mode = true
		button.custom_minimum_size = Vector2(0, 30)
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		button.add_theme_color_override("font_color", Roles.color(role))
		button.add_theme_color_override("font_pressed_color", Palette.NIGHT)
		button.add_theme_color_override("font_hover_pressed_color", Palette.NIGHT)
		var pressed_box := StyleBoxFlat.new()
		pressed_box.bg_color = Roles.color(role)
		pressed_box.set_content_margin_all(8.0)
		pressed_box.content_margin_left = 10.0
		pressed_box.anti_aliasing = false
		button.add_theme_stylebox_override("pressed", pressed_box)
		button.add_theme_stylebox_override("hover_pressed", pressed_box)
		button.pressed.connect(_on_role_pressed.bind(role))
		_roles_box.add_child(button)
		_role_buttons[role] = button


func _on_role_pressed(role: int) -> void:
	# Повторне натискання своєї ролі — відмовитися від неї.
	GameState.request_pick_role(Roles.Id.NONE if GameState.my_role == role else role)


func _refresh() -> void:
	_code_label.text = GameState.room_code if not GameState.room_code.is_empty() else "····"
	_refresh_players()
	for role: int in _role_buttons:
		var button: Button = _role_buttons[role]
		var taken: bool = GameState.is_role_taken(role)
		var available: bool = Roles.is_available(role)
		button.set_pressed_no_signal(GameState.my_role == role)
		button.disabled = not available or taken
		if not available:
			button.text = "%s  · етап %d" % [Roles.display_name(role).to_upper(), Roles.unlock_stage(role)]
		elif taken:
			button.text = "%s  · зайнято" % Roles.display_name(role).to_upper()
		else:
			button.text = "%s  · %s" % [Roles.display_name(role).to_upper(), Roles.hint(role)]
	if Network.is_host():
		_start_button.disabled = not GameState.can_start()
		_hint_label.text = "Почати можна, коли кожен обрав роль" if _start_button.disabled else "Банда готова"
	else:
		_hint_label.text = "Хост почне справу, коли всі оберуть ролі"


func _refresh_players() -> void:
	for child: Node in _players_box.get_children():
		child.queue_free()
	for player: Dictionary in GameState.lobby_players:
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 6)
		var dot := ColorRect.new()
		dot.custom_minimum_size = Vector2(6, 6)
		dot.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		dot.color = Roles.color(int(player["role"])) if bool(player["connected"]) else Palette.SLATE
		row.add_child(dot)
		var name_label := Label.new()
		var suffix: String = ""
		if bool(player["host"]):
			suffix += " ★"
		if bool(player["me"]):
			suffix += " (ти)"
		name_label.text = str(player["name"]) + suffix
		name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		name_label.clip_text = true
		row.add_child(name_label)
		var role_label := Label.new()
		if not bool(player["connected"]):
			role_label.text = "немає зв'язку"
			role_label.add_theme_color_override("font_color", Palette.DANGER)
		else:
			role_label.text = Roles.display_name(int(player["role"])) if int(player["role"]) != Roles.Id.NONE else "обирає…"
			role_label.add_theme_color_override("font_color", Roles.color(int(player["role"])))
		row.add_child(role_label)
		_players_box.add_child(row)
