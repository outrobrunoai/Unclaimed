# Sala técnica — referências e decisões

## Função da sala

A sala abriga os quadros elétricos e o comando do necrotério. Ela não funciona como oficina, depósito nem sala de servidores. Há três equipamentos na parede norte:

1. **QDG (quadro de distribuição geral):**
   - recebe o alimentador pelo piso;
   - distribui os circuitos das áreas.
2. **Painel de contingência (P02):**
   - alimentação provisória da luz geral da autópsia, da luminária de exame e da refrigeração;
   - é o equipamento ficcional simplificado descrito em `Assets/História` (P02, D04).
3. **Comando da câmara fria:**
   - controlador digital de temperatura;
   - sinaleiros de ligado, degelo e alarme.

A bancada de manutenção do Mauro fica na parede norte, a leste, com o painel de ferramentas acima. A história descreve um técnico que conserta dobradiças e "separa parafusos em potes de café".

**Ficaram de fora:**
- **Condensadora:** o antigo marcador "Condensadora" foi removido. A unidade condensadora real fica fora do prédio, e o mapa não tem lugar previsto para ela no entorno desta sala.
- **Rack de servidores:** a história não menciona servidores.
- **Escada:** saiu para não obstruir a circulação.

## Referências reais

| Fonte | O que foi aproveitado |
|---|---|
| Painel de sobrepor 400×400×120 mm ([MadeiraMadeira](https://www.madeiramadeira.com.br/painel-embutir-caixa-quadro-400x400x120mm-799128332.html)) | Referência principal dos quadros: chapa de aço com porta de abas dobradas, dobradiças externas, gaxeta, fecho de 1/4 de volta com chave triangular, orelhas de fixação e placa de montagem. |
| Full Gauge TC-900E Log ([produto](https://fullgauge.com/en/products/tc-900e-log); [guia de instalação Robertshaw](https://www.robertshaw.com/wp-content/uploads/2026/06/352-00356-002-TC-900E-2HP-Install-Guide.pdf)) | Controlador da câmara fria: recorte de 71×29 mm, display de 3 dígitos vermelho e quatro teclas sob a janela. Modelado sem marca. |
| Full Gauge, caso de câmara fria pequena ([notícia](https://www.fullgauge.com/en/news/small-cold-storage-company-doubles-production-with-full-gauge-controls-technology-and-the-sitrad-software)) | Caixa de comando na parede com display, ao lado da câmara. Painéis com poucos sinaleiros, sem telas grandes. |
| Wikimedia Commons, *Fuse box with temporary circuits* (CC BY-SA 3.0, `referencias/commons_quadro_provisorio.jpg`) | Caixa cinza de alimentação provisória com o esquema preso na porta. Reforçou a ideia de usar só um cartão de legenda e uma planta, presos com rebites. |
| Wikimedia Commons, *Fuse box on hager components* (CC BY-SA 3.0, `referencias/commons_disjuntores_hager.jpg`) e *Distribution board in Slovakia 05* (CC BY 4.0, `referencias/commons_quadro_eslovaquia.jpg`) | Identificação de circuitos por posição. No QDG, a relação de circuitos lista as áreas atendidas, sem correntes nominais. |
| Wikimedia Commons, *Pt tupper cable spreading room* (CC BY 3.0, `referencias/commons_bandejas_cabos.jpg`) e [engehall — como instalar eletrocalha](https://engehall.com.br/como-instalar-eletrocalha/) | Eletrocalha perfurada sobre mão-francesa, cabos soltos no fundo e saídas para eletroduto. |
| Especificação UFU ([PDF](https://licitacoes.ufu.br/sites/licitacoes.ufu.br/files/1ccg-ele-05-SALAS%20AULA-02_02.pdf)) | Calha perfurada só acima de 2,5 m ou em área técnica (aqui está a 2,70 m); derivações saem pela lateral. |
| SES-SP ([PDF](https://saude.sp.gov.br/resources/ses/perfil/cidadao/licitacoes-cga/hrlb_pe_sis_005_2.pdf)), memorial FUNPEC ([PDF](https://funpec.br/wp-content/uploads/2026/08/5.-MEMORIAL-DESCRITIVO-PROJETO-DE-INSTALACOES-ELETRICAS.pdf)), SINAPI ([insumo](https://orcamentor.com/insumo/39761/)) | Eletroduto aparente de aço galvanizado pesado (NBR 5624) e quadros de chapa de aço com pintura eletrostática. |
| Wikimedia Commons, *The Case Of The Missing Screwdriver* (CC BY 3.0, `referencias/commons_painel_ferramentas.jpg`) e painel Marcon PM-P 640×970 ([Loja do Mecânico](https://www.lojadomecanico.com.br/produto/76091/11/440/painel-em-chapa-perfurada-640-mm-x-970-mm-marcon-pm-p)) | Painel perfurado de 970×640 mm com ganchos de arame. As chaves ficam penduradas pela haste e os alicates pelos cabos, com poucas ferramentas. |

## Decisões

- **Disposição na parede norte:** QDG em x −3,00, painel de contingência em −1,95 e comando da câmara fria em −1,05, todos com os comandos entre 1,25 e 1,55 m. Há 1 m livre na frente dos quadros.
  - A folha ativa da porta varre x −4,22…−3,36 / z ≤ 6,73. Por isso o extintor fica na parede oeste, em z 7,40, fora dessa faixa.
- **Cabos e eletrodutos:** nenhum cabo fica sem destino.
  - Cada quadro sobe por um eletroduto de cerca de 1 m até uma saída sob a eletrocalha.
  - Três eletrodutos saem pela lateral da calha, fazem uma curva de 90° e entram na laje, onde os circuitos seguem embutidos até as salas. O mapa não tem forro rebaixado.
  - O alimentador do QDG vem do piso.
  - O cabo do sensor da câmara fria sai por prensa-cabos e sobe preso à parede com presilhas.
- **Painel de contingência:** implementa os objetos do P02 só como estado visual. A lógica fica para uma próxima rodada.
  - Seletores: refrigeração no seletor com chave, travado em 1; luz geral em 1; luminária de exame em 0.
  - Sinaleiros: refrigeração e luz geral acesos; luminária de exame apagada.
  - O display mostra **9** no TOTAL e **10** no LIMITE, em dígitos de sete segmentos, sem depender de cor.
  - O cartão D04 traz o texto do documento, sem acréscimos.
  - A planta foi desenhada a partir dos retângulos de zona do gerador. A autópsia aparece hachurada como "LUZ GERAL (3)", e a luminária de exame tem seu símbolo na posição real do foco cirúrgico.
  - O seletor e o sinaleiro são assets separados, para o P02 poder girá-los e acendê-los.
- **QDG:** placa, adesivo NR-10 e relação de circuitos por área. O circuito 10 é a "alimentação provisória — painel de contingência", com a nota do Mauro "ver painel ao lado". Não há números de corrente, código nem senha.
- **Comando da câmara fria:** o controlador mostra 3.0 °C. Os sinaleiros são LIGADO (aceso), DEGELO e ALARME (apagados). Não há seletor, para não sugerir uma segunda interação de refrigeração.
- **D03:** folha de caderno do Mauro com a resposta da Helena, presa com fita crepe na parede, à direita do comando da câmara fria (x −0,60, y 1,42).
- **Marcas de uso:**
  - os quadros têm poeira nas faces superiores, polimento e sujeira de mão em volta do fecho e lascas na aresta do fecho;
  - o QDG, mais antigo, tem mais desgaste;
  - o tampo da bancada tem a borda gasta e marcas de ferramenta;
  - o piso tem desgaste na frente dos quadros, na entrada e na bancada.
- **Fora do escopo:** sem faíscas, piscadas, vazamentos ou sujeira uniforme. A iluminação geral aprovada fica igual; as duas luminárias da sala só ganharam o modelo visível.

Capturas da validação do bloco estão em `layout/` e as finais em `capturas/`.
