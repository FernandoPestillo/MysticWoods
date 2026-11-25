# MysticWoods

MysticWoods e um jogo 2D de aventura desenvolvido na Unity. O projeto usa uma estrutura de cena e scripts inspirada em jogos classicos de exploracao, combate top-down, coleta de chaves, menus e encontro com chefe.

## Estrutura

Este repositorio agora esta organizado como a raiz direta do projeto Unity. Abra esta pasta no Unity Hub, sem entrar em uma subpasta:

```text
MysticWoods/
├── Assets/
├── Packages/
├── ProjectSettings/
├── README.md
└── .gitignore
```

## Requisitos

- Unity 6000.6.7f1
- Universal Render Pipeline 17.6.0 (Renderer 2D, ativo desde a reestruturação)
- Input System 1.20.1
- Cinemachine 6.6.0
- TextMesh Pro incluido no Unity UI (UGUI) 2.6.0

As dependencias do projeto ficam em `Packages/manifest.json` e serao restauradas automaticamente pela Unity ao abrir o projeto.

## Como abrir

1. Abra o Unity Hub.
2. Selecione `Add project from disk`.
3. Escolha a pasta raiz deste repositorio: `MysticWoods`.
4. Abra com a Unity `6000.6.7f1`.

## Cenas

As cenas configuradas para build ficam em `Assets/Scenes`:

- `TelaInicial.unity`
- `FaseMysticWoods.unity`
- `TelaFinal.unity`

## Controles

- Movimento: `WASD` ou setas
- Correr: `Shift`
- Atacar (combo de três golpes): `Space`, `J` ou clique esquerdo do mouse
- Esquivar (invulnerável durante o rolamento): `K`, `Ctrl` esquerdo ou clique direito do mouse
- Controle (gamepad): analógico esquerdo, gatilho esquerdo para correr, botão oeste ataca, botão leste esquiva

O projeto usa apenas Input System Package (New). Para jogar no editor, abra somente `Assets/Scenes/TelaInicial.unity`, pressione Play e escolha Jogar. A partida começa imediatamente com seis corações, usando as configurações do antigo modo normal. Não há seleção de dificuldade.

## Desenvolvimento

O jogo usa três montagens próprias em `Assets/Gameplay`: `MysticWoods.Player` (personagem), `MysticWoods.Combat` (vida, dano e feedback) e `MysticWoods.Enemies` (IA). `Assets/Scripts` mantém menus, chaves, portões e ambiente.

Antes de commitar alteracoes, confirme que a Unity gerou/atualizou os arquivos `.meta` correspondentes aos assets modificados.

## Code Assist no macOS

A integracao opcional Code Assist fica desativada no Editor do macOS pela restricao `!UNITY_EDITOR_OSX` em `Assets/Plugins/CodeAssist/Editor/CodeAssistAssembly.asmdef`. A versao incluida do plugin depende de Visual Studio 2022 e seu encerramento do NetMQ pode bloquear o recarregamento de assemblies ao entrar em Play Mode no macOS.

Essa restricao afeta apenas a integracao do editor com a IDE. Os scripts do jogo continuam habilitados, e o plugin permanece disponivel nos outros sistemas. Depois de aplicar a alteracao, reabra a Unity para descarregar a versao do plugin que ja estava carregada.

## Nova tela inicial

A cena `Assets/Scenes/TelaInicial.unity` utiliza o prefab editável `Assets/UI/MainMenu/MainMenu.prefab`, com letreiro exclusivo, floresta em pixel art e os painéis Comandos e Configurações. Consulte [o guia da interface](Documentation/MainMenu/README.md) para edição, comportamento das configurações e capturas de validação.

## Personagem e inimigos

A adaptação para High-Detail Pixel Art, os atlas, a integração e as capturas estão em [Documentation/Characters](Documentation/Characters/README.md).

## Mapa High-Detail Pixel Art

A floresta reconstruída, os assets, os prefabs editáveis, as capturas e os testes estão em [Documentation/Map](Documentation/Map/README.md).

## Reestruturação gráfica, de combate e da IA

Pipeline URP 2D com iluminação e pós-processamento, combate com combo, esquiva e feedback de impacto, e IA com percepção, coordenação de ataques e chefe em fases. Consulte [Documentation/Restructure](Documentation/Restructure/README.md).
