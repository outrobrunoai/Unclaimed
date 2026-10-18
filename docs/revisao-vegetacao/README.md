# Revisão — overhaul da vegetação externa (PrototipoNecroterio, 2026-10-17)

Céu 8K, chuva, áudio, layout, limites, iluminação bakeada, interiores e puzzle preservados. Sem commit/push.

## Conteúdo da pasta
- `antes/`, `depois/` — 13 vistas do Player (1920×1080, chuva ligada), mesmas poses.
- `comparacoes/` — antes | depois lado a lado (`fundos_oeste`: a pose antiga saía preta e foi trocada; sem par válido).
- `video/vento_vegetacao.mp4` — 26 s, 1280×720, 30 fps, chuva ligada: grama de perto, ornamentais do canteiro, copa da sibipiruna sob o poste de jardim e caminhada pelo jardim (`RevisaoVegetacao.Gravar`).
- `desempenho/antes.txt`, `desempenho/depois.txt` — medições brutas.

## Diagnóstico (antes)
- Árvores com ~1,5–2k tris: tronco + ~5 ramos bezier com 2–4 sub-ramos, sem hierarquia de galhos finos; copa = 300–400 cards de ~1 m (leques de 6–8 frondes) espalhados numa casca elipsoidal → silhueta de "bola de placas", folhas flutuando sem galhos que as sustentem, repetição de leques.
- Tom amarelado das folhas, casca única genérica com escala arbitrária, nenhum vento.
- Gramado = textura plana no submesh "Gramado" + tufos esparsos de cards cruzados (planos de lado).
- LODs sem crossfade; impostores sem material próprio (caminho de textura errado no construtor).

## O que mudou
**Árvores** (`Blender/Entorno/scripts/arvores.py`, novo): esqueleto ramificado recursivo (tronco → 5–7 ramos principais → secundários → raminhos, raios pelo "pipe model", fototropismo, ramos nascendo dentro do tronco abaixo da forquilha, filhos em filotaxia para não se cruzarem), limitado por um envelope de copa irregular. Folhas = cards de 0,4–0,8 m pendurados ao longo dos raminhos, com normais misturando aglomerado e copa → massas com profundidade, aberturas e variação de densidade. LOD1 usa o mesmo esqueleto (sem troca de forma no crossfade). Casca por espécie a partir de scans Poly Haven com tamanho real de tile.
- Sibipiruna A/B: copa em guarda-chuva larga e achatada; Mangueira A/B: cúpula densa, folhas nas pontas, casca cinza-escura; Árvore pequena: muda de sibipiruna com guia central.
- Tris LOD0/LOD1: Sibipiruna A 29k/11k, B 21k/8k, Mangueira A 35k/14k, B 27k/10k, Pequena 5,6k/1,8k; LOD2 impostor (4 tris).

**Grama**: tufos de lâminas opacas reais (`blade_tuft` em `vegetacao.py`; 38–54 lâminas de 7–19 cm, curvas, afinando, 8 famílias de cor). `Tools/Unclaimed/Gramado/Build Lawn Grass` (`UnclaimedGramado.cs`) espalha os tufos sobre "Gramado" (20/m²) e "Grama rala" (4/m²) com manchas e falhas por ruído, densidade/altura reduzidas a 0,35 m das bordas (pavimento, canteiros, terra), descarte junto a obstáculos (ralos, bases de postes, muros, bancos) e grama mais alta junto a muros. Combinado em 54 chunks de 4×4 m com LODGroup crossfade (LOD1 ~12 m, corte ~40 m), sem colisores e sem projetar sombra. Tufo_Grama e Capim_Alto espalhados também viraram lâminas.

**Vento** (`VentoVegetacao.cs` + shader `Unclaimed/Vegetacao`): objeto "Vento da vegetacao" na cena; direção = `ChuvaLeve.vento` (a chuva não foi recalibrada), intensidade 0,35 com rajadas suaves que atravessam o jardim (campo de ruído em espaço mundial). Deformação no vértice (mesma nas passes de sombra/profundidade) por cor de vértice: A flexão principal (0 no chão/tronco), R ramos finos, B tremor de folha, G fase. Raízes e bases fixas; tronco e galhos grossos estáveis. Amplitudes por planta (`vegetacao.json` → variantes de material): grama 3,5 cm/unid., capim 16 cm, espada-de-são-jorge 1,2 cm (rígida), filodendro 2–3 cm, árvores 4 cm (principal) + 10 cm (ramos). `LimitesVento` folga os bounds. Ornamentais (Espada/Filodendro) passaram ao shader de vegetação no modo "sem cor" (`UnclaimedExteriorFinishes.Wind`).

**Interferências**: verificação por amostragem de vértices contra colliders e luzes; headings fixos no gerador (`heading` em `UnclaimedMorgueGenerator.Entorno.Vegetacao.cs`): nenhuma madeira/folha dentro de muros, gradil ou guarita; folhas ≥ 1,2 m do globo do poste de jardim (sibipiruna da entrada: yaw 20°, escala 0,90).

**Chuva/iluminação**: o mapa de abrigo ignora tudo sob "Vegetacao" (copas e gramado não viram telhado) → nenhum mapa refeito. Vegetação não é GI-static: lightmaps e sondas não contêm copas, sombras da lua sobre/das plantas são em tempo real e seguem o vento → nenhum bake necessário.

## Desempenho (Editor, Play Mode, OpenGLCore, RX 9070 XT, 2560×1440, chuva ON, 120 quadros, mesmas poses)
| Pose | Render antes | Render depois | Quadro antes | Quadro depois |
|---|---|---|---|---|
| 1 jardim | 4,7 ms | 7,6 ms | 8,9 ms | 12,2 ms |
| 2 jardim amplo | 6,1 ms | 7,9 ms | 11,8 ms | 14,1 ms |
| 3 gramado/copa | 5,0 ms | 6,4 ms | 9,0 ms | 10,8 ms |
| 4 entrada | 4,9 ms | 6,8–7,9 ms | 9,2 ms | 11,4–12,5 ms |
| 5 lateral | 5,8 ms | 8,1 ms | 11,3 ms | 14,4 ms |

Custo extra ~1,5–2,9 ms de render; tudo abaixo de 16,7 ms. Medição no Editor (não em build): valores absolutos de build tendem a ser menores.

## Limitações
- Folhas diretamente sob as luminárias quentes (2700–3300 K) ainda ficam verde-oliva; foi aplicada tinta fria e translucidez baixa, sem mexer na iluminação aprovada.
- Árvores das vizinhanças usam os mesmos prefabs (mais tris que antes); fora do lote não foi feita checagem de interferência.
- Grama da vizinhança fora do lote não recebeu tufos (só textura).
- Desempenho medido só no Editor.

## Fontes / referências
- Sibipiruna: laudo técnico de arborização, UFES (ccae.ufes.br) — copa ampla arredondada/achatada, 6–18 m.
- Mangueira: UF/IFAS ST404 (edis.ifas.ufl.edu/publication/st404); Virginia Tech dendrologia, ficha 1134; botanybrisbane.com/?p=10388.
- Grama-esmeralda (Zoysia japonica): aula "Gramados", e-Disciplinas USP.
- Assets: Poly Haven CC0 — bark_platanus, bark_brown_01 (cascas); island_tree_02, jacaranda_tree, shrub_02 (folhas). Ver `Blender/Entorno/fontes/LICENCAS.md`.

## Como reproduzir
```
python3 Blender/Entorno/scripts/vegetacao_texturas.py
blender -b --factory-startup --python Blender/Entorno/scripts/vegetacao.py [-- --only Nome1,Nome2]
Unity: Tools/Unclaimed/Build Entorno Prefabs → Tools/Unclaimed/Gramado/Build Lawn Grass
```
