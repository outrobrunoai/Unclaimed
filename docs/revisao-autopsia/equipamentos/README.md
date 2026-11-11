# Autópsia — equipamentos rígidos (etapa 5)

## Fonte, saída e integração

- **Fonte:** `Blender/Autopsia/scripts/gerar.py`, com as convenções de `Blender/Recebimento` e o mesmo inox acetinado.
- **Saída:**
  - modelos em `Assets/Modelos/Autopsia/*.fbx`;
  - texturas de 4 mapas em `Texturas/`;
  - prefabs em `Prefabs/`, gerados por `Tools > Unclaimed > Autopsia > Build Prefabs`.
- **Posições:** `Assets/UnclaimedPrototype/Autopsia/Layout.json`.
- **Atualização na cena:** `Tools > Unclaimed > Autopsia > Update Autopsy Room`. A rotina troca só o mobiliário e os equipamentos da autópsia e preserva a mesa, o corpo, as marcas de uso, as luminárias de teto, a sonda e as outras salas.

| Peça | Medidas | Observações |
|---|---|---|
| BancadaAutopsia | 3,80 × 0,65 m, tampo a 0,90 | Tampo AISI 304 com frontal dobrado, borda de contenção, espelho de 150 mm, cuba funda e bica alta clínica. Gabinetes inox sobre rodapé recuado. |
| ArmarioSuspenso (+ Vidro) | 1,60 × 0,70 × 0,33 | Duas folhas de vidro de correr e prateleira de vidro. Poucos insumos, sem impressão. |
| ArmarioInstrumental (+ Vidro) | 1,40 × 0,45 × 1,95 | Base inox fechada e parte superior envidraçada, com caixas de instrumental. |
| LavatorioJoelho | 0,60 × 0,45, borda a 0,90 | Frontal de 160 mm, fundo inclinado, alavanca de joelho e bica pescoço de ganso. |
| BalancaVisceras | braço a 2,05 m | Mostrador de 260 mm só com graduações (sem números). O prato fica 0,40 m acima do tampo. |
| LuminariaExame (+ Lente) | cabeça de 410 mm a 2,1 m | Cabeça única e dois braços articulados. **Luz e emissão juntas** pelo componente `LuminariaExame` (`Ligar(bool)`); **começa apagada** e não está ligada a nenhum puzzle. |
| ComandoLuminaria | parede sul, x 2,30 | Interruptor de sobrepor a 1,30 m, eletroduto aparente até o teto e condulete ao lado da canopla. |
| QuadroBranco | 0,80 × 0,60 m | Apagado: sem texto, nomes ou números. |

**Reaproveitados:**
- CarroAuxiliar, LixeiraClinica e SuporteEPI (Recebimento);
- Relógio de parede (Descanso);
- EstanteRasa (Depósito);
- Bloco de emergência (Circulação);
- Saboneteira e Toalheiro (WC).

## Revisão

1. **Estúdio neutro no Blender** (`estudio_*.jpg`, renderizado por `Blender/Autopsia/scripts/render.py`). Correções feitas:
   - o aro maciço da balança escondia o mostrador; virou anel;
   - a bica do lavatório fazia um laço junto da parede;
   - a válvula de joelho ficava pendurada baixa demais;
   - havia uma fresta entre o tampo e o frontal do lavatório;
   - o sifão genérico atravessava a parede;
   - o condulete do comando interpenetrava a canopla da luminária.
2. **Câmera do Player na Unity** (`player_camera.jpg`, com as sondas duplicadas desligadas só durante a captura): proporções, alturas e circulação conferem com o layout validado.

   O espelho traseiro escuro também aparece na bancada aprovada do Recebimento: é o reflexo de baixa luminância da sonda atual (ver `../mesa/diagnostico_reflexo.md`), e será tratado na etapa 9.

**Fica na cena, sem alteração:** `Pista - laudo da autopsia`, o placeholder amarelo do sistema de pistas que fica sobre a bancada.
