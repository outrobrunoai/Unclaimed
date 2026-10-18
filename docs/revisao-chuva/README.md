# Chuva leve — exterior da PrototipoNecroterio

17/10/2026. Chuva fina, contínua e quase vertical, sem tempestade, raios, neblina ou efeitos de tela.

- Vídeo pela câmera do Player (1280×720, 30 fps, 35 s, sem áudio): `video/percurso_camera_player_1280x720.mp4`. Percurso: recepção olhando pela porta de vidro → soleira → jardim sob o poste → chão e poça de perto → frente e lateral → volta para dentro → olha o exterior de dentro.
- Capturas: `antes/` e `depois/` (mesmas câmeras, 1280×720, Play Mode), `depois/painel.png`, `comparacao_antes_depois.png`.
- Mapa de abrigo visto de cima: `planejamento/mapa_abrigo_tipos.png` (cinza = duro, marrom = terra, verde = vegetação, vermelho escuro = cobertura; tons apagados = abrigado).
- Áudio: `audio/Chuva_leve_loop.wav` (24 s, estéreo, sintetizado por `Blender/Clima/scripts/audio_chuva.py`, sem licença de terceiros) e `audio/espectro.png`.

## Estrutura

Objeto raiz **Chuva leve** (fora de "Unclaimed - Necroterio (planta)", não é tocado por Rebuild Exterior nem pelo gerador):

| Filho | Função |
|---|---|
| `ChuvaLeve` (no raiz) | todos os parâmetros: intensidade, vento e variação lenta, largura/rastro/velocidade das gotas, respingos, ondulações, umidade, área, fade, poças, volumes do áudio |
| Gotas | ParticleSystem em espaço World, caixa 20 × 7 × 20 m centrada no jogador; gotas já emitidas não acompanham o jogador |
| Respingos / Ondulacoes | emitidos por script em pontos do mapa de abrigo (sem física) |
| Superficies molhadas | camada sobreposta só nos triângulos de piso duro externo (2238 tri) |
| Audio chuva | AudioSource 2D em loop + AudioLowPassFilter |

Abrigo: `Assets/UnclaimedPrototype/Clima/MapaAbrigoChuva.asset`, altura da superfície mais alta e tipo por célula de 0,125 m (688 × 656 células, 2 s de cálculo no Editor numa cena de pré‑visualização, ignorando colisores invisíveis e vegetação). O shader descarta por pixel qualquer gota abaixo dessa altura: não há chuva sob laje, platibanda, cobertura das vagas, guarita ou dentro do prédio, mas a chuva lá fora continua visível pela porta. Nenhuma consulta física em runtime.

Menus: Tools > Unclaimed > Chuva > Setup Rain Group / Bake Shelter Map / Build Wet Overlay. Depois de Rebuild Exterior, rode os dois últimos (o mapa e a camada molhada copiam a geometria).

## Configuração aprovada nesta rodada

- Intensidade 0,5 (taxa máx. 9000 gotas/s → 4500 vivas), largura 12 mm, rastro 0,38 m, queda 7 ± 0,8 m/s, vento (0,25; 0,08) m/s com oscilação Perlin 0,12 m/s, sem rajadas.
- Área: meia largura 10 m, altura 7 m, fade 6–9,5 m e perto da câmera 0,8–2,4 m.
- Iluminação das gotas: luz ambiente fraca + espalhamento frontal da lua e de todas as luzes adicionais (Forward+) com sombras — destacam-se sob o poste e a arandela. Nenhuma luz nova.
- Respingos: 5 impactos/m²/s num raio de 7 m; concreto 2–3 gotículas, terra 1 mais escura, grama raramente. Ondulações só nas 5 poças (14 anéis/m²/s).
- Umidade 0,65: escurecimento 0,14–0,24 com ruído, smoothness 0,35–0,62, reflexo do ambiente fraco; poças 0,45 de escurecimento e smoothness 0,93, borda irregular. Só cimentado, concreto, calçada, acesso de serviço e asfalto; grama, terra, paredes e metais intactos. Áreas abrigadas ficam secas, com transição suave. Nenhum material existente foi alterado.
- Áudio: volume 0,48 a céu aberto → 0,35 na soleira → 0,25 dentro da recepção; corte do passa‑baixas 13,6 kHz → 3,5 kHz → 1,1 kHz, suavizado em 0,8 s.

## Desempenho (Editor, Play Mode, OpenGLCore, RX 9070 XT, 1920×1080 renderizado pela câmera do Player, 120 quadros por medida, ON/OFF alternados duas vezes na mesma pose)

| Pose | Render OFF | Render ON | Δ | Quadro do Editor OFF → ON |
|---|---|---|---|---|
| Jardim sob o poste | 3,44–3,47 ms | 3,61–3,66 ms | +0,18 ms | 7,33–7,38 → 7,65–7,76 ms |
| Entrada e lateral | 5,77–5,78 ms | 5,88–5,90 ms | +0,12 ms | 12,58–12,60 → 12,90–13,06 ms |
| Recepção (dentro) | 4,09–4,12 ms | 4,16–4,18 ms | +0,06 ms | 8,51–8,60 → 8,71–8,75 ms |

ParticleSystem: ~0,05 ms de CPU (jobs de geometria). Sem colisão de partículas, sem luzes em partículas, sem sombras; uma transparência por gota.

## Verificações

- Compilação sem erros; Console sem erros novos; shaders sem mensagens.
- Validação numérica em Play Mode: nenhuma ondulação e nenhum respingo nasce em célula abrigada; partículas abaixo da altura de abrigo são descartadas pelo shader (soleira e interior testados).
- Revisão visual das capturas e do vídeo: sem chuva na recepção; chuva visível pela porta; transição na soleira sem corte brusco.
- Cena salva fora do Play Mode; Player, portas, puzzle, espelho e layout não alterados; sem bake, sem regeneração; 3 lightmaps preservados.

## Limitações

- Vídeo e capturas são renders da câmera do Player com o Editor fora de foco; o vídeo não tem áudio (o Unity Editor não grava o mixer por este caminho).
- Árvores não abrigam a chuva (atravessa a copa). Abrigo é um mapa 2D: uma superfície sob outra mais alta não recebe chuva (ok para este mapa).
- O áudio é sintetizado; pode ser trocado por gravação real CC0 no mesmo `AudioSource`. WC e outros cômodos internos usam o mesmo mapa (toda a área coberta), mas só a recepção foi percorrida no vídeo.
- Ferramenta de revisão `Assets/Scripts/Clima/RevisaoChuva.cs` (só no Editor) refaz vídeo e medições.

## Calibração visual (18/10/2026)

Pastas: `calibracao/` — vídeo `percurso_calibrado_1280x720.mp4`, `comparacao_movimento_lado_a_lado.mp4` (antes | depois, mesmo percurso e quadros), `comparacao_*.png` (antes à esquerda, mesmas câmeras) e `depois/`.

- Rastros próximos: até 0,8 m somem; entre 0,8 e 3 m são desenhados até 2,2× mais finos, ~45% mais curtos e mais fracos (`_AfinarPerto` em Gotas.mat). Densidade, área e brilho sob as luminárias inalterados; além de 3 m nada muda.
- Ondulações: anel fino (≈1/2 da espessura), expansão desacelerada, entrada suave e saída longa; brilho 10→6, opacidade 0,6→0,35; vida 0,45–1,05 s e tamanho 0,12–0,26 m variados.
- Manchas claras no piso: eram o brilho especular da lua sobre a camada molhada recortado pelas sombras da copa (a borda muito ruidosa das poças também fragmentava cada poça em "nuvens"). Especular da luz principal na camada molhada reduzido a 0,2 (`_BrilhoLua`); reflexos das luminárias e do ambiente mantidos; poças com contorno coeso e linha d'água mais nítida, reflexo do ambiente na poça 0,6 (`_ReflexoPoca`).
- Sem mudança em abrigo, áudio, iluminação, materiais internos, densidade ou mapa. Sem bake. Cena salva.

## Transição do áudio interior/exterior (18/10/2026)

Causa: o abafamento vinha só da "exposição" média de 17 pontos em anéis de 3 m e 8 m ao redor da câmera (cobertura acima de cada ponto). Do lado de fora, perto da fachada, metade dos pontos caía sob o telhado: na soleira externa o som já estava em 0,35 de volume e 3,5 kHz de corte, e o jardim a 3–4 m da porta já perdia agudos.

Ajuste: o mapa de abrigo ganhou `profundidadeInterna` — distância percorrida por dentro desde a abertura, só em células de piso interno ("Pisos") cobertas, com paredes até o chão bloqueando (`audio/profundidade_interna.png`; pátio de serviço aberto fica como exterior). Fora do prédio, inclusive sob beirais, o áudio é sempre o externo. O abafamento começa 15 cm após o plano da porta (zona morta contra oscilação na soleira), completa em `faixaInterna` = 1,2 m e então usa a mesma exposição em anéis de antes — timbres e volumes internos aprovados mantidos (recepção 0,245 / 1103 Hz). Posição avaliada pela câmera do Player a cada quadro, suavização 0,15 s; a exposição em anéis segue com 0,8 s. Alturas, tipos e exposição do mapa ficaram idênticos (hash conferido); chuva visual inalterada.

Testes em Play Mode (`audio/trilha_transicao_*.txt`): entrar, sair, parado na soleira com balanço de 3 cm, rente à fachada a 0,5 m da porta e porta dupla do recebimento (pátio). Fora: 0,492 / 16 kHz constantes; ao sair, externo pleno a ~0,3 m do plano.

## Modelo de áudio com portal — entrada principal (18/10/2026, aguardando escuta)

Pasta `audio-portal/`: `demo_entrada_principal_com_audio.mp4` (90 s, câmera do Player + áudio do motor capturado com AudioRenderer, 48 kHz estéreo), `trecho_curto_fora_soleira_dentro.mp4` (primeiros 36 s), `demo_audio_motor_48k.wav`, `roteiro_tempos.txt`, `analise_sinal.txt`.

Camadas (mesmo loop, deslocamentos de 0, 1/3 e 2/3 do clip, sem fase sincronizada; ganhos complementares em potência):
1. Ambiência externa 2D (som aprovado, sem filtro): volume × √(1 − dentro).
2. Ambiência interna 2D, passa-baixas FIXO 1100 Hz: volume 0,4 × dentro (±15% pela exposição).
3. Portal "Entrada principal" (fonte 3D, atenuação feita pelo script): ganho × √dentro × atenuação pela distância percorrida por dentro (campo geodésico só por células internas — paredes bloqueiam) × abertura das folhas (fechada: 0,25 e corte 1400 Hz, do vidro). Com visada, a fonte fica no ponto do vão mais próximo; sem visada, na direção do caminho (porta do cômodo). Espalhamento 140°→40° com a distância. Coloração lenta (0,6 s) pela distância (22 kHz → 6 kHz em 8 m) e 3 kHz sem visada.
`dentro` = profundidade interna (zona morta 0,15 m, faixa 1,6 m), por quadro; ganhos suavizados em 0,08 s.

Roteiro: fora 2,6 m → fora 0,3 m → soleira → 1 m dentro → 3,2 m dentro (de frente e de costas) → 3,6 m lateral → porta fechada/reaberta com o jogador parado → WC → fora. Leitura do sinal (não substitui a escuta): fora, soleira e 1 m dentro com o mesmo nível/brilho (≈ −27 dB, 4,6 kHz); 3,2 m: −7 dB; lateral: −5,3 dB L/R para o lado da porta; porta fechada: −12 dB e 0,9 kHz; WC: −12 dB e 2,8 kHz. Saltos espectrais só ao fechar/abrir a porta.

Pendente: escuta do usuário. A porta dupla do recebimento (pátio) ainda não tem portal — ao entrar por ela há só a troca externa/interna, sem varredura; aplicar o portal depois da validação.

## Correção — porta de vidro fechada soando aberta (18/10/2026)

Pasta `audio-portal/porta-fechada/`: `porta_fechada_corrigida.mp4` (39 s, câmera do Player + áudio do motor), `roteiro_tempos.txt`, `analise_sinal.txt`, `sonda_antes.txt` / `sonda_depois.txt` (ganho e corte de cada camada por posição).

Camada responsável: **ambiência externa 2D**. Causa: `dentro` vinha só da profundidade interna medida a partir do vão, sem olhar o estado das folhas. Rente ao vidro fechado (0,43 m) a profundidade quase não passa da zona morta, então a ambiência externa seguia em 0,461 (fora: 0,492), ou seja, o som da porta aberta. O portal 3D estava correto (0,043, 1400 Hz, fonte no plano do vidro, do lado certo), e a ambiência interna estava quase zerada.

Correção: perto da entrada (±1 m além da largura do vão, 2,5 m do plano), o lado vem do plano real da porta. Do lado de dentro, `dentro` = lerp(1, profundidade, abertura das folhas): com as folhas fechadas a câmera está totalmente dentro, e só a parte aberta deixa entrar o externo. Cada folha conta separadamente: a fonte do portal fica no trecho aberto mais próximo e a transmissão é a média por folha (fechada 0,25, 1400 Hz). Longe da porta nada mudou.

Sonda (camadas: externa / interna / portal):
- Fechada, dentro a 1 m, 0,43 m e ao longo da junta: 0 / 0,24 / 0,12 a 1400 Hz (antes: 0,46 / 0,03 / 0,04).
- Fora, nas mesmas posições: 0,492 / 0 / 0, sem mudança.
- Ambas abertas: a 1,2 m dá 0,24 / 0,18 / 0,43 a 22 kHz; a 0,43 m dá 0,46 / – / 0,17, igual ao modelo anterior.
- Só a folha esquerda aberta: 0,33 / – / 0,23 a 22 kHz, com a fonte na metade aberta.

No vídeo (`analise_sinal.txt`), dentro com a porta fechada fica em −38 dB e cerca de 920 Hz em todas as distâncias e ao longo da junta. Com ambas as folhas abertas: −28 dB, 4,5 kHz. Com só uma aberta: −32 dB, 4,5 kHz. Fora: −27 dB, 4,6 kHz. Saltos espectrais aparecem só ao abrir ou fechar e no corte de câmera de dentro para fora.

Alcance: com o CharacterController real, a câmera para a 0,43 m do vidro por dentro e a 0,42 m por fora, e não atravessa o vidro (near clip 0,01).

Não foram alterados: CharacterController, outras entradas, chuva visual, iluminação e puzzle. Sem bake.
