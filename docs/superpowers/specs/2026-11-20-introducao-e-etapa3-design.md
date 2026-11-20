# Introdução (chegada ao necrotério) e etapa 3 — design curto

Data: 2026-11-20.

## Intenção
O jogador precisa saber, antes de entrar, por que o jornalista está ali. Hoje o jogo começa dentro da recepção, sem contexto. A introdução mostra a conversa com Celso no celular e a chegada ao prédio na chuva. A chegada também é o que faz Daniel ouvir alguém e esconder Léo em C-02 (já é o estado inicial do jogo).

A etapa 3 coloca no mapa o que falta dos sistemas do José: zonas de sinal e saídas do prédio.

## Caminhos considerados
1. **Tudo em primeira pessoa, sem cortes.** Escolhido pelo autor. Reaproveita o celular e o mapa externo; não precisa de modelo do jornalista.
2. Texto em tela preta e depois a chegada. Explicação fora do mundo do jogo.
3. Cutscene em terceira pessoa (Timeline). Exige modelo e animações do jornalista, que não existem.

## Como fica (minhas propostas, para o autor corrigir)

### Sequência (cerca de 60 a 80 s)
1. **Escuro → calçada.** Chuva, noite, 23h30. O jogador está na calçada, em frente ao gradil, com o celular na mão.
2. **Conversa com Celso.** As mensagens aparecem uma a uma no celular (cerca de 3 s cada). Texto proposto:
   - Celso: "Chegou mais um hoje. 083, Léo Martins, da Limiar. Entrou às 21h50."
   - Eu: "É o terceiro da Limiar. Preciso ver o corpo antes do exame."
   - Celso: "A equipe saiu às 22h35. Deixei a recepção aberta pra você."
   - Celso: "Faço a ronda entre os prédios até as três. Não mexe em nada que eu tenha que explicar depois."
   - Eu: "Te passo o resto do dinheiro amanhã."
   (A última mensagem do Celso substitui a `celso_01` atual, que diz quase o mesmo.)
3. **Caminhada.** O celular desce. A câmera anda sozinha pela calçada, passa pelo portão de pedestres e vai até a porta da recepção, com balanço leve de passos. A guarita do Celso fica visível ao fundo, apagada ou com a luz da ronda.
4. **Entrada.** A porta da recepção abre, a câmera entra alguns passos e para. O controle passa ao jogador. Aparece o objetivo atual ou nada (fica como está hoje).

### Regras
- Só toca em **Novo jogo**. **Continuar** começa direto do save.
- Pode ser **pulada** segurando uma tecla (E ou Espaço) por 3 s, com uma indicação discreta. Pular leva ao fim da sequência (jogador dentro da recepção, mensagens recebidas).
- Durante a introdução: sem andar, sem abrir caderno e sem pausar a lógica do Daniel (ele ainda não age; o estado inicial já é Daniel na mesa).
- A câmera pode olhar em volta um pouco com o mouse durante a caminhada (até uns 30°), para não parecer um vídeo. Minha proposta; dá para travar se preferir.
- Ao terminar, grava o fato `intro_vista` e as mensagens da conversa como recebidas.
- Sons ficam para o fim do projeto (chuva, passos, porta).

### Etapa 3
- **Zonas de sinal:** recepção (2 barras) e pátio (3 barras), cobrindo cada área com o BoxCollider. Dentro do resto do prédio, sem sinal.
- **Saídas do prédio:** no portão de pedestres da frente, com `fatoQueBloqueia = corrente` (a porta de vidro fica livre, o jogador sai ao jardim); e no portão do pátio.
- A introdução passa pela porta da recepção. A saída só fica ativa depois da introdução.

## Quem faz
- **Gemini:** o script da introdução (sequência, pular, travar o jogador, mensagens) e os testes.
- **Claude:** pontos do caminho na cena, portão e porta da recepção, zonas de sinal, saídas, textos no `celular.json`, ligação com o Novo jogo e verificação em Play Mode.

## Fora do escopo
- Modelo do jornalista, carro, cortes de câmera.
- Sons.
