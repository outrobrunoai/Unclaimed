# Mesa: diagnóstico do inox escuro (etapa 4)

Testes de 08/11/2026, todos temporários. Os objetos de teste usaram `HideAndDontSave` ou `DontSave` e foram destruídos ao final, e os valores originais foram restaurados. A cena ficou com `isDirty=false` e nenhum asset foi gravado. A geometria e o material da mesa não foram tocados.

## Conferências

- **Material** `Aco inox - mesa` (URP Lit, metallic 1, smoothness 1 × alpha do mapa, BaseColor branco):
  - BaseMap em sRGB;
  - Normal e DetailNormal importados como NormalMap;
  - MetallicSmoothness e Occlusion em linear.

  Está correto.
- **Normais:** a malha tem 10.450 vértices, com normais e tangentes completas.
- **Canais isolados** sob um cubo cinza neutro (`canais_material.jpg`): sem AO, sem normal map e com o material plano o resultado é o mesmo. O material não é a causa.
- **Seleção da sonda:** o renderer usa `BlendProbes`, e a consulta por objeto aponta para `Sonda de reflexo` (Autopsia) com peso 1. Porém o projeto usa **Forward+**, que monta um atlas por câmera com **no máximo 64 sondas** (`UniversalRenderPipeline.maxVisibleReflectionProbes`). Nesse caminho, a seleção por objeto não vale.

## Causa principal: sondas duplicadas

A cena tem **166 sondas de reflexo**. As 15 sondas das outras salas aparecem **11 vezes cada**, na mesma posição, em `Luzes/Sondas de reflexo`. O mesmo vale para as sondas de luz: são 11 grupos `Sondas de luz` iguais, com 1.101 pontos cada.

A origem é `UnclaimedMorgueGenerator.Iluminacao.cs`, em `ReflectionProbes()` e `LightProbeGrid()`. A função `Sub()` reaproveita o grupo existente, e cada regeneração completa acrescenta outro conjunto sem apagar o anterior. Com mais de 64 sondas visíveis, a sonda da autópsia fica fora do atlas e o metal reflete o céu noturno, o que dá o tom quase preto e azulado.

O teste de controle (`controle_sem_duplicatas.jpg`) confirma isso. Com as duplicatas ligadas, uma esfera metálica e uma sonda cinza nova não fazem diferença nenhuma. Com as duplicatas desligadas por alguns instantes, as duas passam a valer.

## Causa secundária: cubemap escuro

`reflexo_comparacao.jpg`:

| Teste | Resultado |
|---|---|
| Cena atual, com 166 sondas | Inox preto |
| Sem duplicatas, com o `Reflexo Autopsia.exr` atual | Inox cinza e fosco: o EXR foi assado com a sala vazia e apagada (média 0,11) |
| Sem duplicatas, com reflexo renderizado na hora sob a luz atual (reflexão conhecida) | Inox claro e escovado, como na prévia aprovada de 08/10 |

## Encaminhamento (etapa 9)

1. Corrigir o gerador para limpar os grupos de sondas antes de recriar, e remover da cena as 150 sondas de reflexo e os 10 grupos de sondas de luz duplicados. A mudança afeta todas as salas (para melhor, mas muda o que se vê), então **depende de aprovação**.
2. Reassar `Reflexo Autopsia.exr` com a sala montada e a luz geral acesa.

Até lá, as capturas de revisão desligam as duplicatas só durante a foto (`capd.sh`, sem sujar a cena).
