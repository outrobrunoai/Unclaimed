# Recepção — referências visuais

Fotos reais usadas só como referência de composição, mobiliário e acabamento (nenhuma é textura dos assets).

1. **UPA de Tangará da Serra (MT), sala de espera** — Pulsar Imagens, foto 11LW978
   https://www.pulsarimagens.com.br/foto/Pessoas-aguardando-atendimento-na-UPA---Unidade-de-Pronto-Atendimento---?codigo-imagem=11LW978&codigo=371236
   Aproveitado: longarinas de 3 lugares em polipropileno sobre pés de aço; balcão laminado claro com
   cartazes e TV na parede atrás; porcelanato bege polido 60 × 60; luminárias tubulares de sobrepor;
   faixa/barrado mais escuro na meia-parede; bebedouro de pressão junto à parede; acesso restrito ao lado do balcão.

2. **UBS Ouro Preto, Olinda (PE), requalificação** — Wikimedia Commons (Prefeitura de Olinda)
   https://commons.wikimedia.org/w/index.php?search=Inaugura%C3%A7%C3%A3o+da+requalifica%C3%A7%C3%A3o+da+Unidade+B%C3%A1sica+de+Sa%C3%BAde+de+Ouro+Preto
   Aproveitado: placas institucionais brancas com faixa azul e pictograma ("RECEPÇÃO", "CONSULTÓRIO");
   revestimento lavável até meia altura e pintura branca acima; luminárias tubulares no teto; quadro de
   avisos pequeno; cadeiras pretas simples na espera.

3. **Hospital do ISSSTE Adolfo López Mateos, Cidade do México, sala de espera** — Wikimedia Commons
   https://commons.wikimedia.org/wiki/File:Sala_de_espera_de_hospital.jpg
   Aproveitado: unidade pública com longarinas em fileira, piso polido e forro modular; a espera
   encostada às paredes com a circulação livre no centro.

4. **Sala de espera de clínica pequena** — Wikimedia Commons
   https://commons.wikimedia.org/wiki/File:A_waiting_room_at_a_medical_healthcare_clinic,_doctor%27s_office,_hospital.jpg
   Aproveitado: assentos agrupados em L de costas para as paredes, corredor livre até a porta interna,
   sinalização discreta acima das portas, forro modular com luminárias embutidas/sobrepostas.

Não foram encontradas fotos adequadas de recepções de IML/SVO ou serviços funerários (as reportagens
mostravam fachadas e obras); a composição segue as unidades públicas de saúde acima.

## Rodada de correções (2026-10-10)

**Símbolo Internacional de Acesso (placa e demarcação no piso)** — Wikimedia Commons, domínio público
https://commons.wikimedia.org/wiki/File:International_Symbol_of_Access.svg
Desenho original da Rehabilitation International (1968), o mesmo adotado pela NBR 9050; arquivo SVG de
Jacklau96. Os três caminhos vetoriais estão copiados sem alteração em `scripts/texturas_recepcao.py`
(`ISA_PATHS`) e são rasterizados com `rsvg-convert`; o símbolo é recortado pelo próprio contorno e
centralizado no campo azul, sem redesenho.

**Teclado, telefone e mouse** — sem fotos: proporções de equipamento comum de escritório.
- Teclado completo (ANSI/ABNT2): passo de 19 mm entre teclas; linha de funções em grupos de quatro;
  bloco principal com Backspace 2u, Tab 1,5u, Caps 1,75u, Enter 2,25u, Shifts 2,25u/2,75u, barra de
  espaço 6,25u; bloco de navegação 2 × 3, setas em T invertido; teclado numérico com + e Enter altos e 0
  largo.
- Telefone de mesa: base inclinada, visor monocromático, teclado 3 × 4 de teclas separadas com coluna de
  funções, monofone apoiado em dois apoios do berço, do lado esquerdo do usuário.
- Mouse de dois botões: botões cobrindo ~40 % da frente, separados por um vão, roda de rolagem de
  borracha no vão.

## Luminárias e porta de vidro (2026-10-10)

Sem fotos baixadas: proporções de produtos comuns em unidades públicas brasileiras.

**Luminária de sobrepor** (`LuminariaSobrepor`): calha para 2 tubos de 1,20 m (T8 ou tubo LED), corpo em
chapa de aço pintada branca com cantos arredondados, 1,27 × 0,26 × 0,072 m; difusor de acrílico leitoso
prismático apoiado nos lábios da chapa e preso por presilhas; tampas laterais injetadas com dois
parafusos; soquetes nas pontas; dois parafusos de fixação no refletor. Materiais separados: carcaça
(atlas bakeado), difusor (acrílico translúcido, aceso/apagado) e tubos (emissivos; reaproveitam
"Difusor aceso/gasto/apagado" da autópsia).

**Arandela da entrada** (`LuminariaExterna`): caixa de alumínio fundido pintado em grafite, com o topo
inclinado, vidro difusor embaixo e placa de LED; reaproveita o difusor e o material emissivo internos.

**Porta da entrada** (`PortaVidroFixos`, `PortaVidroFolha`, `PortaVidroVidro`): vão livre medido
1,94 × 2,17 m (entre batentes; soleira 6 mm). Duas folhas de vidro temperado de 10 mm, de 0,96 m cada:
uma folha de 1,94 m seria pesada e fora do padrão para pivô, e vãos assim em clínicas e prédios públicos
usam porta dupla pivotante com mola de piso. Sistema de ferragens para vidro temperado (tipo
1101 superior / 1013 inferior com mola hidráulica de piso, fechadura inferior 1520 com contra-fechadura
de piso), puxadores tubulares de inox de 600 mm dos dois lados, prendedores de piso que seguram as folhas
abertas. Faixa de película jateada entre 1,30 e 1,40 m (faixa superior pedida pela NBR 9050 em portas
de vidro); marcas de mão junto aos puxadores e sujeira leve no rodapé do vidro (textura).

## Revisão do posto de atendimento (2026-10-10)

**Mouse** (`office_mouse` em `scripts/recepcao_modelo.py`) — fotos examinadas (Wikimedia Commons):
- Vista oblíqua/superior, mouse óptico USB com fio: https://commons.wikimedia.org/wiki/File:Lenovo_USB_Optical_Wheel_Mouse.png (CC BY 2.0)
- Vista superior, mouse óptico de dois botões: https://commons.wikimedia.org/wiki/File:Raton_optico_Logitech_-_Logitech_optical_mouse_-_01.jpg (CC BY-SA 4.0)
- Vista lateral ortogonal (perfil e base plana): https://commons.wikimedia.org/wiki/File:Apple_Mighty_Mouse_Side_Orthogonal_View.jpg (CC BY-SA 3.0)
- Detalhe da roda na fenda entre os botões: https://commons.wikimedia.org/wiki/File:MS_Mouse_tilt_wheel.JPG (CC BY-SA 3.0)
Aproveitado: contorno oval mais largo atrás e frente arredondada; botões cobrindo ~40 % da frente com
divisão central reta e costura transversal; roda numa fenda a ~25 % da frente; cabo saindo baixo no
centro da frente; perfil baixo com o ponto mais alto a ~60 % para trás. 115 × 62 × 37 mm, sem marcas.

**Caneta** (`pen`): esferográfica comum de corpo sextavado, cone branco com ponta metálica, tampa com
clipe e tampinha traseira (140 mm tampada); o mesmo modelo está no porta-canetas e sobre o livro.
**Passa-cabos** (`grommet`): passa-fio de 60 mm de embutir, com aro de aba, luva e tampa com recorte.
