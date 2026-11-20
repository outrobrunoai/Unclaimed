# Introdução e etapa 3 — plano de implementação

> Execução: código pelo Gemini (skill `delegar-gemini`), cena e verificação pelo Claude (CLAUDE.md do projeto). Sem commit sem pedido do autor.

**Goal:** Novo jogo começa na calçada, mostra a conversa com Celso no celular, caminha sozinho até a recepção e entrega o controle; zonas de sinal e saídas do prédio colocadas.

**Architecture:** Um componente `IntroChegada` no mapa move o jogador (com `Movimento` e `CharacterController` desligados, como em `PercursoDeMedicao`) por pontos da cena e mostra as mensagens chamando o celular. Mensagens novas em `celular.json` com `quando = "intro_vista"`, entregues uma a uma pela introdução.

**Tech Stack:** Unity 6000.5 / URP, Input System, testes EditMode (NUnit).

**Spec:** `docs/superpowers/specs/2026-11-20-introducao-e-etapa3-design.md`

## Global Constraints
- Pular: segurar E ou Espaço por **3 s**, com indicação discreta e barra de progresso.
- Só no Novo jogo. Continuar (save pendente) ou fato `intro_vista` já marcado → não toca.
- Ao terminar ou pular: jogador em `(-11.375, 1, -11.5)` (ponto final da cena), fato `intro_vista`, todas as mensagens da introdução recebidas.
- Mouse olha até 30° em volta durante a caminhada e volta suave ao centro.
- Textos em português claro (memória escrita-clara). Sem sons agora.
- Sem `?.` em objetos da Unity.

## Review Focus
1. Salvar durante a introdução (checkpoint/pausa) não pode gravar o jogador na calçada: `MenuPausa` e `Salvamento.Salvar` ficam bloqueados enquanto `IntroChegada.Ativa`.
2. Pular no meio de uma mensagem: nenhuma mensagem duplicada nem faltando.
3. `SaidaDoPredio` da recepção não dispara durante a introdução (a câmera passa por ela).
4. Abrir caderno, celular (T) ou interagir (E) durante a introdução: nada acontece; E só conta para pular.
5. Continuar um save feito depois da introdução: não toca, mensagens continuam lá.

---

### Task 1 (Gemini): código da introdução
**Files:**
- Create: `Assets/Scripts/Historia/IntroChegada.cs`
- Modify: `Assets/Scripts/Celular/Celular.cs` (método `MostrarConversa`), `Assets/Scripts/Celular/CelularTela.cs` (abrir direto numa conversa), `Assets/Scripts/Celular/CelularLogica.cs` (`Entregar`), `Assets/Scripts/Finais/SaidaDoPredio.cs`, `Assets/Scripts/Menu/MenuPausa.cs`, `Assets/Scripts/Salvamento.cs`
- Test: `Assets/Editor/Testes/IntroChegadaTestes.cs`

**Interfaces (produz):**
- `CelularLogica.Entregar(string idMensagem) -> bool` — marca `msg:<id>`, dispara `AoReceber` se `de != "eu"`; false se já recebida ou id inexistente.
- `Celular.MostrarConversa(string contato)` — põe o celular na mão aberto na conversa; `Celular.Guardar()` já existe.
- `Salvamento.VaiCarregar -> bool` — há save pendente a aplicar.
- `IntroChegada : MonoBehaviour` com `public static bool Ativa`, `public Transform[] pontos` (pose = posição + giro do ponto; o último é o fim), `public float velocidade = 1.2f`, `public string[] mensagens`, `public float segundosPorMensagem = 3f`, `public string contato = "celso"`, `public FolhaDePorta[] portasParaAbrir`, `public float segurarParaPular = 3f`, `public const string Fato = "intro_vista"`, `public static bool DeveTocar()` e `public void Concluir()` (vai ao fim na hora: teleporta, entrega o que faltar, marca o fato, devolve o controle).
- `SaidaDoPredio.Interagir` não faz nada enquanto `IntroChegada.Ativa`. `MenuPausa` não abre e `Salvamento.Salvar` devolve false enquanto `Ativa`.

- [ ] Testes: `DeveTocar` false com `intro_vista`; `Entregar` marca uma vez e devolve false na segunda; `Concluir` marca o fato, entrega todas as `mensagens` sem duplicar e desliga `Ativa`; `Salvamento.Salvar` false com `Ativa`.
- [ ] Implementar; compilar; testes passam (Claude roda).

### Task 2 (Claude): cena, textos e etapa 3
**Files:** `Assets/Resources/Historia/celular.json`, `Assets/Editor/UnclaimedMorgueGenerator.Entorno.Lote.cs` (folha do portão de pedestres separada e entreaberta, corrente solta), cena `PrototipoNecroterio.unity`.
- [ ] Mensagens `intro_01..05` (contato `celso`, `quando = "intro_vista"`) com o texto do spec; remover `celso_01`.
- [ ] Portão de pedestres com uma folha aberta ~70° e corrente pendurada; colisor do gradil com vão de 1 m no portão.
- [ ] Objeto `Introducao` com `IntroChegada`: pontos calçada (x -11.4, z -24) → portão (z -19) → caminho (z -15) → porta (z -12.9) → fim (-11.375, 1, -11.5); porta de vidro da entrada em `portasParaAbrir`.
- [ ] `ZonaDeSinal` recepção (2 barras) e pátio (3 barras); `SaidaDoPredio` na porta de vidro (lado de fora, `fatoQueBloqueia = corrente`) e no portão do pátio.
- [ ] Verificar em Play Mode (Novo jogo): capturas da calçada com celular, do portão e do fim; pular segurando E por 3 s; Continuar não toca.
