class_name AlarmBar
extends Control
## Шкала тривоги 0–100% (TZ: «Тривога (0–100%)»). Сегменти, позначка 50% (охорона посилюється),
## після 50% шкала пульсує — щоб читалося за пів секунди без тексту.

const SEGMENTS: int = 20
const GAP: float = 1.0

var value: float = 0.0:
	set(v):
		value = clampf(v, 0.0, 100.0)
		queue_redraw()

var _pulse: float = 0.0


func _process(delta: float) -> void:
	if value >= 50.0:
		_pulse += delta * (4.0 if value < 80.0 else 8.0)
		queue_redraw()


func _draw() -> void:
	var w: float = size.x
	var h: float = size.y
	draw_rect(Rect2(0, 0, w, h), Palette.NIGHT)
	draw_rect(Rect2(0, 0, w, h), Palette.SLATE, false, 1.0)
	var inner := Rect2(2, 2, w - 4, h - 4)
	var seg_w: float = (inner.size.x - GAP * (SEGMENTS - 1)) / SEGMENTS
	var lit: int = ceili(value / 100.0 * SEGMENTS - 0.001)
	var flash: float = 0.5 + 0.5 * sin(_pulse) if value >= 50.0 else 1.0
	for i: int in SEGMENTS:
		var x: float = floorf(inner.position.x + i * (seg_w + GAP))
		var rect := Rect2(x, inner.position.y, floorf(seg_w), inner.size.y)
		var t: float = float(i) / float(SEGMENTS - 1)
		var color: Color = _segment_color(t)
		if i < lit:
			draw_rect(rect, color.lerp(Palette.TEXT, 0.25 * (1.0 - flash)) if i >= SEGMENTS / 2 else color)
		else:
			draw_rect(rect, Color(color, 0.12))
	# Позначка 50%.
	var mid: float = floorf(w / 2.0)
	draw_rect(Rect2(mid, -2, 1, h + 4), Palette.TEXT if value < 50.0 else Palette.DANGER)


func _segment_color(t: float) -> Color:
	if t < 0.5:
		return Palette.SAFE.lerp(Palette.LAMP, t * 2.0)
	return Palette.LAMP.lerp(Palette.DANGER, (t - 0.5) * 2.0)
