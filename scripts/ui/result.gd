extends Control
## Підсумок справи.
## ЗАГЛУШКА: газетна шпальта з трюками слави — етап 3 (TZ). Зараз — сухі цифри.

@onready var _title: Label = %Title
@onready var _reason: Label = %Reason
@onready var _stats: Label = %Stats
@onready var _again_button: Button = %AgainButton
@onready var _leave_button: Button = %LeaveButton
@onready var _wait_label: Label = %WaitLabel


func _ready() -> void:
	_again_button.pressed.connect(GameState.back_to_lobby)
	_leave_button.pressed.connect(GameState.leave_room)
	_again_button.visible = Network.is_host()
	_wait_label.visible = not Network.is_host()
	GameState.result_received.connect(_show)
	_show(GameState.last_result)


func _show(summary: Dictionary) -> void:
	var outcome: int = int(summary.get("outcome", HeistSim.Outcome.NONE))
	match outcome:
		HeistSim.Outcome.SUCCESS:
			_title.text = "СПРАВУ ЗРОБЛЕНО"
			_title.add_theme_color_override("font_color", Palette.SAFE)
			_reason.text = "Здобич винесли"
		HeistSim.Outcome.CAUGHT:
			_title.text = "ПРОВАЛ"
			_title.add_theme_color_override("font_color", Palette.DANGER)
			_reason.text = "Тривога 100% — спрацювала сигналізація"
		HeistSim.Outcome.TIMEOUT:
			_title.text = "ПРОВАЛ"
			_title.add_theme_color_override("font_color", Palette.DANGER)
			_reason.text = "Прийшла ранкова зміна"
		_:
			_title.text = "…"
			_reason.text = ""
	var seconds: int = int(summary.get("time_used", 0))
	_stats.text = "Час: %d:%02d\nМакс. тривога: %d%%\nПомилок на сейфі: %d" % [
		seconds / 60, seconds % 60, int(summary.get("max_alarm", 0)), int(summary.get("safe_mistakes", 0))]
