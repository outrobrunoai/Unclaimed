# Autópsia: piloto de corpo e lençol (para revisão)

- **Fonte:** `Blender/Autopsia/scripts/corpo.py`. As etapas são `corpo`, `lencol`, `detalhes`, `exportar` e `render`.
- **Saída do piloto:** `Assets/Modelos/Autopsia/Piloto/*.fbx`. Ainda não há prefab nem nada na cena.
- **Referencial:** o mesmo da mesa (posição 2,70 / 0 / −0,45, yaw 0), com a cabeça a oeste.

## Estado

- **Corpo:**
  - em decúbito dorsal, com a cabeça sobre o apoio;
  - braços ao longo do corpo e mãos espalmadas no leito;
  - contato com o leito sem penetração. A verificação é automática e o resultado está em `verificacao.json`.
- **Lençol:** simulação de tecido sobre o corpo e a mesa real.
  - Cobre a cabeça e o tronco.
  - A bainha sul fica em y −0,25 a −0,27 e deixa a mão direita exposta, onde está o adesivo.
  - A bainha norte fica em y 0,27 a 0,29 e deixa o antebraço esquerdo exposto para a foto com a luminária.
  - Nenhum vértice entra no corpo, nas mãos ou na mesa.
- **Mãos:** dedos fletidos até tocar o leito e polegar medial. Conferidas em close no estúdio e pela câmera do Player.
- **Detalhes:**
  - adesivo irregular de cerca de 22 × 15 mm, moldado ao dorso da mão direita;
  - etiqueta de movimentação de 90 × 55 mm presa ao lençol por alfinete, sobre o abdome, do lado direito. Ainda está em branco: a impressão "083" entra na etapa 8.
- **Material:** simples, só para validar a silhueta. Ainda não há textura.

## Imagens

- `estudio_silhueta.jpg`: estúdio neutro com as vistas geral sul, cabeceira, geral norte, mão direita, antebraço esquerdo e topo.
- `estudio_detalhes.jpg`: adesivo e etiqueta.
- `player_camera.jpg`: Unity, câmera do Player. O corpo provisório ficou oculto só durante a captura, e o piloto entrou como objeto temporário fora da cena.

## Pendências para depois da aprovação

- **Orçamento:** o corpo exportado tem cerca de 100 mil triângulos. Como ele nunca aparece sem o lençol (na P08 a mesa fica vazia), a versão final mantém só as partes visíveis: antebraços, mãos e o contorno sob a bainha. A meta é menos de 15 mil triângulos.
- **Texturas:** pele, lençol de algodão hospitalar, adesivo e etiqueta.
- **Variantes** como filhos da mesa, mutuamente exclusivas e desativadas por padrão quando aplicável:
  - **ocupada:** substitui "Corpo provisório - autopsia";
  - **P08:** lençol caído para um lado com a etiqueta 083 presa e uma marca de sola úmida (`Decal_MarcaSola.png`).
