# Arquivo / escritório (administração) — referências e plano

Base de história: `Assets/História/Unclaimed_Puzzles_e_Storytelling_Demo.docx` (v1.0, 18/10/2026).
Sala do mapa: zona "Arquivo e escritorio" — corpo x -6.5..3.5, z -10.25..-6 e braço norte x -2..3.5, z -6..-3.5;
porta para a recepção em x -6.5 (folha com ferrolho do lado do arquivo), vão para a circulação em x -2, z -4.75.
Layout, tamanhos e conexões aprovados: não mudam. Não há janelas construídas nesta sala.

## Referências de mobiliário (tipologia de repartição pública brasileira)

1. **Mesa de servidor — especificação da Prefeitura de Guarulhos (ME-015, 2010)**
   https://portaldoservidor.guarulhos.sp.gov.br/files/me015.pdf
   Aproveitado: tampo de cantos arredondados, painel de fundo (modéstia), gaveteiro de 2 gavetas preso em estrutura
   de aço; um gaveteiro para servidor, dois para chefia.
2. **Arquivo de aço 4 gavetas para pasta suspensa** (anúncios Pandin / MadeiraMadeira / usados no Mercado Livre)
   https://www.madeiramadeira.com.br/gaveteiro-arquivo-em-aco-4-gavetas-suporta-45kg-cada-vermelho-cinza-marrom-646529.html
   https://lista.mercadolivre.com.br/arquivo-aco-usado
   Aproveitado: chapa 24/26 pintada (cinza / bege antigo), puxador-concha com porta-etiqueta, fechadura no topo
   travando todas as gavetas, corrediças telescópicas, cantos com pintura gasta nos usados.
3. **Estantes de aço com caixas-arquivo** — TR do TRF2 (troca de armários de madeira por estantes) e ETP de Estância Velha
   https://trf2.jus.br/system/files/2026/04/termo-de-referencia_5.pdf
   https://estanciavelha.rs.gov.br/uploads/edital/29567/Estudo_Tecnico_Preliminar___ETP_Estantes_de_Aco_com_prateleiras.pdf
   Aproveitado: estante de aço com prateleiras reguláveis, ~20 caixas por estante.
4. **Caixa-arquivo e pasta AZ** — cotação PR / TR de papelaria GO
   https://www.administracao.pr.gov.br/sites/default/arquivos_restritos/files/documento/2026-08/relatorio_pesquisa_mapa.pdf
   https://goias.gov.br/casacivil/wp-content/uploads/sites/47/2013/11/termo-de-referencia-papelaria-201300013003947-corrigido-af7.pdf
   Aproveitado: caixa de papelão kraft 360 × 260 × 140 mm com furos de ventilação; caixa de polipropileno
   350 × 250 × 130 mm; pasta AZ dorso largo A4 com visor e mecanismo niquelado.
5. **Guarda de pertences** — protocolo PRA NSE 001 do HC de Botucatu (guarda de pertences, inclusive de quem morre sem
   familiar presente, em armários específicos) e roupeiros de aço com portas numeradas/trinco para cadeado
   https://hcfmb.unesp.br/wp-content/uploads/2024/11/PRA-NSE-001-PROTOCOLO-SOBRE-A-GUARDA-DE-PERTENCES-finalizado.pdf
   https://www.casasbahia.com.br/armario-roupeiro-aco-12-portas-guarda-volume-trinco/p/1569954382
   Aproveitado: armário de aço de compartimentos com portas e etiquetas de endereço, trinco para cadeado.
6. **Lacre numerado / envelope de segurança** — guia de custódia de malotes e envelopes "tamper-evident"
   https://cursa.app/pt/pagina/entrega-de-valores-e-malotes-do-caixa-lacres-protocolos-e-seguranca
   https://dvm360.com/view/tamper-evident-envelope
   Aproveitado: número do lacre repetido no protocolo e no envelope; canhoto destacável; faixa adesiva que deforma.

Não foram encontradas fotos públicas de setores administrativos de SVO/IML; a composição segue as compras públicas acima
e a direção já aprovada ("necrotério em uso diário, manutenção irregular, sem ruína").

## O que a história exige desta sala

- **Mesa de Nina** (a mesa central atual): cópia das palavras cruzadas de Celso (prefab já existe), lista de pendências
  ("conferir original de 15/08/2024 nas pastas antigas da coordenação"), cópia da ficha D01 numa bandeja.
- **Impressora** com bilhete de rotina em cima; ao lado, as pastas de manuais — a segunda pasta de Nina tem aba "041"
  discretamente visível (H03, Cemitério Santa Amélia, setor C, registro 12).
- **Armário de pertences**: colunas A/B × níveis 1/2/3, legenda colada na porta "G: guarda separada — endereço: coluna
  e nível"; B2 guarda o gravador P26 item 03 com a cópia do D10. Três bolsas P24/P25/P26 com etiquetas danificadas.
- **Bancada** com cabo USB de alimentação preso (carregar o gravador) e a pasta de guarda (outra via do D10).
- **Estante da coordenação**: pastas por data (Helena); agosto/2024 com aba "saídas / revisão" — H07 atrás de uma ordem
  de conserto; H04 (relatório 20/08/2024), proposta de transferência e três desculpas riscadas junto dos papéis.
- **Pasta do contrato Limiar Serviços**: referência 784 (dois transportes extraordinários, aprovação D. Vilar LV-12,
  rodapé de Léo) e a lista de nomes João / Célia / Léo / Daniel.
- **Arquivo de prontuários** (aço, gavetas): registro antigo onde H02 vincula P17 ao caso 041 (pedido de Irene).
- Regras do documento: no máximo uma leitura pessoal de destaque no primeiro percurso; não colocar todos os documentos em
  gavetas idênticas (usar bandeja, pasta, envelope, aba, quadro); nada de senha em objetos pessoais.

## Plano de execução

1. Modelos Blender (Claude, pipeline da recepção: atlas baked): mesa de servidor, arquivo de aço 4 gavetas, estante de
   aço com caixas/AZ, armário de pertences 2×3 com portas, bancada, impressora, bolsas de pertences, gravador, bandejas,
   pastas com abas. Reaproveitados: cadeira do atendente, lixeira, luminária de sobrepor, kit de computador.
2. Papéis com o pipeline de documentos da recepção (Pillow) e os textos literais do documento de design.
3. Gerador: `UnclaimedMorgueGenerator.Arquivo.cs` com ZoneFinish e posicionamento; portas do armário via PecaDeMovel.
4. Fora deste passo (mecânicas): reprodução do gravador, carregamento, navegação pelas pastas, puzzles P05–P09/O02.
