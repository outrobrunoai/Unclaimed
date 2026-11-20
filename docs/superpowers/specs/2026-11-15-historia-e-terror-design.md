# Unclaimed — história, puzzles e terror da demo (design)

Data: 2026-11-15. Base: `História_Template/` (dois .docx), planta `Assets/Planta/planta_necroterio.png`, cena `Assets/Scenes/PrototipoNecroterio.unity`.

## 1. Intenção

Jogo de investigação em primeira pessoa com picos de terror. O jornalista entra no necrotério atrás de uma matéria sobre três mortes. Daniel Vilar está escondido no prédio e passa a noite atrapalhando e assustando o jogador para que ele desista antes de achar o gravador de Léo.

**Decidido pelo autor:** Daniel sabota a noite toda e vira ameaça real só no final, depois que o jogador pega o gravador (fuga curta, esconderijo, chance de falhar).

### Objetivos
- A investigação do template (083, gravador, Vera e a 784, caso 041) continua sendo o centro.
- Toda sabotagem de Daniel tem saída para o jogador. Nenhuma trava a progressão.
- O medo vem de um homem real com um motivo claro. Nada sobrenatural.
- Duração-alvo da demo: 45 a 60 minutos (confirmar no primeiro playtest completo).

### Fora do escopo
- Combate. O jornalista não revida.
- Perseguição livre antes do gravador.
- O lugar onde Daniel guarda os corpos (a demo só mostra a foto da estante).
- Chegada da ajuda em tempo real (fica no epílogo).

### O que seria um fracasso
- O jogador não conseguir ler documentos em paz. A leitura pausa Daniel.
- O jogador perceber cedo que nada o machuca e parar de sentir medo.
- Sustos aleatórios, sem ligação com o que Daniel quer.

## 2. Caminhos considerados

1. **Daniel só sabota e assusta, sem game over.** Barato, mas o medo acaba quando o jogador nota que não há risco.
2. **Sabota a noite toda e caça no final.** Escolhido pelo autor. O terror cresce com a investigação e a IA de perseguição fica restrita a um trecho.
3. **Perseguidor livre desde cedo.** Exige IA completa de patrulha e atrapalha a leitura.

Para a implementação de Daniel nas fases de sabotagem, a escolha (minha) é um **diretor por estados e gatilhos**, sem IA de navegação: Daniel ocupa uma sala num grafo abstrato do prédio e só age em salas que o jogador não está vendo. A navegação real (NavMesh) entra apenas na caça.

## 3. Pontos de melhoria na história

| # | No template | Proposta | Por quê |
|---|---|---|---|
| 1 | Daniel fica na mesa, sai uma vez e reaparece no fim. | Daniel age a noite inteira, em quatro fases (seção 5). | É a vibe pedida. |
| 2 | Não fica claro por que Daniel não ataca logo nem vai embora. | Ele não quer uma quarta morte que não consiga disfarçar: Celso sabe que o jornalista entrou e a redação espera retorno. Ele precisa do gravador e quer que o visitante saia de mãos vazias. Só muda de ideia quando vê o aparelho com o jogador. | Dá lógica à sabotagem e à virada para a caça. |
| 3 | Não fica claro por que Daniel desvia corpos. | Daniel vende os corpos desviados a um comprador clandestino, que revende para cursos de anatomia particulares e para retirada de tecidos. Ele escolhe corpos que ninguém reclama, por isso ninguém percebe. Vera Moura foi o erro: tinha uma filha, Bia, procurando por ela. | Dá um motivo concreto e liga a trama ao nome do jogo. |
| 4 | O mercado precisa aparecer em prova, não só em diálogo. | A pasta de Léo traz as mensagens que João copiou do celular de Daniel, com o pedido do comprador (D15). A foto de Célia mostra o galpão da Limiar com uma caixa marcada "lote 784" e o chaveiro de Vera (D12). Na gravação, Daniel diz que alguém paga por esses corpos (D13). | O jogador prova a venda com as próprias mãos. |
| 5 | A câmera só registra. | A câmera registra o que muda durante a noite: o braço na mesa em outra posição, depois a mesa vazia. Daniel não destrói provas nem mexe nas coisas do jogador; ele só atrapalha o caminho. | Mantém a foto útil sem tirar o controle do jogador. |
| 5b | Daniel assina a própria entrada mesmo sabendo que não deveria estar ali. | A porta do recebimento tem um sensor com leitor de crachá. Ele registra sozinho a entrada no computador da recepção: crachá LV-12, 23h04, sem saída. Daniel pretendia apagar o registro depois de pegar o gravador, mas o jornalista chegou pela recepção e ele não teve tempo. | Pedido do autor. Também avisa o jogador que Daniel ainda está no prédio. |
| 6 | O caso 041 não tem ligação com Daniel. | Ponte opcional: o transporte do 041 em 2024 foi feito pela Limiar, com LV-12 no recibo. A pressa de Helena foi a brecha que mostrou a Daniel que ninguém conferia as saídas. Os dois casos continuam resolvíveis em separado. | Hoje a trama paralela compete com a principal. Com a ponte, ela a explica. |
| 7 | São doze nomes para uma demo curta. | Apresentar em camadas: no ato 1 só Léo, Celso, Nina e Mauro. Vera, João, Célia e Daniel no ato 3. Helena, Irene e Samuel ficam inteiros nas salas calmas (descanso, arquivo) e são opcionais. | Evita confusão no começo. |
| 8 | O jogador pode simplesmente sair ou ligar para a polícia. | Chove forte: o sinal só funciona perto da recepção e do pátio, e enviar arquivos leva tempo. Daniel passa a corrente na entrada principal no ato 3. Sair cedo continua possível e dá o epílogo parcial. | Mantém a liberdade do template e dá custo às escolhas. |
| 9 | A troca na mesa pode parecer forçada. | Reforçar nos documentos: a porta da autópsia tem visor, o quadro manda procurar o 083 ali, e uma mesa vazia chamaria mais atenção que uma ocupada. | A cena é a melhor imagem do jogo; só precisa de apoio. |
| 10 | Pequenas inconsistências. | "Nina Celso e Irene" é título de seção, mas lê como nome; Nina é Rocha. Fixar a data em 18/11/2026 em todos os papéis. | Limpeza. |

Itens 3, 4, 6 e 8 são propostas minhas e mudam conteúdo. O item 5b e as regras de Daniel do item 5 foram pedidos pelo autor. Os demais só organizam o que já estava no template.

## 4. A noite em quatro atos

| Ato | O jogador | Daniel | Checkpoint |
|---|---|---|---|
| **1. O 083** | Entra pela recepção (P00), lê a ficha, fotografa a pessoa na mesa (P01), lê os quadros do recebimento. | **Fase Mesa.** Imóvel sob o lençol. Sinais discretos: o lençol respira quando o jogador não olha de frente; duas fotos tiradas com intervalo mostram o braço em posições diferentes. | CP01 ao abrir a circulação |
| **2. Dois corpos, um número** | Resolve a luz (P02), compara a tala (P03), acha Léo em C-02 (P07), volta e encontra a mesa vazia (P08). | **Fase Sombra.** Sai da mesa e procura o gravador. O jogador acha armários revirados, ouve passos e portas, vê um vulto no visor de uma porta ou no espelho do WC. | CP02 ao confirmar a troca |
| **3. Ele sabe que você sabe** | Bolsa P26 (P04), registro do sensor no PC da recepção (P05), pasta da Limiar e mensagens do comprador (P09), caso 041 opcional (O01, O02). | **Fase Obstrução.** Apagão, portas travadas por dentro, armadilha na câmara fria, corrente na entrada. | CP03 antes de abrir o armário de pertences |
| **4. Deixa o gravador** | Abre G-B2 (P06), ouve e copia o áudio, envia o que tem, foge (P10). | **Fase Caça.** Perseguição real até a saída. | CP04 ao pegar o gravador |

O jogador pode achar pistas fora de ordem, como no template. As fases de Daniel avançam por fatos, não por tempo: Sombra começa quando ele abandona a mesa; Obstrução quando o jogador tem duas provas da troca (foto da mesa + C-02, ou observação direta + C-02); Caça quando o jogador pega o gravador.

## 5. Daniel

### Regras
- Nas fases Sombra e Obstrução ele nunca toca no jogador.
- Ele não destrói, esconde nem altera provas, e não mexe nas coisas do jogador (decidido pelo autor). As sabotagens atrapalham o caminho: luz, portas e saídas.
- Só age em sala que o jogador não está vendo (sala atual e salas visíveis por porta aberta ficam protegidas).
- Não age enquanto o jogador lê um documento, usa o PC ou ouve o gravador. Na caça, a leitura fica bloqueada.
- Intervalo mínimo entre ações (valor inicial: 90 s na Sombra, 60 s na Obstrução; ajustar em playtest).
- Cada ação escolhe o alvo pelo que o jogador está perto de descobrir.
- Estado salvo: fase, sala atual dele, sabotagens já usadas e resolvidas.

### Sabotagens

| Cód. | O que ele faz | Quando | Como o jogador contorna | Reaproveita |
|---|---|---|---|---|
| S1 | **Apagão.** Tira o fusível da luz geral na sala técnica. Ficam só as luzes de emergência. | Obstrução, com o jogador longe da técnica. | Lanterna do celular como luz; fusível reserva no depósito; refazer o painel de P02. | Painel de P02 |
| S2 | **Porta travada.** Passa o ferrolho pelo outro lado. | Sombra e Obstrução, uma porta por vez, nunca as duas pontas do circuito. | Dar a volta pelo circuito autópsia – câmara fria – recebimento – vestíbulo. | `FerrolhoDePorta` |
| S3 | **Preso na câmara fria.** Fecha a porta frigorífica e corta a luz com o jogador dentro. | Obstrução, uma vez, depois de P07. | Trava de segurança interna, achada com a lanterna do celular. A outra porta (recebimento) abre em seguida. | Portas frigoríficas, `LuzDeCompartimento` |
| S4 | **Presença.** Torneira aberta na autópsia, descarga no WC, passos no corredor, porta batendo, vulto no visor ou no espelho. | Sombra e Obstrução. | Nada a resolver. São sustos de baixa intensidade que mostram onde ele está. | `Torneira`, `Descarga`, `EspelhoPlanar`, `PassosDoJogador` |
| S5 | **Corrente na entrada.** Passa corrente e cadeado na porta principal. | Início da Obstrução. | Saída pelo pátio; ou achar a chave no molho que ele deixa cair no encontro. | `CadeadoCombinacao` (visual) |

Removidas a pedido do autor em 2026-11-15: arrancar o registro da entrada e colar etiquetas nas coisas do jornalista.

### Caça (fase 4)
- Começa quando o jogador pega o gravador em G-B2. Daniel aparece na porta do arquivo depois de alguns segundos.
- Daniel usa NavMesh e persegue por visão e som (correr faz barulho, andar agachado não).
- **Esconderijos:** armários do descanso e os compartimentos vazios C-05 a C-07 da câmara fria. Entrar numa gaveta espelha o que Daniel fez na mesa.
- **Antes da caça:** Daniel aparece na porta do arquivo e diz "Deixa o gravador. Pode ir." Se o jogador deixa o gravador, Daniel o pega e não persegue. Se o jogador fica com o gravador, a caça começa.
- **Captura:** fim de jogo imediato, recarrega CP04 (decidido pelo autor em 2026-11-15).
- **Copiar o áudio** no PC do arquivo leva cerca de 40 s reais, com Daniel se aproximando. O jogador decide se copia antes de fugir.
- **Rota:** arquivo → circulação → vestíbulo → recebimento → pátio, com o circuito pela autópsia e câmara fria como alternativa. Se o jogador preparou o portão do pátio antes, a fuga encurta.

## 6. Puzzles

Os textos de D01 a D14 e H01 a H07 ficam como no template, salvo as mudanças indicadas.

| Cód. | Puzzle | Onde | Estado | Mudança em relação ao template |
|---|---|---|---|---|
| P00 | Acesso à circulação | Recepção | **Já implementado** | Nenhuma. |
| P01 | Destino previsto e primeira foto | Recepção, recebimento, autópsia | Novo | A segunda foto da mesa pode mostrar o braço em outra posição. |
| P02 | Luz de exame (limite 10: refrigeração 6, geral 3, exame 4) | Sala técnica | Novo | Daniel volta a mexer nele em S1. |
| P03 | A tala (foto da mesa × D05/D06) | Caderno | Novo | Nenhuma. |
| P04 | Bolsa P26 | Arquivo (pertences) | Novo | O armário aparece revirado por Daniel. |
| P05 | Entrada de Vilar (D07 × D08 × D14) | Recepção (computador) e recebimento (sensor da porta) | Novo | D07 deixa de ser papel assinado: é o registro automático do sensor, lido no computador da recepção. Mostra a entrada às 23h04 e nenhuma saída. |
| P06 | Gravador em G-B2 | Arquivo (pertences) | Novo | O compartimento de guarda tem cadeado de combinação (4817). Por procedimento, Nina anota a combinação no livro de guarda, que fica com o vigia na guarita; a legenda do armário avisa isso. Celso manda a combinação pelo celular quando o jogador conclui a venda (`con:venda`), então o gravador só fica ao alcance no fim do ato 3. Explica também por que Daniel ainda não o pegou. |
| P07 | Léo em C-02 | Câmara fria | Novo | Depois dele vem S4. |
| P08 | A mesa vazia | Autópsia | Novo (há `EstadoMesaAutopsia` com os dois estados) | Marca a passagem para a fase Sombra. |
| P09 | Vera Moura e a 784 | Arquivo | Novo | Inclui as mensagens do comprador (D15 × D11): o corpo de Vera foi vendido. |
| P10 | A saída | Pátio ou recepção | Novo | Passa a ser a caça. |
| O01 | Localizar Samuel (H01 + H03) | Descanso, arquivo | Novo, opcional | Recibo de transporte da Limiar com LV-12 (ponte, melhoria 6). |
| O02 | Rever a apuração (H04 × H07) | Arquivo | Novo, opcional | H07 fica na estante da coordenação, no arquivo, e não na sala técnica: os papéis de Helena ficam nas salas calmas (ver melhoria 7). |

## 7. Mecânicas a construir

| Mecânica | O que faz | Já existe |
|---|---|---|
| **Estado da noite** | Registro central de fatos (evidências, conclusões, fase de Daniel), salvo. | `Salvamento`, `ISalvavel` |
| **Câmera e álbum** | Alvos amplos com fotos predefinidas. Sem flash, e sem foto com a lanterna acesa: a foto usa a luz do lugar (ex.: a luminária de exame para a F01). | `Interacao`, `MiraDeInteracao` |
| **Caderno** | Evidências, transcrições e comparações de duas peças que geram hipótese ou conclusão. | `DiarioDePistas` |
| **Celular** | Mensagens (Celso, redação, Irene), envio de provas com sinal só na recepção e no pátio, pedido de ajuda. | UI do `ComputadorRecepcao` como referência |
| **Painel de energia** | Interação curta de soma até o limite; estado da luz por sala. | `LuminariaExame` |
| **Compartimentos com conteúdo** | C-01, C-02, C-04 ocupados; C-03 interditada; C-05 a C-07 vazios e usáveis como esconderijo. | `PecaDeMovel`, `LuzDeCompartimento` |
| **Gravador** | Áudio com legenda (Voz 1 e Voz 2), cópia no PC. | `ComputadorRecepcao` |
| **Diretor de Daniel** | Fases, grafo de salas, escolha e execução das sabotagens. | — |
| **Caça** | NavMesh, percepção, esconderijos, confronto. | — |
| **Epílogos** | Texto final montado com o que saiu do prédio. | Menus |

## 8. Finais

O epílogo combina três eixos, como no template:
- **Provas:** nada enviado / envio parcial / conjunto da 784 enviado (a redação encontra Bia; a ampliação da foto mostra a fita "Saudade").
- **Gravador:** original com o jogador / entregue a Daniel / abandonado. A cópia enviada vale mesmo sem o original.
- **Samuel (opcional):** não localizado / enviado primeiro a Irene ("Agora eu sei onde levar as flores.") / enviado primeiro à redação (Irene pede que esperem).

Sair antes do ato 3 dá o epílogo parcial.

## 9. Plano de implementação (fases)

Cada fase termina num playtest jogável. O plano detalhado, tarefa a tarefa, vem depois da aprovação deste documento.

| Fase | Entrega | Quem |
|---|---|---|
| **F0. Fundação** | Estado da noite com save; catálogo de evidências D01–D14 e H01–H07 em dados; caderno com comparações. | Scripts: Codex. Revisão: Claude. |
| **F1. Investigação sem Daniel** | Câmera e álbum; documentos colocados na cena; painel de energia; compartimentos com Léo, João e Célia; P01 a P07 e P09 jogáveis com papel provisório. | Scripts: Codex. Cena e prefabs: Claude. |
| **F2. Celular e finais** | Mensagens, envio com sinal, epílogos em texto, O01 e O02. | Scripts: Codex. Cena: Claude. |
| **F3. Daniel sabotador** | Corpo na mesa, saída encenada (P08), diretor com fases Mesa, Sombra e Obstrução, sabotagens S1 a S5. | Diretor: Codex. Encenação, áudio e cena: Claude. |
| **F4. Caça** | NavMesh, perseguição, esconderijos, confronto, cópia do áudio sob pressão, checkpoints CP01–CP04. | Scripts: Codex. NavMesh e cena: Claude. |
| **F5. Acabamento** | Áudio (vozes, recado de Bia, gravação de Léo), casos de borda do template, ajuste de ritmo. | Claude, com playtests do autor. |

Casos de borda a testar (do template): chegar primeiro a C-02, ligar a luz cedo, ficar observando Daniel, pegar o gravador sem ler D09, sair antes da ameaça, pedir ajuda antes do confronto, entregar o original depois do envio.

## 10. Em aberto

**Decidido pelo autor em 2026-11-15:**
1. A ponte entre o 041 e a Limiar (melhoria 6) entra. O recibo de transporte com LV-12 fica no arquivo, junto dos documentos do 041.
2. A captura na caça é fim de jogo imediato.
3. Daniel não destrói provas nem coloca bilhetes ou etiquetas nas coisas do jogador.
4. Daniel vende os corpos desviados a um comprador clandestino.
5. A entrada de Daniel fica registrada por um sensor na porta do recebimento, e não por assinatura. Ele não teve tempo de apagar o registro no computador da recepção.

**Deixado para quem implementar:**
- Valores de intervalo das sabotagens e tempo de cópia.
- Modelo e animações de Daniel (precisa de personagem com esqueleto; há um corpo-base em `docs/revisao-autopsia/corpo_base`).
- Vozes: gravadas ou sintetizadas.
