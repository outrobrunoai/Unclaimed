# WC — referências visuais

Fotos reais (Wikimedia Commons) usadas só como referência de layout, louças, metais e acabamento; nenhuma
virou textura.

1. **Banheiro de hospital, Suresnes (França)** — Philippe HICKX, domínio público
   https://commons.wikimedia.org/wiki/File:Bathroom_in_hospital_in_Suresnes,_Arrondissement_de_Nanterre,_%C3%8Ele-de-France.jpg
   Aproveitado: vaso com caixa acoplada encostado na parede do fundo, barra de apoio na parede lateral,
   papeleira de rolão branca na parede; paredes lisas e claras, rodapé escuro, piso cerâmico fosco.
2. **Sanitário público com barras de apoio** — EMsmile, CC BY-SA 4.0
   https://commons.wikimedia.org/wiki/File:Public_toilet_with_grab_bars_(ambulant_toilet).jpg
   Aproveitado: barra de inox escovado em L afastada da parede por flanges; papeleira junto ao vaso, na
   altura da mão sentada.
3. **Pias em sanitário público, Edimburgo** — Loranchet, CC BY 3.0
   https://commons.wikimedia.org/wiki/File:Sinks_in_a_public_toilet,_Edinburgh,_Scotland.JPG
   Aproveitado: lavatório de louça suspenso, torneira de mesa com acionamento por pressão, sifão e
   flexíveis aparentes sob a pia; saboneteira de refil e dispensers de papel toalha pequenos na parede;
   sujeira concentrada ao redor das pias e no rejunte.
4. **Cabine de sanitário, North Middlesex Hospital (Londres)** — Acabashi, CC BY-SA 4.0
   https://commons.wikimedia.org/wiki/File:North_Middlesex_Hospital_toilet_cubicle_Enfield_London_England.jpg
   Aproveitado: lixeira de pedal ao lado do vaso, uso intenso visível no piso e no rejunte junto ao pé
   do vaso; paleta institucional dessaturada.

## Decisões

- **Planta**: contorno e posição do acesso mantidos (4,18 × 3,05 m livres, porta na parede sul). O vão
  passou de 1,50 para 1,00 m (porta de giro de 0,93 m de folha; 1,50 m não existe em porta comum de
  sanitário). Vaso na parede norte com eixo a 0,45 m da parede oeste, barra e papeleira nessa parede;
  lavatório, espelho, saboneteira, toalheiro e lixeira na parede leste. O giro da porta (95°, para dentro,
  dobradiças a leste) fica livre de acessórios. Há itens de acessibilidade (barra de apoio, folha larga),
  mas a sala **não** foi dimensionada nem verificada pela NBR 9050.
- **Acabamentos**: piso porcelanato 45 × 45 cinza-bege fosco (rejunte 3 mm); azulejo branco 30 × 45 até
  1,80 m (quatro fiadas inteiras desde o piso, alinhadas em todas as paredes), perfil de alumínio no
  arremate, tinta lavável acima; forro de PVC em réguas de 10 cm com moldura; rodapé de porcelanato;
  soleira de granito na porta. As texturas usam UV em metros (mesma escala em todas as paredes).
- **Desgaste localizado**: rejunte escurecido em volta do pé do vaso, calcário atrás da torneira, marcas
  de água no piso em frente à pia, ralo.
- **Louças e metais** (`scripts/wc_modelo.py`): vaso 0,65 m de profundidade, caixa acoplada 38 × 18 × 37 cm
  com acionamento duplo, assento e tampa separados, dobradiças, registro e flexível; lavatório 50 × 42 cm
  a 0,80 m, cuba por booleana elipsoidal, sifão de copo e flexíveis. Superfícies arredondadas e
  subdivididas, sem facetamento visível.
- **Espelho** 0,50 × 0,70 m, vidro de 4 mm com borda lapidada e quatro presilhas cromadas. Desde a rodada
  de 2026-10-11 o reflexo é **planar** (`Assets/Scripts/EspelhoPlanar.cs` + shader `Unclaimed/Espelho planar`):
  uma câmera auxiliar refletida no plano do espelho, com corte oblíquo, que só renderiza quando o espelho está
  visível. A sonda bakeada do WC ficou como reserva (Edit Mode / espelho fora da tela).
- **Porta**: folha opaca laminada de 35 mm, batente com batedor, guarnições, maçanetas de alavanca dos
  dois lados, roseta de chave decorativa (sem sistema de tranca), chapas de proteção e prendedor de piso.
  Reaproveita `FolhaDePorta` (sentido, limite de 95°, colisor da folha, empurrão do jogador).

## Rodada de acabamento e interações (2026-10-11): referências brasileiras

Fotos do Wikimedia Commons, só como referência (nenhuma virou textura):

5. **Banheiro da Escola Ferreira Mendes** — SrCalabres, CC0
   https://commons.wikimedia.org/wiki/File:Banheiro_da_Escola_Ferreira_Mendes.jpg
   Aproveitado: sifão sanfonado branco aparente sob a pia, barras de apoio de inox, revestimento até o teto
   com rejunte encardido, ralo no piso entre as cabines, sujeira concentrada perto das louças.
6. **Ibirubá (RS) — banheiro público (2)** — Sintegrity, CC BY-SA 4.0
   https://commons.wikimedia.org/wiki/File:Ibirub%C3%A1_-_Banheiro_p%C3%BAblico_(2).jpg
   Aproveitado: forro de PVC em réguas brancas, azulejo branco, dispensers plásticos brancos de sabonete e
   papel na parede ao lado da pia, porta comum de giro, torneira de parede/mesa simples.
7. **Banheiro 001 (2019)** — Edvaldo LL Souza, CC BY-SA 4.0
   https://commons.wikimedia.org/wiki/File:Banheiro_001_2019-01-29.jpg
   Aproveitado: azulejo branco até ~1,5–1,8 m com pintura acima, dispensers de papel e sabonete brancos,
   vaso com assento branco e papel ao lado.

Decisões desta rodada:
- **Sifão**: o sifão de copo cromado virou **sifão sanfonado de PVC branco** com bucha e canopla brancas
  (o que se vê na maioria dos sanitários públicos brasileiros); a válvula cromada continua.
- **Papel no lixo**: placa "Favor não jogar papel no vaso — use a lixeira" sobre a caixa acoplada e uma
  segunda lixeira de pedal no canto ao lado do vaso (hábito brasileiro, coerente com a placa).
- **Escova sanitária** com pote branco no vão entre o vaso e a parede oeste (fora da circulação).
- **Instalação/manutenção**: ralo quadrado de inox 15 × 15 cm centrado numa peça do piso, com marca de
  umidade em volta; grelha de exaustão no forro (sanitário sem janela); silicone branco no pé do vaso e no
  encontro do lavatório com o azulejo; cabide de inox no lado de dentro da porta; ficha de **controle de
  limpeza** (data, hora, responsável, visto) num porta-folhas de acrílico ao lado da porta.
- **Interações**: torneira de pressão (E abre/fecha; o botão afunda e volta; fluxo, espuma e respingos na
  cuba), descarga de acionamento duplo (E aperta o botão total; ciclo de 6 s sem acumular). A tampa do vaso
  fica fechada, então não há água visível na bacia para animar.
- Circulação, acesso ao vaso e à pia, barra de apoio e arco da porta preservados; nada novo tem colisor na
  área de passagem (só a lixeira do canto e o pote da escova, encostados nas paredes).
