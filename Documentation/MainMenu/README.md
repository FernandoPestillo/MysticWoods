# Tela inicial — Mystic Woods

## Abrir e editar

Abra `Assets/Scenes/TelaInicial.unity` na Unity 6000.6.7f1. A cena referencia o prefab editável `Assets/UI/MainMenu/MainMenu.prefab`. Edite o prefab pelo Inspector ou em Prefab Mode.

O prefab contém o fundo, o letreiro transparente, o menu principal, o painel de comandos, o formulário de configurações e a confirmação de vídeo. As imagens estão em `Assets/UI/MainMenu/Art/`; os textos restantes usam fontes TextMesh Pro próprias desta interface.

As duas fontes da interface possuem 123 caracteres pré-renderizados, incluindo acentos em português. Para ampliar o conjunto de caracteres, use o Font Asset Creator do TextMesh Pro preservando as referências aos assets.

Os scripts ficam em `Assets/UI/MainMenu/Scripts/`. `MenuPrincipalManager`, em `Assets/Scripts/`, mantém as ações públicas do menu anterior e adiciona a navegação para Configurações.

## Navegação

- Mouse ou setas para navegar; Enter para selecionar.
- Jogar abre a fase existente e preserva a escolha de dificuldade.
- Comandos mostra os controles atuais. Voltar e Esc retornam ao menu e restauram a seleção.
- Configurações oferece volume geral, resoluções disponíveis e Janela/Tela cheia sem bordas. Esc fecha a lista de opções antes de fechar o painel.
- Sair encerra a aplicação no build e interrompe Play Mode no Editor.

## Configurações

O volume começa em 100%, é pré-visualizado durante o ajuste e volta ao valor confirmado ao cancelar. Aplicar salva o volume e, quando necessário, abre a confirmação de vídeo.

Manter confirma e salva a configuração de vídeo. Reverter volta ao formulário com os valores anteriores. Sem confirmação, a reversão acontece após 15 segundos de tempo real, inclusive com `Time.timeScale = 0`. Fechar a interface sem confirmar também restaura os valores anteriores.

Somente valores confirmados são persistidos, nas chaves PlayerPrefs `MysticWoods.Menu.MasterVolume`, `MysticWoods.Menu.Width`, `MysticWoods.Menu.Height` e `MysticWoods.Menu.WindowMode`. As resoluções são deduplicadas e incluem o tamanho atual da janela.

No Editor, a simulação visual de resolução é controlada pelo seletor do Game View. A resolução e o modo de janela são aplicados pela Unity à janela do jogador no build. Esta etapa não adiciona trilha sonora.

## Cores no Game View

A saída do monitor usa SDR: `Allow HDR Display Output` e `Use HDR Display Output` ficam desativados em Player Settings. O pipeline ativo é Built-in, que não suporta saída HDR. Com essas opções ligadas, o Game View estava ativando HDR10 no macOS e distorcendo a paleta da interface. O espaço de cores Linear e as cores dos assets foram preservados. Reinicie o Editor após alterar essas opções.

## Validação

A validação usa a Unity gráfica e dispositivos virtuais de entrada em uma cópia temporária. As preferências existentes são restauradas depois dos testes.

- Layouts de 1280×720, 1920×1080, 2560×1080 e 1920×1200, com elementos dentro da tela e botões sem sobreposição.
- Textos visíveis sem overflow, foco inicial, mouse, navegação por teclado e retorno do foco.
- Volume em pré-visualização, cancelamento, aplicação e recarga das preferências.
- Listas de resolução sem duplicações e fechamento de dropdown por Esc.
- Confirmação, reversão explícita e timeout real de 15 segundos com o jogo pausado.
- Início da partida, escolha de dificuldade, retorno ao menu e Sair no Editor.

As capturas finais ficam em `Captures/`. Os prompts e o método de geração da arte estão em [Artwork.md](Artwork.md).

Personagem, inimigos e mapa permanecem para as próximas etapas.
