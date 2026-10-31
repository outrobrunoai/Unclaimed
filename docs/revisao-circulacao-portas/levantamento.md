# Circulação e portas — PrototipoNecroterio

Data: 2026-10-25. Cena: `Assets/Scenes/PrototipoNecroterio.unity` (salva, bake de iluminação e reflexos refeitos depois da integração).

## Acessos e estados

| Acesso | Vão (m) | Porta | Estado inicial | Observação |
|---|---|---|---|---|
| Entrada principal | 2,00 | vidro duplo (existente) | aberta | mantida |
| Recepção ↔ WC | 1,00 | porta do WC (existente) | aberta | mantida |
| Recepção ↔ circulação | 1,50 | porta da circulação (existente) | trancada, chave `chave_circulacao` | trava e chave preservadas |
| Recepção ↔ arquivo | 1,50 | porta do arquivo (existente) | trancada | puzzle da recepção, intocado |
| Circulação ↔ arquivo/administração | 1,50 | institucional administrativa (2 folhas) | fechada, destrancada | |
| Circulação ↔ descanso | 1,50 | institucional descanso (2 folhas) | fechada, destrancada | |
| Circulação ↔ depósito | 1,50 | institucional depósito (2 folhas) | fechada, destrancada | |
| Circulação ↔ sala técnica | 1,50 | técnica com veneziana (2 folhas) | fechada, destrancada | |
| Circulação ↔ autópsia | 1,50 | clínica dupla, visor | fechada, destrancada | maca |
| Autópsia ↔ câmara fria | 1,50 | frigorífica dupla | fechada, destrancada | maca |
| Câmara fria ↔ recebimento | 1,50 | frigorífica dupla | fechada, destrancada | maca |
| Vestibulo ↔ recebimento | 1,50 | clínica dupla, visor | fechada, destrancada | maca |
| Recebimento ↔ pátio | 2,00 | porta de serviço dupla (existente) | aberta | mantida |
| Pátio ↔ rua | 2,50 | portão de serviço | sem folha | mantido |
| Circulação ↔ vestíbulo | 1,25 | **sem porta** | passagem livre | intencional: ligação entre trechos do mesmo corredor, rota alternativa preservada |

Nenhum vão foi reduzido; as folhas ocupam a largura original. Portas novas começam destrancadas, e nenhuma interfere na progressão.

## Famílias

- **Institucional** (administrativa, descanso, depósito): folha opaca, puxador e dobradiças metálicas, rodapé de proteção inferior. Variações de cor e placa de identificação por setor.
- **Técnica**: igual em estrutura, com veneziana de ventilação (alta e baixa) e fechadura de cilindro.
- **Clínica dupla**: duas folhas, visor, para-choque inferior e faixa de proteção para maca.
- **Frigorífica dupla**: aspecto isolante, ferragem própria, batente largo.

Todas têm folha secundária liberável, batentes, dobradiças e maçanetas dos dois lados. Folhas espelhadas reaproveitam o mesmo prefab (`Espelhada`, escala X -1).

## Mecânica (verificada em Play Mode)

- 16 folhas dos 8 acessos novos: todas abrem a 95° sem bloqueio, os arcos estão livres de mobiliário e de outras portas.
- Inversão no meio do movimento: porta a 60,8° ao reverter para abrir; termina em 95° sem salto.
- Obstrução: cubo no arco bloqueia a folha (`Bloqueada = true`) e ela abre normalmente quando o cubo é removido.
- Empurrão do Player: Player no arco foi deslocado (z -0,98 → -1,43) e a folha chegou a 94,8°.
- Colisores acompanham as folhas (um por folha).
- Portas trancadas (circulação, arquivo) continuam trancadas.
- Nenhuma alteração no Player.

## Corredores

Piso em granilite com juntas, barrado e rodapé, cantos protegidos, corrimãos de maca (`StretcherRail`) nos trechos de circulação de macas, teto modular com luminárias fluorescentes, luz de emergência, extintores com etiqueta e sinalização do setor. Placas seguem o guia de comunicação visual (Nimbus Sans, fundo #e4e1d6, texto #2b2d29; vermelho e amarelo só para risco). Variações de desgaste e de equipamentos ao longo do trajeto.

## Arquivos

- `corredores/` — vistas depois; `antes/` — vistas antes.
- `portas/` — cada família aberta e fechada, frente e verso.
- `percurso_circulacao.mp4` — percurso do corredor oeste até o vestíbulo (21 s).

## Verificações e limitações

- Compilação limpa (`error CS` ausente no log) depois de reiniciar o Editor, que travou em "compiling".
- Bake de iluminação, oclusão e reflexos refeitos depois da integração; cena salva sem alterações pendentes.
- Aviso `Inconsistent counts in appended probe data` do bake: aparece no log do lightmapper; não afeta a renderização verificada.
- Áudio: as folhas novas têm `AudioSource` mas nenhum clipe atribuído (silenciosas); atribuir os mesmos clipes das portas existentes é o próximo passo, não feito aqui.
- Navegação: nenhum script do projeto usa NavMesh. `FolhaDePorta` é referenciada por `PortalDeChuva`, `MiraDeInteracao` e `PercursoDeMedicao`; não foram executados com as portas novas além do teste de abrir/fechar acima.
- O vídeo é renderizado por câmera temporária, não por gravação de tela.
