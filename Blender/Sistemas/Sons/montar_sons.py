# Monta os sons do jogo a partir das gravações (numpy + o ffmpeg do imageio-ffmpeg).
#   python montar_sons.py "<pasta com os .mp3 do Freesound>" "<pasta com as gravações já tratadas do José>"
# Fontes: Freesound, só sons CC0 (domínio público); a lista com autor e link fica em
#   Assets/Resources/Sons/creditos.json. As gravações próprias (passos internos, porta comum, gaveta da mesa)
#   vêm do tratar_sons.py e só são copiadas.
# Saída: Assets/Resources/Sons/<pasta>/<slot>_NN.wav (48 kHz, mono, 16 bits). O SonsDoMapa carrega cada slot
#   pelo prefixo do nome, então variações novas entram só pondo outro arquivo com o mesmo prefixo.
#
# Os cortes foram escolhidos olhando a forma de onda e o espectro de cada gravação (não ouvindo):
#   "golpe" = procura o pico perto do tempo indicado e começa o corte logo antes dele (batidas tocadas
#   quando a porta chega ao batente); "trecho" = corta de/até.
import json
import os
import shutil
import subprocess
import sys
import wave

import imageio_ffmpeg
import numpy as np

FF = imageio_ffmpeg.get_ffmpeg_exe()
SR = 48000
AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.abspath(os.path.join(AQUI, "..", "..", ".."))
SAIDA = os.path.join(RAIZ, "Assets", "Resources", "Sons")
BAIXADOS, PROPRIOS = sys.argv[1], sys.argv[2]

_cache = {}


def ler(sid, filtro="afftdn=nr=10:nf=-60,highpass=f=50"):
    chave = (sid, filtro)
    if chave not in _cache:
        cmd = [FF, "-v", "error", "-i", os.path.join(BAIXADOS, f"{sid}.mp3")]
        if filtro:
            cmd += ["-af", filtro]
        cmd += ["-f", "f32le", "-ac", "1", "-ar", str(SR), "-"]
        _cache[chave] = np.frombuffer(subprocess.run(cmd, capture_output=True, check=True).stdout, dtype=np.float32).copy()
    return _cache[chave]


usados = {}
contagem = {}


def salvar(x, pasta, slot, sid):
    os.makedirs(os.path.join(SAIDA, pasta), exist_ok=True)
    contagem[(pasta, slot)] = contagem.get((pasta, slot), 0) + 1
    nome = f"{slot}_{contagem[(pasta, slot)]:02d}"
    with wave.open(os.path.join(SAIDA, pasta, nome + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype("<i2").tobytes())
    usados.setdefault(str(sid), []).append(f"{pasta}/{nome}")


def fades(x, entrada=0.004, saida=0.08):
    x = x.copy()
    a, b = min(len(x) // 2, int(SR * entrada)), min(len(x) // 2, int(SR * saida))
    if a:
        x[:a] *= np.linspace(0, 1, a)
    if b:
        x[-b:] *= np.linspace(1, 0, b) ** 2
    return x


def normalizar(x, pico_db):
    p = np.abs(x).max()
    return x * (10 ** (pico_db / 20) / p) if p > 0 else x


def envelope(x, janela=0.002):
    n = max(1, int(SR * janela))
    k = len(x) // n
    return np.sqrt((x[:k * n].reshape(k, n) ** 2).mean(axis=1)), n


def pico_em(x, t0, t1):
    env, n = envelope(x)
    i0, i1 = int(t0 * SR / n), max(int(t0 * SR / n) + 1, int(t1 * SR / n))
    return (i0 + int(np.argmax(env[i0:i1]))) * n / SR


def trecho(sid, pasta, slot, a, b, pico_db=-3, fi=0.006, fo=0.12, filtro=None):
    x = ler(sid) if filtro is None else ler(sid, filtro)
    salvar(normalizar(fades(x[int(a * SR):int(b * SR)], fi, fo), pico_db), pasta, slot, sid)


def golpe(sid, pasta, slot, t0, t1, antes=0.03, depois=0.8, pico_db=-2, fo=0.25):
    """Corta a partir de logo antes do maior pico entre t0 e t1 (batida no batente)."""
    x = ler(sid)
    t = pico_em(x, t0, t1)
    salvar(normalizar(fades(x[int((t - antes) * SR):int((t + depois) * SR)], 0.003, fo), pico_db), pasta, slot, sid)


def loop(sid, pasta, slot, a, b, pico_db=-10, cruz=1.0, filtro=None):
    """Trecho que emenda sem estalo: o fim é misturado ao começo (crossfade de potência constante)."""
    x = ler(sid) if filtro is None else ler(sid, filtro)
    s = x[int(a * SR):int(b * SR)].copy()
    c = int(cruz * SR)
    t = np.linspace(0, np.pi / 2, c)
    corpo = s[:-c].copy()
    corpo[:c] = s[-c:] * np.cos(t) + corpo[:c] * np.sin(t)
    salvar(normalizar(corpo, pico_db), pasta, slot, sid)


def passos(sid, pasta, slot, a=0.0, b=None, limiar_extra=9, intervalo=0.38, dur=0.36, maximo=12, pico_db=-8):
    """Separa cada passo de uma caminhada (procura o estalo do pé sem os graves) e salva até 'maximo'."""
    x = ler(sid, "afftdn=nr=16:nf=-50,highpass=f=70")
    x = x[int(a * SR):int((b or len(x) / SR) * SR)]
    alto = ler(sid, "afftdn=nr=16:nf=-50,highpass=f=400")[int(a * SR):int((b or len(x) / SR + a) * SR)]
    env, n = envelope(alto, 0.005)
    e = 20 * np.log10(np.maximum(env, 1e-9))
    limiar = np.percentile(e, 60) + limiar_extra
    ultimo, achados = -1e9, []
    for i in range(1, len(e) - 1):
        t = i * n / SR
        if e[i] > limiar and e[i] >= e[i - 1] and e[i] >= e[i + 1] and t - ultimo > intervalo:
            j = i + int(np.argmax(e[i:i + 12]))
            achados.append((j * n / SR, e[j]))
            ultimo = j * n / SR
    # Os mais fortes (e bem separados) primeiro, para fugir de passos abafados ou sobrepostos.
    achados.sort(key=lambda p: -p[1])
    for t, _ in achados[:maximo]:
        i0, i1 = int(t * SR), int((t + dur) * SR)
        if t < 0.05 or i1 > len(x):
            continue
        salvar(normalizar(fades(x[int((t - 0.04) * SR):i1], 0.005, 0.12), pico_db), pasta, slot, sid)


def copiar_proprios(prefixo, pasta, slot):
    for f in sorted(os.listdir(PROPRIOS)):
        if f.startswith(prefixo) and f.endswith(".wav"):
            os.makedirs(os.path.join(SAIDA, pasta), exist_ok=True)
            contagem[(pasta, slot)] = contagem.get((pasta, slot), 0) + 1
            nome = f"{slot}_{contagem[(pasta, slot)]:02d}"
            shutil.copy(os.path.join(PROPRIOS, f), os.path.join(SAIDA, pasta, nome + ".wav"))
            usados.setdefault("gravacao_propria", []).append(f"{pasta}/{nome}")


# ------------------------------------------------------------------ gravações próprias
copiar_proprios("passo_interno", "Passos", "interno")
copiar_proprios("porta_abrir", "Portas", "comum_abrir")
copiar_proprios("porta_fechar", "Portas", "comum_fechou")
copiar_proprios("gaveta_abrir", "Moveis", "gaveta_mesa_abrir")
copiar_proprios("gaveta_fechar", "Moveis", "gaveta_mesa_fechar")

# ------------------------------------------------------------------ passos
passos(459964, "Passos", "concreto", pico_db=-9)
passos(539556, "Passos", "asfalto_molhado", a=2, b=100, pico_db=-9)
passos(670305, "Passos", "grama_molhada", a=2, b=150, pico_db=-10, dur=0.42)
passos(698697, "Passos", "metal", pico_db=-8, maximo=8)
passos(349865, "Daniel", "passos", pico_db=-6, maximo=10, dur=0.42)   # sapato duro: os passos de Daniel

# ------------------------------------------------------------------ portas
# Técnica (metal pesado): rangido de abrir e batidas de metal grande.
trecho(383830, "Portas", "tecnica_abrir", 0.80, 3.05, -4)
golpe(383830, "Portas", "tecnica_fechou", 6.40, 7.40, depois=1.1)
golpe(814976, "Portas", "tecnica_fechou", 2.90, 4.30, depois=1.1)
# Frigorífica (câmara fria): estalo da vedação ao abrir (mais agudo) e o baque abafado ao fechar.
for t in (4.38, 11.90, 18.46):
    trecho(870662, "Portas", "frigorifica_abrir", t - 0.03, t + 1.4, -3)
for t in (0.01, 8.16, 14.76):
    golpe(870662, "Portas", "frigorifica_fechou", max(0.0, t - 0.05), t + 0.4, depois=0.9)
# Vidro com batente de alumínio (entrada).
trecho(736559, "Portas", "vidro_abrir", 0.10, 1.95, -4)
golpe(736559, "Portas", "vidro_fechou", 2.30, 2.95, depois=0.9)
golpe(125529, "Portas", "vidro_fechou", 11.85, 13.40, depois=0.9)
# Vai e vem (serviço e clínica): sem trinco, só o giro.
trecho(451932, "Portas", "vaivem_abrir", 8.15, 9.05, -4, fo=0.25)   # cabe nos 0,9 s da porta clínica
# Fechar: é um balanço sem batida (cresce até o fim), então toca no começo do giro e termina com a folha (0,9 s).
trecho(451932, "Portas", "vaivem_fechar", 10.25, 11.12, -6, fo=0.2)
# Gerais.
trecho(390328, "Portas", "destrancar", 3.35, 5.45, -3)
for t in (12.76, 14.02, 15.45, 16.67):
    trecho(391794, "Portas", "ferrolho", t - 0.35, t + 0.45, -3)
trecho(235473, "Portas", "trancada", 2.30, 3.25, -4)
trecho(235473, "Portas", "trancada", 3.80, 4.85, -4)
trecho(571797, "Portas", "batida_forte", 0.95, 2.50, -1)

# ------------------------------------------------------------------ móveis
# Arquivo de aço: trilho metálico; o fechar tem a batida a 0,55 s (fim da animação).
trecho(581404, "Moveis", "arquivo_abrir", 0.35, 1.65, -4)
trecho(581404, "Moveis", "arquivo_abrir", 5.15, 6.45, -4)
golpe(581404, "Moveis", "arquivo_fechar", 3.20, 4.30, antes=0.55, depois=0.45)
golpe(581404, "Moveis", "arquivo_fechar", 7.62, 9.00, antes=0.55, depois=0.45)
# Armário de pertences (locker de metal).
trecho(442370, "Moveis", "locker_abrir", 0.15, 1.65, -4)
golpe(442370, "Moveis", "locker_fechou", 4.95, 5.85, depois=0.7)
for t in (0.40, 4.22, 8.06):
    golpe(450792, "Moveis", "locker_fechou", t, t + 1.0, depois=0.7, pico_db=-4)
# Roupeiro e armário de apoio (madeira).
trecho(513341, "Moveis", "madeira_abrir", 0.40, 1.12, -4, fo=0.22)   # cabe nos 0,7 s da animação
golpe(513341, "Moveis", "madeira_fechou", 4.78, 5.15, depois=0.6)
golpe(513341, "Moveis", "madeira_fechou", 9.75, 10.90, depois=0.6)
# Câmara mortuária: portinha de cada gaveta (trinco + vedação) e a maca deslizando no trilho.
for t in (4.38, 11.90, 18.46):
    trecho(870662, "Moveis", "camara_porta_abrir", t - 0.03, t + 1.1, -4)
for t in (6.62, 19.19, 34.05, 47.10, 55.97):
    golpe(149853, "Moveis", "camara_porta_fechou", t - 0.1, t + 0.3, depois=0.8, pico_db=-3)
# Maca (1,8 s de animação): ao abrir, o trilho e o batente de fim de curso caindo a 1,65 s; ao fechar, só o
# deslizar (o PecaDeMovel troca pela batida "maca_fechou" quando ela chega ao fundo).
golpe(165110, "Moveis", "maca_abrir", 3.70, 4.55, antes=1.65, depois=0.3, pico_db=-3, fo=0.2)
trecho(165110, "Moveis", "maca_fechar", 7.00, 8.38, -3, fo=0.15)
golpe(165110, "Moveis", "maca_fechou", 8.40, 9.95, depois=0.7, pico_db=-3)

# ------------------------------------------------------------------ objetos
trecho(708668, "Objetos", "cadeado_disco", 1.86, 2.02, -8, fo=0.03)
trecho(678254, "Objetos", "cadeado_disco", 1.44, 1.60, -8, fo=0.03)
trecho(536414, "Objetos", "cadeado_abrir", 0.0, 0.25, -3, fo=0.05)
trecho(642877, "Objetos", "cadeado_erro", 0.0, 1.90, -5)
trecho(278205, "Objetos", "registro", 0.0, 0.60, -8)
loop(405231, "Objetos", "agua_loop", 4.0, 26.0, -8)
trecho(331897, "Objetos", "descarga", 0.30, 6.80, -3, fo=0.8)
trecho(267711, "Objetos", "pegar_chave", 0.0, 2.15, -4)
trecho(556261, "Objetos", "pegar_objeto", 0.02, 0.50, -6)
trecho(556261, "Objetos", "pegar_objeto", 2.07, 2.72, -6)
trecho(708668, "Objetos", "encaixe", 2.78, 3.10, -4, fo=0.05)
for sid in (130152, 130153, 130154):
    trecho(sid, "Energia", "disjuntor", 0.0, 0.75, -4, fo=0.06)
trecho(400194, "Energia", "desarme", 1.50, 3.35, -3, fo=0.4)
trecho(636578, "Energia", "apagao", 0.0, 3.90, -2, fo=0.8, filtro="highpass=f=40")
trecho(788042, "Energia", "chave", 1.85, 3.60, -6)
trecho(419023, "Energia", "recusado", 0.0, 0.31, -8, fo=0.04)
loop(119910, "Energia", "zumbido_loop", 2.0, 30.0, -20, filtro="highpass=f=50")
loop(370935, "Energia", "compressor_loop", 10.0, 40.0, -16, cruz=1.5, filtro="highpass=f=30")

# ------------------------------------------------------------------ celular
trecho(760369, "Celular", "mensagem", 0.0, 0.60, -8)
trecho(760370, "Celular", "enviado", 0.0, 0.60, -10)
trecho(476804, "Celular", "puxar", 0.95, 1.95, -14)
trecho(476804, "Celular", "guardar", 8.95, 10.65, -14)
trecho(187335, "Celular", "lanterna", 0.0, 0.56, -10, fo=0.05)
trecho(431588, "Celular", "obturador", 0.0, 0.55, -8, fo=0.05)

# ------------------------------------------------------------------ Daniel e perseguição
# Daniel andando no corredor (susto de presença): 7 passos dele emendados, ritmo de quem anda devagar.
_d = [f for f in sorted(os.listdir(os.path.join(SAIDA, "Daniel"))) if f.startswith("passos_") and f.endswith(".wav")]
_seq = np.zeros(int(SR * 5.2), dtype=np.float32)
for k in range(7):
    with wave.open(os.path.join(SAIDA, "Daniel", _d[k % len(_d)])) as w:
        p = np.frombuffer(w.readframes(w.getnframes()), "<i2").astype(np.float32) / 32768
    g = 0.55 + 0.45 * np.sin(np.pi * (k + 0.5) / 7)          # chega mais perto e se afasta
    i = int(SR * (0.15 + k * 0.68))
    _seq[i:i + len(p)] += p[:len(_seq) - i] * g
salvar(normalizar(fades(_seq, 0.01, 0.4), -4), "Daniel", "passos_corredor", 349865)
trecho(330635, "Daniel", "corrente", 0.55, 4.85, -3, fo=0.3)
trecho(734641, "Daniel", "trava", 0.0, 1.65, -4)
trecho(637481, "Daniel", "esconderijo_sair", 0.45, 2.45, -5)
golpe(442370, "Daniel", "esconderijo_entrar", 4.95, 5.85, depois=0.6, pico_db=-8)
trecho(556261, "Daniel", "gravador_pegar", 1.05, 1.50, -6)
loop(125400, "Daniel", "copia_loop", 0.0, 10.0, -16, cruz=0.4)
trecho(415762, "Daniel", "copia_pronto", 0.0, 1.38, -8)
trecho(879279, "Daniel", "captura", 0.0, 5.20, -1, fo=1.0)

# ------------------------------------------------------------------ créditos
cred = json.load(open(os.path.join(BAIXADOS, "creditos.json"), encoding="utf-8"))
saida = {"licenca": "Sons do Freesound marcados como CC0 (domínio público): uso livre, sem atribuição obrigatória.",
         "sons": {}}
for sid, arquivos in usados.items():
    if sid == "gravacao_propria":
        saida["sons"]["gravacoes_do_jose"] = {"arquivos": arquivos}
    else:
        c = cred[sid]
        saida["sons"][sid] = {"titulo": c["titulo"].replace("Freesound - ", ""), "url": c["url"], "licenca": "CC0", "arquivos": arquivos}
json.dump(saida, open(os.path.join(SAIDA, "creditos.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
for (pasta, slot), n in sorted(contagem.items()):
    print(f"{pasta}/{slot}: {n}")
