# Descanso e vestiário — referências e decisões

## Referências efetivamente examinadas

| Fonte | O que foi aproveitado |
|---|---|
| Nilko NK 1332 (roupeiro de aço, 8 portas) e roupeiros de aço de varejo brasileiros (Madeira Madeira) — página e foto de produto | módulo de 2 armários, largura ~0,33 m por porta, profundidade 0,40–0,46 m, altura ~1,9 m, rodapé baixo, venezianas no alto e embaixo da porta, fecho com orelha furada para cadeado, porta cinza azulada sobre corpo cinza claro |
| NR-24, item 24.4.3 (vestiários: armários individuais com chave/cadeado, assentos laváveis) | armários individuais por funcionário, banco lavável (ripas em estrutura metálica) e circulação livre entre bancos e armários |
| Fotos do Wikimedia Commons de vestiários/salas de funcionários (não brasileiros; usadas só para organização espacial) | ilha de armários perpendicular à parede para criar recato, banco paralelo aos armários a ~0,8–1,0 m, cabideiro de parede separado dos armários |

Limitação: não foi possível obter fotos de vestiários/salas de descanso de hospitais brasileiros com licença aberta (busca de imagens bloqueada no ambiente); as referências brasileiras são de produto e norma, as fotos de ambiente são de uso geral.

## Decisões de layout (coordenadas Unity)
- Interior livre: x −12,875…−5,375, z −3,75…0,75.
- Ilha de armários (2 módulos) saindo da parede norte em x −9,45, portas para oeste; o fundo liso de aço, voltado para a entrada, carrega o quadro de avisos e corta a linha de visão da porta para a área de troca.
- Área de troca (oeste): banco 1 a 1 m das portas da ilha, banco 2 a 0,7 m do banco de 3 módulos na parede oeste; cabideiro de parede na parede sul com jaleco e capa de chuva.
- Descanso (leste): mesa 0,9 × 0,9 com 3 cadeiras (nenhuma no lado do balcão), balcão da copa na parede sul com frigobar, lixeira, relógio na parede leste.
- Portas de armário: `PecaDeMovel` (Porta, eixo vertical, 105°); dois armários ficam trancados com cadeado por composição (texto "Armário trancado com cadeado do funcionário"), sem chaves nem puzzles.

## Textos
- Comunicado interno (D14): redigido sem o modelo azul e branco; papel off-white, cabeçalho em versalete espaçado.
- Bilhetes do café e da marmita: manuscrito, textos do roteiro (Assets/História).
- Escala de limpeza: inventada para a cena, sem informação de puzzle.

## Revisão dos assets problemáticos (2026-10-27)
Fotos do Wikimedia Commons (todas CC, baixadas em `Blender/Descanso/referencias/` por `Blender/Descanso/scripts/commons.py`, 1100 px; não entram no jogo, só serviram de conferência de silhueta e construção). Exemplos identificados:

| Arquivo local | Origem | Licença |
|---|---|---|
| jaleco_3 | Laboratory Coat.jpg | CC BY-SA 4.0 |
| capa_1 | Alte Regenjacke grün aa.jpg | CC BY 4.0 |
| capa_4 | Clear cape1.jpg | CC BY-SA 3.0 |
| mochila_1 | Eastpak1.jpg | CC BY-SA 3.0 |
| mochila_4 | Backpack-1.jpg | CC BY-SA 4.0 |
| bota_1 | Boots (70953443).jpeg | CC BY 3.0 |
| bota_4 | Gumboot planters at the front door.jpg | CC BY 4.0 |
| sapato_1 | Clog mule.jpg | CC BY-SA 2.0 |
| sapato_4 | Caterpillar Outline steel toe work boots.jpg | CC BY 3.0 |

Os demais arquivos da pasta (jaleco_1/2/4/5, capa_2/3/5, mochila_2/3/5/6, bota_2/3/5, sapato_2/3/5) vêm da mesma busca; o título e a licença de cada um se refazem rodando `commons.py` (imprime arquivo, título, licença e URL). Nem todas as fotos baixadas foram usadas.

Decisões de construção: jaleco e capa (painéis frente/costas com dobras de gravidade, mangas em varredura, gola, bolsos, botões, cabide que de fato sustenta a peça), mochila (loft superelíptico com bolsos, zíperes e alças soltas, base apoiada), bota de PVC (pé + cano ocos, borda enrolada, par E/D) e tamanco (cabedal oco com furos, tira do calcanhar), banco (4 ripas com veio no comprimento, 3 travessas, quadros de pé soldados, parafusos), itens da copa (garrafa térmica com bomba, 2 potes de vidro, pilha de copos, 2 canecas abertas, colher), agrupados por uso no balcão com áreas livres.

## Piloto do jaleco por simulação de tecido (2026-10-27)

Referências (Wikimedia Commons, só estudo; nenhuma imagem incorporada ao jogo):

| Arquivo | Título | Licença | Uso |
|---|---|---|---|
| jaleco_5 | [File:Lab coat and scrubs.jpg](https://commons.wikimedia.org/wiki/File:Lab_coat_and_scrubs.jpg) | CC BY-SA 3.0 | principal: jaleco vazio no cabide, de frente (gola, lapelas, bolsos, abotoamento) |
| jaleco_2 | [File:Lab Coats.JPG](https://commons.wikimedia.org/wiki/File:Lab_Coats.JPG) | CC BY-SA 4.0 | caimento e perfil de jalecos pendurados em gancho |
| jaleco_4 | [File:Wardrobe of chemist.jpg](https://commons.wikimedia.org/wiki/File:Wardrobe_of_chemist.jpg) | CC BY-SA 4.0 | apoio: vários jalecos lado a lado |

Método: `Blender/Descanso/scripts/jaleco_sim.py` (volume único corpo + mangas, remalha voxel, aberturas cortadas, ombros presos ao cabide,
colisão com cabide e parede, simulação de tecido de 340 quadros, malha estática salva em `Blender/Descanso/malhas/jaleco_caimento.json`;
determinístico) e `pecas_jaleco.py` (espessura, lapelas, gola, vista com botões, bolsos e caneta projetados sobre o caimento).
Comparações em `docs/revisao-descanso/piloto-jaleco/`.

## Piloto do jaleco v2: moldes planos (2026-10-29)
Referência única: jaleco_5 (File:Lab coat and scrubs.jpg, CC BY-SA 3.0), jaleco vazio em cabide. A jaleco_2 (gancho) deixou de ser usada.
Método (`Blender/Descanso/scripts/jaleco_molde.py` → `malhas/jaleco_caimento_v2.json`; render cinza `jaleco_cinza.py`): costas, frente esquerda e direita (com lapela já dobrada na linha de quebra) e mangas cortadas como contornos 2D planos, triangulados por igual, deitados em volta do cabide e unidos nas costuras. O repouso das molas de flexão é tecido plano; só a costura do ombro sobre os braços do cabide é presa; as frentes se unem apenas nos 4 botões. A gola é costurada no decote já assentado e deitada sobre a superfície (simulada, ela enrugava nas pontas — 3 tentativas). A v1 (`jaleco_caimento.json`) continua intacta e é a que está na Unity.
Imagens: `piloto-jaleco/v2/`.
Ajustes da v2 (2026-10-29, mesmo dia): costura do ombro passou a fechar de fato (antes ficava 1–2 mm aberta); decote das costas sustentado de leve (pin parcial) para a gola não cruzar o braço do cabide; botão de cima desabotoado e barra 2 cm mais larga (quebra a regularidade das dobras); ponta do cabide arredondada na colisão; bolsos finos (borda a 0,8 mm do painel). Teste na Unity: prefab `Jaleco v2` (JalecoV2.fbx) no cabideiro, v1 desativado ao lado; capturas em `piloto-jaleco/v2/unity_*.png`.
