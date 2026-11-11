# Autópsia — layout validado (etapa 3)

Medidas conferidas na cena em 08/11/2026: o interior vai de x −1,90 a 7,40 e de z −3,40 a 3,525, com pé-direito de 3,20 m. A porta dupla clínica fica no oeste, para a circulação, com vão em z −0,75…0,75. A porta dupla frigorífica fica no leste, para a câmara fria, com vão em z −0,50…1,00. **As duas abrem para dentro da autópsia**, e as folhas a 95° varrem x −1,90…−1,15 (oeste) e x 6,65…7,40 (leste). O circuito circulação → autópsia → câmara fria foi preservado.

## Zonas

| Zona | Conteúdo | Origem |
|---|---|---|
| Mesa principal | Mesa de pedestal (2,8 × 0,8 m, tampo a 0,9 m), cabeça a oeste, apoio de borracha na cabeceira, cuba no pé. Corpo coberto (estado ocupado) ou lençol caído (variante P08, desativada). | Mesa mantida. Corpo e lençol novos. |
| Iluminação de trabalho | Luminária de exame de cabeça única no teto, sobre a mesa, com braço articulado. Fica **apagada**, com emissão e luz controladas juntas. O comando local fica na parede sul, com eletroduto aparente até o teto e o aviso de alimentação provisória. | Nova. Substitui o "foco cirúrgico". |
| Bancada de apoio (norte) | Bancada inox com cuba e gabinetes; balança de vísceras suspensa entre a cuba e o armário; armário suspenso de vidro; suporte de luvas; coletor de perfurocortantes na ponta leste do tampo; um único grupo de trabalho (tábua com poucos instrumentos), com o resto do tampo livre. | Bancada, balança e armário novos. Suporte de EPI reaproveitado (Recebimento). |
| Higienização (sul) | Lavatório inox com acionamento por joelho, dispensers e lixeira clínica de pedal. | Lavatório novo. Lixeira reaproveitada (Recebimento). Dispensers reaproveitados (WC). |
| Armazenamento | Armário de instrumental com portas de vidro (parede oeste, ao sul da porta); estante de insumos (parede leste, ao norte da porta da câmara fria). | Armário novo. Estante e caixas reaproveitadas (Depósito). |
| Carro | Carro auxiliar estacionado ao sul do pé da mesa, junto da cuba (x 4,55…5,43, z −2,00…−1,34). | Reaproveitado (Recebimento). |
| Registros | Quadro branco apagado na parede oeste (z 1,6…2,4) e relógio acima da porta. | Quadro novo. Relógio reaproveitado (Descanso). |
| Emergência | Bloco autônomo acima da porta oeste, com alimentação independente (P02). | Reaproveitado (Circulação). |

**Removidos:** a maca de transporte provisória (no P08, a maca do recebimento é evidência), o banco, o enrolador de mangueira na parede (a mesa já tem torneira e ducha), a lixeira comum duplicada, a balança de prato, os frascos repetidos e as caixas soltas sobre a bancada.

## Verificações

[`layout/planta_validacao.png`](layout/planta_validacao.png) foi gerada por `planta.py`, com teste de eixo separador:

- Maca de 2,20 × 0,73 m fazendo o circuito porta oeste → lado norte da mesa → porta leste, com amostra a cada ~5 cm:
  - folga mínima de **0,235 m** aos móveis, no pé da mesa;
  - folga mínima de **0,176 m** aos batentes da porta leste.
- Nenhuma folha de porta, aberta a 95°, encosta em móvel.
- Espaço livre em volta da mesa:
  - **2,92 m** ao norte, até a bancada (circulação e transferência);
  - **0,49 m** ao sul, até o carro, que fica só no trecho do pé;
  - **2,55 m** ao sul, até a parede, no resto do lado sul;
  - **2,95 m** na cabeceira oeste.
- Gavetas e portas da bancada abrem para o lado norte, dentro da faixa livre de 2,9 m.

Bloqueio com volumes simples pela câmera do Player (`layout/bloco_*.png`): os placeholders foram escondidos com `forceRenderingOff` e os blocos usaram `HideAndDontSave`, então a cena não foi alterada (`isDirty=false`). Isso mostrou que o carro na diagonal sudoeste ficava em primeiro plano demais na vista da entrada e competia com a mesa. Ele foi levado para o pé da mesa.
