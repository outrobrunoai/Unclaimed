# Unclaimed

Jogo de investigação em primeira pessoa, com terror, feito em Unity. Projeto de faculdade.

## A história

Chove forte numa noite de novembro. Você é um jornalista que entra num necrotério municipal atrás de uma matéria sobre três mortes que ninguém explicou. O vigia, que é sua fonte, deixou a recepção aberta para você.

Os corpos que ninguém vem buscar, os "não reclamados", somem do registro sem que ninguém perceba. Lá dentro, você lê documentos, e-mails e bilhetes dos funcionários, fotografa o que encontra e junta as provas no seu caderno.

Mas você não está sozinho no prédio. Alguém quer que você vá embora de mãos vazias. A noite inteira ele atrapalha o seu caminho, e ele não vai deixar a prova principal sair dali.

- **Investigação:** cerca de 40 evidências entre papéis, fotos, arquivos no computador da recepção e mensagens no celular.
- **Puzzles:** cadeados de combinação, a energia do prédio, portas trancadas. As pistas estão nos documentos, nunca soltas na tela.
- **Terror:** sem nada sobrenatural. O medo vem de uma pessoa real, com um motivo claro.
- **Vários finais:** dependem do que você descobre, do que consegue enviar pelo celular e de como sai do prédio.

## Baixar e jogar

As versões jogáveis estão em **[Releases](../../releases)**:

- **Windows:** baixe `Unclaimed-beta-Windows.zip`, extraia a pasta inteira e abra o `Unclaimed.exe`.
- **Linux:** baixe `Unclaimed-beta-Linux.zip`, extraia e rode `./Unclaimed.x86_64`. Se ele não abrir, dê permissão de execução com `chmod +x Unclaimed.x86_64`.

Mantenha os arquivos juntos. O executável precisa da pasta `Unclaimed_Data` ao lado dele.

## Controles

| Ação | Tecla |
|---|---|
| Andar | W A S D |
| Correr | Shift |
| Agachar | C ou Ctrl |
| Interagir, pegar, abrir, esconder-se | E |
| Caderno de evidências | Tab |
| Celular (abrir e guardar) | T |
| Câmera do celular, tirar foto | F |
| Aplicativos do celular | Q |
| Lanterna | L |
| Pausar | Esc |

## Abrir o projeto

- Unity **6000.5.9f1** (Unity 6.5) com URP.
- Os arquivos grandes (modelos, texturas, áudio) estão no **Git LFS**. Instale o LFS antes de clonar:

  ```bash
  git lfs install
  git clone https://github.com/outrobrunoai/Unclaimed.git
  ```

- A cena do jogo é `Assets/Scenes/Jogabilidade.unity`. O menu é `Assets/Scenes/main.unity`.
- Os testes ficam em `Assets/Editor/Testes` e rodam no Test Runner, em modo EditMode.

## Organização

| Pasta | O que tem |
|---|---|
| `Assets/Scripts` | Código do jogo: movimento, interação, celular, caderno, salvamento, perseguição e finais |
| `Assets/Editor` | Ferramentas que geram o prédio, os móveis e a iluminação, além dos testes |
| `Assets/Resources/Historia` | Textos do jogo: evidências, mensagens do celular, gravação e finais |
| `Assets/UnclaimedPrototype` | Dados e texturas das salas, como o computador da recepção e o quadro de avisos |
| `Blender` | Scripts e arquivos de origem dos modelos feitos no Blender |
| `docs` | Documentos de design da história, dos sistemas e da direção de arte |

## Créditos de terceiros

- Animações: *Universal Animation Library*, da Quaternius (CC0). A licença está em `Assets/Animacoes/Quaternius/Licenca_CC0.txt`.
- Base do personagem: MakeHuman / MPFB, com assets CC0.
- Sons: a lista com as fontes está em `Assets/Resources/Sons/creditos.json`.
