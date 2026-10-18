# Teste Meshy — poste de jardim com globo leitoso (2026-10-17)

Substituição apenas do poste de jardim da fachada (`Area externa/Fachada/Poste de jardim`, cena
`Assets/Scenes/PrototipoNecroterio.unity`, pé em -13.0, -0.05, -16.8) por um modelo gerado no Meshy e ajustado no Blender.

## Geração (única operação paga)

| Item | Valor |
|---|---|
| Ferramenta | Meshy Text-to-3D (preview, só malha) |
| Modelo | `meshy-t2.5`, `model_type` smart-topology |
| Parâmetros | `target_polycount` 6000, `target_formats` ["glb"], `origin_at` bottom |
| ID da tarefa | `01a14bb6-1f50-7143-84c9-bb84a37d1f7f` |
| Saldo antes → depois | 1120 → 1115 (conferido no Meshy) |
| Consumo | 5 créditos — confirmado (`consumed_credits` da tarefa e diferença de saldo) |
| Etapas pagas extras | nenhuma (sem refine/textura, remesh, UV unwrap ou variantes) — 0 crédito |

Prompt enviado:

> Single outdoor garden lamp post for a Brazilian public building, isolated object, complete, standing upright,
> realistic proportions. Total height 2.9 m. Straight round steel tube shaft about 7 cm diameter, matte black painted
> metal. Round flat base flange plate 24 cm diameter with four bolts and a short collar at the foot of the shaft. At
> the top a simple round neck collar holding one spherical milky white opal globe, 30 cm diameter, smooth and
> symmetric. Plain institutional design, no ornaments, not colonial, not futuristic. Lamp switched off: no glow, no
> light beams, no halo, no painted shadows. No ground, no plants, no background.

Referências usadas no brief (postes de jardim vendidos no Brasil): Telhanorte 1721330 e 1721305, AECweb/Eterna 53071,
GC Migliorança (Mercado Livre), Magalu P23 — haste reta preta/grafite, base com flange e parafusos, globo leitoso de
25–30 cm, visual institucional simples.

## O que o Meshy entregou

- Malha de triângulos em peças separadas, altura normalizada (0,95), sem UVs e sem materiais.
- Ignorou a disposição pedida: globo no meio da haste e uma lanterna colonial com remate no topo.
- Aproveitável: base com flange, saia e 6 parafusos (pedido: 4), haste lisa, globo redondo em peça própria
  (18 segmentos).

## Retrabalho (Blender, `Blender/Entorno/scripts/poste_meshy_ajuste.py`)

1. Corte da haste, lanterna e remate acima do centro do globo; ponta da haste fechada dentro do globo.
2. Abertura do topo do globo fechada em leque, com o centro sobre a esfera.
3. Só o trecho liso da haste esticado para o globo ficar no topo; escala uniforme 1,8 para o tamanho real
   (altura 2,86 m, igual ao poste antigo; haste ~7 cm, globo ~28 cm, flange ~30 cm); origem no pé.
4. Três materiais: metal pintado, globo leitoso, parafusos galvanizados.
5. Normais suaves com cantos vivos a 40° (sem vincos no globo) e UVs (smart project).
6. Globo exportado como objeto próprio (renderer sem sombra, como o poste anterior).
7. Silhueta do globo: ajuste de elipsoide aos vértices do Meshy (semi-eixos 0,0782 / 0,0795 normalizados; o maior
   deslocamento de vértice original foi 0,0020 normalizado ≈ 3,6 mm reais), uma subdivisão só nas arestas do globo e
   projeção dos vértices nessa superfície. O aro inferior, escondido no colar, ficou no lugar.
   Globo: 380 → 1520 triângulos; o restante da malha não foi alterado.

Total final, contado na Unity: 4261 triângulos (poste 2741 + globo 1520); antes da suavização eram 3121.

Não foi necessário reconstruir o modelo; as correções são cortes, deformação de um trecho, fechamento de abertura,
subdivisão local, materiais, normais e UVs.

## Arquivos

- `../poste_jardim_original.glb` / `../poste_jardim_original.blend` — resultado bruto do Meshy.
- `../poste_jardim_ajustado.blend` — versão ajustada.
- `Assets/Modelos/Entorno/PosteJardim/Poste_Jardim_Meshy.fbx` e `Poste_Jardim.prefab` (Tools > Unclaimed > Build
  Poste Jardim). O gerador usa o prefab quando existe e mantém o poste procedural antigo como alternativa.

## Capturas desta pasta

| Arquivo | Conteúdo |
|---|---|
| `01_meshy_original_*` | modelo bruto do Meshy |
| `02_antes_procedural_*` | poste procedural antigo na cena |
| `03_*_v1_*` | primeira integração (globo de 18 segmentos) e comparações antes/depois |
| `04_blender_v1_topo_facetado.png`, `04_blender_v2_*` | renders do Blender antes e depois da suavização da silhueta |
| `05_*` | cena com o globo suavizado, mesmas câmeras do antes |

Câmeras na Unity: perto (-12.1, 1.6, -15.6) → (-13.0, 1.9, -16.8); distância normal do Player (-11.4, 1.6, -14.2) →
(-13.0, 1.6, -16.8); ambas FOV 60, 1200×900.

## Avaliação

O Meshy poupou a modelagem da base com parafusos e de um globo razoável, mas não respeitou a composição pedida e
entregou malha sem UVs e materiais. O retrabalho (corte, reposicionamento, escala, materiais, UVs, separação das peças
e suavização do globo) ficou perto do tempo de modelar um poste simples assim direto no Blender. Para objetos simples
e bem especificados, o Text-to-3D rendeu pouco; tende a compensar mais em peças orgânicas ou detalhadas.

## Rodada de acabamento (sem créditos, sem nova geração)

- **Globo:** shader próprio `Assets/Modelos/Entorno/PosteJardim/GloboLeitoso.shader` ("Unclaimed/Globo Leitoso"), PBR da
  URP com emissão que varia pela vista (núcleo 1.0, borda 0.22, potência 1.3) e pela altura em relação à lâmpada (base
  −50 %, topo −20 %). Simétrico em torno do eixo vertical: nenhuma sombra direcional fixa. Luz pontual existente
  inalterada (única), exposição, GI e pós-processamento intocados.
- **Vegetação:** as folhas da sibipiruna da frente ficavam a ~5 cm da superfície do globo e eram estouradas pela luz
  pontual (queda com o quadrado da distância). Material (Lit, sem especular, alpha clip) e normais (sem viés para a
  luz) estavam corretos, então nada compartilhado foi alterado. Só essa árvore girou +20° (202,9° → 222,9°): a folha
  mais próxima passou de 0,19 m para 0,84 m do centro do globo; ainda recebe luz quente.
- **Base:** apoio de concreto 0,44 × 0,44 m, 8 cm acima do gramado e 10 cm enterrado, aresta chanfrada, berço de
  argamassa sob a flange, seis chumbadores roscados saindo das porcas, anel de terra irregular no encontro com o
  gramado. A haste foi encurtada 9 cm para a altura total e o globo continuarem iguais (centro do globo em y 2,676,
  luz em 2,67). Um tufo de grama que atravessava a base foi removido.
- **Gerador:** prefab inclui o objeto "Apoio" e um BoxCollider; o gerador da vegetação aplica o giro dessa árvore e
  descarta forração sobre a base sem alterar o restante do layout semeado.
- **Triângulos:** Poste 2909, Globo 1520, Apoio 184.
- **Capturas:** `06_acabamento_v2_*` (câmera do Player, FOV 60, olho a 1,65 m) e
  `06_teste_globo_shader_valores_iniciais.png` (primeira tentativa do shader, apagada demais).
