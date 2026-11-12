"""Перевірка 2: векторні примітиви (draw_*, ColorRect, Polygon2D) у скриптах і сценах ігрових екранів.

Дозволено лише те, що записано в tools/checks/allowlist.txt у форматі
    шлях:шаблон  # пояснення
Запуск: python tools/checks/check_primitives.py [--scope menu|all]
  --scope menu — лише файли головного меню (екрани, які вже переведені на нові стандарти арту);
  --scope all  — увесь проєкт (за замовчуванням).
"""
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
PATTERN = re.compile(r"\b(draw_circle|draw_rect|draw_line|draw_arc|draw_polyline|draw_colored_polygon|draw_polygon|ColorRect|Polygon2D)\b")
SCOPES = {
	"menu": ["scenes/ui/main_menu.tscn", "scripts/ui/main_menu.gd", "scenes/ui/city_backdrop.tscn", "scripts/ui/city_backdrop.gd"],
}
## Екрани, які вже перемальовані під нові стандарти арту: для них потрібно 0 порушень.
## Решта файлів — «ще не перемальовані» (етап 3): друкуються окремим звітом і не валять перевірку.
REDRAWN = {
	"Головне меню": SCOPES["menu"],
}


def load_allowlist() -> dict:
	allowed = {}
	with open(os.path.join(os.path.dirname(__file__), "allowlist.txt"), encoding="utf-8") as f:
		for line in f:
			line = line.strip()
			if not line or line.startswith("#"):
				continue
			entry, _, reason = line.partition("#")
			path, _, pattern = entry.strip().partition(":")
			if not reason.strip():
				print(f"FAIL allowlist: запис без пояснення: {line}")
				continue
			allowed.setdefault(path.strip(), set()).add(pattern.strip())
	return allowed


def scan(rel: str, allowed: dict) -> list[str]:
	found = []
	with open(os.path.join(ROOT, rel), encoding="utf-8") as f:
		for no, line in enumerate(f, 1):
			for m in PATTERN.finditer(line):
				if m.group(1) not in allowed.get(rel, set()):
					found.append(f"{rel}:{no}: {m.group(1)}")
	return found


def report_by_screen() -> int:
	"""Звіт по екранах: перемальовані — мають бути чисті; неперемальовані — окремий список."""
	allowed = load_allowlist()
	all_files = []
	for top in ("scripts", "scenes"):
		for base, _d, names in os.walk(os.path.join(ROOT, top)):
			for n in names:
				if n.endswith((".gd", ".tscn")):
					all_files.append(os.path.relpath(os.path.join(base, n), ROOT).replace(os.sep, "/"))
	done = set()
	fail = 0
	print("== Перемальовані екрани (потрібно 0 порушень)")
	for screen, files in REDRAWN.items():
		found = [v for rel in files for v in scan(rel, allowed)]
		done.update(files)
		fail += len(found)
		print(f"{'OK  ' if not found else 'FAIL'} {screen}: файлів {len(files)}, порушень {len(found)}")
		for v in found:
			print(f"     {v}")
	print("== Ще не перемальовані (етап 3) — окремий звіт, перевірку не валять")
	todo = {}
	for rel in sorted(set(all_files) - done):
		found = scan(rel, allowed)
		if found:
			todo[rel] = len(found)
	for rel, n in todo.items():
		print(f"TODO {rel}: {n}")
	print(f"Підсумок: перемальовані — {fail} порушень; неперемальовані — {sum(todo.values())} у {len(todo)} файлах")
	return 1 if fail else 0


def main() -> int:
	if "--by-screen" in sys.argv:
		return report_by_screen()
	scope = sys.argv[sys.argv.index("--scope") + 1] if "--scope" in sys.argv else "all"
	allowed = load_allowlist()
	files = []
	if scope in SCOPES:
		files = SCOPES[scope]
	else:
		for top in ("scripts", "scenes"):
			for base, _d, names in os.walk(os.path.join(ROOT, top)):
				for n in names:
					if n.endswith((".gd", ".tscn")):
						files.append(os.path.relpath(os.path.join(base, n), ROOT).replace(os.sep, "/"))
	violations = 0
	for rel in sorted(files):
		with open(os.path.join(ROOT, rel), encoding="utf-8") as f:
			for no, line in enumerate(f, 1):
				for m in PATTERN.finditer(line):
					if m.group(1) in allowed.get(rel, set()):
						continue
					violations += 1
					print(f"FAIL {rel}:{no}: {m.group(1)}")
	print(f"Примітиви (scope={scope}): файлів {len(files)}, порушень {violations}")
	return 1 if violations else 0


if __name__ == "__main__":
	sys.exit(main())
