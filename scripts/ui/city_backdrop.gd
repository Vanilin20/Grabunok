extends CanvasLayer
## Нічне місто під дощем — фон меню й лобі.
## Шари: далеке місто → дощ (далекий) → ближні будинки → світло (адитивні конуси й ореоли) → дощ (близький).
## Неон мерехтить, як старі трубки: здебільшого рівно, зрідка короткі збої.

@export var flicker_nodes: Array[NodePath] = []

var _rng := RandomNumberGenerator.new()
var _base_alpha: Dictionary = {} # CanvasItem -> базова прозорість
var _flicker_left: Dictionary = {} # CanvasItem -> скільки ще триває збій


func _ready() -> void:
	_rng.randomize()
	for path: NodePath in flicker_nodes:
		var item := get_node_or_null(path) as CanvasItem
		if item != null:
			_base_alpha[item] = item.modulate.a
			_flicker_left[item] = 0.0


func _process(delta: float) -> void:
	for item: CanvasItem in _base_alpha:
		var left: float = _flicker_left[item]
		var alpha: float = _base_alpha[item]
		if left > 0.0:
			left -= delta
			if _rng.randf() < 0.5:
				alpha *= 0.2
		elif _rng.randf() < delta * 0.25: # у середньому раз на ~4 секунди
			left = _rng.randf_range(0.08, 0.35)
		item.modulate.a = alpha
		_flicker_left[item] = left
