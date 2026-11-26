# Novas artes do cenário (Codex)

Entrega de 26/11/2026. Quatro artes novas para a `FaseMysticWoods` foram geradas com o **Codex CLI** (`codex exec`, ferramenta `image_generation`). Os atlas `woodland-props.png` e `woodland-ruins.png` e os terrenos `forest-floor.png` e `earth-path.png` serviram de referência de estilo. As artes foram importadas e espalhadas na fase por um builder determinístico.

| Arte | Arquivo | Conteúdo |
|---|---|---|
| Decoração de chão | `Textures/ground-decor.png` | 16 decalques planos sem colisão: 3 tufos de grama, trevo, folhas douradas/verdes, pedrinhas, galho, flores marfim/douradas/azuis, círculo de cogumelos, musgo, raízes, poça, brotos de samambaia. |
| Segunda flora | `Textures/woodland-flora-2.png` | Pinheiro, bétula, árvore morta oca, toco com cogumelos, muda, cogumelos gigantes luminosos, arbusto de frutinhas, capim alto, espinheiro, rocha com cristais âmbar, raiz tombada, samambaias com campânulas. |
| Bordas grama↔terra | `Textures/terrain-edges.png` | 6 faixas de grama que transbordam sobre a terra e 2 cantos em L. |
| Piso de ruínas | `Textures/ruin-floor.png` | Lajes de ardósia com musgo nas juntas e repetição contínua. Fica na arena do chefe e no caminho de pedra até o portão. |

Os prompts completos estão em [codex-prompts.json](codex-prompts.json). Cada job gerou duas variações. As escolhidas estão listadas em `chosen`.

O gerador sempre entrega 1254×1254 px ou 1086×1448 px, ignorando o tamanho pedido. Por isso os recortes não usam uma grade fixa: [sprite-slices.json](sprite-slices.json) guarda retângulos calculados pelas faixas vazias de transparência. O halo semitransparente (alfa < 64) e os pontos soltos fora dos recortes foram removidos. Os pixels não foram repintados.

## Como foi aplicado na fase

Menu **Mystic Woods → Scenery → Build and Dress Level** ou, em batch:

```text
Unity -batchmode -projectPath . -executeMethod MysticWoods.EditorTools.SceneryExpansion.RunBatch
```

O builder fica em `Assets/Editor/Scenery/SceneryExpansion.cs`. Ele é **idempotente**: ao rodar de novo, remove o que ele mesmo colocou e refaz o mesmo resultado, usando semente fixa e sorteio por posição.

| Etapa | Resultado |
|---|---|
| Árvores | 122 dos 365 carvalhos viraram pinheiros, bétulas ou árvores mortas. O ruído de baixa frequência agrupa as espécies em bosques: pinheiros num lado e bétulas no outro. A posição, a escala relativa e a ordem na hierarquia foram mantidas. |
| Sub-bosque | 52 plantas e pedras novas na grama, a uma célula da trilha. Elas ficam a pelo menos 3 u dos pontos de jogo: spawns, chave, portão, marcos, cercas, medalhão e saída. |
| Bordas | 382 faixas de grama no contorno da trilha. Cada faixa gira para a média da grama ao redor (vizinhança 5×5), então as escadinhas de células viram uma diagonal orgânica. Os cantos convexos recebem tufos em L. |
| Decoração | 629 decalques: pedrinhas, folhas, galhos e poças na terra; flores, trevos, musgo e samambaias na grama, até 2 células da trilha. Mais longe, as copas já cobrem o chão. |
| Piso de ruínas | 330 células no Tilemap novo `Grid/PisoRuinas`, com elipse irregular ao redor do medalhão e passarela da ruína até o portão. As bordas têm entulho e musgo. |

Objetos novos na cena:

- `High Detail Forest/Scenery Details/{Edges, Ground Decor, Ruin Rubble}`: só SpriteRenderers estáticos, sem colisão.
- `Woodland Props`: as árvores e plantas novas, como instâncias de prefab.

### Prefabs novos (`Assets/Art/Environment/Prefabs/`)

| Prefab | Base clonada |
|---|---|
| `FirTree`, `BirchTree`, `DeadTree`, `Sapling` | `OakA` |
| `MossStump`, `CrystalRock`, `RootBall` | `MossRockA` |
| `BerryBush`, `TallGrass`, `Bramble`, `Bellflowers` | `FernBush` |
| `GlowShrooms` | `MushroomBush` (Light2D ciano) |

A colisão de todos os prefabs de cenário é definida em `SceneryPhysics`; veja [Física do cenário](#física-do-cenário).

### Ordem de desenho (sorting layer `Bottom`)

`Solo` 0 → `Transicoes` 1 → `PisoRuinas` 2 → bordas 3 → `Detalhes` (flores do tilemap) e decalques 4. Personagens e objetos continuam na layer `Objects`, sempre por cima.

### Shader `Forest Ground`

O shader ganhou a propriedade `_EdgeSoftness`, que define a largura do esfumado da borda em células. O padrão 0,27 é o valor fixo anterior, então `Forest Path.mat` não muda. `Ruin Floor.mat` usa 0,46 e `_TextureWorldSize` = 8. A textura do piso foi importada com limite de 512 px, cerca de 64 texels por unidade, a mesma densidade da grama.

## Props regenerados (`woodland-props.png`)

Os 12 props originais foram redesenhados com o Codex: 3 carvalhos, 3 arbustos de samambaia, 3 rochas com musgo, tronco caído e 2 cercas. Cada item ficou no mesmo lugar e manteve a silhueta. O traço ficou mais nítido, as bordas ficaram limpas, sem halo, e a paleta acompanha `woodland-flora-2.png`. O prompt está em `codex-prompts.json` → `props`. Foram geradas quatro variações, e a escolhida foi a de sprites maiores. A grade do recorte está em [woodland-props-grid.png](Captures/woodland-props-grid.png).

- As copas encostam umas nas outras, então o recorte corta na coluna mais vazia entre elas. Ilhas soltas com menos de 40 px dentro de cada recorte foram apagadas.
- A troca preserva os **nomes e spriteIDs** dos sprites, com `ISpriteEditorDataProvider`. Os prefabs continuam apontando para os mesmos sprites.
- A altura no mundo de cada prop foi mantida. Os prefabs e as 283 instâncias da cena foram reescalados pela razão entre a altura antiga e a nova do sprite.

## Física do cenário

Menu **Mystic Woods → Scenery → Configure Physics** ou `MysticWoods.EditorTools.SceneryPhysics.RunBatch`. O `SceneryExpansion` também chama esta etapa no final. Ela é idempotente. As regras ficam em `Assets/Editor/Scenery/SceneryPhysics.cs`, numa tabela por prefab:

| Tipo | Prefabs | Configuração |
|---|---|---|
| Árvore | Oak A/B/C, Fir, Birch, Dead, Sapling | Cápsula horizontal só na base do tronco, gatilho na copa e `TransparetObject`. |
| Sólido orgânico | rochas, toco, tronco caído, rocha de cristal, raiz, pilar quebrado, marco âmbar | Cápsula horizontal na pegada no chão. |
| Construído | cercas, muro | Caixa fina na linha da base. |
| Arco | `OpenArch` | Uma caixa em cada pilar, vão livre de cerca de 4,5 u, e gatilho de transparência. |
| Caminhável | arbustos, flores, capim, espinheiro, cogumelos luminosos, medalhão | Sem collider e sem corpo. |
| Jogo | `BossGate`, `ArenaEntrySeal`, `FinalSeal` | Geometria mantida, porque os scripts controlam. Só layer, corpo e material. Chave, saída e entrada da arena não mudaram. |

Regras comuns:

- Os obstáculos ficam na layer nova **`Environment`** (9), num `Rigidbody2D` **Static**.
- Todos usam o material **`Assets/Settings/Physics/Environment Obstacle`**, com atrito 0 e quique 0, para os personagens deslizarem em vez de grudar.
- A layer `Environment` colide exatamente com o que `Default` colide.
- O contorno do mapa (`Bloqueios`) também está nessa layer e usa esse material.
- Os gatilhos não bloqueiam movimento e são ignorados pela linha de visão do `HitQuery`.

**Correções encontradas na revisão:**

- O `AmberWaystone` não tinha colisão: dava para atravessar os 6 marcos.
- Nas 87 instâncias com `flipX`, a arte espelhava mas os colliders não. Agora o offset é espelhado em cada instância; são 128 colliders.
- A instância do arco na cena tinha colliders, corpo e `TransparetObject` adicionados à mão. Com o prefab assumindo a física, eles ficariam duplicados, então foram removidos.
- As caixas das cercas ficavam abaixo dos postes.
- Antes, sólidos com caixa e atrito padrão prendiam o personagem em 7 de 38 aproximações diagonais. Depois da revisão, isso não acontece em nenhuma.

O teste `Assets/Tests/World/EditMode/SceneryPhysicsTests.cs` cobre essas regras: layer, corpo estático, material sem atrito, pegada só na base (até 35% da altura), um gatilho por prop com transparência, vegetação caminhável e colliders espelhados nas instâncias com `flipX`.

| Overlay de colisão (vermelho = sólido, ciano = gatilho) | Antes | Depois |
|---|---|---|
| Entrada | [antes](Captures/physics-entrance-antes.jpg) | [depois](Captures/physics-entrance-depois.jpg) |
| Ruínas / portão | [antes](Captures/physics-ruins-antes.jpg) | [depois](Captures/physics-ruins-depois.jpg) |
| Borda da floresta | [antes](Captures/physics-forest-edge-antes.jpg) | [depois](Captures/physics-forest-edge-depois.jpg) |

## Validação

Tudo foi rodado primeiro numa cópia APFS do projeto e depois no projeto real, com Unity 6000.6.7f1. O builder gerou os mesmos números nas duas execuções.

- `RestructureRunner.VerifyBatch`: **PASS**. Nenhum script ausente nem shader com erro nas 4 cenas.
- Testes EditMode: **36/36**, incluindo os 6 de física do cenário. Testes PlayMode: **24/24**.
- Rota física: busca em largura com uma caixa do tamanho do jogador a partir do início. A chave, o portão, a arena e a saída continuam alcançáveis.
  - Na expansão do cenário, a área caminhável caiu 0,24% (109.565 → 109.301 amostras).
  - Na revisão de física, caiu mais 0,9% (→ 108.336), porque os marcos, os pilares do arco e as pegadas agora bloqueiam.
- Deslizamento: um corpo igual ao do jogador (cápsula de 0,55×0,3, dinâmica, a 4,2 u/s) foi empurrado na diagonal contra uma instância de cada obstáculo, por dois lados. Nenhum ficou preso, e a penetração máxima foi de 0,016 u.
- Pendente: playtest humano para avaliar densidade e legibilidade em movimento. O desempenho não foi medido de novo; bordas, decalques e entulho somam 1.068 SpriteRenderers estáticos a mais, e a cena passou de 4,1 MB para 7,2 MB.

## Capturas (antes → depois)

| | Antes | Depois |
|---|---|---|
| Visão geral | [overview-antes](Captures/overview-antes.jpg) | [overview-depois](Captures/overview-depois.jpg) |
| Entrada | [entrance-antes](Captures/entrance-antes.jpg) | [entrance-depois](Captures/entrance-depois.jpg) |
| Borda da trilha | [edges-closeup-antes](Captures/edges-closeup-antes.jpg) | [edges-closeup-depois](Captures/edges-closeup-depois.jpg) |
| Ruínas / portão | [ruins-antes](Captures/ruins-antes.jpg) | [ruins-depois](Captures/ruins-depois.jpg) |
| Arena | [arena-antes](Captures/arena-antes.jpg) | [arena-depois](Captures/arena-depois.jpg) |
