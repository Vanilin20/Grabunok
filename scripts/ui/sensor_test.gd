extends Control
## Тест датчиків для етапу 1 (TZ: «Перевірка вібрації, гіроскопа й мікрофона на Android»).
## Показує сирі значення й одразу — те, як їх використає гра: «Завмри!» і рівень шуму.

const MIC_BUS: String = "MicCapture"
const STILL_THRESHOLD: float = 0.25 # рад/с: нижче — телефон вважається нерухомим (поріг для «Завмри!»)
const MIC_PERMISSION: String = "android.permission.RECORD_AUDIO"

@onready var _info_label: Label = %InfoLabel
@onready var _gyro_label: Label = %GyroLabel
@onready var _accel_label: Label = %AccelLabel
@onready var _still_bar: ProgressBar = %StillBar
@onready var _still_label: Label = %StillLabel
@onready var _mic_bar: ProgressBar = %MicBar
@onready var _mic_label: Label = %MicLabel
@onready var _mic_button: Button = %MicButton
@onready var _back_button: Button = %BackButton
@onready var _orientation_button: Button = %OrientationButton

var _mic_player: AudioStreamPlayer = null
var _capture: AudioEffectCapture = null
var _mic_db: float = -80.0
var _still_time: float = 0.0


func _ready() -> void:
	_info_label.text = "%s · %s" % [OS.get_name(), OS.get_model_name()]
	%VibShort.pressed.connect(_vibrate.bind(40, 1.0))
	%VibLong.pressed.connect(_vibrate.bind(400, 1.0))
	%VibWeak.pressed.connect(_vibrate.bind(60, 0.2))
	%VibPattern.pressed.connect(_vibrate_pattern)
	_mic_button.pressed.connect(_toggle_mic)
	_back_button.pressed.connect(Router.show_menu)
	_orientation_button.pressed.connect(_toggle_orientation)
	%BenchmarkButton.pressed.connect(Router.show_benchmark)
	get_tree().on_request_permissions_result.connect(_on_permission_result)


func _exit_tree() -> void:
	_stop_mic()


func _process(delta: float) -> void:
	var gyro: Vector3 = Input.get_gyroscope()
	var accel: Vector3 = Input.get_accelerometer()
	_gyro_label.text = "ГІРОСКОП  x %+.2f  y %+.2f  z %+.2f" % [gyro.x, gyro.y, gyro.z]
	_accel_label.text = "АКСЕЛЕР.  x %+.1f  y %+.1f  z %+.1f" % [accel.x, accel.y, accel.z]
	# «Завмри!»: скільки секунд поспіль телефон нерухомий.
	var motion: float = gyro.length()
	_still_time = _still_time + delta if motion < STILL_THRESHOLD else 0.0
	_still_bar.value = clampf(motion / (STILL_THRESHOLD * 4.0), 0.0, 1.0) * 100.0
	_still_label.text = "нерухомо %.1f с" % _still_time if motion < STILL_THRESHOLD else "РУХ!"
	_still_label.add_theme_color_override("font_color", Palette.SAFE if motion < STILL_THRESHOLD else Palette.DANGER)
	_update_mic()


# --- Вібрація -------------------------------------------------------------

func _vibrate(duration_ms: int, amplitude: float) -> void:
	if not OS.has_feature("mobile"):
		Router.toast("На ПК вібрації нема — перевір на телефоні")
		return
	Input.vibrate_handheld(duration_ms, amplitude)


func _vibrate_pattern() -> void:
	# Ритм «клац-клац-БАХ», як у Зломщика біля потрібної цифри.
	for step: Array in [[10, 0.3, 0.12], [10, 0.3, 0.12], [80, 1.0, 0.0]]:
		_vibrate(step[0], step[1])
		if float(step[2]) > 0.0:
			await get_tree().create_timer(step[2]).timeout


# --- Мікрофон -------------------------------------------------------------

func _toggle_mic() -> void:
	if _mic_player != null:
		_stop_mic()
		return
	if OS.get_name() == "Android" and not OS.get_granted_permissions().has(MIC_PERMISSION):
		_mic_label.text = "чекаю дозволу на мікрофон…"
		OS.request_permission(MIC_PERMISSION)
		return
	_start_mic()


func _on_permission_result(permission: String, granted: bool) -> void:
	if permission != MIC_PERMISSION:
		return
	if granted:
		_start_mic()
	else:
		_mic_label.text = "дозвіл на мікрофон не дано"


func _start_mic() -> void:
	var bus: int = _ensure_mic_bus()
	_capture = AudioServer.get_bus_effect(bus, 0) as AudioEffectCapture
	_capture.clear_buffer()
	_mic_player = AudioStreamPlayer.new()
	_mic_player.stream = AudioStreamMicrophone.new()
	_mic_player.bus = MIC_BUS
	add_child(_mic_player)
	_mic_player.play()
	_mic_button.text = "ВИМКНУТИ МІКРОФОН"


func _stop_mic() -> void:
	if _mic_player != null:
		_mic_player.stop()
		_mic_player.queue_free()
		_mic_player = null
	_capture = null
	_mic_db = -80.0
	_mic_button.text = "УВІМКНУТИ МІКРОФОН"


## Окрема шина: ефект Capture забирає семпли, а гучність -80 дБ не пускає звук у динамік (без фону й луни).
func _ensure_mic_bus() -> int:
	var bus: int = AudioServer.get_bus_index(MIC_BUS)
	if bus == -1:
		AudioServer.add_bus()
		bus = AudioServer.bus_count - 1
		AudioServer.set_bus_name(bus, MIC_BUS)
		AudioServer.add_bus_effect(bus, AudioEffectCapture.new())
		AudioServer.set_bus_volume_db(bus, -80.0)
	return bus


func _update_mic() -> void:
	if _capture == null:
		_mic_bar.value = 0.0
		if _mic_player == null and not _mic_label.text.begins_with("чекаю") and not _mic_label.text.begins_with("дозвіл"):
			_mic_label.text = "вимкнено"
		return
	var frames: int = _capture.get_frames_available()
	if frames > 0:
		var buffer: PackedVector2Array = _capture.get_buffer(frames)
		var sum: float = 0.0
		for frame: Vector2 in buffer:
			sum += frame.x * frame.x
		var rms: float = sqrt(sum / float(buffer.size()))
		var db: float = linear_to_db(maxf(rms, 0.00001))
		# Швидко вгору, повільно вниз — як у звичайного індикатора рівня.
		_mic_db = db if db > _mic_db else lerpf(_mic_db, db, 0.1)
	_mic_bar.value = clampf(remap(_mic_db, -60.0, 0.0, 0.0, 100.0), 0.0, 100.0)
	_mic_label.text = "%d дБ" % roundi(_mic_db)


func _toggle_orientation() -> void:
	if ScreenOrientation.is_portrait():
		ScreenOrientation.set_landscape()
	else:
		ScreenOrientation.set_portrait()
