# Expansão da câmara fria — entrega Blender

## Malhas e bake

Tempos abaixo somam as cinco passagens do baker (base, roughness, metallic, normal e AO), sem UV/exportação/gravação.
- `ModuloTorre`: 15,678 triângulos; bake 23.8 s; atlas 2048 px; degeneradas 0.
- `InstalacoesCamara`: 11,394 triângulos; bake 2.4 s; atlas 1024 px; degeneradas 0.
- `CarroElevador`: 18,636 triângulos; bake 6.5 s; atlas 1024 px; degeneradas 0.
- `SuporteBandejas`: 13,284 triângulos; bake 8.2 s; atlas 1024 px; degeneradas 0.
- `PostoConferencia`: 4,312 triângulos; bake 11.0 s; atlas 1024 px; degeneradas 0.

Total de bake: 51.9 s. As medidas em `malhas.json` são as caixas das malhas completas, incluindo ferragens, alças e bandejas.

## Medidas e integração

- Torre: 1,165 × 2,44 × 2,33 m (W/H/D). Centro no referencial do 2×2: Blender (-1,7775, 0, 0), Unity (-1,7775, 0, 0). Junta de 30 mm. Os pivôs C-05/C-06/C-07 são locais à torre e estão em `pivos.json`, no mesmo formato Unity das posições anteriores.
- Controlador na parede: Blender (-2,945, 2,33, 1,45); Unity (-2,945, 1,45, 2,33). O FBX aprovado do controlador não foi regenerado.
- Carro exportado com topo dos roletes em 1,062 m. Envelope externo: 0,692 m em X × 2,015 m em Y; a mesa fica a h e a alça chega a h+0,205 m. Para encostar diante de uma coluna: Blender (centro da coluna, -1,010, 0), deixando 20 mm entre o batente frontal e o gabinete.
- Vão útil entre os lábios superiores das canaletas ajustado de 620 para 630 mm: o quadro tubular aprovado tem 625 mm por fora. Os roletes laterais e seus suportes ficam livres dentro da seção C, atrás dos lábios.
- O teste no trilho baixo requer h=0,312 m, inferior ao limite nominal de 0,40 m no briefing. A geometria foi estendida até 0,312 m para o encaixe nesse nível; mantém toda a faixa 0,40–1,85 m. Barras de tesoura com seção de 10 × 20 mm deixam folga entre os dois estágios também nessa posição baixa.
- Suporte: estrutura de aproximadamente 2,00 × 0,36 × 1,05 m; as duas bandejas aprovadas transformadas ampliam o envelope total para 2,0527 × 0,3801 × 1,035 m. Elas mantêm os puxadores e roletes originais. Inclinação de 8° para a parede; terceiro berço vazio.
- Posto: prateleira 600 × 300 mm; prancheta 235 × 330 mm girada 87° em torno de Z no Blender para caber deitada, com leve giro em relação à orientação paisagem. O cordão ultrapassa a frente da prateleira em 10,8 mm; o cordão tem extremidades ligadas à caneta e a uma ancoragem metálica na prateleira.

### Quads do posto — coordenadas Blender no referencial do asset

A origem da prateleira é z=0; no Unity ela será instalada a 1,05 m do piso. Converta os vetores por Unity=(x,z,y), como nos pivôs.

- Papel A4: centro **(-0,085, -0,150, 0,0085) m**, área **0,210 × 0,297 m**, eixos locais antes do giro: largura +X, topo +Y, normal +Z. Rotação Blender **(0°, 0°, 87°)**. A superfície da prancheta está em z=0,008 m, deixando 0,5 mm para o quad. Não incluir espessura de papel na malha. O clipe fica sobre a margem superior.
- Papel em Unity: centro **(-0,085, 0,0085, -0,150)**; direção da largura **(0,052336, 0, 0,998630)**, direção do topo **(-0,998630, 0, 0,052336)**, normal **(0, 1, 0)**. Esses eixos evitam ambiguidades na orientação de quads.
- Rótulo da caixa: centro **(0,190, -0,123, 0,067) m**, área **0,180 × 0,060 m**, largura +X, topo +Z, normal -Y. Plano vertical frontal; a face da caixa está em y=-0,120 m. O rótulo fica acima da abertura das etiquetas. Em Unity: centro **(0,190, 0,067, -0,123)**; largura +X, topo +Y, normal -Z.

## Verificações

- SO_MALHAS=1 SEM_EXPORT=1: cinco assets gerados; `degenerate: 0` em todos (logs `malhas2.log` e `posto_malhas.log` para o refinamento final do posto).
- Geração completa seletiva: cinco FBX e vinte PNG PBR gravados. Dados finais de bake em `malhas.json`; logs `bake2.log` e `posto_bake.log` (refinamento final da ancoragem do cordão).
- `gerar.py -- --check`: ângulos por estágio de **1,417° / 25,901° / 58,092°** nas alturas **0,312 / 1,062 / 1,812 m**. Verifica fechamento trigonométrico, folga da barra entre estágios e envelope integral do carro a 20 mm do gabinete. Detalhes em `geometria.json`.
- `python3 Blender/CamaraFria/scripts/impressos.py`: concluído. C-01/04 ocupadas, C-03 interditada, C-02/05/06/07 livres; formulário sem preenchimento.
- `md5sum -c Blender/CamaraFria/revisao/protegidos.md5`: 50 arquivos preservados, incluindo os oito FBX aprovados, seus 32 mapas PBR e os dez impressos antigos protegidos. QuadroOcupacao é a única exceção autorizada. Hashes iniciais em `protegidos_md5_antes.json`.
- As chaves originais dimensoes/compartimentos/nota de pivos.json permanecem idênticas; apenas modulos foi acrescentado.
- Decais RGBA: RiscosTransferencia 2048×1024, alfa máximo 46/255; MarcasPuxador 512×512, alfa máximo 37/255. Ambos têm alfa zero no fundo; riscos seguem o eixo U longo.

## Ambiente

Blender disponível: 5.2.3 LTS. Nenhum comando Unity foi executado. Nenhuma cena, prefab, .meta, asset serializado, pacote ou configuração foi editado.

O sandbox bloqueia a miniatura auxiliar em ~/.cache/thumbnails e trava o encerramento do áudio após scripts concluídos. Os artefatos e bakes são gravados normalmente. A execução final usa `--python-expr 'import os; os._exit(0)'` após o script e antes de `--`, evitando apenas o travamento do encerramento. Não há bake pendente por GPU/tempo.

## Vistas geradas

`render.py` concluído; oito imagens de 1600 × 1100 em `revisao/estudio2/`.

- `torre_modulo_frente.png`
- `torre_modulo_tres_quartos.png`
- `torre_aberta_carro.png`
- `carro_elevador.png`
- `suporte_bandejas.png`
- `posto_conferencia.png`
- `instalacoes_topo_linhas.png`
- `instalacoes_arremates.png`

Revisão visual: os oito enquadramentos foram inspecionados. A vista da torre aberta foi ampliada para incluir todos os rodízios; a vista de arremates mostra a junta frontal e as fixações junto ao piso. As vistas anteriores continuam disponíveis com `ESTUDIO_ANTIGO=1`.
