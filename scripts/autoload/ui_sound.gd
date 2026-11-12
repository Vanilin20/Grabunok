extends Node
## Звуки інтерфейсу для всіх кнопок гри: клац на натискання, глухий «тук» на вимкнену кнопку.
## Кнопки підхоплюються автоматично, щойно з'являються в дереві сцен.

# Не preload: автозавантаження компілюється до першого імпорту, і на свіжому клоні
# preload ще не імпортованого .wav дав би ERROR. Вантажимо під час запуску.
const CLICK_PATH: String = "res://assets/audio/ui_click.wav"
const DENY_PATH: String = "res://assets/audio/ui_deny.wav"

var _player: AudioStreamPlayer
var _click: AudioStream
var _deny: AudioStream


func _ready() -> void:
	_click = load(CLICK_PATH)
	_deny = load(DENY_PATH)
	_player = AudioStreamPlayer.new()
	_player.bus = &"UI"
	_player.max_polyphony = 4
	add_child(_player)
	get_tree().node_added.connect(_on_node_added)


func _on_node_added(node: Node) -> void:
	var button := node as BaseButton
	if button == null:
		return
	button.pressed.connect(func() -> void: _play(_click))
	button.gui_input.connect(_on_button_input.bind(button))


func _on_button_input(event: InputEvent, button: BaseButton) -> void:
	if not button.disabled:
		return
	var tap := event as InputEventScreenTouch
	var click := event as InputEventMouseButton
	if (tap != null and tap.pressed) or (click != null and click.pressed and click.button_index == MOUSE_BUTTON_LEFT):
		_play(_deny)


func _play(stream: AudioStream) -> void:
	_player.stream = stream
	_player.play()
