extends Control
## Неоновий напис: м'яке «дихання» яскравості й рідкісні збої, як у старої трубки.
## Вузол — контейнер із кількох шарів тексту (ореоли з адитивним змішуванням + яскраве ядро),
## мерехтіння змінює modulate всього контейнера.

@export var breathe_speed: float = 1.3
@export var glitch_chance_per_sec: float = 0.18

var _rng := RandomNumberGenerator.new()
var _time: float = 0.0
var _glitch_left: float = 0.0


func _ready() -> void:
	_rng.randomize()


func _process(delta: float) -> void:
	_time += delta
	var brightness: float = 0.94 + sin(_time * breathe_speed) * 0.06
	if _glitch_left > 0.0:
		_glitch_left -= delta
		if _rng.randf() < 0.5:
			brightness = 0.3
	elif _rng.randf() < delta * glitch_chance_per_sec:
		_glitch_left = _rng.randf_range(0.06, 0.25)
	modulate = Color(brightness, brightness, brightness, 1.0)
