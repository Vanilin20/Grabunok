extends Control
## Вхід у кімнату: код із 4 цифр (кімнату знаходимо пошуком у локальній мережі)
## або запасний шлях — IP хоста вручну, якщо мережа блокує пошук.

const ROOM_BUTTON_MIN_HEIGHT: float = 28.0

@onready var _code_edit: LineEdit = %CodeEdit
@onready var _status_label: Label = %StatusLabel
@onready var _rooms_box: VBoxContainer = %RoomsBox
@onready var _ip_edit: LineEdit = %IpEdit
@onready var _ip_button: Button = %IpButton
@onready var _back_button: Button = %BackButton

var _joining: bool = false


func _ready() -> void:
	_code_edit.text_changed.connect(_on_code_changed)
	_ip_button.pressed.connect(_on_ip_pressed)
	_back_button.pressed.connect(_on_back_pressed)
	Network.rooms_changed.connect(_on_rooms_changed)
	Network.join_failed.connect(_on_join_failed)
	Network.joined_room.connect(_on_joined)
	Network.start_discovery()
	_on_rooms_changed(Network.get_rooms())
	_code_edit.grab_focus()


func _exit_tree() -> void:
	Network.stop_discovery()


func _on_code_changed(text: String) -> void:
	var digits: String = ""
	for ch: String in text:
		if ch >= "0" and ch <= "9":
			digits += ch
	if digits != text:
		_code_edit.text = digits
		_code_edit.caret_column = digits.length()
	_try_join_by_code()


func _try_join_by_code() -> void:
	var code: String = _code_edit.text
	if code.length() < 4 or _joining:
		_set_status("Введи код з екрана хоста" if code.is_empty() else "")
		return
	var ip: String = Network.find_room_ip(code)
	if ip.is_empty():
		_set_status("Кімнату %s не видно. Ви в одній Wi-Fi?" % code, Palette.LAMP)
		return
	_join(ip, "Підключаюся до кімнати %s…" % code)


func _on_rooms_changed(rooms: Array[Dictionary]) -> void:
	for child: Node in _rooms_box.get_children():
		child.queue_free()
	if rooms.is_empty():
		var hint := Label.new()
		hint.text = "шукаю кімнати в мережі…"
		hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		hint.add_theme_color_override("font_color", Palette.MUTED)
		_rooms_box.add_child(hint)
	for room: Dictionary in rooms:
		var button := Button.new()
		button.custom_minimum_size.y = ROOM_BUTTON_MIN_HEIGHT
		button.text = "%s  ·  %s  ·  %d/%d" % [room["code"], room["host"], room["players"], GameState.MAX_PLAYERS]
		button.disabled = not bool(room["open"])
		button.pressed.connect(_join.bind(str(room["ip"]), "Підключаюся до кімнати %s…" % room["code"]))
		_rooms_box.add_child(button)
	# Код уже введено, а кімната з'явилася пізніше — заходимо.
	if _code_edit.text.length() == 4:
		_try_join_by_code()


func _on_ip_pressed() -> void:
	_join(_ip_edit.text.strip_edges(), "Підключаюся до %s…" % _ip_edit.text.strip_edges())


func _join(ip: String, status: String) -> void:
	if _joining:
		return
	_joining = true
	_set_status(status, Palette.TEXT)
	Network.join(ip)


func _on_join_failed(reason: String) -> void:
	_joining = false
	_code_edit.text = "" # інакше пошук знову побачить кімнату й піде на нову спробу по колу
	_set_status(reason, Palette.DANGER)
	Network.start_discovery() # join() зупиняє пошук — відновлюємо


func _on_joined() -> void:
	_set_status("Підключено, чекаю відповіді хоста…", Palette.SAFE)


func _on_back_pressed() -> void:
	Network.leave()
	Router.show_menu()


func _set_status(text: String, color: Color = Palette.MUTED) -> void:
	_status_label.text = text
	_status_label.add_theme_color_override("font_color", color)
