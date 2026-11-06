# Cadência de movimento e combate

O jogador anda a 3,5 u/s e corre a 5,25 u/s. Slime, esqueleto e chefe se movem a 2,0, 2,4 e 1,7 u/s. `CadencedMovement2D` suaviza o movimento em FixedUpdate, preserva impulsos de knockback e controla somente o multiplicador `LocomotionRate` do estado de caminhada. A aceleração/frenagem usa 0,12/0,08 s no jogador e 0,18/0,10 s nos inimigos; os Rigidbody2D usam interpolação.

## Golpes

Cada toque de espaço ou clique esquerdo executa um ciclo completo de 0,50 s. A janela de dano começa em 0,10 s e termina em 0,30 s; os últimos 0,15 s aceitam um próximo comando. Há somente uma posição na fila. Soltar não cancela e segurar não repete. Comandos simultâneos contam como um toque. Durante o golpe, o movimento fica limitado a 35% da caminhada, inclusive ao segurar Shift.

A direção cardinal fica fixa até o fim do golpe. Um próximo golpe pode escolher outra direção. Diagonais usam o eixo dominante; em empate, conservam a direção anterior. A direção inicial é para baixo.

`PlayerController` é a única autoridade do ciclo e da abertura das hitboxes. `Movimento` preserva os valores 0/1/2 para repouso/caminhada/ataque; `AttackRate` sincroniza os clips com a duração configurada. A máquina de estados passa diretamente entre os três estados e prioriza morte. Não há dependência de eventos de animação ou de soltar o botão para destravar.

As quatro hitboxes são triggers simétricos. O alcance é 1,30 e a largura 1,35, em unidades locais do jogador. O centro corporal é (0; 0,1), e cada caixa se estende desse centro para a direção do golpe. `SwordHitbox` consulta a área ao abrir e em cada passo físico ativo, resolve `IDamageable` nos ancestrais e registra cada alvo uma vez por golpe. Colliders de detecção, o jogador, alvos mortos e não selecionáveis são excluídos. A espada não causa colisão física. O dano permanece 1 e a força de knockback é 600, com origem no jogador.

O relógio do golpe usa o tempo físico escalado. A pausa congela o golpe e descarta comandos pendentes e comandos recebidos enquanto pausado. Morte e desativação fecham todas as hitboxes e limpam a fila. O movimento dos inimigos cede temporariamente aos impulsos de dano; mortos não perseguem. Ressurreição do esqueleto e abertura da barreira do chefe foram preservadas.

## Ajustar no Inspector

- `PlayerController`: velocidades, duração do ataque, início/fim da janela ativa, fila, fator de movimento, alcance, largura e centro corporal.
- `CadencedMovement2D`: aceleração e frenagem.
- Controladores de inimigos: velocidade de perseguição nos campos públicos existentes.
- `SwordHitbox`: dano e força. Selecione um dos quatro objetos filhos para ver a caixa no Scene View.

Os valores foram aplicados nos quatro prefabs e no controlador próprio do chefe da cena. O prefab do chefe agora usa `SlimeBoss`; a instância existente mantém sua referência à barreira final. A arte PNG, o IDLE suave e as colisões da grama permanecem preservados.

## Validação reproduzível

[validation.txt](validation.txt) contém os resultados das verificações físicas e de input. Foram executados 30 golpes pelo teclado e 30 pelo mouse, além dos testes de fila, sobreposição, deduplicação, direções, pausa, knockback, morte, reativação, reinício, chave, portão e barreira do chefe. As quatro direções de ataque foram renderizadas e revisadas.

`CombatValidation.cs` é o harness usado na cópia isolada; permanece fora de Assets para não entrar no jogo. Para repetir:

1. Copie Assets, Packages e ProjectSettings para uma pasta de validação descartável. O plugin CodeAssist pode ser omitido.
2. Copie este harness para Assets/Editor/CombatValidation.cs dessa cópia e crie um arquivo vazio CombatValidationClone.marker na raiz da cópia.
3. Execute Unity 6000.6.7f1 com `-batchmode -projectPath <copia> -executeMethod CombatValidation.Run -logFile <log>`.
4. Confira RESULT PASS em combat-validation.txt na raiz da cópia. O processo termina com código 0 em sucesso e 1 em falha; as prévias combat-down/up/right/left.png ficam na mesma pasta.

Use uma cópia isolada: o harness abre cenas, injeta dispositivos de input, altera configurações de teste e encerra o processo do Editor ao concluir.

## Preparação e intervalos dos inimigos

O dano ao encostar foi substituído por `EnemyMeleeAttack`, compartilhado pelos slimes, esqueletos e chefe. Cada golpe tem preparação, janela ativa, recuperação e cooldown. O inimigo para e fixa a direção antes de atacar. Sair da área marcada ou passar para trás evita o golpe. Acertar o inimigo interrompe a preparação; morte e desativação removem imediatamente o aviso e o dano pendente. O esqueleto volta a preparar um golpe completo após ressuscitar.

| Inimigo | Preparação | Janela ativa | Recuperação | Cooldown após recuperação | Intervalo medido entre acertos |
| --- | ---: | ---: | ---: | ---: | ---: |
| Slime | 0,50 s | 0,16 s | 0,40 s | 0,65 s | 1,72 s |
| Esqueleto | 0,60 s | 0,18 s | 0,50 s | 0,75 s | 2,04 s |
| Chefe | 0,80 s | 0,22 s | 0,65 s | 1,00 s | 2,68 s |

O aviso amarelo mostra a área e a direção do próximo golpe; fica vermelho na janela ativa. O esqueleto prepara a espada com seu clip de ataque existente. Os slimes comprimem o corpo visualmente antes do golpe, preservando Transform, colliders e a arte PNG. A deformação/tintura usa MaterialPropertyBlock e não altera a respiração do jogador. O setor é uma malha sem collider, renderizada acima do chão e abaixo dos personagens.

O alcance além do corpo é de 0,45/0,60/0,80 unidade, com arcos de 100/110/120 graus para slime/esqueleto/chefe. As distâncias usam o collider no mundo, inclusive na escala maior do chefe. Cada golpe tenta causar dano apenas uma vez na janela ativa e não acompanha a mudança de direção do jogador.

### Knockback curto

O impulso recebido de inimigos comuns caiu para 8; o chefe usa 12, incluindo a instância própria da cena. O recuo físico medido foi de aproximadamente 0,16 e 0,24 unidade, respectivamente. O impulso reverso que arremessava o próprio inimigo também foi removido com o dano por contato.

`HeartSystem` limita impulsos recebidos a 12, zera a velocidade anterior antes de aplicar o recuo e devolve o movimento após 0,18 s. Uma proteção de 0,35 s impede que golpes simultâneos somem dano e impulsos. O dano de cada golpe continua 1. Os limites e tempos podem ser ajustados em Damage Response no Inspector do jogador; preparação, alcance e cooldown ficam em EnemyMeleeAttack nos três prefabs.

### Verificações desta etapa

[enemy-attacks-validation.txt](enemy-attacks-validation.txt) registra 87 verificações novas e a repetição das 186 verificações do combate anterior. Inclui contato sem dano, tempo de preparação, um acerto por golpe, cooldown, esquiva para fora/para trás, interrupção por dano, morte, ressurreição, pausa, desativação, recuo físico limitado e golpes simultâneos. A regressão preservou os 30 ataques por teclado e 30 por mouse, a chave, o portão e a barreira do chefe.

Para repetir os testes novos, use `EnemyAttackValidation.cs` no mesmo procedimento de cópia isolada acima, com `-executeMethod EnemyAttackValidation.Run`. O resultado é salvo em enemy-attack-validation.txt. O harness requer o arquivo CombatValidationClone.marker e permanece fora de Assets no projeto principal.
