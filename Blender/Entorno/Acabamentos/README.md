# Exterior — acabamento integrado

Cena: `Assets/Scenes/PrototipoNecroterio.unity`. Modelagem e integração feitas diretamente por Codex no Blender e na Unity, sem delegação, Meshy, créditos, commit ou push.

**Revisão do letreiro principal, 17/10/2026:** proposta A — chapa administrativa clara — escolhida e integrada. A fonte específica é `fontes/Placa_Fachada_A.svg`, gerada por `scripts/letreiro.py`; o Blender e o prefab foram atualizados somente para essa placa. Consultar o [guia visual](../../../docs/direcao-arte/comunicacao-visual.md) e a [comparação real na Unity](../../../docs/direcao-arte/integracao-a/comparacao.html). Usar **Build Main Sign Only** para revisões isoladas. O layout não se aplica automaticamente às demais categorias.

## Substituições

- 8 condutores pluviais: DN100 circular, curvas contínuas, bolsas, abraçadeiras ancoradas, passagem pela platibanda alinhada à laje e boca aberta sobre os ralos existentes.
- 1 instalação de água: alimentação DN25, união, registro, torneira, engate, enrolador e mangueira geométrica.
- 1 interfone externo: caixa, grelha, botão, parafusos, identificação e entrada traseira do cabo. Preservada a posição de uso.
- 5 placas: identificação, atendimento, número, não estacione e portaria; tipografia precisa, corpo com espessura, afastadores e fixações. Textos existentes preservados, sem novos brasões ou informação narrativa.
- 6 touceiras de espada-de-são-jorge e 5 filodendros ornamentais: folhas fechadas com espessura, nervuras, curvatura e variação determinística. Demais árvores, gramíneas, bananeiras e arbustos mantidos.
- 2 condensadoras: modelo compartilhado com variante de desgaste, abertura real para cinco pás atrás da grelha, aletas, tampas, cantoneiras, ancoragens, borrachas, linhas frigorígenas isoladas e conduíte entrando na parede.
- 4 conjuntos de cadeado externo de chave: latão, arco de aço, cilindro, olhais, chapas e correntes com elos alternados. Arco atravessando elos terminais; fechos de portões de chapa reassentados na face da folha.

Total: **13 modelos Blender, 14 prefabs e 32 instâncias**. Materiais compartilhados URP Lit. Não foram adicionados luzes, colisores detalhados ou componentes de gameplay.

## Fontes e manutenção

- [Referências brasileiras e características aproveitadas](REFERENCIAS.md).
- Fonte: `Acabamentos_Exterior.blend`, com os scripts também incorporados como Text blocks.
- `scripts/modelos.py`: geometria determinística e exportação FBX em metros; `scripts/texturas.py`: tipografia e texturas RGB originais.
- Exports, materiais e prefabs: `Assets/Modelos/Entorno/Acabamentos/`.
- `Assets/Editor/UnclaimedExteriorFinishes.cs`: **Build Exterior Finishes Prefabs** atualiza os assets preservando GUIDs; **Apply Exterior Finishes Only** executa a primeira troca seletiva e recusa duplicação.
- Geradores atualizados em `UnclaimedMorgueGenerator.Entorno.Lote.cs` e `.Vegetacao.cs`. Não foi executado **Rebuild Exterior** nesta rodada.
- Somente os triângulos dos placeholders foram retirados das malhas compartilhadas de fachada e grades. Câmera de segurança, elétrica, estrutura, portões e drenagem existente permanecem.

## Verificações

- Cena salva fora do Play Mode, sem alterações não salvas ao terminar. Compilação sem falhas e Console atual sem erros.
- **46 luzes e 308 colisores idênticos** aos registros iniciais; três lightmaps mantidos. Os 54 blocos serializados de MonoBehaviours da cena permanecem iguais aos da cópia inicial, incluindo o puzzle. `Morgue.unity` mantém o hash inicial.
- Quatro percursos livres, com **145 amostras de cápsula de raio 0,35 m**, em Edit Mode, ignorando o próprio Player. Os trajetos passam ao redor do canteiro e da bananeira já existentes; nenhum deles foi movido. Quatro portões continuam bloqueados por seus colisores originais.
- 32 instâncias reconhecidas; nenhuma malha ou material ausente. Os 13 modelos têm UVs, sem vértices inválidos ou faces de área zero na inspeção final.
- [12 comparações antes/depois](revisao/comparacoes/), [17 vistas finais](revisao/depois/) e [resumo de verificação](revisao/resumo-verificacao.json). Câmeras temporárias a FOV 60, 1440 × 1080, mesma iluminação e pós-processamento do Player; capturas de inspeção elevadas identificadas no nome. Câmeras removidas após renderizar.
- Registros detalhados: `revisao/audit-antes.json`, `audit-depois.json`, `verificacao.json` e `blender-verificacao.json`. A auditoria “depois” precede o comando final de salvar, por isso seu campo `dirty` registra a cena ainda em edição.
- Sem novo bake: as peças substituídas já usavam light probes e não contribuíam para o bake estrutural. Não houve mudança de luzes, exposição ou pós-processamento. A bateria completa do puzzle não foi repetida.

## Limitações

- Cadeados nas partes sem iluminação direta continuam discretos à noite. Não foi acrescentada iluminação ou emissão artificial para destacá-los.
- A avaliação de instalação é visual, com verificações geométricas e de circulação; não há simulação hidráulica, elétrica, térmica, de correntes ou de chamadas.
- Não foi medido desempenho em build. Os prefabs usam geometria compartilhada e materiais comuns, sem novas animações, som ou sistema de interação.
