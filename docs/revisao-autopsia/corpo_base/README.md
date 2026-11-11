# Autópsia: corpo a partir de base MakeHuman/MPFB (para revisão)

- **Fonte:** `Blender/Autopsia/scripts/corpo_base.py`. As etapas são `base`, `render`, `maos`, `pele`, `lencol`, `comlencol`, `detalhes`, `jogo`, `comparar`, `p08`, `exportar` (revisão) e `exportar_jogo`. O arquivo-fonte gerado é `Blender/Autopsia/CorpoBase.blend`, com o corpo completo e o rig.
- **Piloto anterior preservado:** `corpo.py`, `Corpo.blend` e o lençol com a configuração da simulação, que ficam como referência.
- **Cena:** o "Corpo provisório - autopsia" foi substituído pelo menu `Tools > Unclaimed > Autopsia > Update Autopsy Body` (backup em `Backups/PrototipoNecroterio_antes_corpo_20261108.unity`).

## Componentes usados e licenças

| Componente | Versão / origem | Licença | Vai para o jogo? |
|---|---|---|---|
| MPFB (MakeHuman Plugin for Blender) | 2.0.18, build 20261010, repositório oficial extensions.blender.org, instalado no perfil do usuário | GPL-3.0-or-later (código do plugin) | Não. Só gera a malha. |
| Malha base MakeHuman (núcleo do MPFB) | Embutida no MPFB 2.0.18 | CC0 ("All core assets are shared under Creative Commons, CC0" — static.makehumancommunity.org/about/license.html; FAQ do MPFB sobre uso em jogos de código fechado: "Yes") | Sim, a malha posada |
| Rig `game_engine` e pesos | Embutidos no MPFB 2.0.18 | CC0 (núcleo) | Só no arquivo-fonte |
| Pele `mindfront_aksel_skin` (cor, normal e especular de 2048 px) | Pacote Skins 02, `skins02_cc0.zip`, baixado do espelho oficial `files2.makehumancommunity.org`, sha256 `1613f1ef3afca53094511d26620ed7cf1d2dedc29ed3d384d60bdebe250698ae` | CC0. Autor: Mindfront. Fonte: makehumancommunity.org/node/850, conforme o metadado `packs/skins02.json` do pacote | Sim: cor e normal, copiadas para `Assets/Modelos/Autopsia/Piloto/Texturas/` |

Nenhum outro pacote, roupa, cabelo ou asset CC-BY da comunidade foi usado.

## Base e pose

- **Base:** homem adulto com gênero 1,0, idade 0,55, altura 0,52 e mistura de etnias padrão. Mede 1,757 m e tem 13.380 vértices visíveis.
- **Posição na mesa:** decúbito dorsal, cabeça a oeste, nuca no apoio de cabeça (contato com folga de 1 mm) e coroa a 6,5 cm da cabeceira.
- **Braços:** ao longo do corpo, com palmas voltadas para o leito. A rotação para a palma ficar para baixo foi dividida entre ombro (55%) e antebraço (45%), para não torcer o cotovelo.
- **Mãos:** orientadas pela geometria, com a linha dos nós dos dedos nivelada e só a inclinação do punho ajustada por bissecção até a mão encostar.
  - Os dedos têm só flexão relaxada e nenhum dedo foi forçado contra o leito.
  - Medido: médio, anelar e mínimo encostam (folga de 0,8 a 2,3 mm); o indicador fica a 6 mm; a palma fica 8 mm acima, no arco natural da mão.
  - O polegar foi posicionado por alvo anatômico, ao lado do indicador, a cerca de 30° do eixo da mão, deitado de lado.
- **Pernas:** estendidas, com leve abdução e pés em rotação externa e flexão plantar relaxada.
- **Contato:** compressão suave em costas, nádegas, panturrilhas, calcanhares e braços. As mãos receberam só uma compressão de polpa de 1 a 2 mm. Nenhum vértice fica abaixo do aço. Detalhes em `verificacao.json`.

## Imagens

- `estudio_simples_*`: Blender, estúdio neutro, material simples. São as vistas gerais, os closes laterais das duas mãos e a vista de cima (polegar).
- `estudio_pele_*`: as mesmas vistas com a pele CC0.
- `unity_simples.jpg` e `unity_pele.jpg`: Unity, câmera do Player, iluminação atual da sala.

## Lençol (simulação sobre o corpo MPFB)

- Parâmetros finais em `LENCOL` (x0 −1,42, x1 0,94, largura 0,70, deslocamento −0,008, passo 12 mm, rigidez à flexão 2,5); o tecido é o mesmo da simulação aprovada.
- Medido (`lencol_verificacao.json`): dorso da mão direita 98,7% exposto, antebraço esquerdo 69,8% exposto, cabeça 100% coberta e **0% visível** dos pontos alcançáveis pelo Player (olhos a 1,1 e 1,6 m nas laterais e na cabeceira), nenhum vértice dentro do corpo ou do aço.
- Imagens: `lencol_geral.jpg`, `lencol_areas_expostas.jpg`.
- **Olhos:** não foram adicionados, porque o rosto não aparece de nenhum ângulo testado nem no reflexo do aço.

## Pistas

- **Adesivo** (dorso da mão direita): cartão de 23 × 16 mm conformado à pele por raios, sem texto; o contorno rasgado vem do alfa de `AdesivoMao.png`. 96 triângulos.
- **Etiqueta 083**: cartão de 90 × 55 mm sobre o lençol, no lado direito do abdome, com o texto de pé para quem está do lado sul (lado da mão direita) e presa por alfinete passando pelo ilhós. A textura é `Etiqueta083.png`, com UV 1:1. Cartão e alfinete somam 676 triângulos.
- Conferidas pela câmera do Player (olho a 1,6 m, de pé ao lado da mesa):
  - "083" fica legível;
  - o cabeçalho só se lê de perto, como numa etiqueta real desse tamanho;
  - o adesivo aparece como um pequeno rótulo claro.
- Imagens: `pistas_estudio.jpg` (Blender), `unity_pistas_aviso.jpg`, `unity_adesivo_close.jpg` e `unity_etiqueta_close.jpg`.

## Versão de jogo

O arquivo-fonte `CorpoBase.blend` mantém o corpo completo, o rig e as malhas de origem.

- **Corpo:**
  - Visibilidade medida a partir de 216 posições de olho do Player (anéis a 0,35 e 0,9 m da mesa, a 0,65, 1,1 e 1,65 m de altura), com lençol, aço e o próprio corpo como oclusores.
  - Só a região **nunca vista** foi reduzida, com margem de 3,5 cm em volta de tudo que é visto.
  - Mãos e antebraços ficam sempre na resolução completa.
  - A região reduzida foi recuada 5 mm e mantida dentro da anatomia de origem (shrinkwrap "inside").
- **Lençol:** colapso com a bainha protegida. As normais vêm de uma cópia suavizada da fonte, sem alterar a geometria: 95% dos pontos ficam a até 0,6 mm da fonte.
- **Verificações** (`jogo_verificacao.json`):
  - nenhum ponto do corpo atravessa o lençol;
  - folga mínima de 4,3 mm sob o lençol;
  - nada abaixo do aço;
  - 1 vértice da região reduzida fica visível, na borda da margem.
- A comparação entre fonte e jogo, de pé e agachado, está em `fonte_vs_jogo.jpg`.

| Peça | Fonte (triângulos) | Jogo (triângulos) | Observação |
|---|---|---|---|
| Corpo | 26.756 | **16.404** | 11.735 projetam sombra; 4.669 (região nunca vista) não |
| Lençol (ocupado) | 22.736 | **10.230** | material dupla face |
| Adesivo | — | 96 | alpha clip |
| Etiqueta + alfinete | — | 676 | 2 materiais |
| **Estado ocupado** | | **27.406** | |
| Lençol P08 | 22.736 | 10.230 | |
| Etiqueta P08 + alfinete | — | 676 | |
| Marca de sola | — | 2 | transparente, sem sombra |
| **Estado P08** | | **10.908** | só um estado fica ativo por vez |
| Aviso provisório | — | 2 | |

- **Texturas:**
  - pele: cor + normal, 2048 px;
  - tecido do lençol: 512 px repetido a cada 0,25 m;
  - impressos (etiqueta, adesivo, aviso) e a marca de sola: pequenos.
- **Colisores:** nenhum, nos dois estados e no aviso.

## Variante P08 (separada e inicialmente desligada)

- **Lençol:** o lençol ocupado, sem o corpo, deslocado 34 cm para o norte e simulado em duas fases:
  1. a queda;
  2. com a parte já assentada na bandeja retida no lugar, o atrito do algodão no aço, que o solver sozinho deixa escorregar, enquanto a parte pendente assenta.
- **Estabilidade:** movimento residual de 6 mm nos últimos 10 quadros, sem penetração na mesa.
- **Etiqueta 083:** continua presa (Surface Deform, a até 3,7 mm do tecido). Vista do norte, fica de cabeça para baixo, coerente com o lençol puxado pelo lado.
- **Marca de sola:** um único decal úmido a 6 mm do piso (o acabamento é desenhado 3 mm acima do colisor), no lado norte, ao lado da mesa, com a ponta para fora da mesa e não voltada para nenhuma porta.
- **Na cena:**
  - "Estado da mesa de autopsia" com o componente `EstadoMesaAutopsia`, que tem só `MostrarP08(bool)` e alterna os dois estados de forma exclusiva;
  - nenhum trigger e nenhum puzzle;
  - o estado P08 começa desligado.
- Imagens: `p08_estudio.jpg` e `unity_p08.jpg`.

## Unity

- **Prefabs** (menu `Tools > Unclaimed > Autopsia > Build Body Prefabs`):
  - `Prefabs/CorpoOcupado.prefab`;
  - `Prefabs/P08_LencolCaido.prefab`;
  - `Prefabs/AvisoProvisorio.prefab`.
- **FBX:** em `Assets/Modelos/Autopsia/Corpo/`, importados com as normais do Blender e sem colisores.
- **Aviso de manutenção:** está em `Layout.json`, na parede acima do comando da luminária e ao lado do eletroduto. Lê-se a cerca de 85 cm.
- **Reflexo:**
  - `Reflexo Autopsia.exr` foi rebakeado só para a sonda da autópsia (backup em `Backups/`);
  - o aço passou a refletir o corpo e o lençol atuais;
  - a iluminação artística não foi alterada;
  - imagem: `unity_reflexo_antes_depois.jpg`.
- **Imagens pela câmera do Player:**
  - `unity_ocupado_geral.jpg`;
  - `unity_pistas_aviso.jpg`;
  - `unity_p08.jpg`.

## Pendência conhecida: faixas leves no lençol em vistas rasantes

- **Sintoma:** em vistas agachadas e rasantes aparecem faixas horizontais suaves no lençol, de até cerca de 2 níveis de brilho em 255. De pé, quase não se notam.
- **Diagnóstico** (por exclusão, com capturas):
  - não vêm da malha nem das normais (persistem com normais recalculadas pela Unity);
  - não vêm do SSAO nem da sombra do próprio lençol;
  - vêm da **sombra do corpo**, que fica milímetros abaixo do tecido, dentro do bias do mapa de sombras das duas luminárias que projetam sombra na sala.
- **Correção possível:** aumentar o normal bias dessas duas luzes. Isso muda a iluminação aprovada, então ficou para decisão.

## Observações

- **Pele:** a especular do Mindfront contém pelos. Ainda não foi usada na Unity (só cor e normal).
- **Prévia antiga:** os arquivos em `Assets/Modelos/Autopsia/Piloto/` (prévia de revisão e piloto antigo) não são usados pelos prefabs. Podem ser removidos quando você quiser.
