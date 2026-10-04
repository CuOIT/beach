# Chess 3D

A complete single-player chess game for Unity 6 (URP), built for mobile: full FIDE rules, a
searching computer opponent, procedurally generated 3D pieces, and the screens a shipped mobile
game needs — menu, settings, pause, promotion, move list, game over and retry.

Open `Assets/ChessGame/Scenes/Chess.unity` and press Play.

## Layout

```
Assets/ChessGame/
  Scripts/
    Core/      rules engine - board, move generation, FEN, SAN, draw detection
    AI/        evaluation, transposition table, alpha-beta search, threading
    Game/      board and piece views, camera, input, audio, GameManager
    UI/        every screen, built from code with uGUI + TextMeshPro
    Visual/    procedural meshes, materials and colour themes
  Editor/      project setup (scene generation, mobile player settings)
  Tests/       EditMode: rules and search
  PlayTests/   PlayMode: the real scene, end to end
  Scenes/      Chess.unity
```

Nothing is authored as an asset: the board, the pieces, the materials, the sprites, the sound
effects and the entire interface are generated at runtime from code. The scene holds a camera,
two lights and one `GameManager`.

## Rules

`Chess.Core` implements the full rule set — castling with all of its conditions, en passant
including the pinned-capture case, under-promotion, checkmate, stalemate, the fifty-move rule,
threefold repetition and insufficient material.

Correctness is established by **perft**: counting the leaf nodes reachable at a given depth from
standard test positions and comparing against published values. A match at these depths is very
hard to achieve unless every rule is exact.

| Position | Depth | Nodes |
|---|---|---|
| Start position | 4 | 197,281 |
| Kiwipete | 3 | 97,862 |
| Rook endgame | 5 | 674,624 |
| Promotion position | 4 | 422,333 |
| Talkchess | 3 | 62,379 |
| Steady middlegame | 3 | 89,890 |

## The opponent

Negamax with alpha-beta, iterative deepening, a Zobrist transposition table, killer and history
move ordering, check extensions, mate-distance pruning, and a quiescence search that resolves
captures before evaluating. The evaluation is material plus piece-square tables, tapered between
middlegame and endgame, with pawn structure, the bishop pair and rook placement on top.

The search runs on a worker thread against its own copy of the position, so the game never
stalls a frame. Difficulty maps to depth, a time budget and how much random noise is added to
root move scores:

| Level | Depth | Budget | Noise |
|---|---|---|---|
| Easy | 2 | 400 ms | 90 cp |
| Medium | 4 | 1000 ms | 35 cp |
| Hard | 6 | 2000 ms | – |
| Expert | 12 | 4000 ms | – |

## Presentation

Pieces are surfaces of revolution with distinguishing parts welded on — a ball for the pawn,
battlements for the rook, a blocky head for the knight, a crown for the queen, a cross for the
king. Each is combined into a single mesh, so a piece costs one draw call; the 64 squares are
merged into two.

The camera solves its own distance by bisection against the projected board corners, using the
measured height of the HUD bars as its safe area, so the board is framed correctly on a tall
phone screen and in landscape. Playing Black turns the camera rather than the board, which keeps
every coordinate in one frame of reference.

Sound effects are synthesised at startup from tones and noise bursts shaped by an envelope.

## Settings

Difficulty, colour (white / black / random), board theme, sound on/off, volume, legal-move
markers, last-move highlight and animation speed. All persisted through `PlayerPrefs`.

## Tests

```bash
unity test . --mode EditMode
```

```bash
unity test . --mode PlayMode
```

EditMode covers the rules and the search. PlayMode loads the real scene and plays through it:
the opponent answers, undo retracts both plies, promotion swaps the model, changing theme
preserves the position, and leaving to the menu cancels a running search. It also renders frames
off-screen into `Screenshots/` and fails if one comes back blank — which is what catches a
missing shader or an empty procedural mesh.

## Regenerating the project

The menu items under **Chess** re-run setup: importing the TextMeshPro essentials, writing the
mobile player settings, and rebuilding `Chess.unity` from scratch.

Building the scene headlessly:

```bash
unity run . -- -executeMethod Chess.EditorTools.ChessProjectSetup.BatchBuildScene
```

## Shader stripping - read this before building

Every material here is created in code through `Shader.Find`. Nothing in the project *references*
those shaders as an asset, so Unity's build-time dependency scan does not see them and strips them
from the player. `Shader.Find` then returns null in a build while continuing to work in the editor:
the board comes up magenta, `new Material(null)` throws out of `GameManager.Awake`, and the pieces
and the interface never get built at all.

No editor test can catch this, because the editor always has every shader loaded. Two things guard
against it:

1. `Universal Render Pipeline/Lit` and `Universal Render Pipeline/Unlit` are listed under
   **Project Settings > Graphics > Always Included Shaders**. The menu item
   **Chess > Include Runtime Shaders In Builds** re-applies that, and `SetupAll` runs it.
2. `MaterialLibrary` degrades instead of throwing, and logs which shader is missing.

After any build, check the player log for `ArgumentNullException ... Parameter name: shader`:

```
C:\Users\<user>\AppData\LocalLow\Bravestars\Chess 3D\Player.log
```

## Notes

- The project's active input handling is set to **Both**. The game reads the legacy `Input` API
  for board taps, which behaves identically for mouse and touch.
- Player settings target Android and iOS with portrait plus both landscape orientations; the
  camera reframes for each.
