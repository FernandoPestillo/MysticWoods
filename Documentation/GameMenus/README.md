# Pause, Game Over e Final

As três interfaces reutilizam a arte, as fontes e os estilos de botões da tela inicial. Os prefabs são editáveis no Inspector e no Prefab Mode. A referência é 1920×1080, com CanvasScaler em Scale With Screen Size e Match de 0,5.

| Tela | Apresentação | Ações |
| --- | --- | --- |
| Pause | Partida congelada e escurecida; JOGO PAUSADO | Retomar, Configurações, Comandos, Menu Principal |
| Game Over | Partida congelada depois da animação de morte; FIM DE JOGO | Tentar novamente, Menu Principal |
| Final | Floresta e logo da tela inicial; VITÓRIA! e agradecimento | Jogar novamente, Menu Principal |

A primeira ação recebe o destaque dourado. Os botões aceitam mouse, setas e Enter, com foco visível, losangos e feedback de hover/pressionamento. Fades e feedback usam tempo não escalado. Esc fecha o dropdown ou confirmação de vídeo antes do formulário, volta de Configurações/Comandos ao Pause e retoma a partida quando o Pause está no nível principal. Esc não dispensa Game Over ou Final.

## Assets e edição

- `Assets/UI/GameMenus/GameMenus.prefab`: overlay da fase, Pause, Game Over, os painéis compartilhados e adaptadores legados.
- `Assets/UI/GameMenus/VictoryMenu.prefab`: interface da cena TelaFinal.
- `Assets/UI/Shared/SettingsPanel.prefab`: formulário e confirmação de vídeo, com todas as referências locais ao prefab.
- `Assets/UI/Shared/CommandsPanel.prefab`: controles, incluindo um golpe por toque, com botão Voltar independente do proprietário.
- `Assets/UI/Shared/MenuCard.prefab`, `PrimaryButton.prefab` e `SecondaryButton.prefab`: moldura, cantos ancorados e os estilos dos botões.
- Os prefabs de fundo, sombra e logo em Shared referenciam os PNGs existentes. As fontes continuam em UI/MainMenu.

O menu inicial agora usa instâncias dos mesmos painéis de Configurações e Comandos. As ações e os estilos anteriores foram preservados. Edite os prefabs compartilhados para atualizar ambos os menus; a arte PNG não foi alterada.

## Fluxos da partida

`GameMenuController` mantém estados exclusivos de jogo, pausa, configurações, comandos, morte, Game Over e transição. O mundo é congelado com Time.timeScale = 0 nas interfaces da fase. Retomar não cancela um golpe em andamento, descarta a fila pendente e impede que teclas/botões de ataque ainda pressionados na interface iniciem um golpe involuntário. Um novo toque funciona normalmente após soltar os botões.

`HeartSystem.Died` é emitido uma vez na passagem de vida positiva para zero. O combate e a seleção física do jogador são desativados imediatamente. O controlador fecha os painéis e cancela ajustes não confirmados, deixa a animação de morte rodar por sua duração de aproximadamente 0,375 s e só depois congela o mundo e abre Game Over. Esc é ignorado durante essa sequência. Sem um controlador conectado ao evento, a referência legada MenuRestart mantém seu comportamento anterior.

Tentar novamente e Jogar novamente recarregam FaseMysticWoods, começando com seis corações e estado novo de inimigos, chave e barreiras. Menu Principal abre TelaInicial. Todas as rotas restauram o tempo normal antes de carregar a cena e protegem contra acionamentos duplicados. PauseMenu, RestartMenu e MenuFinalManager mantêm os métodos públicos anteriores como adaptadores.

## Configurações e preferências

O mesmo MenuSettingsController atende o menu inicial e o Pause: volume em pré-visualização, Aplicar/Cancelar, resolução, modo de janela e confirmação de vídeo por 15 segundos reais. Cancelar ou morrer reverte alterações não confirmadas. Os valores confirmados persistem nas chaves PlayerPrefs já usadas pela tela inicial, inclusive ao reiniciar ou retornar ao menu.

## Validação

[validation.txt](validation.txt) registra 783 verificações das interfaces e layouts, 186 regressões do combate e 87 regressões dos inimigos. Inclui mouse e teclado, foco, Esc, aplicação/cancelamento, timeout real com o mundo pausado, pausa durante golpes, prevenção de ataques involuntários ao retomar, animação de morte, evento único, reinício, chefe → Final e ambos os retornos da vitória.

As capturas de referência em `Captures/` foram renderizadas e revisadas. Foram verificados nove estados de interface nas quatro resoluções documentadas. O mapa e suas colisões foram preservados.

Para repetir, copie Assets, Packages e ProjectSettings para uma pasta descartável, copie GameMenusValidation.cs para Assets/Editor dessa cópia e crie um arquivo vazio GameMenusValidationClone.marker na raiz. Execute Unity 6000.6.7f1 com `-batchmode -projectPath <copia> -executeMethod GameMenusValidation.Run -logFile <log>`. O processo grava game-menus-validation.txt e as capturas, restaura as preferências e termina com 0 em sucesso ou 1 em falha. O harness altera cenas e dispositivos de input e deve ser usado somente na cópia isolada.
