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

| Prefab | Base clonada | Colisão |
|---|---|---|
| `FirTree`, `BirchTree`, `DeadTree`, `Sapling` | `OakA` | Caixa no tronco, gatilho na copa e `TransparetObject`: a copa fica translúcida quando o jogador passa atrás. |
| `MossStump`, `CrystalRock`, `RootBall` | `MossRockA` | Caixa na base. |
| `BerryBush`, `TallGrass`, `Bramble`, `Bellflowers` | `FernBush` | Nenhuma. |
| `GlowShrooms` | `MushroomBush` | Nenhuma. Light2D ciano. |

### Ordem de desenho (sorting layer `Bottom`)

`Solo` 0 → `Transicoes` 1 → `PisoRuinas` 2 → bordas 3 → `Detalhes` (flores do tilemap) e decalques 4. Personagens e objetos continuam na layer `Objects`, sempre por cima.

### Shader `Forest Ground`

O shader ganhou a propriedade `_EdgeSoftness`, que define a largura do esfumado da borda em células. O padrão 0,27 é o valor fixo anterior, então `Forest Path.mat` não muda. `Ruin Floor.mat` usa 0,46 e `_TextureWorldSize` = 8. A textura do piso foi importada com limite de 512 px, cerca de 64 texels por unidade, a mesma densidade da grama.

## Validação

Tudo foi rodado primeiro numa cópia APFS do projeto e depois no projeto real, com Unity 6000.6.7f1. O builder gerou os mesmos números nas duas execuções.

- `RestructureRunner.VerifyBatch`: **PASS**. Nenhum script ausente nem shader com erro nas 4 cenas.
- Testes EditMode: **30/30**. Testes PlayMode: **24/24**.
- Rota física: busca em largura com uma caixa do tamanho do jogador a partir do início. A chave, o portão, a arena e a saída continuam alcançáveis. A área caminhável caiu 0,24% (109.565 → 109.301 amostras), por causa dos tocos e rochas novos fora da trilha.
- Pendente: playtest humano para avaliar densidade e legibilidade em movimento. O desempenho não foi medido de novo; bordas, decalques e entulho somam 1.068 SpriteRenderers estáticos a mais, e a cena passou de 4,1 MB para 7,2 MB.

## Capturas (antes → depois)

| | Antes | Depois |
|---|---|---|
| Visão geral | [overview-antes](Captures/overview-antes.jpg) | [overview-depois](Captures/overview-depois.jpg) |
| Entrada | [entrance-antes](Captures/entrance-antes.jpg) | [entrance-depois](Captures/entrance-depois.jpg) |
| Borda da trilha | [edges-closeup-antes](Captures/edges-closeup-antes.jpg) | [edges-closeup-depois](Captures/edges-closeup-depois.jpg) |
| Ruínas / portão | [ruins-antes](Captures/ruins-antes.jpg) | [ruins-depois](Captures/ruins-depois.jpg) |
| Arena | [arena-antes](Captures/arena-antes.jpg) | [arena-depois](Captures/arena-depois.jpg) |
