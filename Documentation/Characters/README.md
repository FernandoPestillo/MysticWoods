# Personagem e inimigos — High-Detail Pixel Art

Esta etapa adapta o aventureiro, o slime, o esqueleto e o chefe slime à direção visual da tela inicial. A paleta usa verdes profundos, esmeralda, bronze e luz dourada. A reconstrução posterior do mapa está documentada em [Map](../Map/README.md).

## Assets e edição

Os novos atlas ficam em `Assets/Art/Characters/HighDetail/`, separados dos originais em `Assets/Mystic Woods/characters/`.

| Atlas | Dimensões | Quadros | Pixels por unidade |
| --- | --- | --- | --- |
| `player-hd.png` | 971 × 1619 | 51 | 77 |
| `slime-hd.png` | 1484 × 1060 | 25 | 146 |
| `skeleton-hd.png` | 1374 × 1145 | 25 | 154 |

Todos usam transparência, filtro Point, sRGB, tamanho original, sem mipmaps e sem compressão. Os pixels por unidade foram calibrados para a escala visual e as colisões existentes; os três atlas possuem densidades diferentes de desenho.

Os recortes e os pivôs estão salvos nos arquivos `.meta` e podem ser editados no Sprite Editor. Os atlas gerados não possuem espaçamento perfeitamente uniforme: preserve os recortes explícitos. Um novo fatiamento automático por grade removeria esse alinhamento. O inventário completo com nomes, identificadores, retângulos e pivôs fica em [sprite-mapping.json](sprite-mapping.json).

## Integração no jogo

Foram atualizadas as referências de sprites de 23 animações e quatro prefabs existentes. Os identificadores locais dos quadros foram preservados. Poses, ordem, duração, curvas de hitbox, flip horizontal e eventos de animação continuam vinculados aos estados originais.

- Aventureiro: parado e movimento nas quatro direções, golpes nas quatro direções e morte.
- Slime: repouso, movimento, dano e dissolução/morte.
- Esqueleto: repouso, movimento, ataque, dano, morte e ressurreição.
- Chefe slime: utiliza a nova arte e as animações do slime, conservando sua escala maior e a abertura da barreira ao ser derrotado.

Abra `Assets/Scenes/TelaInicial.unity`, escolha Jogar para verificar a integração na fase existente. Os prefabs também podem ser inspecionados em `Assets/Animations/Player/` e `Assets/Animations/Enemies/`.

Os scripts de controle, dano, detecção e dificuldade, os Animator Controllers e os colliders foram preservados. Esta entrega trata da aparência e da integração das animações.

## Origem da arte

As folhas de personagem do pacote `mystic_woods_2.2`, fornecido pelo usuário, foram usadas como referência de identidade. Os layouts já usados pelo projeto orientaram as poses e a compatibilidade com as animações. O fundo de referência foi `Assets/UI/MainMenu/Art/enchanted-forest.png`.

Os três PNGs foram redesenhados com a ferramenta integrada **imagegen** e incorporados sem repintura posterior. Os prompts completos estão em [imagegen-prompts.json](imagegen-prompts.json); as dimensões efetivamente retornadas pela ferramenta estão na tabela acima. A análise dos pixels serviu apenas para calcular os recortes e os pivôs dos sprites.

O pacote-base é de Game Endeavor. Sua licença permite uso em jogos e modificações, e restringe redistribuição ou revenda dos assets como pacote, inclusive modificados. Os assets originais foram mantidos no projeto.

## Validação e capturas

O relatório em [validation.txt](validation.txt) registra a importação, a resolução das referências, a preservação dos dados de gameplay e os testes em Play Mode na Unity 6000.6.7f1 com interface gráfica.

- [Prévia ampliada dos personagens](Captures/character-lineup.png).
- [Personagem na fase atual](Captures/gameplay-spawn.png).

A prévia usa o fundo da tela inicial para comparar a direção visual. A captura da fase registra o mapa ao final da etapa dos personagens; as capturas do mapa reconstruído estão em [Map](../Map/README.md).
