# Pátio ligado ao recebimento

Conjunto instalado na cena `Assets/Scenes/PrototipoNecroterio.unity`, sob `Area externa/Patio do recebimento`. Sete modelos originais, 40.492 triângulos, UVs e zero faces degeneradas.

## Modelagem

Cobertura em balanço com telha ondulada, mãos francesas, ancoragens, rufo, calha, condutor e grelha; duas folhas metálicas de serviço fixas abertas, com proteção inferior e ferragens; contentor metálico com tampas, munhões e rodízios; dois paletes; armário ventilado com fecho; paleteira manual estacionada. As 47 luzes existentes foram mantidas, incluindo o refletor sob a cobertura e a luminária da agenda de coleta.

Os três blocos provisórios são preservados na hierarquia, com renderer e colisor desabilitados. Piso cimentado e reboco reutilizam os materiais existentes do exterior; UVs em metros. A área central permanece livre.

[Vista do acesso](revisao/depois/acesso.png) · [Porta](revisao/depois/porta.png) · [Área de apoio](revisao/depois/apoio.png) · [Cobertura — vista elevada de inspeção](revisao/depois/cobertura-inspecao.png) · [Referências](REFERENCIAS.md).

## Chuva e verificação

Mapa de abrigo e malha de piso molhado recalculados pela ferramenta existente. O cálculo de profundidade interna foi corrigido para excluir os pisos chamados `Piso - Patio de servico`: estar sob a cobertura externa deve abrigar da chuva sem classificar o jogador como dentro do prédio. Mudança apenas no baker de Editor; scripts de gameplay não foram alterados.

`revisao/verificacao.json`: seis trajetos, 134 amostras de cápsula com raio de 0,35 m, sem bloqueios. Envelope central de 2,3 × 5 m livre. Exposição à chuva = 0 sob a cobertura e 1 fora; profundidade interna = 0 sob a cobertura. Nenhuma malha ou material ausente. Três lightmaps existentes preservados, sem novo bake de iluminação ou de Umbra.

Capturas reais da Unity a 1440 × 1080, FOV 60°, com o pós-processamento do Player. Cinco vistas têm pares antes/depois; armário e paleteira têm detalhes adicionais finais. Renders de estúdio em `revisao/estudio` mostram os modelos reais sob luz neutra. Câmeras temporárias removidas ao terminar.

Validação física em Edit Mode, não ensaio completo de gameplay ou medição em build. A compilação automática geral do Editor já estava pendente no início desta tarefa; integrador, baker e verificadores são compilados e executados diretamente por `run_script`. Não foi forçado reinício do Editor.

Conferência final: as 47 luzes e todos os blocos de MonoBehaviour preexistentes na cena permaneceram iguais; nenhum MonoBehaviour foi acrescentado. Os 16 arquivos do gerador, dos integradores e do baker de chuva passaram em compilação conjunta isolada, sem erros. Detalhes em `revisao/resumo.json`.

## Fontes e manutenção

- `PatioRecebimento.blend`: fonte editável dos sete modelos.
- `scripts/gerar.py`: geometria e bake PBR, reutilizando os helpers e o baker da recepção/autópsia. Um atlas e um material URP Lit por modelo, 2048 ou 1024 conforme a peça.
- `Assets/Modelos/PatioRecebimento`: FBX, quatro mapas PBR por modelo, materiais, prefabs e malhas de acabamento.
- **Tools > Unclaimed > Patio do recebimento > Build Prefabs** atualiza os assets; **Install Assets** faz a primeira instalação, recusando duplicação. O gerador completo também instala o conjunto quando os prefabs estão disponíveis; ele não foi executado nesta tarefa.
- `scripts/PatioReview.cs`: `Before`, `After`, `Audit` e `Verify`, para executar por `unity command run_script`.

Regeneração: `blender -b --factory-startup --threads 8 --python Blender/PatioRecebimento/scripts/gerar.py`. Acrescentar `-- ContentorMetalico` para atualizar somente essa peça. Depois atualizar o prefab na Unity; mudanças de silhueta da cobertura exigem novo **Bake Shelter Map**.

Sem commit ou push.
