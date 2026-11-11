# Câmara fria — revisão (2026-11-08/09)

O conjunto de gavetas provisórias deu lugar a um **módulo refrigerado 2 × 2 com compartimentos individuais (C-01..C-04)**.
Referências, medidas e decisões estão em [`referencias.md`](referencias.md).
Nada foi comitado: a confirmação com o José ainda está pendente.

## O que existe agora
- **Módulo** `CamaraMortuaria.prefab` (Blender `CamaraFria.blend`, gerado por `scripts/gerar.py`):
  - corpo de 2,33 × 1,69 × 2,33 m, com base, pés niveladores e divisória central;
  - 4 portas isoladas com dobradiça de pivô, trinco e porta-fichas;
  - 4 macas em trilho telescópico; o trilho intermediário anda à metade da velocidade (`SeguidorDeCurso`);
  - luz interna por compartimento (`LuzDeCompartimento`), que acende com a porta aberta.
- **Interação (E)** pelo `PecaDeMovel` existente:
  - a porta abre 95°;
  - a maca corre 1,00 m, que é a primeira parada do trilho de dois estágios;
  - a maca só sai com a porta aberta, e a porta não fecha sobre a maca estendida.
- **Ocupação** (P07/D02): C-01 tem João (081) coberto. C-02 tem Léo, o 083 verdadeiro, com o braço esquerdo para fora, a tala no antebraço e a pulseira 083. C-03 está vazio e interditado. C-04 tem Célia (082) coberta.
  - Prefabs `Ocupante_C-0N`, gerados por `scripts/ocupantes.py` a partir do MPFB (CC0).
  - Renders de estúdio em `Blender/CamaraFria/revisao/ocupantes/`.
- **Equipamentos**: quadro de ocupação, controlador digital (1,8 °C, SET 1,0), instalações frigoríficas com bucha na parede, unidade condensadora do lado de fora (parede leste) e ralo no piso.
- **Etiquetas e impressos** (`scripts/impressos.py`): placas C-0N, fichas das portas (só C-01 e C-04 preenchidas, como no quadro), aviso de interdição e pulseira.
- **Iluminação**: a aprovada foi mantida. As luminárias de teto passaram a usar o prefab de luminária do projeto, na mesma posição. A sonda de reflexo própria da sala ("Reflexo - Camara fria") é Custom, e o menu Update a refaz.

## Imagens
| Arquivo | Conteúdo |
|---|---|
| `capturas/antes_depois.jpg` | Mesmas 6 vistas antes (esquerda) e depois (direita) |
| `capturas/equipamentos.jpg` | Quadro, controlador, vista geral, topo e linhas, condensadora externa, ralo |
| `capturas/play_bandejas_abertas.jpg` | Play Mode com macas abertas: C-02 (tala e pulseira), C-03 vazio, C-04 coberto |
| `capturas/detalhes.jpg` | Lateral, trinco, dobradiça, etiquetas, junta com o piso, linhas no teto |
| `capturas/fichas.jpg` | Ficha da porta C-01 (081 / P24) e frente do módulo |
| `Blender/CamaraFria/revisao/estudio/`, `.../ocupantes/` | Renders de estúdio do módulo e dos ocupantes |

## Verificações executadas
**Play Mode**
- As 8 peças existem, estão destravadas e têm texto e curso corretos.
- As 4 portas (95°) e as 4 macas (1,00 m) abrem ao mesmo tempo, com a luz interna acesa.
- Uma porta que fecha sobre a maca estendida trava em ~86°.
- Uma maca empurrada com a porta fechada para em 0,07 m, quando o puxador encosta na porta.
- **Jogador na frente**:
  - a maca para em 0,25 m sem deslocar o jogador, e a porta para no jogador (~28°);
  - com o caminho livre, a maca abre até 1,00 m.
  - Antes da correção, o jogador era empurrado 0,74 m (ver abaixo).
- **Mira** (`MiraDeInteracao.Alvo`):
  - com a porta fechada, o alvo é a porta, nas 4 posições;
  - com a porta aberta, o alvo é a maca pelo puxador, nas 4 posições;
  - as macas de baixo são alcançadas a ~0,7 m de lado ou bem de frente. A 1,6 m de distância ficam fora do alcance de 1,8 m.
- **Circulação** (CapsuleCast r 0,35, tudo aberto): os caminhos porta da autópsia → frente do módulo (3 pontos), porta do recebimento → frente e recebimento → autópsia estão livres.

**Blender** (`ocupantes.py check`)
- Nenhum tecido fica fora dos trilhos.
- A cabeça fica coberta (C-01 0,986; C-02 e C-04 1,0).
- A pulseira e o topo da tala de C-02 ficam livres e saem com a maca a 1 m.

**Desempenho** (render da câmera do jogador a 1920×1080, ms por quadro, o melhor de várias rodadas)

| Vista | Antes | Depois |
|---|---|---|
| cf-entrada | 2,67 | 2,16 |
| cf-modulo | 2,73 | 1,97 |
| cf-porta-receb | 4,27 | 2,81 |
| autopsia-sul (controle, fora da sala) | 6,63 | 4,50 |

A medição "antes" rodou as 4 vistas numa só chamada. A "depois" teve de rodar uma vista por chamada, porque a chamada única passou do limite de 5 s do Editor. A vista de controle, que não mudou, também caiu (6,63 → 4,50 ms), então a queda vem do método e não é ganho real.

Em relação ao controle, a sala fica estável: cf-modulo vai de 0,41 para 0,44, cf-entrada de 0,40 para 0,48 e o recebimento de 0,64 para 0,62. Não há regressão relevante.

Malhas exportadas:
- C-01: lençol com 11,9 mil triângulos.
- C-02: corpo com 15,3 mil triângulos (reduzido de 26,8 mil, só o que se vê pela abertura), lençol com 12,4 mil, tala com 1,4 mil e pulseira com 0,8 mil.
- C-04: lençol com 11,2 mil triângulos.

## Correções durante a revisão
- **Inox esverdeado**: os painéis novos não eram ReflectionProbeStatic, e a sonda refletia as paredes antigas. As flags agora estão marcadas e a sonda é Custom. Textura e iluminação não mudaram.
- **Jogador empurrado pela maca/porta** (`Assets/Scripts/PecaDeMovel.cs`, compartilhado):
  - Causa: a busca pelo CharacterController só ampliava a caixa na vertical. A cápsula só era encontrada depois de já tocar a peça, e o controller se afastava dentro do `skinWidth` antes disso.
  - Agora a busca tem margem horizontal de 0,25 m e buffer próprio (o de 16 enchia com o corpo do móvel), e o teste de cilindro usa raio + `skinWidth`.
  - Isso vale para todas as gavetas e portas `PecaDeMovel` do jogo: elas param cerca de 8 cm antes, ao encostar no jogador.
- **Ficha da porta invisível**: ela ficava 1,5 mm atrás da face do porta-fichas. Agora fica à frente, e só C-01 e C-04 têm dados.
- **Luz interna** reduzida para 0,22 / alcance 1,1 m, porque estourava o interior das portas.
- **Colisor da maca** passou a incluir os puxadores, porque ela atravessava 0,10 m da porta fechada.

## Limitações conhecidas
- O porta-fichas é opaco (não simula policarbonato), então a ficha fica na frente dele.
- A maca de baixo exige se aproximar de lado, porque o puxador fica a 0,38 m do chão e o jogador não agacha.
- Não há áudio novo (compressor, trinco): fica para a etapa de áudio. Não há névoa nem gelo, de propósito.
- O trilho chega a 1,95 m no modelo, mas a jogabilidade usa só o primeiro estágio (1,00 m).

## Como atualizar
1. Blender:
   - `blender -b -P Blender/CamaraFria/scripts/gerar.py` (módulo e equipamentos);
   - `impressos.py` (etiquetas);
   - `ocupantes.py` (ocupantes).
2. Unity: `Tools/Unclaimed/Camara Fria/Build Prefabs` e depois `Update Camara Fria`. O Update instala a sala, remove o provisório e refaz a sonda.

## Quem fez
- **Claude**:
  - levantamento e referências;
  - os scripts Blender (módulo, impressos, ocupantes);
  - texturas;
  - a maior parte de `UnclaimedCamaraFriaAssets.cs` (ocupantes, equipamentos, sonda, colisores);
  - a cena, os prefabs e o Editor;
  - a correção do `PecaDeMovel`;
  - os testes em Play Mode, as capturas e o bench.
- **Codex** (via `delegar-codex`): `SeguidorDeCurso.cs`, `LuzDeCompartimento.cs`, a primeira versão de `UnclaimedCamaraFriaAssets.cs` e a mudança no gerador que instala a sala.
- **levantador**: não foi usado.

---

# Rodada 2 — composição e detalhamento (2026-11-09/10)

A sala passou de 4 para **7 compartimentos**: o 2 × 2 aprovado (C-01..C-04) ficou onde estava e ganhou ao lado uma **torre de 1 coluna × 3 níveis (C-05..C-07)**. Nada foi comitado.

## Distribuição
- Sala: 7,30 × 8,55 m, pé-direito 3,20 m. Portas: autópsia (parede oeste) e recebimento (parede norte). Nada disso mudou.
- Blockout pela câmera do Player com 7 e com 10 compartimentos (`capturas/rodada2_blockout.jpg`). Ficou a de 7: a de 10 domina a sala e deixaria sete posições "livres", diluindo a busca pela C-02 (P07).
- Banco na parede leste, frentes alinhadas em x = 12,565: 2 × 2 no canto sudeste, junta de 30 mm com tampa, torre (1,165 × 2,44 × 2,33 m) ao norte.
- A metade oeste continua livre: é a rota autópsia → recebimento e a área de manobra do carro.

## O que foi acrescentado
| Item | Onde | Função |
|---|---|---|
| Torre `CamaraMortuariaTorre.prefab` | parede leste, ao norte do 2 × 2 | 3 compartimentos com as mesmas portas, trilhos, macas, luz interna e interior (evaporador, dreno, caimento) |
| `CarroElevador.prefab` | estacionado na parede leste, ao norte | carro pantográfico de tesoura dupla com mesa de roletes; leva a bandeja aos três níveis (trilhos a 0,30 / 1,05 / 1,80 m). **Substitui a maca de transferência**, que não alcançava o nível de cima e repetia o equipamento do recebimento |
| `SuporteBandejas.prefab` | parede sul, lado oeste | estacionamento de bandejas removíveis: duas guardadas, um berço vazio |
| `PostoConferencia.prefab` | sob o quadro de ocupação | prateleira de escrita, prancheta com formulário em branco, caixa de etiquetas em branco |
| Instalações (`InstalacoesCamara`) | topo e parede | ramal frigorífico isolado até a torre, eletroduto do controlador por cima da torre, segundo ralo de piso, tampa-junta entre módulos, cantoneiras de arremate com a parede, sapatas de fixação ao piso |
| Protetores de parede | sul (entre o suporte e os módulos) e leste (atrás do carro, duas alturas) | só onde equipamento com rodízio corre junto da parede |
| Desgaste | portas e piso | polido de mão ao lado dos trincos (mais forte na altura do peito, independente de ocupação) e riscos finos no piso em frente aos módulos, no eixo das bandejas |

Mudaram de lugar, na mesma parede: controlador e quadro de ocupação (cerca de 1,4 m para o norte, porque a torre ocupa o lugar antigo); o protetor sul encurtou.

## Narrativa
- Ocupação inalterada: C-01 (081) e C-04 (082) cobertos, C-02 com Léo, C-03 interditada e vazia. **C-05, C-06 e C-07 estão vazias**, com ficha em branco, e aparecem como "livre" no quadro, igual à C-02.
- Nenhum corpo, nome, número, data ou pista novos. O formulário da prancheta traz só os códigos C-01..C-07 impressos; as etiquetas estão em branco.

## Imagens
| Arquivo | Conteúdo |
|---|---|
| `capturas/rodada2_antes_depois.jpg` | 8 vistas da câmera do Player, antes (esquerda) e depois (direita), mesmos enquadramentos |
| `capturas/rodada2_bandejas_abertas.jpg` | Play Mode com as 7 portas e 7 macas abertas |
| `capturas/rodada2_detalhes.jpg` | torre, junta e fixações, topo e linhas, carro, posto, suporte, piso, puxador |
| `capturas/rodada2_blockout.jpg` | blockout das duas distribuições testadas |
| `capturas/rodada2_estudio.jpg`, `Blender/CamaraFria/revisao/estudio2/` | renders de estúdio dos modelos novos |
| `mosaico_ref_rodada2.jpg` | fotos de referência (fabricante) |

## Verificações executadas
**Play Mode**
- 14 peças (7 portas, 7 macas): portas a 95°, macas a 1,00 m, luz interna acesa com a porta aberta.
- Maca com a porta fechada para em 0,065 m (as 7). Porta fechada sobre a maca estendida trava em ~86° (C-05 e C-06).
- Jogador na frente da torre: a porta para em ~14° e a maca em 0,45 m, sem deslocar o jogador; com o caminho livre, a maca completa 1,00 m.
- Mira (mesmo raio do `MiraDeInteracao`): as 7 portas são alvo de frente a 1,2 m; cada maca aberta é alcançável de 17 a 20 das posições livres testadas (C-02 e C-04, junto da parede sul, de 8/8).
- Circulação com tudo aberto (cápsula r 0,35): autópsia ↔ recebimento, as três frentes, posto, suporte e alça do carro livres a partir das duas portas. Entre duas macas vizinhas estendidas não se passa (vão de 0,55 m) — já era assim no 2 × 2.
- Console sem erros.

**Transferência com o carro** (envelope de 0,692 × 2,015 m)
- Encaixe diante de cada um dos 7 compartimentos, só com a porta alvo aberta: livre a 0,10 m da face nos níveis de cima (a porta fechada de baixo fica 7 cm saliente) e a 0,02 m nos de baixo.
- Trajeto estacionamento → área de manobra → giro de 90° → alinhamento e aproximação a cada coluna: livre. O suporte de bandejas foi deslocado duas vezes para o oeste porque bloqueava a coluna C-02/C-04.
- Geometria (Blender, `gerar.py -- --check`): a tesoura fecha nas três alturas (1,4° / 25,9° / 58,1° por estágio).

**Blender**: 5 assets novos ou alterados, 0 faces degeneradas; os 8 FBX aprovados, seus mapas e os impressos antigos ficaram idênticos (md5), exceto `QuadroOcupacao.png`.

## Limitações
- A bandeja da C-05 fica a 1,80 m, acima dos olhos do jogador (1,65 m): dá para abrir e puxar, mas não para ver o fundo dela.
- O carro elevador é cenário: não é interativo, e a transferência foi testada por geometria e colisão, não como mecânica.
- Os desgastes são muito discretos (diferença máxima de ~6 % no pixel).
- Desempenho não foi medido de novo; a sala ganhou cerca de 63 mil triângulos nos modelos estáticos (torre 15,7 mil; carro 18,6 mil; suporte 13,3 mil; posto 4,3 mil; instalações 11,4 mil), mais 3 portas, 3 macas e 3 luzes internas desligadas por padrão.
- O formulário e o rótulo usam o mesmo material opaco dos outros impressos.

## Como atualizar
1. `blender -b --factory-startup --python Blender/CamaraFria/scripts/gerar.py -- ModuloTorre InstalacoesCamara CarroElevador SuporteBandejas PostoConferencia` e `python3 Blender/CamaraFria/scripts/impressos.py`.
2. Unity: `Tools/Unclaimed/Camara Fria/Build Prefabs`, depois `Update Camara Fria` (só troca o grupo da sala e refaz a sonda; não regenera o mapa).
3. Cópias de segurança de antes desta rodada: `Backups/PrototipoNecroterio_antes_camara7_20261109.unity` e `Backups/Reflexo Camara fria_antes_camara7_20261109.exr`.

## Quem fez
- **Claude**: levantamento, referências, blockout e escolha da distribuição; `UnclaimedCamaraFriaAssets.cs` (torre, colisão genérica, equipamentos, desgastes, instalação na sala); ajuste da intensidade dos decalques em `impressos.py`; cena, prefabs e Editor; testes em Play Mode e de transferência; capturas.
- **Codex** (via `delegar-codex`): `gerar.py` (torre, instalações, carro, suporte, posto), `impressos.py` (placas, quadro, formulário, rótulo, decalques), `render.py`, bake e renders de estúdio (`Blender/CamaraFria/revisao/entrega2.md`).
- **levantador**: não foi usado.
