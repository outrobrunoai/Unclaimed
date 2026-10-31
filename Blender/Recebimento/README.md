# Sala de recebimento

Assets originais modelados no Blender e instalados em `Assets/Scenes/PrototipoNecroterio.unity`, na mesma família clínica da autópsia. Referências em [REFERENCIAS.md](REFERENCIAS.md).

## Entrega

Nove modelos: maca com mecanismo de tesoura, pistão, roletes e rodízios; lençol sobre corpo coberto; estação de lavagem com grelha e ducha; bancada com cuba profunda; carro auxiliar; suporte de EPI; lixeira de pedal; kit de bancada; bandeja com instrumental para a pista existente. Saboneteira e toalheiro reutilizam os prefabs do WC.

Fonte editável: `Recebimento.blend`. Exports, materiais URP Lit, mapas PBR e prefabs: `Assets/Modelos/Recebimento`. Cada modelo tem um atlas e um material; mapas de cor, normal, metallic/smoothness e oclusão. Atlas de 2048 nas peças principais e 1024 nas menores. Total de 95.456 triângulos, sem faces degeneradas.

Piso, azulejo, pintura e forro reutilizam a família da autópsia, com UVs em metros em faces voltadas apenas para o recebimento. As novas faces não recebem GI estática; equipamentos usam probes e a iluminação existente. Não foi feito novo bake de iluminação ou de oclusão nesta entrega.

## Revisão

- [Vista geral na Unity](revisao/depois/entrada.png), [entrada do pátio](revisao/depois/patio.png), [lavagem](revisao/depois/lavagem.png), [bancada](revisao/depois/bancada.png).
- Cinco pares de capturas antes/depois: 1440 × 1080, FOV 60°, posições iguais e pós-processamento do Player. Câmeras temporárias removidas ao terminar.
- [Verificação de circulação](revisao/verificacao.json): seis trajetos, 127 amostras de cápsula de raio 0,35 m, sem bloqueios; pista alcançada pelo primeiro raycast a 0,897 m.
- Nove malhas com UVs e zero faces degeneradas; nenhuma malha ou material ausente na instalação.
- As 47 luzes e os três lightmaps foram preservados. Scripts de gameplay e todos os blocos de MonoBehaviour preexistentes na cena permaneceram iguais ao registro inicial desta tarefa. Alterações anteriores de desempenho preservadas.
- A pista conserva seu objeto, transform, colisor e configuração; apenas a apresentação visual foi substituída por um filho modelado. Os cinco volumes provisórios de mobiliário ficam preservados na hierarquia com renderer/colisor desabilitados.

Verificação de circulação e interação feita em Edit Mode com consultas físicas; não é uma bateria completa de gameplay nem uma medição de desempenho em build. Equipamentos são estáticos, sem novos sistemas de torneira ou movimentação da maca.

Código verificado por compilação isolada dos 14 arquivos do gerador e do integrador via `run_script --dry_run`, sem erros. A recompilação automática geral do Editor permaneceu pendente, com `scriptCompilationFailed=false`; não foi alegado um ciclo completo de compilação/reload ou de Play Mode. O integrador e os verificadores foram compilados e executados diretamente no Editor por `run_script`.

## Correções do recebimento — 20/10/2026

Lençol afastado da borda da maca, com as pontas terminando antes das alças e o volume fechando suavemente sobre a bandeja. A verificação BVH entre as malhas finais registrou **zero pares de triângulos em interseção** ([resultado](revisao/correcoes/geometria.json)). Mangueira da lavagem reposicionada à frente, com alimentação inferior e suporte próprio, fora da bandeja. Cuba auxiliar reconstruída com fundo e paredes; compressas e pacotes apoiados nas prateleiras. Suporte de EPI reconstruído com três berços de caixas de luvas e aberturas ovais.

Condutor da cobertura atualizado para o PVC DN100 da família exterior, com bolsas e abraçadeiras, terminando sobre o ralo. Duas luminárias internas e três arandelas externas usam os prefabs já existentes. As 47 fontes de luz mantêm suas configurações.

[Capturas finais na Unity](revisao/correcoes), [verificação visual e de materiais](revisao/correcoes/verificacao.json). Doze trajetos livres entre sala e pátio, pista acessível, abrigo de chuva preservado. Verificação em Edit Mode. A compilação geral do Editor terminou nesta revisão, com `isCompiling=false` e `scriptCompilationFailed=false`; isso resolve a pendência registrada na entrega anterior. Cena salva.

## Manutenção

`scripts/gerar.py` usa os helpers e o baker existentes de Recepcao/MesaAutopsia. Executar pelo Blender em processo separado:

```sh
blender -b --factory-startup --threads 8 --python Blender/Recebimento/scripts/gerar.py
```

Para refazer só peças selecionadas, acrescentar `-- BancadaRecebimento InstrumentoRecebimento`; o script preserva os demais modelos na fonte. `scripts/render.py` produz renders de estúdio dos modelos reais, separados das capturas da Unity.

Na Unity: **Tools > Unclaimed > Recebimento > Build Prefabs** atualiza os prefabs. **Install Room Assets** faz a primeira instalação e recusa duplicação. A geração completa do necrotério também instala o conjunto quando os nove prefabs estão disponíveis. O gerador completo não foi executado nesta tarefa.

`scripts/RecebimentoReview.cs`, método `RecebimentoReview.Verify`, pode ser executado por `unity command run_script` para repetir as consultas físicas. Nenhum commit ou push realizado.
