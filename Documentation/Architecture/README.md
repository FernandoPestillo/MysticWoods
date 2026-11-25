# MysticWoods — base independente do personagem

## Como testar

No projeto Unity, use **Mystic Woods → Player → Open Sandbox** ou abra:

`Assets/Gameplay/Player/Sandbox/PlayerSandbox.unity`

Pressione Play. WASD ou setas movem o personagem; Shift esquerdo/direito altera a velocidade. As paredes e os dois obstáculos permitem testar frenagem, bloqueio e deslizamento. O painel de diagnóstico da cena mostra estado, direção, velocidade efetiva e quantidade de trocas de animação.

A cena principal continua usando o personagem anterior. Esta entrega disponibiliza a base nova para validação no laboratório; ela não realiza a migração da partida nem implementa o combate da próxima fase. O sandbox não possui um menu de Pause próprio. A pausa foi exercitada pelo teste que controla Time.timeScale, sem reconstruir interfaces.

## Estrutura resultante

```text
Assets/Gameplay/Player/
├── Runtime/
│   ├── MysticWoods.Player.asmdef
│   ├── PlayerConfig.cs
│   ├── PlayerFacing.cs
│   ├── PlayerInputReader.cs
│   ├── PlayerController.cs
│   ├── PlayerMovement.cs
│   └── PlayerAnimationController.cs
├── Configuration/
│   ├── DefaultPlayerConfig.asset
│   └── PlayerControls.inputactions
├── Animation/
│   ├── PlayerLocomotion.controller
│   ├── Idle_Down / Right / Up / Left.anim
│   └── Walk_Down / Right / Up / Left.anim
├── Prefabs/PlayerFoundation.prefab
├── Editor/
│   ├── MysticWoods.Player.Editor.asmdef
│   ├── PlayerFoundationBuilder.cs
│   └── PlayerSpriteAlignment.json
└── Sandbox/
    ├── PlayerSandbox.unity
    ├── MysticWoods.Player.Sandbox.asmdef
    ├── PlayerSandboxDiagnostics.cs
    ├── SandboxSolid.shader
    └── Sandbox Floor / Grid / Obstacles.mat
Assets/Tests/Player/
├── EditMode/   (assembly e testes de assets/arquitetura/alinhamento)
└── PlayMode/   (assembly e testes de comportamento físico/visual)
Documentation/Architecture/
├── Diagnostico.md
├── README.md
├── Validation.md
├── FileManifest.md
├── AuditInventory.json
└── Validation/ e Captures/
```

O `.meta` de cada novo recurso e de suas pastas acompanha a entrega. Não houve substituição, movimentação ou exclusão de recursos anteriores.

## Configuração e componentes

O prefab novo é construído a partir de componentes próprios; não é uma variante que herda os scripts do personagem antigo.

```text
Player Foundation                  (raiz: física e comandos)
  Rigidbody2D + CapsuleCollider2D
  PlayerInputReader + PlayerController + PlayerMovement
└── Visual                         (raiz da apresentação)
    Animator + PlayerAnimationController
    └── Sprite                     (único alvo dos novos clips)
        SpriteRenderer
```

A configuração fica em `DefaultPlayerConfig.asset`, editável no Inspector. É possível criar outra em **Create → Mystic Woods → Player → Configuration** e atribuí-la a PlayerMovement.

| Campo | Valor inicial | Significado |
|---|---:|---|
| Walk Speed | 3,5 u/s | Velocidade de caminhada |
| Run Speed | 5,25 u/s | Velocidade com Shift |
| Acceleration Time | 0,12 s | Aproximação da velocidade alvo a partir do repouso |
| Braking Time | 0,08 s | Frenagem até parar |
| Moving Threshold | 0,03 u/s | Entrada em Walk; saída usa metade para evitar oscilação |
| Direction Hysteresis | 0,03 | Tolerância entre componentes da velocidade nas diagonais |
| Idle Breathing | 0,015 | Respiração visual de até 1,5%, sem mover a física |
| Breathing Period | 1,8 s | Período da respiração |

O motor escreve a velocidade do Rigidbody2D em FixedUpdate. O corpo tem gravidade/amortecimento zero, interpolação, detecção contínua e rotação travada. O collider fica nos pés: tamanho 0,55×0,30 e centro local (0; 0,15). A apresentação lê a velocidade física resultante, de modo que um bloqueio por parede não mantém uma caminhada sem deslocamento.

PlayerInputReader instancia seu próprio InputActionAsset. Assim, desativar esse personagem não desativa ações compartilhadas de outra instância. Não existem referências a vida, UI, inimigos, inventário, portas, IDamageable ou espadas na assembly de runtime.

## Contrato de animação

- Estados: **Idle e Walk**. Cada um possui quatro direções cardeais.
- Corrida: reaproveita o ciclo Walk existente e eleva sua reprodução conforme a velocidade. Não há clip de Run dedicado no material disponível.
- Idle: uma pose válida existente por vista, com a respiração sutil já adotada no estilo do projeto. A vista esquerda espelha a direita.
- Walk: seis quadros existentes por vista, a 8 fps na velocidade de caminhada; duração de 0,75 s. Em corrida, o multiplicador chega a 1,5.
- Um único parâmetro no Animator: **PlaybackRate**, float, centralizado em `PlayerAnimationController.PlaybackRateParameter`. Ele altera somente Walk; a velocidade global do Animator continua em 1.
- Estados nomeados `Idle_Down`, `Idle_Right`, `Idle_Up`, `Idle_Left` e equivalentes `Walk_*`. Não há estados vazios, Any State, blend trees ou transições que misturem sprites discretos.
- A apresentação chama Play apenas quando muda estado/direção. Trocar direção enquanto caminha conserva a fase do passo. Manter o comando não reinicia o clip.
- O espelhamento pertence somente a PlayerAnimationController. Nenhum clip novo escreve flipX.
- Os clips escrevem sprite e posição local do filho Sprite. A compensação de alinhamento é calculada a partir da base da silhueta opaca (alfa > 32), com correção do sinal horizontal na vista espelhada. Raiz, Rigidbody2D e collider não são animados.
- Nas diagonais próximas de um empate, conserva-se o eixo anterior, ajustando o sinal ao movimento atual; ao parar, a última direção permanece.
- Attack, Hurt e Death não possuem regras nem comandos nesta base. Há arte antiga de ataque/morte preservada; Hurt precisa de uma decisão artística própria. Não foram criados placeholders para essas ações.

## Ferramentas e testes

As assemblies de testes usam o Unity Test Framework já instalado. No Test Runner, execute `MysticWoods.Player.EditModeTests` em EditMode e `MysticWoods.Player.PlayModeTests` em PlayMode.

A ferramenta PlayerFoundationBuilder cria os recursos ausentes e abre o laboratório pelo menu. Ela não reescreve automaticamente a cena/prefab se eles já existem. As configurações e objetos criados podem ser editados normalmente no Inspector. O JSON de alinhamento documenta os deslocamentos usados para gerar os clips; editá-lo sozinho não altera clips já salvos.

Veja [o diagnóstico](Diagnostico.md), [os resultados e limites](Validation.md) e [a relação de arquivos](FileManifest.md).

## Limitações e pendências

1. A qualidade anatômica e as pequenas diferenças de desenho internas dos quadros continuam sendo características da arte atual. O alinhamento da base foi corrigido; o atlas não foi redesenhado. Não há promessa de um novo ciclo artístico.
2. Validação realizada no Editor macOS com Unity 6000.6.7f1. Não foi produzido um build Web/desktop nesta etapa, nem feito teste manual prolongado em navegadores, controles ou dispositivos móveis. As ações implementadas são de teclado.
3. ~~A integração com a fase, o HeartSystem, os menus de morte/pausa e os inimigos ainda não foi migrada.~~ Concluída na [reestruturação de 25/11/2026](../Restructure/README.md): a fase usa esta base, com combate, vida e HUD próprios.
4. A lista de build possui um caminho desabilitado antigo para Telarestart.unity. A configuração gráfica global aponta um GUID de pipeline que não foi localizado nos recursos; a execução de teste confirmou o pipeline Built-in. Nenhuma dessas configurações foi alterada nesta entrega. O material do sandbox foi ajustado ao ambiente efetivo, com um teste visual para erro de shader.
5. Os testes automatizados e as capturas verificam estados, física, referências, continuidade e alinhamento. A aprovação final do estilo e da sensação dos passos continua sendo uma avaliação humana.

## Próxima fase recomendada

Primeiro validar este personagem no laboratório. Depois especificar comandos de ação e prioridades (locomoção, ataque, reação e morte), contratos de dano e sincronização visual. A integração de saúde deve separar dados/regras da atualização de corações e oferecer um adaptador aos menus existentes. A escolha de arte de Hurt precede sua animação.

Migrar a fase principal somente após esses contratos e testes, por um prefab/área de cada vez, evitando dois controladores do mesmo personagem simultaneamente. Em etapas seguintes, auditar colisões/portas por área, resolver a referência gráfica pendente com regressão das interfaces e revisar a composição da IA. Inventário, loot e economia permanecem posteriores; nada disso foi implementado agora.
