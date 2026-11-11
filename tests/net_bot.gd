extends Node
## Мережевий бот для автотесту на одному ПК: хост + 2 клієнти в окремих процесах.
## Запуск (кожен у своєму терміналі або у фоні):
##   godot --headless --path . res://tests/net_bot.tscn -- --bot=host --token=t-host --name=Хост
##   godot --headless --path . res://tests/net_bot.tscn -- --bot=shadow --token=t-shadow --name=Тінь --discover
##   godot --headless --path . res://tests/net_bot.tscn -- --bot=safe --token=t-safe --name=Сейф
## Кожен бот друкує рядки «[bot:...] PASS/FAIL ...» і завершується сам.

var _bot: String = ""
var _discover: bool = false
var _time: float = 0.0
var _phase_time: float = 0.0
var _states: int = 0
var _first_pos: Vector2 = Vector2.INF
var _last_pos: Vector2 = Vector2.INF
var _last_view: Dictionary = {}
var _last_shared: Dictionary = {}
var _static: Dictionary = {}
var _step: int = 0
var _failed: bool = false
var _reconnected_ok: bool = false


func _ready() -> void:
	Router.auto_navigate = false
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--bot="):
			_bot = arg.trim_prefix("--bot=")
		elif arg == "--discover":
			_discover = true
	GameState.heist_state_received.connect(_on_state)
	GameState.heist_static_received.connect(func(d: Dictionary) -> void: _static = d)
	GameState.phase_changed.connect(func(p: GameState.Phase) -> void: _log("фаза → %s, роль %s" % [GameState.Phase.keys()[p], Roles.display_name(GameState.my_role)]))
	GameState.notice.connect(func(t: String) -> void: _log("повідомлення: " + t))
	if _bot == "host":
		Network.host_room()
	elif _discover:
		Network.start_discovery()
	else:
		Network.join("127.0.0.1")


func _process(delta: float) -> void:
	_time += delta
	_phase_time += delta
	if _time > 30.0:
		_check(false, "тайм-аут тесту на кроці %d" % _step)
		_finish()
		return
	match _bot:
		"host":
			_host_logic()
		"shadow":
			_shadow_logic()
		"safe":
			_safe_logic()


func _host_logic() -> void:
	match _step:
		0:
			if GameState.phase == GameState.Phase.LOBBY:
				GameState.request_pick_role(Roles.Id.HACKER)
				_next()
		1:
			if GameState.lobby_players.size() == 3 and GameState.can_start():
				_log("усі обрали ролі, старт")
				GameState.start_heist()
				_next()
		2:
			if _phase_time > 1.0 and _states > 5:
				_check(_static.has("cams") and not _static.has("combo"), "Хакер має схему й не має комбінації сейфа")
				_check(_last_view.has("cams_off") and not _last_view.has("p"), "Хакер не отримує позицію Тіні")
				GameState.request_action("camera_off", {"index": 0})
				_next()
		3:
			if _phase_time > 0.5:
				var off: Array = _last_view.get("cams_off", [])
				_check(off.size() > 0 and float(off[0]) > 0.0, "камера 0 вимкнулась після дії Хакера")
				_next()
		4:
			# Чекаємо, поки Зломщик перепідключиться й відкриє сейф.
			if _phase_time > 12.0:
				var players: Array = _last_shared.get("players", [])
				_check(players.size() == 3, "у спільному стані 3 гравці")
				_check(float(_last_shared.get("alarm", 0.0)) >= 10.0, "помилка на сейфі підняла тривогу")
				_finish()


func _shadow_logic() -> void:
	match _step:
		0:
			if _discover and Network.mode == Network.Mode.OFFLINE:
				var rooms: Array[Dictionary] = Network.get_rooms()
				if not rooms.is_empty():
					_log("знайшов кімнату %s через пошук (%s)" % [rooms[0]["code"], rooms[0]["ip"]])
					_check(true, "пошук кімнати за кодом у локальній мережі")
					Network.join("127.0.0.1" if str(rooms[0]["ip"]).is_empty() else str(rooms[0]["ip"]))
				elif _time > 4.0:
					_log("пошук не бачить кімнату (на одному ПК broadcast може не ходити) — заходжу за IP")
					_discover = false
					Network.join("127.0.0.1")
			if GameState.phase == GameState.Phase.LOBBY:
				GameState.request_pick_role(Roles.Id.SHADOW)
				_next()
		1:
			if GameState.phase == GameState.Phase.HEIST and _states > 3:
				_check(not _static.has("combo") and not _static.has("cams"), "Тінь не отримує ні схему, ні комбінацію")
				_check(_last_view.has("p") and not _last_view.has("cams_off"), "Тінь отримує свою позицію, але не стан камер")
				_first_pos = _last_view["p"]
				_next()
		2:
			GameState.send_move_input(Vector2.RIGHT)
			if _phase_time > 1.0:
				GameState.send_move_input(Vector2.ZERO)
				_check(_last_pos.x > _first_pos.x + 20.0, "Тінь рухається: x %.1f → %.1f" % [_first_pos.x, _last_pos.x])
				_next()
		3:
			if _phase_time > 11.0:
				_finish()


func _safe_logic() -> void:
	match _step:
		0:
			if GameState.phase == GameState.Phase.LOBBY:
				GameState.request_pick_role(Roles.Id.SAFECRACKER)
				_next()
		1:
			if GameState.phase == GameState.Phase.HEIST and _states > 3:
				_check(_static.has("combo") and not _static.has("cams"), "Зломщик має комбінацію й не має схеми")
				GameState.request_action("safe_try", {"numbers": [99, 99, 99]})
				_next()
		2:
			if _phase_time > 0.6:
				_check(int(_last_view.get("mistakes", 0)) == 1, "хибна комбінація зарахована як помилка")
				_log("імітую обрив зв'язку")
				Network.leave() # як телефон, що втратив Wi-Fi
				_states = 0
				_next()
		3:
			if _phase_time > 1.0:
				Network.join("127.0.0.1")
				_next()
		4:
			if GameState.phase == GameState.Phase.HEIST and GameState.my_role == Roles.Id.SAFECRACKER and _states > 3:
				_check(true, "після перепідключення повернувся на роль Зломщика посеред справи")
				_check(int(_last_view.get("mistakes", 0)) == 1, "стан сейфа збережено після перепідключення")
				GameState.request_action("safe_try", {"numbers": _static.get("combo", [])})
				_next()
		5:
			if _phase_time > 0.6:
				_check(bool(_last_view.get("open", false)), "правильна комбінація відкрила сейф")
				_next()
		6:
			if _phase_time > 4.0:
				_finish()


func _on_state(shared: Dictionary, view: Dictionary) -> void:
	_states += 1
	_last_view = view
	_last_shared = shared
	if view.has("p"):
		_last_pos = view["p"]


func _next() -> void:
	_step += 1
	_phase_time = 0.0


func _check(ok: bool, what: String) -> void:
	if not ok:
		_failed = true
	_log(("PASS " if ok else "FAIL ") + what)


func _log(text: String) -> void:
	print("[bot:%s] %s" % [_bot, text])


func _finish() -> void:
	_log("ЗАВЕРШЕНО: %s" % ("FAIL" if _failed else "OK"))
	set_process(false)
	get_tree().quit(1 if _failed else 0)
