extends Node
## Глобальний стан партії: гравці, ролі, фаза гри.
##
## На хості цей вузол авторитетний: тримає всіх гравців і симуляцію справи (HeistSim)
## та розсилає КОЖНОМУ гравцю тільки те, що бачить його роль.
## На клієнтах — лише копія того, що хост дозволив бачити; клієнт надсилає тільки дії.
##
## Гравець ідентифікується токеном (випадковий рядок, зберігається на телефоні),
## тож після обриву зв'язку він повертається на свою роль, навіть з новим peer_id.

signal players_changed
signal phase_changed(phase: Phase)
signal role_changed(role: int)
signal heist_static_received(data: Dictionary)
signal heist_state_received(shared: Dictionary, view: Dictionary)
signal result_received(summary: Dictionary)
signal notice(text: String)
## Хост відмовив у вході (кімната повна тощо).
signal rejected(reason: String)

enum Phase { MENU, LOBBY, HEIST, RESULT }

const SETTINGS_PATH: String = "user://settings.cfg"
const MAX_PLAYERS: int = 5
## Етап 1: дозволяємо старт з будь-якою кількістю гравців, щоб тестувати з 1–2 телефонами.
## У MVP мінімум буде 3 (TZ: «MVP — одне пограбування магазину на трьох гравців»).
const MIN_PLAYERS_TO_START: int = 1
const STATE_SEND_RATE: float = 15.0 # TZ: хост оновлює стан 10–20 разів на секунду

var phase: Phase = Phase.MENU
var my_name: String = ""
var my_token: String = ""
var my_role: int = Roles.Id.NONE
## Копія списку гравців для інтерфейсу: [{name, role, connected, host, me}].
var lobby_players: Array[Dictionary] = []
## Код кімнати, яку показує лобі (клієнт дізнається його від хоста).
var room_code: String = ""
var last_static: Dictionary = {}
var last_result: Dictionary = {}

# --- Тільки на хості ---
var _players: Dictionary = {} # token -> {name, role, peer_id, connected}
var _sim: HeistSim = null
var _send_accum: float = 0.0
var _test_override: bool = false


func _ready() -> void:
	_load_settings()
	Network.hosting_started.connect(_on_hosting_started)
	Network.joined_room.connect(_on_joined_room)
	Network.reconnected.connect(_on_reconnected)
	Network.connection_lost.connect(_on_connection_lost)
	Network.peer_left.connect(_on_peer_left)


func _process(delta: float) -> void:
	if not Network.is_host() or phase != Phase.HEIST or _sim == null:
		return
	_sim.tick(delta)
	if _sim.is_finished():
		_finish_heist()
		return
	_send_accum += delta
	if _send_accum >= 1.0 / STATE_SEND_RATE:
		_send_accum = 0.0
		_broadcast_heist_state()


# --- Налаштування гравця --------------------------------------------------

func set_my_name(value: String) -> void:
	my_name = value.strip_edges().left(14)
	_save_settings()


func _load_settings() -> void:
	var config := ConfigFile.new()
	config.load(SETTINGS_PATH) # якщо файлу ще нема — лишаються значення за замовчуванням
	my_name = str(config.get_value("player", "name", ""))
	my_token = str(config.get_value("player", "token", ""))
	if my_token.is_empty():
		my_token = "%08x%08x" % [randi(), randi()]
		_save_settings()
	# Для тестів кількох копій гри на одному ПК: godot -- --name=Оля --token=test2
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--name="):
			my_name = arg.trim_prefix("--name=")
			_test_override = true
		elif arg.begins_with("--token="):
			my_token = arg.trim_prefix("--token=")
			_test_override = true


func _save_settings() -> void:
	if _test_override:
		return # тестові ім'я/токен з командного рядка не перезаписують справжні
	var config := ConfigFile.new()
	config.set_value("player", "name", my_name)
	config.set_value("player", "token", my_token)
	var err: Error = config.save(SETTINGS_PATH)
	if err != OK:
		push_warning("[GameState] Не вдалося зберегти налаштування (помилка %d)" % err)


func display_name() -> String:
	return my_name if not my_name.is_empty() else "Гравець"


# --- Публічні дії (викликає інтерфейс) ------------------------------------

func request_pick_role(role: int) -> void:
	_send_to_host("_rpc_pick_role", [role])


func request_action(action: String, args: Dictionary = {}) -> void:
	_send_to_host("_rpc_action", [action, args])


## Вектор руху Тіні — часто й без гарантії доставки (втрата одного пакета не страшна).
func send_move_input(direction: Vector2) -> void:
	if Network.is_host():
		_rpc_move_input(direction)
	elif _is_connected_client():
		_rpc_move_input.rpc_id(1, direction)


## Тільки хост: почати справу.
func start_heist() -> void:
	if not Network.is_host() or not can_start():
		return
	_sim = HeistSim.new()
	_send_accum = 0.0
	phase = Phase.HEIST
	_beacon_update()
	for token: String in _players:
		_send_phase_to(token)
	_broadcast_heist_state()
	print("[GameState] Справу розпочато, гравців: %d" % _players.size())


## Тільки хост: повернути всіх у лобі.
func back_to_lobby() -> void:
	if not Network.is_host():
		return
	_sim = null
	phase = Phase.LOBBY
	# Гравці, що вийшли під час справи, звільняють місця.
	for token: String in _players.keys():
		if not _players[token]["connected"]:
			_players.erase(token)
	_beacon_update()
	for token: String in _players:
		_send_phase_to(token)
	_broadcast_lobby()


func can_start() -> bool:
	if phase != Phase.LOBBY:
		return false
	var count: int = 0
	for token: String in _players:
		var p: Dictionary = _players[token]
		if not p["connected"]:
			continue
		if not Roles.is_available(p["role"]):
			return false
		count += 1
	return count >= MIN_PLAYERS_TO_START


## Вийти в головне меню (закрити кімнату або від'єднатися).
func leave_room() -> void:
	Network.leave()
	_reset_local()
	phase = Phase.MENU
	phase_changed.emit(phase)


func is_role_taken(role: int) -> bool:
	for p: Dictionary in lobby_players:
		if int(p["role"]) == role and not bool(p["me"]):
			return true
	return false


# --- Обробка мережевих подій ----------------------------------------------

func _on_hosting_started(_code: String) -> void:
	_reset_local()
	_players[my_token] = {"name": display_name(), "role": Roles.Id.NONE, "peer_id": 1, "connected": true}
	phase = Phase.LOBBY
	_beacon_update()
	_broadcast_lobby()
	phase_changed.emit(phase)


func _on_joined_room() -> void:
	_reset_local()
	_rpc_register.rpc_id(1, display_name(), my_token)


func _on_reconnected() -> void:
	# Хост упізнає нас за токеном і поверне на ту саму роль.
	_rpc_register.rpc_id(1, display_name(), my_token)
	notice.emit("Зв'язок відновлено")


func _on_connection_lost(reason: String) -> void:
	_reset_local()
	phase = Phase.MENU
	phase_changed.emit(phase)
	notice.emit(reason)


func _on_peer_left(peer_id: int) -> void:
	var token: String = _token_by_peer(peer_id)
	if token.is_empty():
		return
	var player: Dictionary = _players[token]
	if phase == Phase.LOBBY:
		_players.erase(token) # у лобі місце звільняється одразу
	else:
		player["connected"] = false # під час справи роль чекає на повернення
	_broadcast_notice("%s — зв'язок втрачено" % player["name"])
	_beacon_update()
	_broadcast_lobby()


func _reset_local() -> void:
	_players.clear()
	_sim = null
	lobby_players.clear()
	room_code = ""
	last_static = {}
	last_result = {}
	my_role = Roles.Id.NONE


# --- RPC: клієнт -> хост --------------------------------------------------

@rpc("any_peer", "call_remote", "reliable")
func _rpc_register(player_name: String, token: String) -> void:
	if not Network.is_host():
		return
	var peer_id: int = _sender_id()
	player_name = player_name.strip_edges().left(14)
	if player_name.is_empty():
		player_name = "Гравець"
	if token == my_token:
		_reject(peer_id, "Цей телефон уже в кімнаті як хост")
		return
	if _players.has(token):
		# Повернення після обриву: та сама роль, новий peer_id.
		var player: Dictionary = _players[token]
		var old_peer: int = int(player["peer_id"])
		if player["connected"] and old_peer != peer_id:
			Network.kick(old_peer) # старе з'єднання ще не встигло відпасти — замінюємо новим
		player["peer_id"] = peer_id
		player["connected"] = true
		player["name"] = player_name
		_broadcast_notice("%s знову в грі" % player_name)
	else:
		if _connected_count() >= MAX_PLAYERS:
			_reject(peer_id, "Кімната повна (%d гравців)" % MAX_PLAYERS)
			return
		# Пізнє підключення під час справи: гравець чекає наступної справи без ролі.
		_players[token] = {"name": player_name, "role": Roles.Id.NONE, "peer_id": peer_id, "connected": true}
		if phase != Phase.LOBBY:
			_broadcast_notice("%s приєднався і чекає наступної справи" % player_name)
	_beacon_update()
	_send_phase_to(token)
	_broadcast_lobby()
	if phase == Phase.HEIST:
		_send_heist_state_to(token)


@rpc("any_peer", "call_remote", "reliable")
func _rpc_pick_role(role: int) -> void:
	if not Network.is_host() or phase != Phase.LOBBY:
		return
	var token: String = _token_by_peer(_sender_id())
	if token.is_empty():
		return
	if role != Roles.Id.NONE:
		if not Roles.is_available(role):
			return
		for other: String in _players:
			if other != token and int(_players[other]["role"]) == role:
				return # роль уже зайнята — список лобі в клієнта оновиться сам
	_players[token]["role"] = role
	_send_phase_to(token)
	_broadcast_lobby()


@rpc("any_peer", "call_remote", "reliable")
func _rpc_action(action: String, args: Dictionary) -> void:
	if not Network.is_host() or phase != Phase.HEIST or _sim == null:
		return
	var role: int = _role_of_peer(_sender_id())
	if role == Roles.Id.NONE:
		return
	_sim.apply_action(role, action, args)


@rpc("any_peer", "call_remote", "unreliable_ordered")
func _rpc_move_input(direction: Vector2) -> void:
	if not Network.is_host() or phase != Phase.HEIST or _sim == null:
		return
	if _role_of_peer(_sender_id()) != Roles.Id.SHADOW:
		return # рухати Тінь може тільки Тінь
	_sim.set_shadow_input(direction)


# --- RPC: хост -> клієнт --------------------------------------------------

@rpc("authority", "call_remote", "reliable")
func _rpc_lobby(players: Array, code: String) -> void:
	lobby_players.assign(players)
	room_code = code
	players_changed.emit()


@rpc("authority", "call_remote", "reliable")
func _rpc_phase(new_phase: int, role: int, static_data: Dictionary) -> void:
	var role_was: int = my_role
	my_role = role
	phase = new_phase as Phase
	if phase == Phase.HEIST:
		last_static = static_data
		heist_static_received.emit(static_data)
	if role_was != my_role:
		role_changed.emit(my_role)
	# Завжди: на хості фаза вже змінена до цього виклику, а екран треба оновити.
	phase_changed.emit(phase)


@rpc("authority", "call_remote", "unreliable_ordered")
func _rpc_heist_state(shared: Dictionary, view: Dictionary) -> void:
	if phase == Phase.HEIST:
		heist_state_received.emit(shared, view)


@rpc("authority", "call_remote", "reliable")
func _rpc_result(summary: Dictionary) -> void:
	last_result = summary
	phase = Phase.RESULT
	result_received.emit(summary)
	phase_changed.emit(phase)


@rpc("authority", "call_remote", "reliable")
func _rpc_notice(text: String) -> void:
	notice.emit(text)


@rpc("authority", "call_remote", "reliable")
func _rpc_rejected(reason: String) -> void:
	rejected.emit(reason)


# --- Розсилка з хоста -----------------------------------------------------

func _broadcast_lobby() -> void:
	for token: String in _players:
		var list: Array[Dictionary] = []
		for other: String in _players:
			var p: Dictionary = _players[other]
			list.append({
				"name": p["name"], "role": p["role"], "connected": p["connected"],
				"host": int(p["peer_id"]) == 1 and other == my_token, "me": other == token,
			})
		_deliver(token, "_rpc_lobby", [list, Network.room_code])


func _send_phase_to(token: String) -> void:
	var role: int = int(_players[token]["role"])
	var static_data: Dictionary = {}
	if phase == Phase.HEIST and _sim != null:
		static_data = _sim.static_view_for(role)
	_deliver(token, "_rpc_phase", [phase, role, static_data])
	if phase == Phase.RESULT:
		_deliver(token, "_rpc_result", [last_result])


func _broadcast_heist_state() -> void:
	var shared: Dictionary = _shared_state()
	for token: String in _players:
		if _players[token]["connected"]:
			var role: int = int(_players[token]["role"])
			_deliver(token, "_rpc_heist_state", [shared, _sim.view_for(role)])


func _send_heist_state_to(token: String) -> void:
	if _sim == null:
		return
	_deliver(token, "_rpc_heist_state", [_shared_state(), _sim.view_for(int(_players[token]["role"]))])


## Спільне для всіх: тривога, таймер, стан кожного гравця (TZ: «Спільні елементи на кожному екрані»).
func _shared_state() -> Dictionary:
	var statuses: Array[Dictionary] = []
	for token: String in _players:
		var p: Dictionary = _players[token]
		var role: int = int(p["role"])
		if role == Roles.Id.NONE:
			continue
		var status: String = _sim.status_for(role) if p["connected"] else "offline"
		statuses.append({"name": p["name"], "role": role, "status": status})
	return {"alarm": _sim.alarm, "time_left": _sim.time_left, "players": statuses}


func _finish_heist() -> void:
	last_result = _sim.result_summary()
	_sim = null
	phase = Phase.RESULT
	_beacon_update()
	for token: String in _players:
		_deliver(token, "_rpc_result", [last_result])
	print("[GameState] Справу завершено: %s" % str(last_result))


func _broadcast_notice(text: String) -> void:
	for token: String in _players:
		if _players[token]["connected"]:
			_deliver(token, "_rpc_notice", [text])


func _reject(peer_id: int, reason: String) -> void:
	_rpc_rejected.rpc_id(peer_id, reason)
	# Даємо повідомленню дійти, потім від'єднуємо.
	get_tree().create_timer(0.5).timeout.connect(Network.kick.bind(peer_id))


func _beacon_update() -> void:
	Network.beacon_info = {"host": display_name(), "players": _connected_count(), "open": _connected_count() < MAX_PLAYERS}


# --- Допоміжне ------------------------------------------------------------

## Доставити виклик гравцю: хосту — напряму, клієнту — через RPC.
func _deliver(token: String, method: String, args: Array) -> void:
	var player: Dictionary = _players[token]
	if not player["connected"]:
		return
	var peer_id: int = int(player["peer_id"])
	if token == my_token:
		callv(method, args)
	else:
		callv("rpc_id", [peer_id, method] + args)


func _send_to_host(method: String, args: Array) -> void:
	if Network.is_host():
		callv(method, args)
	elif _is_connected_client():
		callv("rpc_id", [1, method] + args)


func _is_connected_client() -> bool:
	return Network.mode == Network.Mode.CLIENT and multiplayer.multiplayer_peer != null \
		and multiplayer.multiplayer_peer.get_connection_status() == MultiplayerPeer.CONNECTION_CONNECTED


## Хто надіслав RPC. Локальний виклик на хості дає 0 — це сам хост.
func _sender_id() -> int:
	var id: int = multiplayer.get_remote_sender_id()
	return id if id != 0 else 1


func _token_by_peer(peer_id: int) -> String:
	for token: String in _players:
		if int(_players[token]["peer_id"]) == peer_id and _players[token]["connected"]:
			return token
	return ""


func _role_of_peer(peer_id: int) -> int:
	var token: String = _token_by_peer(peer_id)
	return int(_players[token]["role"]) if not token.is_empty() else Roles.Id.NONE


func _connected_count() -> int:
	var count: int = 0
	for token: String in _players:
		if _players[token]["connected"]:
			count += 1
	return count
