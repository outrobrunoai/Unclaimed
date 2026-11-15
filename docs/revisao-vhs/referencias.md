# Filtro VHS — referências

Data: 2026-11-15. Shader: `Assets/UnclaimedPrototype/Filtros/FiltroVHS.shader`. Controlador: `Assets/Scripts/Efeitos/FiltroVHS.cs`.

## O problema da primeira versão

A primeira versão tinha uma faixa cinza de ruído que subia pelo meio da tela o tempo todo. Em fita VHS de verdade, isso não acontece com a fita tocando normalmente. O ruído fixo fica no pé da imagem, e a faixa de rastreamento só aparece quando a fita perde o alinhamento.

## Como cada defeito aparece numa fita real

| Defeito | Onde e como aparece | Causa | Fonte |
|---|---|---|---|
| Ruído de troca de cabeça | Faixa fina e torta no pé da imagem, logo antes do sincronismo vertical. Fica escondida na TV, mas aparece em gravações digitalizadas sem corte, que é o caso do "found footage". Em fita ruim, ela pula para cima e pode atravessar a imagem. | As cabeças de leitura se alternam a cada campo. Servo instável ou bateria fraca na câmera pioram o efeito. | [AV Artifact Atlas: head switching noise](https://avartifactatlas.com/artifacts/head_switching_noise.html) |
| Erro de rastreamento | Uma faixa da imagem rasga para o lado. É ocasional, não constante. | As cabeças saem da trilha gravada. | [AV Artifact Atlas: tracking error](https://avartifactatlas.com/artifacts/tracking_error.html), [Made Good Designs: VHS Effect](https://madegooddesigns.com/tools/vhs-effect/) |
| Dropout | Risco branco curto e horizontal, que dura um ou dois quadros. | Perda de óxido na fita, sujeira ou cabeça entupida. | [The Pixel Farm: defeitos comuns de fita](https://www.thepixelfarm.co.uk/identifying-common-tape-defects) |
| Borrão de cor | A cor escorre para os lados, principalmente vermelhos e azuis. O brilho continua nítido. | A cor é gravada com muito menos banda que o brilho. | [VideoHelp Forum](https://forum.videohelp.com/showthread.php?p=2794101), [shaders.com: VHS](https://shaders.com/docs/components/vhs) |
| Tremor de linha | As linhas tremem alguns pixels, de forma irregular. | Instabilidade mecânica da fita. | [shaders.com: VHS](https://shaders.com/docs/components/vhs) |

Referências de implementação:

- [ntsc-rs](https://mltframework.org/plugins/FilterOpenfx-wtf-vala^ntscrs/): emulador de sinal analógico. O ruído de troca de cabeça tem altura de 8 linhas (cerca de 1,7% de uma imagem de 480 linhas), deslocamento horizontal de até 72 px e começa no meio da linha.
- [Godot: VHS Post Processing](https://godotshaders.com/shader/vhs-post-processing/): feito para um jogo de terror analógico. Usa uma faixa no pé da imagem com tremor próprio.
- [Lost Signal VHS](https://modrinth.com/shader/lost-signal-vhs): voltado a "found footage". Combina troca de cabeça, rasgos de rastreamento e dropouts.

## O que mudou no filtro

- **Removido:** a faixa cinza que subia pelo meio da tela.
- **Novo, sempre ligado:** ruído de troca de cabeça no pé da imagem. A faixa ocupa 1,6% da altura e a imagem dentro dela é empurrada para o lado (até 45 px, mais forte na borda de baixo), com um pouco de chuvisco por cima.
- **Novo, ocasional:** dropouts. Em poucos quadros aparecem um ou dois riscos claros curtos. Cada risco começa de repente e some aos poucos para a direita, como o sinal voltando. Ele sai quebrado em pedaços de cerca de 3 px e é translúcido, então parece falha da fita, não uma linha desenhada por cima.
- **Saturação:** subiu de 0,7 para 0,88. A cor ainda fica um pouco lavada, como em fita, mas sem parecer quase preto e branco.
- **Só nos sustos (`FiltroVHS.Falha`):** a faixa de troca de cabeça cresce e pula para cima, e uma faixa da imagem rasga para o lado em posição aleatória. Os dropouts também ficam mais frequentes.
- **Mantido:** lente levemente curva, vinheta, borrão de cor, aberração nas bordas, granulado, linhas de varredura e preto lavado.

Controles novos no material `FiltroVHS.mat`: altura e deslocamento do ruído de troca de cabeça, e frequência dos dropouts.

## Capturas

- `vhs_normal.png`: filtro em funcionamento normal.
- `vhs_falha.png`: pico de falha, com a faixa de rastreamento rasgando a imagem.
- `vhs_dropouts_simulacao.png`: o formato dos dropouts, recalculado fora do Unity sobre um fundo liso, em quatro quadros.
- `vhs_troca_de_cabeca_zoom.png`: dois quadros de falha e, embaixo, o pé da imagem ampliado com o ruído de troca de cabeça.
