# Reestruturação gráfica, de combate e da IA

Entrega de 25/11/2026. A fase `FaseMysticWoods` passou a usar o novo personagem (`PlayerFoundation`), o pipeline **URP 2D** com iluminação e pós-processamento, um sistema de combate compartilhado e uma IA única para slimes, esqueletos e o chefe.

## Controles

| Ação | Teclado | Mouse | Controle |
|---|---|---|---|
| Mover | WASD / setas | — | Analógico esquerdo |
| Correr | Shift | — | Gatilho esquerdo |
| Atacar (combo de 3 golpes) | Espaço / J | Botão esquerdo | Botão oeste (X / □) |
| Esquivar (invulnerável) | K / Ctrl esquerdo | Botão direito | Botão leste (B / ○) |
| Pausar | Esc | — | — |

O painel **Comandos** (pausa e menu inicial) ganhou a linha da esquiva.

## 1. Gráficos — URP 2D

- `Assets/Settings/Rendering/MysticWoods-URP.asset` + `MysticWoods-2DRenderer.asset` atribuídos em Graphics Settings (todos os níveis de qualidade herdam). O renderer ordena transparências pelo eixo Y, como a câmera fazia antes; HDR interno ligado para o bloom.
- Os quatro shaders próprios foram reescritos em HLSL/URP, mantendo nome e GUID (os materiais não mudaram de referência):
  - **Idle Sprite** (personagens): iluminado por Light2D, respiração/deformação ancorada nos pés, **flash de dano** pós-iluminação, dissolução em pixels (`_Fade`), deslocamento visual para pulos (`_VisualOffset`) e um piso de luz para o personagem nunca sumir no escuro.
  - **Forest Ground** (Tilemaps): mesma repetição espelhada e bordas suavizadas, agora iluminado.
  - **Attack Telegraph**: não iluminado de propósito — o aviso precisa ser legível no escuro.
  - **Effect Sprite** (novo): partículas, vaga-lumes e rastros, com blend aditivo ou alfa.
- Iluminação (`Assets/Art/Effects/Prefabs/ForestAtmosphere.prefab`): luz global levemente fria; luzes pontuais quentes no personagem, marcos âmbar, chave, portão e saída; ciano nos cogumelos; verde no chefe e nos projéteis. As luzes ficam **nos prefabs** do ambiente, então novas instâncias já vêm iluminadas.
- Pós-processamento: `ForestPost` (bloom, vinheta, contraste, sombras azul-petróleo/realces quentes) e `DamagePulse` (vinheta vermelha, aberração cromática, dessaturação), acionado ao receber dano e pulsando com um coração restante.
- Sprites do mundo usam `Sprite-Lit-Default`; manchas de sol e halos pintados usam `Sprite-Unlit-Default` para não serem escurecidos nem estourados pelo bloom.
- Sombra circular sob cada personagem, ícones “!” (alerta) e “?” (procura), barra do chefe no HUD e corações que saltam ao perder vida.

## 2. Combate

Montagem `MysticWoods.Combat` (`Assets/Gameplay/Combat`):

- `Health` — vida, time (jogador/inimigo), invulnerabilidade empilhável, **postura** (poise) e eventos `Damaged`, `Staggered`, `Died`. Não move nem desenha nada.
- `DamageInfo` / `IHittable` / `IPushable` — contrato único de dano (quantidade, direção, recuo, postura, golpe pesado).
- `HitQuery` — caixas e setores que acertam cada alvo uma vez por golpe; linha de visão que ignora personagens.
- Feedback: `HitStop` (nunca disputa o tempo com o menu de pausa), `CameraShake` + `CinemachineShake`, `CombatVfx` (faíscas, poeira, explosão de morte, respingo, onda de choque), `SpriteFlash`, `AfterImage`, `DamageVignette`.

Jogador (`Assets/Gameplay/Player`): `PlayerController` virou o árbitro de ações — **Morte > Dano > Esquiva > Ataque > Locomoção**.

| Golpe | Duração | Janela de acerto | Dano | Postura | Recuo |
|---|---:|---|---:|---:|---:|
| 1 | 0,36 s | 0,07–0,20 s | 1 | 1 | 6 |
| 2 | 0,34 s | 0,06–0,19 s | 1 | 1 | 6 |
| 3 (finalizador) | 0,52 s | 0,12–0,28 s | 2 | 3 | 11 |

- Cada golpe avança um passo; o comando é guardado por 0,2 s e a direção é a que estava pressionada **no instante do aperto**.
- Esquiva: 0,30 s, 2,6 u, invulnerável de 0,02 a 0,26 s, recarga 0,38 s, cancela o fim de um golpe (nunca a parte ativa).
- Dano recebido: atordoamento de 0,22 s, recuo limitado a 9 u/s, 0,9 s de invulnerabilidade com piscar.
- Animações: os quadros de ataque e morte do atlas existente viraram `Attack_*` e `Death` no controller novo. Não há arte de rolamento nem de dano: a esquiva usa o ciclo de caminhada acelerado com rastro, e o dano usa flash + recuo.
- Tudo fica em `DefaultPlayerConfig.asset` (Inspector).

## 3. Inteligência dos inimigos

Montagem `MysticWoods.Enemies` (`Assets/Gameplay/Enemies`). Os três prefabs foram convertidos no lugar (GUIDs mantidos), então todas as instâncias da fase seguiram automaticamente.

- **Percepção**: raio de visão com linha de visão (árvores, cercas e muros bloqueiam), raio maior de perseguição, memória da última posição, **audição** dos golpes de espada e alerta aos aliados próximos.
- **Estados**: Parado → Vagando → Alerta (“!”, tempo de reação) → Perseguindo → Cercando → Atacando (preparação/ativo/recuperação) → Atordoado → Recuando → Procurando (“?”) → Voltando (limite de distância da origem) → Caído/Morto.
- **Movimento**: steering por contexto (16 direções) que contorna obstáculos e evita amontoar; slimes se movem em pulos.
- **Coordenação**: no máximo **2 atacantes simultâneos** por alvo, com intervalo mínimo entre inícios de ataque; os demais circulam.
- **Postura**: slimes são interrompidos a cada golpe; esqueletos aguentam um golpe; o chefe tem super-armadura nos ataques.

| Inimigo | Vida | Ataques | Comportamento próprio |
|---|---:|---|---|
| Slime | 3 | Bote (setor) · Salto (investida com faixa de aviso) | pulos, alerta aliados |
| Esqueleto | 4 | Corte (setor) · Estocada (investida longa) | circula, recua após apanhar (35%), **levanta uma vez** com metade da vida |
| Rei Slime | 24 | Esmagar · Salto esmagador (anel no chão) · Cuspe ácido (5 projéteis, fase 2+) · Chamado (2 slimes, fase 2+) | 3 fases (66% e 33%): rugido invulnerável que empurra o jogador, fica mais rápido; derrota abre a barreira final |

Configurações editáveis em `Assets/Gameplay/Enemies/Configuration/*.asset`.

## Estrutura e arquivos removidos

```text
Assets/Gameplay/Combat/Runtime/      contrato de dano, vida, consultas e feedback
Assets/Gameplay/Enemies/Runtime/     cérebro, motor, telegrafia, apresentação, projétil
Assets/Gameplay/Enemies/Configuration/ SlimeConfig, SkeletonConfig, SlimeBossConfig
Assets/Gameplay/Player/Runtime/      + PlayerCombat; controller, input e motor ampliados
Assets/Art/Effects/                  texturas geradas, shader e materiais de efeito, prefabs
Assets/Settings/Rendering/           URP, renderer 2D e perfis de volume
Assets/UI/HUD/Scripts/               HeartsHud, BossHealthBar
Assets/Editor/Restructure/           builders idempotentes (menu Mystic Woods → Restructure)
Assets/Tests/Combat/                 testes EditMode e PlayMode
```

O personagem e os inimigos antigos foram substituídos e seus scripts apagados: `PlayerController` (legado), `HeartSystem`, `SwordHitbox`, `SwordAttack`, `CadencedMovement2D`, `SlimeController`, `SkeletonController`, `SlimeBoss`, `EnemyMeleeAttack`, `DetectionController`, `IdleSpriteMotion`, `IDamageable` e `Assets/Animations/Player/`. Eles continuam no histórico do git. `GameMenuController`, `GameplayNotice`, `PortaBoss`, `TransparetObject` e `PauseMenu` foram religados ao personagem novo.

## Reaplicar ou ajustar

Os builders podem ser executados de novo pelo menu **Mystic Woods → Restructure** (1. Rendering, 2. Effects/Player/Enemies, 3. Migrate Level, Verify). Pela linha de comando:

```text
Unity -batchmode -projectPath . -executeMethod MysticWoods.EditorTools.RestructureRunner.RunAll
Unity -batchmode -projectPath . -executeMethod MysticWoods.EditorTools.RestructureRunner.VerifyBatch
```

Atenção: os builders **sobrescrevem** as configs de inimigos e os prefabs gerados com os valores do código. Depois de ajustar valores no Inspector, não rode o passo 2 de novo sem levar os valores para o código.

## Validação

- [Verificação](Validation/verify.txt): nenhuma cena ou prefab com script faltando, nenhum shader com erro, pipeline `MysticWoods-URP` ativo.
- [EditMode](Validation/editmode-results.xml): **30/30**. [PlayMode](Validation/playmode-results.xml): **24/24** — inclui os 10 testes da base do personagem e 14 novos (combo, direção do golpe, janela de invulnerabilidade da esquiva, recuo, morte, hit-stop × pausa, reação e telegrafia do slime, paredes bloqueando a visão, limite de atacantes, ressurreição única do esqueleto, fases e barreira do chefe, retorno à origem, audição, recuo dos inimigos).
- Capturas em execução: [início](Captures/spawn.png), [aviso do slime](Captures/telegraph.png), [morte](Captures/death.png), [anel do salto do chefe](Captures/boss-telegraph.png), [mudança de fase](Captures/boss-phase.png), [cuspe ácido e invocação](Captures/boss-attack.png), [dano no jogador](Captures/hurt.png).

## Limitações

1. Validado no Editor do macOS (Unity 6000.6.7f1) em batchmode. Não foi gerado build standalone/Web, nem feito playtest humano prolongado — o equilíbrio de dificuldade (sobretudo o chefe) precisa ser sentido jogando.
2. Não existe arte de rolamento nem de dano do personagem; foram usados ciclo acelerado + rastro e flash + recuo, sem quadros inventados.
3. Sombras 2D projetadas (ShadowCaster2D) não foram ativadas; as sombras são as manchas sob os personagens.
4. Os harnesses de validação antigos em `Documentation/Combat` e `Documentation/GameMenus` referenciam scripts removidos e ficam apenas como registro histórico.
5. A cena de recuperação `Assets/_Recovery/1.unity` (fora do build) referenciava o prefab antigo do jogador e passa a mostrá-lo como ausente.
