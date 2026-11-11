extends Node2D
## Тримає вузол у центрі видимої області: фон ширший за базові 640 px
## і на широких телефонах лишається відцентрованим у масштабі 1:1 (без розмиття пікселів).


func _ready() -> void:
	get_viewport().size_changed.connect(_recenter)
	_recenter()


func _recenter() -> void:
	position = (get_viewport().get_visible_rect().size / 2.0).round()
