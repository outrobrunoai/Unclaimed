# Câmara fria — referências e decisões

Sistema escolhido (com o usuário): **módulo de compartimentos individuais 2 × 2** (4 posições, C-01..C-04), não câmara walk-in.
Todas as fontes são fabricantes brasileiros. As fotos servem só como referência visual; nenhum modelo foi baixado ou reaproveitado.
Mosaico das fotos usadas: `mosaico_ref.jpg`.

| # | Fonte | Uso |
|---|---|---|
| R1 (principal) | Grumed, *Câmara refrigerada para conservação modulada, 02 a 12 gavetas*, [página](https://grumed.com.br/home/equipamentos-para-necropsia/camara-refrigerada-para-conservacao-modulada-com-capacidade-de-02-a-12-gavetas/) | Portas individuais isoladas, com dobradiças de pivô em inox e "trincos de alta pressão" com manopla de baquelite. Porta-fichas de policarbonato. Trilhos telescópicos sobre roletes de nylon, com **parada em dois estágios**. Macas removíveis em inox 304 para 250 kg. Sistema split, termômetro digital com alarme, ponto de luz por porta. |
| R2 | Tectermica, *Morgue frigorífico*, [página](https://www.tectermica.com.br/produtos/morgue-frigorifico/) e [catálogo técnico PDF](https://www.tectermica.com.br/assets/morgue-frigorifico_catalogo-tecnico.pdf) | **Dimensões**: pilha de dois níveis com 1590 mm mais 100 mm de base, 1165 mm por coluna, 2330 mm de profundidade. Porta individual de 550 × 800 mm, divisão superior a 750 mm (+100). Faixa de 0/2 °C. Condensação split ou plug-in. |
| R3 | Iromtec, *Câmara mortuária*, [página](https://iromtec.com.br/produtos/camara-mortuaria/) | Pés em tubo inox de 2,5" com sapatas niveladoras de polietileno. Piso interno inclinado para um ralo por gravidade. Iluminação em cada compartimento. Faixa de 2–6 °C. |
| — | Grunox ([página](https://www.grunox.com.br/produto/camara-frigorifica-mortuaria/)) e Metalrocha ([página](http://metalrocha.com.br/produtos/camaras/camara-mortuaria-2-corpos-/11)) | As páginas não carregaram, então não foram usadas. |

## Decisões derivadas
- **Medidas**: módulo de 2,33 × 1,69 × 2,33 m (R2). Portas de 0,80 × 0,55 m, abrindo 95° (D com dobradiça à direita, E à esquerda). Bandeja de 1,88 × 0,60 m.
- **Trilho em dois estágios** (R1): a primeira parada fica a 1,00 m, que é o curso jogável (P07: "o movimento da bandeja tem um limite curto"). A extensão total é de 1,95 m. O trilho intermediário anda à metade da velocidade.
- **Refrigeração coerente com o split** (R1/R2):
  - evaporadores dentro do módulo, cada um com dois ventiladores;
  - linhas frigoríficas isoladas e uma bucha de passagem na parede;
  - **unidade condensadora do lado de fora** (parede leste), sem condensadora doméstica dentro da sala.
- **Controlador digital na parede** (R1): mostra 1,8 °C, com SET 1,0 dentro da faixa de 0/2 °C (R2) e o alarme apagado, já que não se inventa alarme. A história não define a temperatura.
- **Drenagem** (R3): fundo dos compartimentos inclinado e ralo no piso em frente ao módulo.
- **Sem** névoa, gelo, água permanente ou áudio novo nesta etapa. O ruído de compressor (E01) fica para a etapa de áudio.

## Ocupação (Assets/Historia, P07/D02, sem inventar números)

| Posição | Quadro | Físico | Modelo |
|---|---|---|---|
| C-01 | 081 ocupada | João Ferraz (P24) | Coberto pelo lençol. Só o lençol é exportado; o corpo MPFB serviu de colisor. |
| C-02 | livre | **o verdadeiro 083, Léo Martins (P26)**, movido sem registro | Corpo MPFB diferente do de Daniel na mesa, com outra pele CC0. Só o braço esquerdo fica fora do lençol, com a tala rígida no antebraço e a pulseira 083. O rosto fica coberto. |
| C-03 | interditada | vazia | Etiqueta grande "INTERDITADA / NÃO UTILIZAR". |
| C-04 | 082 ocupada | Célia Prado (P25) | Coberta pelo lençol. Só o lençol é exportado. |

- As pistas do 083 da mesa (adesivo na mão direita, etiqueta de movimentação no lençol) **não** se repetem em C-02.
- A pulseira traz só "083" e o nome, que constam do documento. Não há data, motivo nem código.
- As fichas das portas seguem o quadro: C-02 e C-03 ficam em branco.

## Base humana
- MPFB 2.0.18 (MakeHuman, CC0), como no corpo da autópsia (`docs/revisao-autopsia/corpo_base/README.md`).
- Pele de Léo: `mindfront_skin_male_african_middleage`, do pacote Skins 02 (CC0, já autorizado pelo usuário). O corpo de Daniel usa outra pele.

## Rodada 2 — capacidade maior (mosaico: `mosaico_ref_rodada2.jpg`)
- R1 (Grumed, mesma página): os módulos são montados com "três corpos sobrepostos na vertical por módulo"; a página lista como acessórios o "carro pantográfico para elevação de corpos" e a estrutura tubular de empilhamento com canaletas-guia. Daí a torre de 3 níveis e o carro elevador.
- Fiocchetti (resultado de busca; a página não abriu): gabinete de 6 corpos em 3 níveis com 191 × 226 × 250 cm e carro hidráulico com elevação de 32 a 165 cm. Serviu só para conferir a ordem de grandeza da altura (torre: 2,44 m; trilho de cima a 1,80 m).
- LEEC, carro de carga e elevação eletro-hidráulico de 180 kg ([página](https://healthmanagement.org/products/view/mortuary-trolley-loading-lifting-electro-hydraulic-180-kg-leec)): confirma o tipo de equipamento; sem medidas visíveis.
- Páginas da MedicalExpo (Labotemp MOR-14 e outras) recusaram o acesso e não foram usadas.
