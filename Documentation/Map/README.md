# Floresta reconstruída — High-Detail Pixel Art

A fase `FaseMysticWoods` foi reconstruída como um mapa fixo e editável de 96×80 células. A direção visual acompanha a tela inicial e os personagens: floresta esmeralda, sombras em azul-petróleo, musgo, bronze e pontos de luz dourada. A duração-alvo de uma primeira partida é 5–10 minutos, dependendo da exploração e do combate.

## Percurso e progressão

| Área | Referência no mundo | Conteúdo |
| --- | --- | --- |
| Entrada | (19.5, 13.5) | Clareira segura, cercas e marco luminoso. |
| Trilha central | (31, 38) | Clareiras conectadas, encontros e caminhos de cinco a seis células. |
| Clareira leste | (53, 29) | A única chave de ouro, em (57, 31). |
| Ruínas | Portão em (63, 54) | Uma chave abre o acesso à arena. |
| Arena nordeste | x=60–87, y=57–76 | Área livre de 28×20 células; chefe em (76, 66). |
| Saída | (92, 66) | Disponível depois da derrota do chefe. |

O jogador coleta a chave de ouro diretamente, abre o portão, entra na arena e derrota o chefe para liberar a saída. A corrente de chave de prata e baú foi removida desta fase. O contador de ouro continua no HUD; os elementos de prata foram ocultados.

Existem sete slimes e cinco esqueletos antes da arena: dois slimes no trecho inicial; três slimes e dois esqueletos na trilha central; dois slimes e dois esqueletos na clareira; um esqueleto nas ruínas. Foram preservados os atributos de combate, controladores, animações e a escala dos personagens e inimigos. Os encontros anteriores ao chefe não são requisitos adicionais para abrir o portão.

## Abrir e editar

Abra `Assets/Scenes/TelaInicial.unity`, escolha Jogar. A partida começa diretamente, com seis corações e as configurações do antigo modo normal. O nome e o GUID da fase foram preservados. O mapa está salvo na cena; nenhum gerador reconstrói a fase durante o Play Mode.

Em `High Detail Forest/Grid`, os Tilemaps são:

- **Solo:** chão contínuo de floresta, sem colisão.
- **Transicoes:** caminhos de terra; o asset `Forest Path` calcula os vizinhos e mantém as bordas conectadas.
- **Detalhes:** pequenos agrupamentos de flores, sem colisão.
- **Bloqueios:** máscara física independente da aparência, com TilemapCollider2D e CompositeCollider2D. O renderer fica desativado; ative-o temporariamente para visualizar a máscara.

Ao mudar uma rota, edite também os bloqueios correspondentes. Copas e luzes não bloqueiam a passagem. Os prefabs de árvores, rochas, troncos e cercas possuem colisões simples nos pontos de apoio. A copa e os arcos ficam transparentes quando encobrem o jogador.

Os prefabs de portão, entrada, barreiras e saída também estão conectados às instâncias da cena. Os prefabs ficam em `Assets/Art/Environment/HighDetail/Prefabs/`. `GoldKey` contém o coletor, o sprite animado e a luz de destaque; sua instância na cena referencia o KeysManager do HUD. Ao inserir outra chave em outra cena, configure essa referência.

A profundidade utiliza as sorting layers existentes: Bottom para o chão, Objects para personagens e objetos, e Foreground para vaga-lumes. A câmera ordena transparências pelo eixo Y e permanece contida no retângulo pintado de 96×80 células.

## Arte, materiais e compatibilidade

Os cinco PNGs estão em `Assets/Art/Environment/HighDetail/Textures/`, separados dos assets originais. Foram gerados com a ferramenta integrada **imagegen**, usando os terrenos, objetos, cercas, chave e arquitetura existentes e o fundo da tela inicial como referências.

Os prompts completos ficam em [imagegen-prompts.json](imagegen-prompts.json). [sprite-mapping.json](sprite-mapping.json) registra dimensões, hashes SHA-256, nomes, recortes e pivôs dos 22 sprites de objetos/efeitos. Os PNGs foram incorporados sem repintura posterior; os retângulos foram obtidos por análise da transparência.

Os importadores usam Point, sRGB, transparência nos atlas, ausência de mipmaps e compressão. Os atlas mantêm sua resolução. As duas texturas de terreno usam limite de importação de 256 pixels e cobrem quatro unidades do mundo por repetição, resultando em aproximadamente 64 texels por unidade. Os pixels por unidade do sprite-base mantêm cada célula em uma unidade do mundo.

O shader `Mystic Woods/Forest Ground` usa coordenadas do mundo e repetição espelhada para compartilhar os mesmos texels nas emendas. O tile de caminho envia os vizinhos para suavizar as bordas. Preserve o material de cada Tilemap ao trocar seus tiles.

O projeto continua com o pipeline Built-in, espaço de cores Linear e saída SDR. A iluminação vem da arte, das sobreposições transparentes e dos vaga-lumes. Esta etapa não adiciona trilha sonora.

`KeyController` ganhou `KeyType { Silver, Gold }`, com padrão Silver, mantendo compatibilidade com referências antigas. A coleta possui proteção contra callbacks duplicados. `KeysManager`, `PortaBoss`, `EntradaBoss` e `AreaFinal` continuam com suas interfaces existentes. `TransparetObject` preserva os campos serializados e agora restaura a opacidade exata, com uma única transição ativa.

## Verificação e capturas

O [relatório de validação](validation.txt) registra os testes na Unity 6000.6.7f1 com interface gráfica, incluindo movimento por rotas físicas, coleta, portão, arena, saída, reinício, pausa, transparência e limites da câmera. A medição de desempenho é feita no Editor em 1920×1080, com os inimigos ativos, após aquecimento. A amostra final registrou 2621 quadros de câmera em seis segundos, média de 2,29 ms e p95 de 4,97 ms. Esses valores medem a renderização no Editor com vSync desativado, não a taxa de atualização do monitor nem um build standalone. Capturas de revisão visual usam os inimigos temporariamente suspensos na cópia de testes.

A duração-alvo de primeira partida permanece uma estimativa de design. O percurso automatizado valida a progressão e as colisões; o tempo de exploração de um jogador novo precisa de playtest humano.

- [Encaixe dos tiles](Captures/terrain-joins.png).
- [Visão geral](Captures/map-overview.png).
- [Entrada](Captures/entrance-1920x1080.png).
- [Trilha](Captures/trail.png).
- [Clareira da chave](Captures/key-clearing.png).
- [Ruínas do portão](Captures/gate-ruins.png).
- [Arena](Captures/arena.png).

Também foram capturadas a entrada em 1280×720, 2560×1080 e 1920×1200, e a arena após o fechamento das barreiras.

## Início direto da partida

A tela de dificuldade e o modo difícil foram removidos. O jogador começa com seis corações, inclusive ao abrir a fase diretamente no Editor ou reiniciar após pausa ou morte. O relatório [direct-start-validation.txt](direct-start-validation.txt) registra a validação desse fluxo; [direct-start.png](Captures/direct-start.png) mostra a partida iniciada sem seleção.
