# MysticWoods — diagnóstico e arquitetura da primeira entrega

Auditoria realizada em 11/11/2026, antes de alterar código de produção. Base: commit `00aa498` (`Criando avisos de ação`). Escopo: personagem independente, estados Idle/Walk, corrida usando o ciclo de locomoção existente, cena de testes e documentação. A migração do mapa principal será posterior à validação.

## 1. Inventário e ambiente

Unity **6000.6.7f1**, URP **17.6.0**, Input System **1.20.1**, Cinemachine **6.6.0**, UGUI/TMP **2.6.0**, Test Framework **1.8.0**. A feature 2D já inclui Animation 16.0.1, Sprite, PSD Importer, Pixel Perfect e Tilemap. Nenhuma dependência nova é necessária.

O inventário de Assets contém 47 prefabs, 26 clips, quatro Animator Controllers e 97 scripts, dos quais 33 pertencem ao jogo/UI; os demais incluem exemplos TMP e integração de editor. Existem 35 arquivos de cena no conjunto de Assets, incluindo exemplos e recuperação. Há três cenas atuais do jogo: TelaInicial, FaseMysticWoods e TelaFinal. A lista de build ainda contém uma entrada desabilitada para Telarestart.unity, cujo arquivo não existe mais; isso é um resíduo de configuração, não uma cena ativa.

| Área | Estrutura atual | Decisão |
|---|---|---|
| UI | Assets/UI/MainMenu, Shared, GameMenus | Preservar integralmente |
| Gameplay | Assets/Scripts, namespace global, Assembly-CSharp | Manter como legado durante migração |
| Personagem atual | Assets/Animations/Player/Player.prefab, Player.controller | Preservar; não reutilizar como dependência do novo prefab |
| Arte atual | Assets/Art/Characters/HighDetail | Reutilizar sprites e material, sem alterar o atlas ou seus .meta |
| Arte original | Assets/Mystic Woods/characters | Preservar como referência |
| Ambientes | Prefabs HighDetail, Tilemaps e cena de 3,9 MB | Auditar por área em fases futuras |
| Exemplos/recuperação | TextMesh Pro/Examples & Extras e _Recovery | Não excluir; verificar dependências antes de eventual limpeza |

## 2. Diagnóstico técnico

### Acoplamento e responsabilidades

- `PlayerController` lê teclado/mouse, escolhe direção, controla física, cronometra golpes, configura hitboxes e escreve parâmetros do Animator. Seu Awake exige quatro espadas: o prefab não é utilizável apenas como personagem de locomoção.
- `CadencedMovement2D` exige Rigidbody2D **e Animator**, calcula a velocidade pretendida e escreve `LocomotionRate`. Física e apresentação não estão separadas. `IsMoving` usa a velocidade interna pretendida, que pode continuar positiva quando uma parede impede deslocamento.
- Existe dependência circular direta: `PlayerController → HeartSystem → PlayerController`. `HeartSystem` também atualiza imagens de corações, escreve no Animator, controla knockback e pausa global no Start.
- `GameMenuController` localiza PlayerController/HeartSystem, escuta morte e suprime ataques ao retomar. As interfaces precisam de um adaptador de integração futuro; não devem passar a depender da nova base nesta fase.
- SlimeController, SkeletonController e SlimeBoss repetem código de perseguição, vida, reação e animação; há diferenças reais entre eles, portanto a unificação deve seguir testes próprios, não substituição automática por herança.
- Chaves, baús e portões referenciam componentes concretos. EntradaBoss ativa barreiras; AreaFinal carrega cenas diretamente. O portão possui animação e transparência próprias que precisam permanecer funcionais até uma etapa específica do ambiente.
- `Sword_attack`, em SwordAttack.cs, não apresentou referência serializada em cenas/prefabs no levantamento. Isso é candidato a investigação, não autorização para exclusão. Classes estáticas e interfaces também não aparecem como componentes serializados; ausência dessa referência não demonstra que estejam sem uso.

### Sprites e animações

- Atlas atual: `player-hd.png`, 971×1619, 51 recortes explícitos, **77 pixels por unidade**, filtro Point, sem mipmaps e sem compressão. Os recortes não formam uma grade regular; refatiar automaticamente quebraria o mapeamento existente.
- Os clips referenciam sprites diretamente por GUID + fileID; não há carregamento por Resources/Addressables. O material de respiração usa MaterialPropertyBlock e shader próprio. Os dados dos sprites do jogador não contêm rig/bones, e não há arte do personagem separada em PSD/PSB/partes para animação esquelética.
- Existem três vistas desenhadas: frente, perfil direito e costas. A esquerda usa espelhamento. As quatro animações de caminhada têm seis quadros a 10 fps, com ciclo de 0,6 s. Idle conserva uma pose por vista; uma respiração procedural já é a solução visual adotada no projeto.
- Pivôs atuais seguem o corpo e a compatibilidade com o personagem anterior, não um pé comum. Na locomoção, a posição da base visível em relação ao pivô varia aproximadamente **0,013 unidade**, cerca de um pixel a 77 PPU. Os sprites diferem também em largura e altura; isso não prova erro de importação nem justifica trocar o estilo artístico.
- O Animator possui Idle/Movement/Attack/Death; a direção usa blend trees 2D para sprites discretos e entradas diagonais repetidas. Clips à esquerda escrevem flipX; os demais nem sempre o fazem explicitamente. Isso espalha a autoridade sobre o espelhamento entre clips, transições e valores padrão.
- Parâmetros legados: Movimento, AxisX, AxisY, isAlive, LocomotionRate, AttackRate. O controlador reinicia Attack por Play; esse comportamento é intencional para golpes, mas não deve ser herdado pela locomoção nova.
- Não há clip de Hurt do jogador. Morte possui três quadros/0,375 s. Ataques possuem arte existente, mas sua implementação e regras ficam fora desta entrega. Não serão criados estados vazios para simular recursos ausentes.

### Física e mapas

O personagem atual usa Rigidbody2D dinâmico, gravidade zero, interpolação, rotação travada e detecção contínua; há amortecimento linear alto (50) ligado à resposta antiga de combate. O collider cobre boa parte do corpo e os colliders da espada ficam em filhos. A nova base usará um collider pequeno nos pés, sem herdar amortecimento/knockback antigos.

A fase contém TilemapCollider2D/CompositeCollider2D, prefabs com colisores e regras de camada globais. A presença desses componentes não basta para declarar toda colisão incorreta. Gramado, obstáculos, portas e progressão exigem testes por área; não serão reconstruídos agora. Não serão alteradas a matriz de colisão, as configurações globais ou as cenas atuais.

### Constatação adicional em execução

A verificação visual do sandbox confirmou que o pipeline efetivo é **Built-in**, apesar do pacote URP instalado. GraphicsSettings aponta o GUID `681886c5eb7344803b6206f758bf0b1c`, para o qual não foi localizado um asset correspondente. A primeira captura revelou a incompatibilidade de um material específico de URP; o sandbox passou a usar um shader simples compatível com o ambiente atual, e foi acrescentado um teste de renderização que rejeita o rosa de erro. A configuração gráfica global foi preservada e sua correção fica para uma etapa própria com regressão visual completa.

## 3. Alternativas visuais avaliadas antes da implementação

| Alternativa | Benefício | Limitação | Custo relativo |
|---|---|---|---|
| Manter frame a frame e reconstruir controle/alinhamento | Preserva identidade, sprites e infraestrutura; permite validar a causa de cada falha | Mantém pequenas variações de desenho entre quadros | Baixo a moderado; opção escolhida |
| Redesenhar os ciclos frame a frame | Permite corrigir silhueta, passos e continuidade na própria arte | Exige aprovação e produção artística; não resolve acoplamento de código | Alto |
| Migrar para rig 2D | Interpolação de poses e reutilização de movimentos | Requer separar o desenho em partes, rig, pesos e animações; pode deformar pixel art | Alto; sem material preparado no projeto |

A escolha é **manter frame a frame** e o mesmo material de respiração. O alinhamento será feito nos novos clips, num filho visual separado da física, sem repintar, recortar novamente ou modificar as referências originais.

## 4. Arquitetura proposta

Nova ilha de código: `MysticWoods.Gameplay.Player`, com assembly `MysticWoods.Player`. Ela referencia apenas Unity e Input System; não pode referenciar Assembly-CSharp, UI ou scripts legados.

- **PlayerInputReader:** ciclo de vida de ações de movimento/corrida, leitura de comandos e limpeza ao perder foco/desativar.
- **PlayerController:** encaminha comandos ao motor; não escreve no Animator nem aplica física.
- **PlayerMovement:** aceleração/frenagem, Rigidbody2D em FixedUpdate e velocidade física observável; não conhece Animator, vida ou combate.
- **PlayerAnimationController:** converte movimento efetivo em Idle/Walk/direção, controla cadência e respiração visual; não altera regras de gameplay.
- **PlayerConfig (ScriptableObject):** velocidades, suavização, limiares e parâmetros de apresentação editáveis. Cada prefab referencia um asset; nenhuma preferência global é modificada.
- **PlayerFacing / PlayerMotionState:** valores pequenos e explícitos. Não é necessário um framework de FSM para dois estados. Ações exclusivas e prioridade de interrupções serão especificadas junto ao combate em outra fase.

```text
Input System → PlayerInputReader → PlayerController → PlayerMovement → Rigidbody2D
                                                          ↓ velocidade efetiva
                                                PlayerAnimationController → Animator + SpriteRenderer
PlayerConfig ────────────────────────────────────→ motor e apresentação
```

A estrutura prepara composição futura: serviços de atributos/dano/ações poderão enviar comandos ou eventos, sem inserir combate no motor ou leitor de entrada. Nenhum barramento global de eventos ou container de injeção será introduzido sem necessidade concreta.

## 5. Política de animação da primeira entrega

Implementar Idle e Walk nas quatro direções. Shift altera a velocidade e a cadência do ciclo Walk existente; **não há arte de Run dedicada** e não será apresentado um novo ciclo como se existisse. Attack/Hurt/Death não terão regras nem comandos nesta base.

Estados direcionais explícitos evitam blend de sprites discretos. Trocar direção durante Walk mantém a fase do passo. Atualizações sem troca de estado não chamam Play novamente. Os novos clips controlam somente o filho Sprite (sprite e compensação de alinhamento); flipX pertence exclusivamente à apresentação. Física, collider, escala da raiz e hitboxes não são animados.

Idle usa a pose válida existente e respiração sutil já compatível com o material atual. A variação de base é corrigida a partir da silhueta opaca; diferenças de desenho e anatomia internas não serão disfarçadas com sprites de outro conjunto. Hurt não será improvisado com morte ou ataque.

## 6. Estratégia de migração e preservação

1. Registrar diagnóstico e hashes dos recursos existentes.
2. Criar novos componentes, configurações, clips, controller, prefab e sandbox em cópia isolada.
3. Executar testes de comportamento/visualização; revisar capturas e limites da arte.
4. Integrar somente os novos arquivos após validação, preservando .meta existentes, UI, cenas de produção, prefabs antigos, Packages e ProjectSettings.
5. Validar o personagem com o usuário antes de projetar combate e migrar a fase principal.

Os arquivos anteriores não serão renomeados, movidos ou apagados. O uso do personagem novo começa pela cena de sandbox, que não substituirá a cena inicial nem entrará automaticamente na lista de build.
