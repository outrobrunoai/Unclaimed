# Sistemas do José — etapas 2 a 5 (entrega para o Bruno)

Data: 19/11/2026. Tudo é código, prefab ou dado do José. As cenas do mapa (`PrototipoNecroterio`, `main`) **não foram tocadas**: o Bruno coloca os prefabs abaixo no mapa, sem editar o prefab.

- Prefabs: `Assets/Prefabs/Sistemas/` (recriados por *Tools > Unclaimed > José > Criar prefabs de mecânica*).
- Cena de testes: `Assets/Scenes/Testes/Jose.unity` (uma sala de cada sistema, com NavMesh). Abra e dê Play: a Jogabilidade carrega junto.
- Jogabilidade: o Player ganhou o celular (com câmera e galeria) e o gravador; há um objeto **Sistemas** (diretor de Daniel, confronto, checkpoints) e um **Daniel (provisorio)** (cápsula, até o modelo do Bruno ficar pronto).
- Testes: 59 testes de modo de edição, um deles joga a noite inteira na cena de testes.

## Controles novos

| Tecla | O quê |
|---|---|
| T | Puxa o celular do bolso (modelo 3D, com animação) / guarda. **Não pausa o jogo**: com ele na mão o jogador anda (WASD) e o mouse mexe na tela do celular; Daniel continua agindo |
| No celular | Interface desenhada na própria tela do modelo: apps **Mensagens**, **Câmera**, **Fotos** e **Redação** (envio de provas). Tocar na barra de cima abre o painel rápido (lanterna) |
| Câmera do celular | O celular deita na frente do rosto ocupando uns 70% da tela; o visor mostra o mundo como uma janela. Clique ou F: foto. Q ou botão direito: volta aos apps. T: guarda |
| L | Lanterna do celular. Acesa, ao guardar o celular ele fica no cantinho de baixo com o LED para a frente. Força, alcance, abertura, cor e sombras ficam no componente **Celular** do Player (seção Lanterna) |
| G | Ouvir a gravação (depois de pegar o gravador) |
| Shift | Correr (faz barulho na perseguição) |

Sem dicas na tela sobre onde há sinal ou sobre a lanterna: o jogador descobre. Mensagem nova com o celular guardado só mostra "O celular vibrou."

O filtro VHS (do Bruno) é desligado só enquanto as câmeras do celular desenham (visor e interface); o filtro da câmera do jogador passa por cima de tudo, então o VHS continua, sem as bordas curvas dentro da tela do celular. O shader do filtro não foi alterado.

O modelo do celular é do José: `Blender/Sistemas/Celular/modelar_celular.py` gera `celular.blend` e `Assets/Prefabs/Sistemas/Celular/Celular.fbx` (rode com `blender -b -P modelar_celular.py`). Ícones e papel de parede: `python Blender/Sistemas/Celular/icones.py` (Pillow). O prefab `Celular na mao` é montado por *Criar prefabs de mecânica*.

## O que o Bruno coloca no mapa

### Etapa 2 — Investigação

| Prefab / componente | Onde | Configurar |
|---|---|---|
| `Alvo fotografico` | Na pessoa da mesa de autópsia, dentro do objeto "ocupado" do `EstadoMesaAutopsia` | `id = mesa_autopsia`, `idEvidencia = F01`, `centro` no tronco, `poses` = [braço pose A, braço pose B] (Daniel troca para a B quando o jogador sai) |
| `Alvo fotografico` | No corpo de Léo na gaveta C-02 | `id = gaveta_c02`, `idEvidencia = F02` |
| `Painel de energia` | Parede da sala técnica | Nada. Ele coleta D04 ao abrir |
| `Consumidor de energia` | Nas luzes | `circuito = geral` nas luzes do prédio; `exame` na luminária da autópsia (arraste a `LuminariaExame` em *luminarias*); `refrigeracao` nos compressores; `inverter` marcado nas luzes de emergência. **As luzes do circuito precisam ser Realtime ou Mixed**, senão o apagão não aparece |
| `ItemColetavel` | Fusível reserva no depósito | `id = fusivel_reserva` |
| `Documento legivel` | Para os documentos que ainda faltam, se não usarem o pipeline de documentos | `idEvidencia` e `descricao`. Os documentos gerados pelo pipeline do Bruno já servem (têm `idEvidencia`) |

D07 já está pronto: aplicativo **Controle de acesso** no computador da recepção (ícone na área de trabalho e no menu). Os registros estão em `Assets/Resources/Historia/registro_sensor.json`. O leitor de crachá do recebimento é só o objeto (já está com o Bruno).

### Etapa 3 — Celular e finais

**Colocado (2026-11-20)** por `Tools/Unclaimed/Sistemas/Colocar etapa 3`: zonas de sinal na recepção (2 barras) e no pátio do recebimento (3), saídas no portão de pedestres da frente (bloqueada pela corrente; a porta de vidro fica livre para o jardim) e no portão do pátio, e a introdução da chegada (`IntroChegada`).

| Prefab | Onde | Configurar |
|---|---|---|
| `Zona de sinal` | Recepção e pátio | Ajustar o BoxCollider para cobrir a área. `barras` de 1 a 4 |
| `Saida do predio` | No vão da porta da recepção, do lado de fora da folha, e no portão do pátio | Na recepção: `fatoQueBloqueia = corrente` |

Textos (do Bruno, **provisórios**, escritos a partir da história): `Assets/Resources/Historia/celular.json` (mensagens e respostas), `finais.json` (epílogos), `gravacao.json` (legendas da gravação). Pode reescrever à vontade, mantendo os `id` e os campos `quando`.

### Etapa 4 — Daniel atrapalha

**Colocada em 20/11/2026** por *Tools > Unclaimed > Sistemas > Colocar etapa 4 (Daniel atrapalha)* (`Assets/Editor/UnclaimedSistemasEtapa4.cs`): as 12 salas com vizinhas e portas, 13 pontos de ação, a torneira do lavatório da autópsia, o vulto atrás do visor do recebimento, a corrente nos puxadores da porta de vidro (por dentro), a trava dentro da câmara fria e a respiração do lençol (`RespiracaoDoLencol`). Mudanças em relação à lista abaixo: `WC/Espelho` virou `WC/Torneira` (um vulto no WC nunca seria visto, porque Daniel não age na sala à vista); as portas duplas usam `outrasPortas`; a corrente só é passada com o jogador dentro do prédio. `Corredor/Passos` está sem som até a etapa de sons.

**Lista de salas** (componente/prefab `Sala`, um BoxCollider trigger cobrindo cada sala; preencher `vizinhas` com a porta entre elas):
`Recepcao`, `WC`, `Arquivo`, `Descanso`, `Deposito`, `Corredor`, `CorredorFundos`, `Tecnica`, `Autopsia`, `CamaraFria`, `Recebimento`, `Patio`.

**Pontos de ação** (prefab `Ponto de acao`; o nome é `Sala/Coisa`, a sala é o texto antes da barra):

| id | tipo | Referências |
|---|---|---|
| `Autopsia/Torneira` | Presenca | `torneira` (aberta quando Daniel sai da mesa) |
| `WC/Descarga` | Presenca | `descarga` |
| `WC/Espelho` | Presenca | `mostrar` = vulto atrás do espelho |
| `Corredor/Passos` | Presenca | `som` + `clip` de passos |
| `Corredor/Porta` (e outras) | Presenca | `porta` (bate a porta) |
| `Recebimento/Visor` | Presenca | `mostrar` = vulto no visor da porta |
| `Autopsia/PortaCamara`, `Recebimento/PortaPatio`… | TrancarPorta | `porta`; `grupo = circuito` nas portas do circuito autópsia–câmara fria–recebimento (nunca ficam as duas trancadas) |
| `Tecnica/Fusivel` | Apagao | — (tira o fusível da luz geral) |
| `CamaraFria/Porta` | PrenderCamaraFria | `porta` frigorífica e `luzes` da câmara |
| `Recepcao/Corrente` | Corrente | `porta` da entrada e `mostrar` = corrente com cadeado |
| `Arquivo/Porta` | Aparicao | onde Daniel aparece no confronto |

Também: prefab `Trava de seguranca` do lado de dentro da câmara fria, perto da porta (só aparece quando a lanterna do celular a ilumina; o celular não tem flash).

### Etapa 5 — Perseguição

| Prefab | Onde | Configurar |
|---|---|---|
| `Gravador` | Lacrado dentro do envelope de guarda, no compartimento G-B2 (cadeado 4817 nas portas A2/B2; a combinação chega por mensagem do Celso em `con:venda`). Na inspeção do envelope: clique abre a aba e tira o gravador; clique de novo pega (`EnvelopeDoGravador`) | — |
| `Copia do audio` | Sobre o monitor do computador do arquivo | — |
| `Esconderijo` | Dentro dos armários do descanso e das gavetas vazias C-05 a C-07 | O collider fica no interior do móvel (o jogador abre a porta e mira dentro). Ajustar `Dentro` (pé do jogador escondido, olhando para a fresta) e `Saida` |
| NavMesh | Mapa inteiro | NavMeshSurface do pacote AI Navigation, cobrindo a rota arquivo → circulação → vestíbulo → recebimento → pátio e o circuito pela autópsia |
| Modelo de Daniel | Substituir a cápsula do `Daniel (provisorio)` na Jogabilidade | Pôr o modelo como filho, ligar em `corpo` e `animator` (parâmetro float `Velocidade`) |

## Fatos combinados (EstadoDaNoite)

Úteis para encenação, sons e textos:

| Fato | Quando |
|---|---|
| `luz_exame` | A luz de exame acendeu (P02) |
| `foto:<alvo>:<variante>`, `mudou:mesa_autopsia` | Fotos tiradas; as duas fotos da mesa são diferentes |
| `mesa_braco_mudou` | Daniel mexeu o braço |
| `daniel_saiu` | Daniel largou a mesa (P08). Fase Sombra |
| `con:troca` | Troca confirmada. Fase Obstrução |
| `apagao`, `apagao_resolvido`, `corrente`, `preso_camara_fria`, `escapou_camara_fria` | Sabotagens |
| `gravador_pego`, `gravacao_ouvida`, `audio_copiado` | Parte 4 |
| `confronto_feito`, `gravador_entregue`, `caca` | Escolha no confronto |
| `env:<id>`, `enviou_algo`, `ajuda_pedida`, `samuel:irene`, `samuel:jornal` | Celular |
| `cp:CP01`…`cp:CP04` | Checkpoints gravados |

## Checkpoints

CP01 ao pegar a chave da circulação; CP02 em `con:troca`; CP03 ao ler D09; CP04 ao pegar o gravador. A captura volta ao último.

## Sons

O `SonsDoMapa` se instala sozinho no Play e, a cada cena carregada, preenche os campos de som **vazios** dos
componentes do mapa. Não precisa mexer em prefab nem em cena; som posto à mão no Inspector tem prioridade.

- Arquivos: `Assets/Resources/Sons/<pasta>/<slot>_NN.wav`. Para trocar um som, substitua o .wav; para ter mais
  variações, acrescente outro número com o mesmo prefixo (ex.: `tecnica_fechou_03.wav`).
- Origem: gravações próprias (passos internos, porta comum, gaveta da mesa) e sons CC0 do Freesound, com autor e
  link em `Assets/Resources/Sons/creditos.json`. Os cortes saem de `Blender/Sistemas/Sons/montar_sons.py`.
- O tipo de porta vem do nome do prefab: vidro, frigorífica, técnica, vai e vem (clínica/serviço dupla) e comum.
  Os móveis seguem a mesma ideia: arquivo de aço, armário de pertences, câmara mortuária (portinha e maca),
  portinhas de madeira e gavetas de mesa.
- Passos: o piso interno é o padrão. Asfalto/calçada, grama e concreto/metal usam o nome do material do chão.
- Energia: zumbido nas luzes do circuito geral (apaga junto), compressor na câmara mortuária (circuito de
  refrigeração) e o som de queda quando a luz geral cai.
- Daniel: nos `PontoDeAcao` de presença sem torneira ou descarga tocam passos no corredor (ou a batida forte, se
  tiver porta); trancar porta = ferrolho; câmara fria = trava; corrente = corrente com cadeado.

## Pendências (precisam de decisão ou do outro lado)

- Encenação da mesa (lençol que respira quando o jogador não olha) é do Bruno.
- Três pistas antigas ("Pista - registro de entrada", "laudo da autopsia", "instrumento deslocado"): **decidido, saem do jogo.** Os papéis delas já estão com evidências novas: registro de entrada → D07 (sensor da porta) e D05; laudo → D06 e a foto F01; instrumento deslocado → ações do Daniel (pontos de ação da etapa 4). Não estão mais na cena PrototipoNecroterio.
- Ritmo: `DiretorDeDaniel.escalaDosIntervalos` (90 s / 60 s), `Confronto.espera` (25 s), `CopiaDoAudio.segundos` (40 s), velocidades de Daniel. Ajustar em playtest.
