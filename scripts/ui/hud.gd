extends Control
## Спільні елементи на кожному екрані справи (TZ, «Інтерфейс і керування»):
## шкала тривоги вгорі, таймер і короткий стан кожного гравця.

const CHIP_SIZE: Vector2 = Vector2(30, 14)
const STATUS_COLORS: Dictionary = {
	"busy": Color("5cf08a"),
	"waiting": Color("736e8c"),
	"danger": Color("ff4a3d"),
	"offline": Color("2b2740"),
}

@onready var _alarm_bar: AlarmBar = %AlarmBar
@onready var _timer_label: Label = %TimerLabel
@onready var _chips: HBoxContainer = %Chips

var _blink: float = 0.0


func _ready() -> void:
	GameState.heist_state_received.connect(_on_state)


func _process(delta: float) -> void:
	_blink += delta
	for chip: Control in _chips.get_children():
		chip.queue_redraw()


func _on_state(shared: Dictionary, _view: Dictionary) -> void:
	_alarm_bar.value = float(shared.get("alarm", 0.0))
	var time_left: int = ceili(float(shared.get("time_left", 0.0)))
	_timer_label.text = "%d:%02d" % [time_left / 60, time_left % 60]
	_timer_label.add_theme_color_override("font_color", Palette.DANGER if time_left <= 60 else Palette.TEXT)
	var players: Array = shared.get("players", [])
	if players.size() != _chips.get_child_count():
		_rebuild_chips(players.size())
	for i: int in players.size():
		_chips.get_child(i).set_meta("player", players[i])


func _rebuild_chips(count: int) -> void:
	for child: Node in _chips.get_children():
		child.queue_free()
		_chips.remove_child(child)
	for i: int in count:
		var chip := Control.new()
		chip.custom_minimum_size = CHIP_SIZE
		chip.draw.connect(_draw_chip.bind(chip))
		_chips.add_child(chip)


## Фішка гравця: колір ролі, смужка стану знизу, перші літери імені.
func _draw_chip(chip: Control) -> void:
	if not chip.has_meta("player"):
		return
	var player: Dictionary = chip.get_meta("player")
	var role_color: Color = Roles.color(int(player["role"]))
	var status: String = str(player["status"])
	var rect := Rect2(Vector2.ZERO, chip.size)
	var offline: bool = status == "offline"
	_draw_chip_body(chip, rect, role_color, offline)
	var status_color: Color = STATUS_COLORS.get(status, Palette.MUTED)
	if status == "danger" and fmod(_blink, 0.4) < 0.2:
		status_color = Palette.TEXT
	chip.draw_rect(Rect2(1, rect.size.y - 3, rect.size.x - 2, 2), status_color)
	var font: Font = get_theme_default_font()
	var initials: String = str(player["name"]).left(3).to_upper()
	chip.draw_string(font, Vector2(2, 9), initials, HORIZONTAL_ALIGNMENT_CENTER, rect.size.x - 4, 8,
		Palette.MUTED if offline else Palette.NIGHT)


func _draw_chip_body(chip: Control, rect: Rect2, role_color: Color, offline: bool) -> void:
	chip.draw_rect(rect, Palette.SLATE if offline else role_color)
	if offline:
		chip.draw_line(Vector2(2, 2), rect.size - Vector2(2, 4), Palette.DANGER, 1.0)
