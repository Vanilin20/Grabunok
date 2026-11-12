#!/bin/bash
# Перевірка UI в кількох розмірах екрана. Запуск з кореня: tools/checks/run_ui.sh res://scenes/ui/main_menu.tscn [user-args...]
scene=$1; shift
fail=0
for size in 1280x720 2400x1080 1600x720; do
	w=${size%x*}; h=${size#*x}
	printf '[display]\nwindow/size/window_width_override=%s\nwindow/size/window_height_override=%s\n' $w $h > override.cfg
	out=$(timeout 120 xvfb-run -a -s "-screen 0 2560x1440x24" godot --path . --rendering-method gl_compatibility -s tools/checks/check_ui.gd -- --scene=$scene "$@" 2>&1)
	echo "$out" | grep -E "^\[ui\] (FAIL|OK:|FAIL:)|^\[ui\] сцена"
	echo "$out" | grep -q "\[ui\] FAIL" && fail=1
done
rm -f override.cfg
exit $fail
