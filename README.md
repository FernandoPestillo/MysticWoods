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

- Unity 2022.3.20f1
- Universal Render Pipeline 14.0.10
- Input System 1.7.0
- Cinemachine 2.9.7
- TextMesh Pro 3.0.6

As dependencias do projeto ficam em `Packages/manifest.json` e serao restauradas automaticamente pela Unity ao abrir o projeto.

## Como abrir

1. Abra o Unity Hub.
2. Selecione `Add project from disk`.
3. Escolha a pasta raiz deste repositorio: `MysticWoods`.
4. Abra com a Unity `2022.3.20f1`.

## Cenas

As cenas configuradas para build ficam em `Assets/Scenes`:

- `TelaInicial.unity`
- `FaseMysticWoods.unity`
- `TelaFinal.unity`

## Controles

- Movimento: `WASD` ou setas
- Correr: `Left Shift`
- Atacar: `Space` ou clique esquerdo do mouse

## Desenvolvimento

Scripts principais ficam em `Assets/Scripts`. O projeto inclui controles de player, combate, inimigos, chefe, menus, sistema de vida, chaves e portas.

Antes de commitar alteracoes, confirme que a Unity gerou/atualizou os arquivos `.meta` correspondentes aos assets modificados.
