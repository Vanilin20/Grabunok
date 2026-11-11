"""Синтез звуків меню (Claude не чує результат — Іван оцінює на слух).

Запуск: python tools/audio/gen_audio.py → assets/audio/*.ogg|*.wav
Що й навіщо:
  rain_loop.ogg  — шум дощу: смуговий шум 1–8 кГц (шипіння) + рідкі краплі по металу/бетону; безшовна петля 8 с.
  city_loop.ogg  — далеке місто: низький гул (броунівський шум до ~150 Гц), повільні наростання трафіку; петля 12 с.
  neon_hum.ogg   — гудіння неонової трубки: 100 Гц (мережа 50 Гц × 2) з гармоніками й легким дребезгом; петля 2 с.
  thunder.ogg    — далекий грім: низький шумовий раскат з кількома ударами, 5 с.
  ui_click.wav   — натискання кнопки: короткий механічний клац + тихий «тік» 2 кГц, 60 мс.
  ui_deny.wav    — натискання вимкненої кнопки: глухий низький «тук», 90 мс.
"""
import os
import subprocess
import wave

import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "audio")
rng = np.random.default_rng(7)


def lowpass(x, cutoff):
	a = np.exp(-2 * np.pi * cutoff / SR)
	y = np.zeros_like(x)
	acc = 0.0
	for i, v in enumerate(x):
		acc = (1 - a) * v + a * acc
		y[i] = acc
	return y


def bandnoise(n, lo, hi):
	spec = np.fft.rfft(rng.standard_normal(n))
	f = np.fft.rfftfreq(n, 1 / SR)
	spec[(f < lo) | (f > hi)] = 0
	return np.fft.irfft(spec, n)


def loopify(x, fade):
	"""Безшовна петля: хвіст плавно накладається на початок."""
	head, tail = x[:fade], x[-fade:]
	t = np.linspace(0, 1, fade)
	x = x[:-fade].copy()
	x[:fade] = head * t + tail * (1 - t)
	return x


def norm(x, peak):
	return x / (np.max(np.abs(x)) + 1e-9) * peak


def write_wav(path, x, stereo=False):
	data = np.clip(x, -1, 1)
	if stereo and data.ndim == 1:
		data = np.stack([data, data], axis=1)
	pcm = (data * 32767).astype(np.int16)
	with wave.open(path, "wb") as w:
		w.setnchannels(2 if pcm.ndim == 2 else 1)
		w.setsampwidth(2)
		w.setframerate(SR)
		w.writeframes(pcm.tobytes())


def write_ogg(name, x):
	tmp = os.path.join(OUT, "_tmp.wav")
	write_wav(tmp, x)
	subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp, "-c:a", "libvorbis", "-q:a", "4", os.path.join(OUT, name)], check=True)
	os.remove(tmp)


def rain(seconds=8.0):
	n = int(SR * seconds)
	fade = SR // 2
	hiss = bandnoise(n + fade, 900, 8000) * 0.5 + bandnoise(n + fade, 200, 900) * 0.25
	drops = np.zeros(n + fade)
	for _ in range(int(seconds * 40)):
		i = rng.integers(0, n + fade - 800)
		freq = rng.uniform(1800, 5200)
		t = np.arange(800) / SR
		drops[i:i + 800] += np.sin(2 * np.pi * freq * t) * np.exp(-t * 90) * rng.uniform(0.05, 0.35)
	return norm(loopify(hiss + drops, fade), 0.5)


def city(seconds=12.0):
	n = int(SR * seconds)
	fade = SR
	brown = np.cumsum(rng.standard_normal(n + fade))
	brown -= np.convolve(brown, np.ones(4410) / 4410, mode="same") # прибираємо дрейф
	rumble = bandnoise(n + fade, 30, 160) * 0.6 + norm(brown, 0.3)
	t = np.arange(n + fade) / SR
	swell = 0.6 + 0.4 * np.sin(2 * np.pi * t / seconds) ** 2 # трафік накочує й відходить (ціле число періодів — петля)
	traffic = bandnoise(n + fade, 150, 900) * 0.25 * swell
	return norm(loopify(rumble + traffic, fade), 0.45)


def hum(seconds=2.0):
	n = int(SR * seconds)
	t = np.arange(n) / SR
	x = sum(np.sin(2 * np.pi * 100 * k * t) / k ** 1.3 for k in range(1, 7))
	buzz = 1 + 0.15 * np.sign(np.sin(2 * np.pi * 100 * t)) * (0.5 + 0.5 * np.sin(2 * np.pi * 3 * t))
	return norm(x * buzz, 0.25)


def thunder(seconds=5.0):
	n = int(SR * seconds)
	t = np.arange(n) / SR
	body = bandnoise(n, 25, 220)
	env = np.zeros(n)
	for start, amp in ((0.0, 1.0), (0.35, 0.7), (0.9, 0.5), (1.6, 0.35)):
		k = t >= start
		env[k] += amp * np.exp(-(t[k] - start) * 1.6)
	crack = bandnoise(n, 400, 3000) * np.exp(-t * 6) * 0.15
	return norm(body * env + crack, 0.8)


def click():
	n = int(SR * 0.06)
	t = np.arange(n) / SR
	x = rng.standard_normal(n) * np.exp(-t * 900) * 0.6 + np.sin(2 * np.pi * 2100 * t) * np.exp(-t * 70) * 0.35
	return norm(x, 0.7)


def deny():
	n = int(SR * 0.09)
	t = np.arange(n) / SR
	x = np.sin(2 * np.pi * 140 * t) * np.exp(-t * 40) + rng.standard_normal(n) * np.exp(-t * 500) * 0.2
	return norm(x, 0.6)


def main():
	os.makedirs(OUT, exist_ok=True)
	write_ogg("rain_loop.ogg", rain())
	write_ogg("city_loop.ogg", city())
	write_ogg("neon_hum.ogg", hum())
	write_ogg("thunder.ogg", thunder())
	write_wav(os.path.join(OUT, "ui_click.wav"), click())
	write_wav(os.path.join(OUT, "ui_deny.wav"), deny())
	print("ok")


if __name__ == "__main__":
	main()
