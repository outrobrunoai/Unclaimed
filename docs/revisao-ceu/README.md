# Céu noturno de chuva leve — PrototipoNecroterio

17/10/2026. Correção do pinçamento radial no zênite e nuvens de chuva leve noturna.

## Diagnóstico

- Chuva pausada (raiz "Chuva leve" desativada) nas capturas `antes/chuva0`: o pinçamento aparece igual sem partículas, logo era do skybox.
- Material `Ceu noturno` = Skybox/Panoramic, lat-long 360°, sem espelho, rotação 0, sem mipmaps (correto). A causa estava na fonte `Blender/Ceu/ceu_noturno.py`:
  - nuvens e "mares" da lua eram ruído 2D no espaço de pixels da imagem lat-long; no zênite cada linha colapsa num ponto → faixas radiais convergentes;
  - o ruído era gerado em meia resolução e ampliado com `resize` bicúbico sem wrap → descontinuidade na coluna u = 0/1;
  - estrelas eram texels isolados → esticadas radialmente perto do polo.
- A reflexão padrão do céu (`ReflectionProbe-13.exr`, 128 px) e a sonda `Reflexo - entrada` carregavam o céu antigo (com o pinçamento) e apareciam nas poças/vidros.

## Correção

`Blender/Ceu/ceu_noturno.py` reescrito: todo pixel é calculado a partir da direção 3D.
- Nuvens projetadas numa camada curva (2 km, mais uma camada baixa fina a 1,3 km) com fBm 3D e warp leve; campo lento de cobertura decide onde abrem as poucas aberturas; camada alta fina (8,5 km) vela a lua.
- Volume sutil: flancos voltados para a lua um pouco mais claros, núcleos densos um pouco mais escuros, tons azul-ardósia.
- Estrelas: gaussianas em distância angular (tamanho igual em qualquer altitude), atenuadas pela transmitância das nuvens — somem sob a camada densa.
- Lua: mesma direção (`MOON_EULER = (20, 10)`) e diâmetro; véu fino com textura sobre o disco e coroa discreta.
- Brilho: médias lineares do céu antigo preservadas (hemisfério superior, > 60°, 0–20°, inferior), impressas pelo script.
- Polos com desvio 0 entre colunas; emenda u = 0/1 na mesma ordem das colunas vizinhas.
- `python3 Blender/Ceu/ceu_noturno.py --preview` (≈ 17 s) gera a textura e `Blender/Ceu/previews/`.

Sondas: só as afetadas. `ReflectionProbe-13.exr` (reflexão padrão do céu) regravada a partir do novo panorama (orientação conferida contra a antiga: erro 2%); `Reflexo - entrada` e `Reflexo - Recepcao 1` re-bakeadas individualmente (só a fatia de céu pela porta mudou; interior 0,015%). Sem bake de lightmaps; luzes, exposição e iluminação interna intactas.

## Arquivos

- `comparacao_zenite.png`, `comparacao_vistas_gerais.png` — antes | depois, chuva desligada.
- `comparacao_chuva_on_off.png` — depois, sem | com chuva.
- `comparacao_panorama_zenite.png` — zênite da própria textura.
- `antes/chuva0|1`, `depois/chuva0|1` — capturas da câmera do Player (Play Mode, 1280×720): jardim (zênite, N/L/S/O, lua, alto), pátio de serviço (zênite, N/L/S/O), recepção, soleira e reflexos.

## Nitidez (17/10/2026, segunda rodada)

- Antes: PNG 4096 × 2048, importado em 4096 × 2048 BC7 (Max Size padrão 4096; Standalone/WebGL sem override, herdam o padrão), sem mipmaps, bilinear. Na Game view 2560 × 1440 e 60° cada texel ocupava ~3,7 px de tela.
- O borrão vinha também da geração: a oitava mais fina das nuvens tinha ~32 m (≈ 1° no zênite); dobrar pixels sozinho quase não mudava nada (`8192 sem detalhe` nos testes).
- Agora: geração nativa 8192 × 4096 com o mesmo cálculo esférico, sementes e parâmetros; 3 oitavas finas de média zero (`DETAIL_OCTAVES = 3`, `DETAIL_GAIN = 3`) somadas sem mudar a normalização, cada uma desligada onde ficaria menor que ~2 texels (horizonte sem ruído fino). Cobertura medida em 400 mil direções: 64,00% coberto e 20,0% aberto, igual; densidade muda 0,007 em média. Estrelas com o mesmo tamanho angular (agora ~2 texels em vez de < 1).
- `--width 4096 --detail 0` reproduz byte a byte o céu aprovado anterior.
- Importação: Max Size 8192, BC7 (CompressedHQ), sem mipmaps; gerador (`NightSky`) atualizado para manter isso. BC7 decodificado vs PNG: erro médio 0,05 nível de 8 bits, p99 1; sem novas faixas. Memória: 8192 × 4096 × 1 byte = 32 MB (o Editor relata 64 MB).
- Reflexos: a reflexão padrão de 128 px mudaria 0,44% em média — não atualizada; nenhuma sonda alterada, sem bake.
- Comparações em `nitidez/` (câmera do Player 2560 × 1440, sem chuva): `comparacao_recortes_1x1.png` (zênite, pátio, alto, lua), `comparacao_quadros.png` (mesmos enquadramentos), `comparacao_chuva.png` (8192 com/sem chuva).
