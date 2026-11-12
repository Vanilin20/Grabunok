extends SceneTree
## Перевірки 4 і 8: читабельність тексту й безпечна зона під виріз камери.
##
## Запуск (потрібен рендер, тож через xvfb на сервері або звичайно на ПК):
##   godot --path . -s tools/checks/check_ui.gd -- --scene=res://scenes/ui/main_menu.tscn [--name=Іван]
## Розмір вікна задає override.cfg (див. tools/checks/run_checks.sh).
##
## Для кожного видимого напису (Label, Button, LineEdit):
##   - колір тексту з теми / перевизначень, фон — медіана пікселів під написом без пікселів тексту й обведення;
##   - контраст за WCAG: (L1 + 0.05) / (L2 + 0.05) ≥ 4.5;
##   - висота літер у базовій роздільності ≥ 7 px (Press Start 2P: висота літери = 7/8 розміру шрифту);
##   - безпечна зона: ≥ 40 px базової роздільності від лівого й правого країв, напис не обрізаний.
## Окремо друкується контраст «текст / обведення» — довідково, у залік не йде.

const SAFE_MARGIN: float = 40.0
const MIN_CONTRAST: float = 4.5
const MIN_HEIGHT: float = 7.0
const WAIT_FRAMES: int = 40

var _failed: int = 0


func _initialize() -> void:
	var scene_path: String = ""
	for arg: String in OS.get_cmdline_user_args():
		if arg.begins_with("--scene="):
			scene_path = arg.trim_prefix("--scene=")
	var router: Node = root.get_node_or_null("Router")
	if router != null:
		router.set("auto_navigate", false)
	var scene: Node = (load(scene_path) as PackedScene).instantiate()
	root.add_child(scene)
	for i: int in WAIT_FRAMES:
		await process_frame
	await RenderingServer.frame_post_draw
	var image: Image = root.get_texture().get_image()
	var base_size: Vector2 = root.get_visible_rect().size
	var scale: float = float(image.get_height()) / base_size.y
	print("[ui] сцена %s, вікно %dx%d, базова %dx%d" % [scene_path, image.get_width(), image.get_height(), base_size.x, base_size.y])
	_walk(scene, image, base_size, scale)
	print("[ui] %s: порушень %d" % ["FAIL" if _failed else "OK", _failed])
	quit(1 if _failed else 0)


func _walk(node: Node, image: Image, base_size: Vector2, scale: float) -> void:
	var control := node as Control
	if control != null and control.is_visible_in_tree():
		var text: String = ""
		if control is Label:
			text = (control as Label).text
		elif control is Button:
			text = (control as Button).text
		elif control is LineEdit:
			var edit := control as LineEdit
			text = edit.text if not edit.text.is_empty() else edit.placeholder_text
		if not text.strip_edges().is_empty():
			_check(control, text, image, base_size, scale)
	for child: Node in node.get_children():
		_walk(child, image, base_size, scale)


func _check(control: Control, text: String, image: Image, base_size: Vector2, scale: float) -> void:
	var color: Color = _text_color(control)
	var outline_size: int = control.get_theme_constant("outline_size")
	var outline: Color = control.get_theme_color("font_outline_color")
	var font_size: int = control.get_theme_font_size("font_size")
	var rect: Rect2 = control.get_global_rect()
	var bg: Color = _background(image, rect, scale, color, outline if outline_size > 0 else Color(-1, -1, -1))
	var ratio: float = _contrast(color, bg)
	var ratio_outline: float = _contrast(color, outline) if outline_size > 0 else 0.0
	var height: float = font_size * 7.0 / 8.0
	var safe: bool = rect.position.x >= SAFE_MARGIN - 0.5 and rect.end.x <= base_size.x - SAFE_MARGIN + 0.5
	var inside: bool = rect.position.y >= 0.0 and rect.end.y <= base_size.y
	var ok: bool = ratio >= MIN_CONTRAST and height >= MIN_HEIGHT and safe and inside
	if not ok:
		_failed += 1
	print("[ui] %s «%s»: контраст %.1f:1 (з обведенням %.1f:1), висота %.0f px, x %d..%d з %d%s" % [
		"OK  " if ok else "FAIL", text.left(28), ratio, ratio_outline, height, rect.position.x, rect.end.x, base_size.x,
		"" if safe else " — у зоні вирізу"])


func _text_color(control: Control) -> Color:
	if control is Button:
		var button := control as Button
		if button.disabled:
			return control.get_theme_color("font_disabled_color")
		return control.get_theme_color("font_color")
	if control is LineEdit and (control as LineEdit).text.is_empty():
		return control.get_theme_color("font_placeholder_color")
	return control.get_theme_color("font_color")


## Медіана кольору під написом без пікселів, близьких до кольору тексту чи обведення.
func _background(image: Image, rect: Rect2, scale: float, text: Color, outline: Color) -> Color:
	var r: Array[float] = []
	var g: Array[float] = []
	var b: Array[float] = []
	var x0: int = int(rect.position.x * scale)
	var y0: int = int(rect.position.y * scale)
	var x1: int = mini(int(rect.end.x * scale), image.get_width())
	var y1: int = mini(int(rect.end.y * scale), image.get_height())
	for y: int in range(maxi(y0, 0), y1, 2):
		for x: int in range(maxi(x0, 0), x1, 2):
			var c: Color = image.get_pixel(x, y)
			if _dist(c, text) < 0.18 or (outline.r >= 0.0 and _dist(c, outline) < 0.12):
				continue
			r.append(c.r)
			g.append(c.g)
			b.append(c.b)
	if r.is_empty():
		return outline if outline.r >= 0.0 else Color.BLACK
	r.sort()
	g.sort()
	b.sort()
	var m: int = r.size() / 2
	return Color(r[m], g[m], b[m])


func _dist(a: Color, b: Color) -> float:
	return absf(a.r - b.r) + absf(a.g - b.g) + absf(a.b - b.b)


func _luminance(c: Color) -> float:
	var lin := func(v: float) -> float: return v / 12.92 if v <= 0.04045 else pow((v + 0.055) / 1.055, 2.4)
	return 0.2126 * lin.call(c.r) + 0.7152 * lin.call(c.g) + 0.0722 * lin.call(c.b)


func _contrast(a: Color, b: Color) -> float:
	var la: float = _luminance(a)
	var lb: float = _luminance(b)
	return (maxf(la, lb) + 0.05) / (minf(la, lb) + 0.05)
