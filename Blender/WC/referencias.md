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
- **Espelho** 0,50 × 0,70 m, vidro de 4 mm com borda lapidada e quatro presilhas cromadas. No URP sem
  ray tracing, um espelho real (câmera planar/render texture) custaria um render extra por quadro e
  precisaria de script; foi usado o **reflection probe bakeado e box-projected do WC** (256 px, sem faixa
  de blend, para não desvanecer na parede). Limites: o reflexo é estático e de baixa resolução — não
  mostra o jogador, objetos que se movem, nem a porta em outro estado além do bakeado (aberta); a
  paralaxe só é correta para as paredes (caixa da sala).
- **Porta**: folha opaca laminada de 35 mm, batente com batedor, guarnições, maçanetas de alavanca dos
  dois lados, roseta de chave decorativa (sem sistema de tranca), chapas de proteção e prendedor de piso.
  Reaproveita `FolhaDePorta` (sentido, limite de 95°, colisor da folha, empurrão do jogador).
