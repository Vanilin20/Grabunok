#!/bin/bash
# Фокус концепту магазину: кадр + маска персонажів того самого кадру, у трьох розмірах екрана.
# Запуск з кореня: tools/checks/run_focus_store.sh [вихідна_тека]
out=${1:-/tmp/focus_store}; mkdir -p $out; fail=0
for size in 1280x720 2400x1080 1600x720; do
	w=${size%x*}; h=${size#*x}
	printf '[display]\nwindow/size/window_width_override=%s\nwindow/size/window_height_override=%s\n' $w $h > override.cfg
	for mode in frame mask; do
		rm -rf $out/$mode && mkdir -p $out/$mode
		args="-- --still"; [ $mode = mask ] && args="-- --still --focus-mask"
		timeout 200 xvfb-run -a -s "-screen 0 2560x1440x24" godot --path . --rendering-method gl_compatibility res://scenes/levels/store_concept.tscn --quit-after 120 --write-movie $out/$mode/f.png $args >/dev/null 2>&1
		cp $(ls $out/$mode/*.png | tail -1) $out/${mode}_$size.png
	done
	python3 tools/checks/check_focus.py $out/frame_$size.png --mask $out/mask_$size.png | sed -n '1p;2p;4p' || true
	python3 tools/checks/check_focus.py $out/frame_$size.png --mask $out/mask_$size.png >/dev/null || fail=1
done
rm -f override.cfg
exit $fail
