class_name Roles
extends RefCounted
## Ролі банди (див. docs/TZ.md, розділ «Ролі») і дані для інтерфейсу.

enum Id { NONE = -1, HACKER = 0, SHADOW = 1, SAFECRACKER = 2, DISTRACTOR = 3, DRIVER = 4 }

## Етап розробки, з якого роль стає грабельною (TZ: «MVP і план розробки»).
const INFO: Dictionary = {
	Id.HACKER: {"name": "Хакер", "hint": "Камери, двері, охорона", "color": Palette.CYAN_2, "stage": 1},
	Id.SHADOW: {"name": "Тінь", "hint": "Крадеться й бере здобич", "color": Palette.STEEL_3, "stage": 1},
	Id.SAFECRACKER: {"name": "Зломщик", "hint": "Відкриває сейф", "color": Palette.LAMP_3, "stage": 1},
	Id.DISTRACTOR: {"name": "Відволікач", "hint": "Заговорює охорону", "color": Palette.NEON_2, "stage": 3},
	Id.DRIVER: {"name": "Водій", "hint": "Везе банду під час втечі", "color": Palette.OK, "stage": 4},
}

## Поточний етап розробки — ролі з пізніших етапів показуються заблокованими.
const CURRENT_STAGE: int = 1

const ALL: Array[int] = [Id.HACKER, Id.SHADOW, Id.SAFECRACKER, Id.DISTRACTOR, Id.DRIVER]


static func is_valid(role: int) -> bool:
	return INFO.has(role)


static func is_available(role: int) -> bool:
	return INFO.has(role) and int(INFO[role]["stage"]) <= CURRENT_STAGE


static func display_name(role: int) -> String:
	if INFO.has(role):
		return INFO[role]["name"]
	return "—"


static func hint(role: int) -> String:
	if INFO.has(role):
		return INFO[role]["hint"]
	return ""


static func color(role: int) -> Color:
	if INFO.has(role):
		return INFO[role]["color"]
	return Color("736e8c")


static func unlock_stage(role: int) -> int:
	if INFO.has(role):
		return int(INFO[role]["stage"])
	return 0
