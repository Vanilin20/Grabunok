extends SceneTree
## Збирає тему інтерфейсу в assets/ui/theme.res з УЖЕ вшитими текстурами й шрифтом.
##
## Навіщо: тему проєкту (gui/theme/custom) редактор вантажить ДО першого імпорту ресурсів.
## Якщо тема посилається на .png чи .ttf, на свіжому клоні вони ще не імпортовані → ERROR.
## Вшиті в .res зображення імпорту не потребують.
##
## Запуск з кореня репо після зміни текстур (tools/art/gen_ui.py) чи кольорів:
##   godot --headless --path . -s tools/ui/build_theme.gd

const SRC: String = "res://tools/art/ui/"
const FONT: String = "res://assets/fonts/press_start_2p.res"
const TARGET: String = "res://assets/ui/theme.res"
const SLICE: float = 8.0 # 9-slice, з них 2 px — зовнішній ореол кнопки
const GLOW: float = 2.0

# Кольори дублюють палітру (scripts/systems/palette.gd): на етапі _init класи проєкту ще не готові.
const NIGHT_0 := Color("07060d")
const NIGHT_1 := Color("0e0c1a")
const NIGHT_4 := Color("2f2849")
const NIGHT_5 := Color("40375e")
const NIGHT_6 := Color("574b78")
const STEEL_1 := Color("5a6175")
const STEEL_2 := Color("8b93a8")
const STEEL_3 := Color("c7cede")
const LAMP_3 := Color("f7c95a")
const LAMP_4 := Color("fff2c4")
const CYAN_2 := Color("3de0ff")
const OK_GREEN := Color("5cf08a")


func _init() -> void:
	var theme := Theme.new()
	theme.default_font = load(FONT)
	theme.default_font_size = 8

	# Кнопки: робоча — бурштинова «неонова трубка» з ореолом, вимкнена — тьмяна пластина без світла.
	theme.set_stylebox("normal", "Button", _box("button_normal", Vector4(10, 9, 10, 10), GLOW))
	theme.set_stylebox("hover", "Button", _box("button_hover", Vector4(10, 9, 10, 10), GLOW))
	theme.set_stylebox("pressed", "Button", _box("button_pressed", Vector4(10, 10, 10, 9), GLOW))
	theme.set_stylebox("hover_pressed", "Button", _box("button_pressed", Vector4(10, 10, 10, 9), GLOW))
	theme.set_stylebox("disabled", "Button", _box("button_disabled", Vector4(10, 9, 10, 10), GLOW))
	theme.set_stylebox("focus", "Button", StyleBoxEmpty.new())
	theme.set_color("font_color", "Button", LAMP_3)
	theme.set_color("font_focus_color", "Button", LAMP_3)
	theme.set_color("font_hover_color", "Button", LAMP_4)
	theme.set_color("font_pressed_color", "Button", LAMP_4)
	theme.set_color("font_hover_pressed_color", "Button", LAMP_4)
	theme.set_color("font_disabled_color", "Button", STEEL_2) # сірий без світіння: читається (≥4.5:1), але явно «не горить»
	theme.set_color("font_outline_color", "Button", NIGHT_0)
	theme.set_constant("outline_size", "Button", 2)

	# Другорядна кнопка (тест датчиків тощо): сталева рамка без світіння.
	theme.add_type("SecondaryButton")
	theme.set_type_variation("SecondaryButton", "Button")
	theme.set_stylebox("normal", "SecondaryButton", _box("button_secondary", Vector4(10, 7, 10, 8), GLOW))
	theme.set_stylebox("hover", "SecondaryButton", _box("button_secondary_hover", Vector4(10, 7, 10, 8), GLOW))
	theme.set_stylebox("pressed", "SecondaryButton", _box("button_secondary_hover", Vector4(10, 8, 10, 7), GLOW))
	theme.set_color("font_color", "SecondaryButton", CYAN_2)
	theme.set_color("font_hover_color", "SecondaryButton", Color("ffffff"))
	theme.set_color("font_pressed_color", "SecondaryButton", Color("ffffff"))

	theme.set_color("font_color", "Label", STEEL_3)
	theme.set_constant("line_spacing", "Label", 4)

	theme.set_stylebox("normal", "LineEdit", _box("edit_normal", Vector4(8, 8, 8, 8)))
	theme.set_stylebox("read_only", "LineEdit", _box("edit_normal", Vector4(8, 8, 8, 8)))
	theme.set_stylebox("focus", "LineEdit", _box("edit_focus", Vector4(8, 8, 8, 8)))
	theme.set_color("font_color", "LineEdit", STEEL_3)
	theme.set_color("font_placeholder_color", "LineEdit", STEEL_2)
	theme.set_color("caret_color", "LineEdit", CYAN_2)
	theme.set_color("selection_color", "LineEdit", NIGHT_5)

	var panel: StyleBoxTexture = _box("panel", Vector4(10, 10, 10, 10))
	theme.set_stylebox("panel", "PanelContainer", panel)
	theme.set_stylebox("panel", "Panel", panel)

	var bar_bg := StyleBoxFlat.new()
	bar_bg.bg_color = NIGHT_1
	bar_bg.set_border_width_all(1)
	bar_bg.border_color = NIGHT_4
	bar_bg.anti_aliasing = false
	var bar_fill := StyleBoxFlat.new()
	bar_fill.bg_color = OK_GREEN
	bar_fill.anti_aliasing = false
	theme.set_stylebox("background", "ProgressBar", bar_bg)
	theme.set_stylebox("fill", "ProgressBar", bar_fill)

	var grabber := StyleBoxFlat.new()
	grabber.bg_color = NIGHT_5
	grabber.content_margin_left = 2.0
	grabber.content_margin_right = 2.0
	var scroll := StyleBoxEmpty.new()
	scroll.content_margin_left = 2.0
	scroll.content_margin_right = 2.0
	for state: String in ["grabber", "grabber_highlight", "grabber_pressed"]:
		theme.set_stylebox(state, "VScrollBar", grabber)
	theme.set_stylebox("scroll", "VScrollBar", scroll)

	var err: Error = ResourceSaver.save(theme, TARGET)
	print("Тема збережена: %s (%s)" % [TARGET, "ok" if err == OK else "помилка %d" % err])
	quit(0 if err == OK else 1)


## StyleBoxTexture з вшитою текстурою (ImageTexture з PNG напряму, без імпорту).
func _box(file_name: String, content: Vector4, expand: float = 0.0) -> StyleBoxTexture:
	var image := Image.load_from_file(ProjectSettings.globalize_path(SRC + file_name + ".png"))
	var box := StyleBoxTexture.new()
	box.texture = ImageTexture.create_from_image(image)
	box.texture_margin_left = SLICE
	box.texture_margin_top = SLICE
	box.texture_margin_right = SLICE
	box.texture_margin_bottom = SLICE
	box.content_margin_left = content.x
	box.content_margin_top = content.y
	box.content_margin_right = content.z
	box.content_margin_bottom = content.w
	box.expand_margin_left = expand
	box.expand_margin_top = expand
	box.expand_margin_right = expand
	box.expand_margin_bottom = expand
	return box
