# Revisão de placas e equipamentos dos corredores

Data: 2026-10-26. Cena `Assets/Scenes/PrototipoNecroterio.unity` (salva; só os corredores foram atualizados, com `Tools > Unclaimed > Circulacao > Update Corridors`, sem regenerar o mapa).

## Referências usadas (fotos e documentos abertos e examinados)

| Família | Fonte | O que foi aproveitado |
|---|---|---|
| Extintor | https://www.aecweb.com.br/produto/extintor-de-po-4-kg-abc-2-a-20-bc/59209 (`referencias/ext_aec.png`) | cilindro mais largo que alto em relação a uma garrafa (raio ≈ 72 mm), pescoço curto, válvula com manômetro frontal, alavanca fixa + gatilho, pino com argola, mangueira presa ao corpo, rótulo único com faixa de classes |
| Extintor | https://commons.wikimedia.org (File:FireExtinguisherABC.jpg; File:Rescue stretcher, defibrillator and fire extinguisher on a wall, Curitiba Airport.jpg) (`abc.jpg`, `curitiba.jpg`) | gancho de parede, altura da válvula (~1,3–1,4 m), cinta de fixação fina em vez de faixas largas |
| Acionador | https://www.aecweb.com.br/produto/acionador-manual-am-013/38746 (`am013.png`) | caixa vermelha em degrau, janela rebaixada com bordas, impressão frontal com texto e traço de quebra |
| Acionador | https://www.intelbras.com/pt-br/acionador-manual-enderecavel-sem-sirene-ame-521 (`AME_521_frontal_led.png`) | proporção 98 × 98 mm, fenda da chave, pictograma de casa em chamas |
| Sinalização | https://www.cbmerj.rj.gov.br/wp-content/uploads/2022/04/NT-2-05-Sinalizacao-de-seguranca-contra-incendio-e-panico-2020-versao-02.pdf (reproduz a ABNT NBR 13434-2) | símbolos de extintor e alarme, proporção símbolo/texto, cores (vermelho, verde de saída, amarelo de risco) |
| Sinalização interna | https://www.ifmg.edu.br/portal/comunicacao/ManualIFMGSinalizaoInterna.pdf | hierarquia de placa de sala (nome em uma linha, complemento menor), margens, setas alinhadas ao texto |
| Sinalização de serviços de saúde | https://prefeitura.sp.gov.br/documents/d/saude/manual_ps-e-hm_maio_2026.pdf | placas de orientação com diretório por setor |

Não foram inventados selos, certificações ou números de norma nas peças. As placas seguem `docs/direcao-arte/comunicacao-visual.md` (Nimbus Sans, `#e4e1d6` / `#2b2d29`; vermelho, verde e amarelo só para segurança).

## O que mudou

- **Placas**: três famílias com gabarito próprio. Identificação de sala (ID_*): uma linha, texto em Nimbus Sans Narrow, sem pictograma, colada. Orientação (OR_*): fundo verde-escuro, seta única paramétrica (mesma espessura e proporção em todas), parafusada. Segurança (SEG_*): símbolo da norma + legenda, parafusada; saídas são caixas luminosas com impressão rebaixada. Texto e pictogramas são SVG vetorial rasterizado (sem geração de imagem) — `Blender/Circulacao/scripts/sinais_vetoriais.py`.
- **Corpo das placas**: uma só malha por placa, três materiais (impressão / borda / parafuso), borda chanfrada e parafusos de cabeça panela — `Blender/Circulacao/scripts/placas.py`. Sem quads sobrepostos.
- **Linha fragmentada na borda inferior do "Descanso e vestiário"**: causa real = a face da placa ficava coplanar com a superfície do revestimento (placa de 3 mm + `FinishOffset` de 3 mm); também havia o submesh de lado/fundo escuro da malha antiga. As placas agora ficam em `parede + FinishOffset` e têm uma malha única.
- **Rotas**: diretório "Circulação interna" aponta → (o corredor segue a leste), "Recebimento de corpos / Câmara fria" aponta ↑ (em frente), a saída sobre a porta da circulação indica a porta (pictograma de frente) e a do corredor de serviço aponta → rumo ao pátio; nomes conferem com as placas de sala.
- **Extintor**: refeito (cilindro, pescoço, válvula, manômetro com mostrador impresso, alavanca, gatilho, pino, mangueira, suporte de parede). Rótulo único, texto legível e **não espelhado** (mapeamento cilíndrico corrigido); as faixas metálicas largas saíram. Placa de localização agora acima do extintor.
- **Acionador**: novo modelo (caixa vermelha em degrau, janela rebaixada, impressão "EM CASO DE INCÊNDIO / QUEBRE O VIDRO" com traço de quebra, fenda da chave), com o aviso (SEG_Alarme) **acima** do equipamento. O texto "quebre o vidro" corresponde ao mecanismo (janela de vidro/membrana). Sem mecânica de alarme.

## Comparações

`comparacoes/*.png` (antes à esquerda, depois à direita), `antes/` e `depois/` (capturas por câmera temporária na Unity, perto e na distância de leitura; `depois/` inclui todas as placas de sala, saídas, extintores e acionadores do corredor de serviço e do vestíbulo).

## O que foi conferido visualmente (na Unity)

Diretório, Arquivo, Descanso (de frente e em close da borda inferior — sem linhas), Depósito, Autópsia, Sala técnica, Recebimento, Câmara fria, direção do recebimento, risco elétrico, saída (frente e pátio), extintores (recuo, serviço, vestíbulo), acionadores (recuo, serviço). Rótulo do extintor e texto do acionador lidos de perto. Não foram conferidos a olho: a placa de câmara fria do lado do recebimento (só a do lado da autópsia), os dois acionadores de perto em ângulo oblíquo.

## Limitações restantes

- Corredor do descanso e câmara fria estão escuros no bake: placas de sala nessas áreas têm pouco contraste com a parede (cores corretas; é a iluminação).
- Parafusos aparecem sobre a borda vermelha do SEG_Alarme/SEG_Extintor (como nas placas reais, mas visualmente pesados no topo).
- Não houve novo bake de iluminação (nenhuma peça nova é estática); o acionador não tem mecânica nem áudio.
- As texturas e scripts antigos (`Placa_*.png`, `texturas_circulacao.py` parte de placas) ficaram no projeto sem uso.
