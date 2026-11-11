extends SceneTree
## Збирає піксельний шрифт у ресурс Godot (.res) з уже вшитими даними й налаштуваннями.
##
## Навіщо: тема проєкту (gui/theme/custom) вантажиться редактором ДО першого імпорту.
## Якщо тема посилається на .ttf, на свіжому клоні шрифт ще не імпортовано → ERROR при першому --import.
## Готовий .res не потребує імпорту, тож тема відкривається одразу.
##
## Запуск з кореня репо (лише коли міняємо шрифт):
##   godot --headless --path . -s tools/fonts/build_font.gd

const SOURCE: String = "res://tools/fonts/press_start_2p.ttf"
const TARGET: String = "res://assets/fonts/press_start_2p.res"


func _init() -> void:
	var font := FontFile.new()
	var err: Error = font.load_dynamic_font(ProjectSettings.globalize_path(SOURCE))
	if err != OK:
		push_error("Не вдалося прочитати %s (помилка %d)" % [SOURCE, err])
		quit(1)
		return
	# Піксельний шрифт: без згладжування, хінтингу й субпіксельного зсуву — літери рівно по пікселях.
	font.antialiasing = TextServer.FONT_ANTIALIASING_NONE
	font.hinting = TextServer.HINTING_NONE
	font.subpixel_positioning = TextServer.SUBPIXEL_POSITIONING_DISABLED
	font.generate_mipmaps = false
	font.multichannel_signed_distance_field = false
	font.allow_system_fallback = false
	err = ResourceSaver.save(font, TARGET)
	print("Шрифт збережено: %s (%s)" % [TARGET, "ok" if err == OK else "помилка %d" % err])
	quit(0 if err == OK else 1)
