# Trata as gravações e gera os sons do jogo (numpy + o ffmpeg do imageio-ffmpeg).
#   python tratar_sons.py "<pasta com as gravações>"
# Gravações esperadas: "caminhar dentro do necroterio.mp3", "Gaveta mesa adm.mp3", "porta abrir.mp3", "porta fechar.mp3".
# Saída: Assets/Audio/Sistemas/{Passos,Portas,Gavetas}/*.wav (48 kHz, mono, 16 bits) e um relatório no terminal.
#   Passos: o chiado de fundo é reduzido e cada passo vira um arquivo (o jogo sorteia sem repetir).
#   Portas: abrir (começo do giro) e fechar (a batida, tocada quando a folha chega ao batente).
#   Gavetas: os trechos da gravação são separados e classificados em abrir e fechar (fechar termina com batida).
import json
import os
import subprocess
import sys
import wave

import imageio_ffmpeg
import numpy as np

FF = imageio_ffmpeg.get_ffmpeg_exe()
SR = 48000
AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", "..", ".."))
SAIDA = os.path.join(RAIZ, "Assets", "Audio", "Sistemas")


def ler(caminho, filtro=None):
    cmd = [FF, "-v", "error", "-i", caminho]
    if filtro:
        cmd += ["-af", filtro]
    cmd += ["-f", "f32le", "-ac", "1", "-ar", str(SR), "-"]
    return np.frombuffer(subprocess.run(cmd, capture_output=True, check=True).stdout, dtype=np.float32).copy()


def salvar(x, pasta, nome):
    os.makedirs(os.path.join(SAIDA, pasta), exist_ok=True)
    caminho = os.path.join(SAIDA, pasta, nome + ".wav")
    dados = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(caminho, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(dados.tobytes())
    return caminho


def db(v):
    return 20 * np.log10(np.maximum(v, 1e-9))


def envelope(x, janela=0.005):
    n = max(1, int(SR * janela))
    k = len(x) // n
    return np.sqrt((x[:k * n].reshape(k, n) ** 2).mean(axis=1)), n


def fades(x, entrada=0.004, saida=0.08):
    x = x.copy()
    a, b = int(SR * entrada), int(SR * saida)
    if a:
        x[:a] *= np.linspace(0, 1, a)
    if b:
        x[-b:] *= np.linspace(1, 0, b) ** 2
    return x


def normalizar(x, pico_db):
    p = np.abs(x).max()
    return x * (10 ** (pico_db / 20) / p) if p > 0 else x


def passa_alta(x, hz=120):
    """Filtro passa-alta de 1ª ordem (tira o ronco grave do ambiente)."""
    rc = 1 / (2 * np.pi * hz)
    a = rc / (rc + 1 / SR)
    y = np.empty_like(x)
    y[0] = x[0]
    for i in range(1, len(x)):
        y[i] = a * (y[i - 1] + x[i] - x[i - 1])
    return y


relatorio = {}

# ------------------------------------------------------------------ passos
def passos(caminho):
    x = ler(caminho, "afftdn=nr=18:nf=-45,highpass=f=70")
    ataque = passa_alta(x, 400)          # o estalo do passo aparece melhor sem os graves
    env, n = envelope(ataque, 0.005)
    e = db(env)
    limiar = np.percentile(e, 60) + 9
    picos, ultimo = [], -1e9
    for i in range(1, len(e) - 1):
        t = i * n / SR
        if e[i] > limiar and e[i] >= e[i - 1] and e[i] >= e[i + 1] and t - ultimo > 0.42:
            # pega o máximo local nos próximos 60 ms
            j = i + int(np.argmax(e[i:i + 12]))
            picos.append(j * n / SR)
            ultimo = j * n / SR
    arquivos = []
    for k, t in enumerate(picos):
        # Recentra no golpe mais forte dos próximos 300 ms (um estalo menor pode vir antes do passo).
        i0, i1 = int(t * SR / n), int((t + 0.3) * SR / n)
        t = (i0 + int(np.argmax(env[i0:i1]))) * n / SR
        a, b = int((t - 0.05) * SR), int((t + 0.36) * SR)
        if a < 0 or b > len(x):
            continue
        s = normalizar(fades(x[a:b], 0.006, 0.14), -8)
        arquivos.append(os.path.basename(salvar(s, "Passos", f"passo_interno_{len(arquivos) + 1:02d}")))
    relatorio["passos"] = {"picos_s": [round(p, 2) for p in picos], "arquivos": arquivos}


# ------------------------------------------------------------------ portas
def porta(caminho, nome, inicio, fim, pico_db):
    x = ler(caminho, "afftdn=nr=14:nf=-50")
    s = x[int(inicio * SR):int(fim * SR)]
    s = normalizar(fades(s, 0.004, 0.35), pico_db)
    relatorio[nome] = {"inicio_s": inicio, "fim_s": fim, "arquivo": os.path.basename(salvar(s, "Portas", nome))}


# ------------------------------------------------------------------ gavetas
def gavetas(caminho):
    """
    Cada take da gravação é o trilho deslizando (um platô de ~1 s); alguns terminam com a batida de fechar.
    O deslizar soa igual nos dois sentidos, então:
      abrir  = só o deslizar, cortado na duração da animação da gaveta (~0,7 s);
      fechar = o fim do deslizar mais a batida, com a batida a 0,55 s (quando a gaveta chega ao fim).
    Os trechos foram escolhidos olhando a forma de onda; a batida exata é procurada em volta de cada tempo.
    """
    x = ler(caminho, "afftdn=nr=16:nf=-55,highpass=f=60")
    deslizes = [0.62, 3.00, 7.00, 21.05, 26.05, 30.00]       # começo de trechos de trilho limpos
    batidas = [4.00, 6.05, 13.15, 18.33, 34.15]              # batidas de fechar (aproximadas)
    abrir, fechar = [], []
    for t in deslizes:
        s = x[int(t * SR):int((t + 0.72) * SR)]
        abrir.append(normalizar(fades(s, 0.01, 0.22), -6))
    env, n = envelope(x, 0.002)
    for t in batidas:
        i0, i1 = int((t - 0.15) * SR / n), int((t + 0.15) * SR / n)
        golpe = (i0 + int(np.argmax(env[i0:i1]))) * n / SR
        s = x[int((golpe - 0.55) * SR):int((golpe + 0.4) * SR)]
        fechar.append(normalizar(fades(s, 0.02, 0.18), -3))
    relatorio["gavetas"] = {
        "abrir": [os.path.basename(salvar(s, "Gavetas", f"gaveta_abrir_{k + 1:02d}")) for k, s in enumerate(abrir)],
        "fechar": [os.path.basename(salvar(s, "Gavetas", f"gaveta_fechar_{k + 1:02d}")) for k, s in enumerate(fechar)],
    }


pasta = sys.argv[1]
passos(os.path.join(pasta, "caminhar dentro do necroterio.mp3"))
porta(os.path.join(pasta, "porta abrir.mp3"), "porta_abrir", 0.05, 1.55, -3)
porta(os.path.join(pasta, "porta fechar.mp3"), "porta_fechar", 0.50, 1.95, -2)
gavetas(os.path.join(pasta, "Gaveta mesa adm.mp3"))
print(json.dumps(relatorio, indent=1, ensure_ascii=False))
