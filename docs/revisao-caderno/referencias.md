# Caderno do jornalista — referências e texturas

Data: 2026-11-15. Script: `Assets/Scripts/Diariodepistas.cs`. Texturas: `Assets/UnclaimedPrototype/Caderno/Texturas/`.

## Referências do objeto

O caderno da interface é um caderno espiral de repórter: papel pautado em azul, margem vermelha, espiral metálica e capa de papelão.

- Bloco de repórter com espiral, pauta Gregg e margem vermelha (TOPS Second Nature, 4 × 8 pol., papel reciclado): https://www.staples.com/tops-second-nature-reporter-s-notebook-4-x-8-gregg-ruled-assorted-colors-70-sheets-pad-74130/product_TOP74130
- Bloco de estenografia com espiral no topo, pauta azul e margem vermelha (Universal 96920): https://myworksupply.com/products/steno-pads-gregg-rule-red-cover-80-white-6-x-9-sheets
- Bloco de repórter sem margem (Blueline), para comparação: https://www.officecrave.com/blueline-reporter-notebook-at8b.html
- Caderno espiral com capa kraft, referência brasileira de capa de papelão: https://escorregaopreco.com.br/ofertas/shopee/2330792
- Bloco pautado com margem vermelha (Tilibra Academie): https://www.magazineluiza.com.br/busca/bloco+pautado

O que veio das referências: pauta azul clara e fina, uma única margem vermelha vertical, papel levemente creme, espiral de arame passando por furos redondos e capa de papelão marrom escuro.

O bloco de repórter real tem a espiral **no topo**. A interface usa a espiral **no meio**, como um caderno aberto em duas páginas, porque a lista de evidências e a página de leitura precisam ficar lado a lado. Se a direção de arte preferir o bloco de repórter fiel, a alternativa é uma página só, com a espiral no topo e a lista em uma faixa lateral.

## Texturas usadas

Todas vêm do ambientCG e têm licença **CC0** (domínio público; uso comercial permitido, sem atribuição obrigatória). Foi usado só o mapa de cor (`Color`) de 1K. Os demais mapas (normal, rugosidade, deslocamento) não são usados na interface.

| Arquivo no projeto | Origem | O que foi feito |
|---|---|---|
| `papel_pagina.png` | Paper001 (fibra de papel branco) × Paper003 (papel amassado) | Tingido para creme (média 0,93 / 0,90 / 0,81). Fibra com contraste 1,6 e amassado com 0,35, para o texto continuar legível. |
| `papel_indice.png` | Mesma base, girada 180° | Tom um pouco mais escuro, para separar a página do índice da página de leitura. |
| `papel_nota.png` | Paper001 × Paper003 | Tingido de amarelo, para o bilhete com o resultado da comparação. |
| `capa_papelao.png` | Paper006 (papelão marrom) | Escurecido para marrom (0,24 / 0,18 / 0,13). |
| `divisoria_cartolina.png` | Paper005 (cartolina) | Tingido para cartolina bege, usado nas abas inativas. |
| `espiral.png` | Desenhada em Python (não vem do ambientCG) | Uma volta de arame com dois furos, repetida na vertical. |

Páginas de origem:

- Paper001: https://ambientcg.com/a/Paper001
- Paper003: https://ambientcg.com/a/Paper003
- Paper005: https://ambientcg.com/a/Paper005
- Paper006: https://ambientcg.com/a/Paper006

Para refazer as texturas: baixe os pacotes `PaperXXX_1K-JPG.zip` dessas páginas, descompacte cada um numa pasta com o nome do material e rode `python3 docs/revisao-caderno/gerar_texturas.py <pasta dos pacotes> <pasta de saída>`.

## Importação no Unity

- Papéis: Default, sRGB, mipmaps, repetição (`Repeat`), compressão HQ, tamanho máximo 1024.
- Espiral: Default, sRGB, `Clamp`, transparência ativada, sem compressão.
- As texturas são ligadas ao componente `DiarioDePistas` do objeto "Caderno" na cena `PrototipoNecroterio`. Sem textura, cada superfície volta à cor lisa.

## Efeitos desenhados no código

- Sombra sob o caderno inteiro.
- Três tiras de papel sob a borda de baixo, simulando a espessura do bloco de folhas.
- Sombra na curva das folhas junto à espiral e bordas externas levemente escurecidas.
- A textura fica presa à tela, e não ao retângulo: o papel não "anda" quando a lista rola.

## Capturas

- `capturas/caderno_evidencias.png`
- `capturas/caderno_conclusoes.png`
- `texturas_previa.jpg` (as seis texturas lado a lado)

## Letra de mão

Fonte **Kalam** (Regular e Bold), da Indian Type Foundry, licença **SIL Open Font License 1.1** (uso comercial permitido; a licença deve acompanhar a fonte). Arquivos e licença em `Assets/UnclaimedPrototype/Caderno/Fontes/` (`Kalam-OFL.txt`). Origem: https://github.com/google/fonts/tree/main/ofl/kalam

- Cobre todos os acentos do português.
- Usada no que o jornalista escreveu: títulos, textos, itens da lista, bilhete da comparação e aviso de evidência nova.
- O que é impresso no caderno continua em fonte de impressão: abas, rótulos pequenos (DOCUMENTO, FOTO), botão, a palavra CADERNO na capa e o rodapé.
- Captura: `capturas/caderno_letra_de_mao.png`

## Animação de abrir e fechar

- Abrir (0,34 s): o caderno sobe de baixo da tela, passa um pouco do lugar e assenta. O fundo escurece junto.
- Fechar (0,22 s): o caderno acelera para baixo e o fundo clareia.
- Usa tempo real, então funciona com o jogo pausado. As durações ficam no componente (`duracaoAbrir`, `duracaoFechar`).
- O controle volta ao jogador assim que o fechamento começa; durante a animação de saída, cliques no caderno são ignorados.
