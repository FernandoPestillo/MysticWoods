# Validação da base do personagem

Unity 6000.6.7f1, Editor macOS, 11/11/2026. Execução em cópia isolada do projeto; os novos arquivos foram integrados apenas após os testes.

## Resultado

- **17/17 testes de Edit Mode passaram.**
- **10/10 testes de Play Mode passaram.**
- Compilação sem novos erros; as assemblies de produção e testes foram compiladas pelo Editor.
- Revisão visual de Idle/Walk nas quatro direções e gravação de 216 quadros em Play Mode, 1280×720, 30 quadros/s. Capturas não apresentaram sprites ausentes nem o rosa de erro de shader.
- Alinhamento da base opaca verificado em todos os quadros usados, incluindo a vista espelhada: desvio inferior a 0,0005 unidade. A raiz física permaneceu imóvel durante a amostragem dos clips.
- 1.202 arquivos originais de Assets, Packages e ProjectSettings preservados byte a byte por SHA-256. A UI, as cenas de produção e seus prefabs não foram reescritos.

A Unity atualizou automaticamente `MysticWoods.slnx` para incluir as novas assemblies na IDE. Esse arquivo gerado fica fora do conjunto de Assets/Packages/ProjectSettings preservado acima e está registrado no manifesto.

## Cobertura executada

### Edit Mode

- PASS — AnimatorContainsEightRealStatesAndOnlyPresentationParameter
- PASS — ConfigGuardsInvalidValuesWithoutChangingAsset
- PASS — DirectionRemainsCardinalAndFollowsMotion(1,0,Down,Right)
- PASS — DirectionRemainsCardinalAndFollowsMotion(-1,0,Down,Left)
- PASS — DirectionRemainsCardinalAndFollowsMotion(0,1,Right,Up)
- PASS — DirectionRemainsCardinalAndFollowsMotion(0,-1,Left,Down)
- PASS — DirectionRemainsCardinalAndFollowsMotion(0,0,Left,Left)
- PASS — DirectionRemainsCardinalAndFollowsMotion(0.02f,0,Down,Right)
- PASS — DirectionRemainsCardinalAndFollowsMotion(-0.02f,0,Up,Left)
- PASS — DirectionRemainsCardinalAndFollowsMotion(1,1,Down,Up)
- PASS — DirectionRemainsCardinalAndFollowsMotion(1,1,Left,Right)
- PASS — DirectionRemainsCardinalAndFollowsMotion(-1,-1,Up,Down)
- PASS — DirectionRemainsCardinalAndFollowsMotion(-1,-1,Right,Left)
- PASS — EveryFrameAlignsItsOpaqueFootprintWithoutTouchingPhysics
- PASS — InputContainsOnlyMovementAndRunWithBothKeyboardLayouts
- PASS — PrefabContainsOnlyFoundationBehavioursAndFootPhysics
- PASS — RuntimeAssemblyHasNoLegacyOrUIReference

### Play Mode

- PASS — ArrowKeysAndRightShiftUseTheSameActions
- PASS — DiagonalAndShiftSpeedsAreNormalizedAndAffectOnlyLocomotionRate
- PASS — FocusLossAndDisableClearMovementWithoutStickingOnReturn
- PASS — OppositeAndVerticalDirectionsNeverLeaveAStaleHorizontalFlip
- PASS — PauseFreezesMotionAndAnimationThenResumes
- PASS — SandboxBootsWithoutLegacySystemsAndIdleDoesNotRestart
- PASS — SandboxRendersWithoutErrorMaterials
- PASS — SolidObstacleStopsPhysicsAndPresentation
- PASS — WalkAdvancesWithoutRestartAndDirectionKeepsItsCycle
- PASS — WalkingAcceleratesAndBrakesAtConfiguredCadence

## Execução reproduzível

As duas assemblies podem ser executadas no Unity Test Runner. Pela CLI, use o executável do Unity 6000.6.7f1 com `-batchmode -projectPath <projeto> -runTests`, `-testPlatform EditMode -assemblyNames MysticWoods.Player.EditModeTests` ou `-testPlatform PlayMode -assemblyNames MysticWoods.Player.PlayModeTests`, além de `-testResults <resultado.xml>` e `-logFile <log>`.

Os testes PlayMode carregam a cena de sandbox pelo Editor, sem acrescentá-la ao build. Eles criam um teclado virtual, preservam/restauram as configurações de entrada e limpam as cenas ao terminar. A pausa é exercitada com Time.timeScale, sem depender dos menus antigos.

## Revisão visual e limites

As capturas vieram de Camera.Render; o painel IMGUI de diagnóstico não aparece nessas imagens, embora esteja disponível ao jogar no Editor. O vídeo mantém os quadros renderizados do personagem em execução, com aproximação de câmera para observar as poses. A vista geral registra a escala normal do laboratório.

O pipeline efetivo na validação foi Built-in. O material de piso inicialmente incompatível foi corrigido apenas no sandbox e passou a ser coberto por SandboxRendersWithoutErrorMaterials. Nenhuma configuração gráfica global foi alterada.

Não foi realizado build Web/desktop, teste de navegação em browser, controle/móvel ou aprovação artística humana prolongada. Não houve migração da fase principal, teste de novo combate, dano, loot ou economia, pois esses sistemas não fazem parte desta entrega. Os ciclos reutilizam a arte existente: a correção de alinhamento não elimina diferenças anatômicas internas do desenho.

As falhas encontradas durante a construção dos testes (referências de teste duplicadas, uso de Count nas bindings e duração de clip) foram corrigidas antes do resultado final acima. As referências e os recursos anteriores foram preservados. A migração de gameplay e a resolução da referência gráfica pendente permanecem documentadas para fases futuras.
