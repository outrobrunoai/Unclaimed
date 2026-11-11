# Letreiro principal — A integrada

17/10/2026. Escolha explícita do usuário: **chapa administrativa clara**. Trabalho feito diretamente pelo Codex no Blender e no Editor Unity, sem delegação.

[Comparação antes/depois — três vistas reais da Unity](comparacao.html) · [guia atualizado](../comunicacao-visual.md) · [matriz SVG final](../../../Blender/Entorno/Acabamentos/fontes/Placa_Fachada_A.svg)

## Alteração

Somente `Area externa/Fachada/Placa - identificacao/Placa`, mantendo a posição, envelope de 2,40 × 0,52 m e os dois textos existentes. Título Nimbus Sans Narrow Bold em uma linha e vinculação Nimbus Sans abaixo, à esquerda; margens e corpo refinados sem comprimir os caracteres. O arquivo em `propostas/a-chapa.svg` é o estudo arquivado; a matriz final está em `Blender/Entorno/Acabamentos/fontes/Placa_Fachada_A.svg`.

Chapa dobrada de 8 mm, afastamento posterior de 8 mm, quatro parafusos com cabeça de 7 mm. Face frontal única com UVs; a face correspondente do corpo foi removida para evitar sobreposição. Corpo e impressão em URP Lit opaco, metalicidade zero e smoothness 0,16; emissão desabilitada. Pintura marfim com variação suave e desgaste localizado junto às fixações e trechos curtos da borda. Nenhuma faixa azul, régua, brasão, ornamento ou luz adicionada.

Fonte Blender `Acabamentos_Exterior.blend` salva e somente `Placa_Fachada.fbx` reexportado. Os outros 12 objetos foram preservados na atualização seletiva, conferindo identidade de objetos/malhas e posição. GUIDs de FBX, textura e prefab existentes mantidos. Material do corpo é específico (`Chapa_Letreiro`), sem alterar o aço compartilhado por outras famílias.

O comando **Tools > Unclaimed > Build Main Sign Only** atualiza só este prefab; não executa a reconstrução do exterior. O gerador usa o prefab existente e, no fallback, a impressão aprovada e profundidade revisada. `scripts/texturas.py` encaminha apenas a identificação principal para sua matriz própria; os demais layouts não foram replicados nem modificados.

## Verificação

- Projeto conectado ao vivo: `/home/umbrunoai/Projetos/Unity/Unclaimed`, Unity 6000.5.9f1; CLI 1.0.0-beta.13.
- Cena `Assets/Scenes/PrototipoNecroterio.unity` salva fora do Play Mode. Prefab atualizado na instância existente.
- Renders noturnos reais antes/depois, 1440 × 1080, FOV 60°, mesma câmera em cada par: frente à altura de 1,65 m; ângulo à mesma altura; perto com câmera elevada de inspeção. O script copia os ajustes do Player e remove sua câmera temporária ao terminar.
- Título legível nas três vistas; vinculação legível de frente e de perto, menor e mais discreta em ângulo. Margens livres, acentos íntegros e sem inversão de UV. Na proximidade a espessura e as fixações são discretas; sombras de árvores continuam sobre a chapa, sem efeito luminoso.
- Auditoria de componentes serializados antes/depois: **zero diferenças fora do letreiro**. Mantidas **46 luzes, 308 colisores**, perfis de Volume e referências dos três lightmaps. Nenhum componente de gameplay alterado.
- Malha Blender: 644 triângulos, UVs presentes e zero faces degeneradas. Não foram adicionados colisores.
- Hashes de cenas, lightmaps e assets não relacionados preservados; lista precisa das alterações em `verificacao.json`. Compilação sem falhas, Console atual com zero erros e um aviso, mesma quantidade de avisos observada no início. GUIDs conferidos pelos arquivos `.meta` preservados.

Capturas em `antes/` e `depois/`; comparações lado a lado em `comparacao-*.png`; auditorias em `audit-*.json` e baseline em `hashes-antes.json`. Reprodução das comparações: `python3 docs/direcao-arte/scripts/comparar_letreiro.py`.

## Limites e manutenção

Sem novo bake: placa dinâmica com light probes, sem contribuição estrutural ao bake e sem alterações nas luzes ou na geometria de circulação. Verificação visual em Edit Mode na iluminação noturna existente; não foi repetida a bateria do puzzle, nem foi feito ensaio de movimento ou avaliação diurna. Nenhuma interação foi criada. Não foi alterado `Morgue.unity`, feito commit ou push.

Para atualizar a impressão isoladamente: `python3 Blender/Entorno/Acabamentos/scripts/letreiro.py`. Para geometria, executar `atualizar_letreiro_blender.py` no Blender com a fonte correta aberta e sem edições não salvas de terceiros; depois usar **Build Main Sign Only** na Unity. Não executar o gerador completo para revisar esta placa.
