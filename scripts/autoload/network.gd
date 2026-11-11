extends Node
## Мережа: створення кімнати, пошук кімнат у локальній Wi-Fi за кодом,
## підключення, тайм-аути й автоматичне перепідключення.
## Ігрової логіки тут немає — лише з'єднання. Логіка живе в GameState (на хості).
##
## Як знаходиться кімната: хост раз на секунду шле UDP-маяк (broadcast) на порт
## DISCOVERY_PORT з кодом кімнати. Клієнт слухає маяки, бачить код і IP хоста
## й підключається через ENet на GAME_PORT. Якщо broadcast у мережі заблокований,
## є запасний шлях — ввести IP хоста вручну (хост показує його в лобі).

signal hosting_started(code: String)
signal hosting_failed(reason: String)
signal joined_room
signal join_failed(reason: String)
signal reconnecting(attempt: int, max_attempts: int)
signal reconnected
## Остаточна втрата з'єднання з хостом (після всіх спроб перепідключення).
signal connection_lost(reason: String)
signal peer_joined(peer_id: int)
signal peer_left(peer_id: int)
signal rooms_changed(rooms: Array[Dictionary])

const GAME_PORT: int = 7777
const DISCOVERY_PORT: int = 7778
const MAX_CLIENTS: int = 4 # 5 гравців разом із хостом
const BEACON_MAGIC: String = "BANDA1"
const BEACON_INTERVAL: float = 1.0
const ROOM_TTL: float = 3.5 # скільки секунд кімната лишається у списку без нового маяка
const CONNECT_TIMEOUT: float = 6.0
const RECONNECT_ATTEMPTS: int = 5
const RECONNECT_DELAY: float = 2.0
# Тайм-аути ENet (мс): обрив помічаємо за 3–8 с замість стандартних ~30 с.
const PEER_TIMEOUT_LIMIT: int = 32
const PEER_TIMEOUT_MIN_MS: int = 3000
const PEER_TIMEOUT_MAX_MS: int = 8000

enum Mode { OFFLINE, HOST, CLIENT }

var mode: Mode = Mode.OFFLINE
var room_code: String = ""
## Що хост пише в маяк (ім'я хоста, кількість гравців, чи можна зайти). Оновлює GameState.
var beacon_info: Dictionary = {"host": "", "players": 0, "open": true}

var _beacon_udp: PacketPeerUDP = null
var _beacon_timer: float = 0.0
var _listen_udp: PacketPeerUDP = null
var _rooms: Dictionary = {} # code -> {code, ip, host, players, open, seen}

var _server_ip: String = ""
var _connecting: bool = false
var _connect_elapsed: float = 0.0
var _reconnect_attempt: int = 0
var _reconnect_wait: float = -1.0
var _was_connected: bool = false


func _ready() -> void:
	multiplayer.peer_connected.connect(_on_peer_connected)
	multiplayer.peer_disconnected.connect(_on_peer_disconnected)
	multiplayer.connected_to_server.connect(_on_connected_to_server)
	multiplayer.connection_failed.connect(_on_connection_failed)
	multiplayer.server_disconnected.connect(_on_server_disconnected)


func _process(delta: float) -> void:
	_process_beacon(delta)
	_process_discovery()
	_process_connect_timeout(delta)
	_process_reconnect(delta)


# --- Хост -----------------------------------------------------------------

func host_room() -> void:
	leave()
	var peer := ENetMultiplayerPeer.new()
	var err: Error = peer.create_server(GAME_PORT, MAX_CLIENTS)
	if err != OK:
		hosting_failed.emit("Не вдалося відкрити порт %d (помилка %d). Можливо, інша гра вже запущена." % [GAME_PORT, err])
		return
	multiplayer.multiplayer_peer = peer
	mode = Mode.HOST
	room_code = _generate_code()
	_start_beacon()
	print("[Network] Кімнату створено, код %s, IP: %s" % [room_code, ", ".join(get_local_ips())])
	hosting_started.emit(room_code)


func is_host() -> bool:
	return mode == Mode.HOST


func is_online() -> bool:
	return mode != Mode.OFFLINE


## Від'єднати одного клієнта (наприклад, кімната повна).
func kick(peer_id: int) -> void:
	if mode == Mode.HOST and multiplayer.multiplayer_peer != null:
		multiplayer.multiplayer_peer.disconnect_peer(peer_id)


func _generate_code() -> String:
	# 4 цифри: на телефоні це одна цифрова клавіатура, без перемикання мов.
	return "%04d" % randi_range(1000, 9999)


func _start_beacon() -> void:
	_beacon_udp = PacketPeerUDP.new()
	_beacon_udp.set_broadcast_enabled(true)
	_beacon_timer = 0.0


func _process_beacon(delta: float) -> void:
	if _beacon_udp == null:
		return
	_beacon_timer -= delta
	if _beacon_timer > 0.0:
		return
	_beacon_timer = BEACON_INTERVAL
	var payload: PackedByteArray = JSON.stringify({
		"m": BEACON_MAGIC,
		"code": room_code,
		"host": beacon_info.get("host", ""),
		"players": beacon_info.get("players", 0),
		"open": beacon_info.get("open", true),
	}).to_utf8_buffer()
	for address: String in _broadcast_addresses():
		_beacon_udp.set_dest_address(address, DISCOVERY_PORT)
		_beacon_udp.put_packet(payload)


## Адреси для broadcast: загальна + широкомовна для кожної локальної підмережі (/24).
## Деякі Android-роутери і точки доступу ігнорують 255.255.255.255, але пропускають x.x.x.255.
func _broadcast_addresses() -> PackedStringArray:
	var result: PackedStringArray = ["255.255.255.255"]
	for ip: String in get_local_ips():
		var parts: PackedStringArray = ip.split(".")
		var subnet: String = "%s.%s.%s.255" % [parts[0], parts[1], parts[2]]
		if not result.has(subnet):
			result.append(subnet)
	return result


## Локальні IPv4-адреси пристрою в приватних мережах (Wi-Fi чи точка доступу).
func get_local_ips() -> PackedStringArray:
	var result: PackedStringArray = []
	for ip: String in IP.get_local_addresses():
		if ip.contains(":"):
			continue # IPv6 не потрібен
		if ip.begins_with("192.168.") or ip.begins_with("10.") or _is_172_private(ip):
			result.append(ip)
	return result


func _is_172_private(ip: String) -> bool:
	if not ip.begins_with("172."):
		return false
	var second: int = ip.split(".")[1].to_int()
	return second >= 16 and second <= 31


# --- Пошук кімнат ---------------------------------------------------------

func start_discovery() -> void:
	stop_discovery()
	_listen_udp = PacketPeerUDP.new()
	var err: Error = _listen_udp.bind(DISCOVERY_PORT)
	if err != OK:
		push_warning("[Network] Не вдалося слухати порт пошуку %d (помилка %d)" % [DISCOVERY_PORT, err])
		_listen_udp = null
		return
	_rooms.clear()
	rooms_changed.emit(get_rooms())


func stop_discovery() -> void:
	if _listen_udp != null:
		_listen_udp.close()
		_listen_udp = null
	_rooms.clear()


func get_rooms() -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for code: String in _rooms:
		result.append(_rooms[code])
	return result


## IP хоста для коду кімнати або "" якщо такої кімнати не видно.
func find_room_ip(code: String) -> String:
	if _rooms.has(code):
		return _rooms[code]["ip"]
	return ""


func _process_discovery() -> void:
	if _listen_udp == null:
		return
	var changed: bool = false
	var now: float = Time.get_ticks_msec() / 1000.0
	while _listen_udp.get_available_packet_count() > 0:
		var packet: PackedByteArray = _listen_udp.get_packet()
		var ip: String = _listen_udp.get_packet_ip()
		var data: Variant = JSON.parse_string(packet.get_string_from_utf8())
		if not (data is Dictionary) or data.get("m", "") != BEACON_MAGIC:
			continue
		var code: String = str(data.get("code", ""))
		if code.length() != 4:
			continue
		var room: Dictionary = {
			"code": code,
			"ip": ip,
			"host": str(data.get("host", "")),
			"players": int(data.get("players", 0)),
			"open": bool(data.get("open", true)),
			"seen": now,
		}
		if not _rooms.has(code) or _room_differs(_rooms[code], room):
			changed = true
		_rooms[code] = room
	for code: String in _rooms.keys():
		if now - float(_rooms[code]["seen"]) > ROOM_TTL:
			_rooms.erase(code)
			changed = true
	if changed:
		rooms_changed.emit(get_rooms())


func _room_differs(a: Dictionary, b: Dictionary) -> bool:
	return a["ip"] != b["ip"] or a["host"] != b["host"] or a["players"] != b["players"] or a["open"] != b["open"]


# --- Клієнт ---------------------------------------------------------------

func join(ip: String) -> void:
	leave()
	_server_ip = ip.strip_edges()
	_reconnect_attempt = 0
	_was_connected = false
	if not _server_ip.is_valid_ip_address():
		join_failed.emit("Некоректна IP-адреса: %s" % _server_ip)
		return
	_start_client()


func _start_client() -> void:
	var peer := ENetMultiplayerPeer.new()
	var err: Error = peer.create_client(_server_ip, GAME_PORT)
	if err != OK:
		_handle_connect_failure("Не вдалося створити з'єднання (помилка %d)" % err)
		return
	multiplayer.multiplayer_peer = peer
	mode = Mode.CLIENT
	_connecting = true
	_connect_elapsed = 0.0


func _process_connect_timeout(delta: float) -> void:
	if not _connecting:
		return
	_connect_elapsed += delta
	if _connect_elapsed >= CONNECT_TIMEOUT:
		_connecting = false
		_close_peer()
		_handle_connect_failure("Хост не відповідає")


func _process_reconnect(delta: float) -> void:
	if _reconnect_wait < 0.0:
		return
	_reconnect_wait -= delta
	if _reconnect_wait <= 0.0:
		_reconnect_wait = -1.0
		_start_client()


func _handle_connect_failure(reason: String) -> void:
	if _was_connected and _reconnect_attempt < RECONNECT_ATTEMPTS:
		_reconnect_attempt += 1
		mode = Mode.CLIENT
		_reconnect_wait = RECONNECT_DELAY
		reconnecting.emit(_reconnect_attempt, RECONNECT_ATTEMPTS)
		return
	var was_connected: bool = _was_connected
	_reset()
	if was_connected:
		connection_lost.emit(reason)
	else:
		join_failed.emit(reason)


func is_reconnecting() -> bool:
	return _reconnect_wait >= 0.0 or (_connecting and _reconnect_attempt > 0)


# --- Спільне --------------------------------------------------------------

## Закрити все: кімнату, підключення, пошук.
func leave() -> void:
	stop_discovery()
	_reset()


func _reset() -> void:
	_close_peer()
	_beacon_udp = null
	mode = Mode.OFFLINE
	room_code = ""
	_connecting = false
	_reconnect_wait = -1.0
	_reconnect_attempt = 0
	_was_connected = false


func _close_peer() -> void:
	if multiplayer.multiplayer_peer != null and not (multiplayer.multiplayer_peer is OfflineMultiplayerPeer):
		multiplayer.multiplayer_peer.close()
	multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()


func _set_fast_timeout(peer_id: int) -> void:
	var enet := multiplayer.multiplayer_peer as ENetMultiplayerPeer
	if enet == null:
		return
	var packet_peer: ENetPacketPeer = enet.get_peer(peer_id)
	if packet_peer != null:
		packet_peer.set_timeout(PEER_TIMEOUT_LIMIT, PEER_TIMEOUT_MIN_MS, PEER_TIMEOUT_MAX_MS)


func _on_peer_connected(peer_id: int) -> void:
	if mode == Mode.HOST:
		_set_fast_timeout(peer_id)
		peer_joined.emit(peer_id)


func _on_peer_disconnected(peer_id: int) -> void:
	if mode == Mode.HOST:
		peer_left.emit(peer_id)


func _on_connected_to_server() -> void:
	_connecting = false
	_set_fast_timeout(1)
	var was_reconnect: bool = _reconnect_attempt > 0
	_was_connected = true
	_reconnect_attempt = 0
	if was_reconnect:
		print("[Network] Перепідключено до %s" % _server_ip)
		reconnected.emit()
	else:
		print("[Network] Підключено до %s" % _server_ip)
		joined_room.emit()


func _on_connection_failed() -> void:
	_connecting = false
	_close_peer()
	_handle_connect_failure("Не вдалося підключитися до хоста")


func _on_server_disconnected() -> void:
	_close_peer()
	_handle_connect_failure("Зв'язок із хостом втрачено")
