using System.Collections.Generic;
using System.Text;
using Chess.AI;
using Chess.Core;
using Chess.UI;
using Chess.Visual;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Chess.Game
{
    public enum GamePhase
    {
        Menu,
        PlayerTurn,
        AiTurn,
        AwaitingPromotion,
        Paused,
        Finished
    }

    /// <summary>
    /// Drives the whole game: owns the position, routes taps into moves, runs the opponent and
    /// keeps the board, camera, audio and interface in step with each other.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        /// <summary>The opponent never answers faster than this, which reads as deliberate rather than instant.</summary>
        private const float MinimumThinkSeconds = 0.45f;

        private readonly Board _board = new Board();
        private readonly MoveGenerator _generator = new MoveGenerator();
        private readonly List<Move> _legalMoves = new List<Move>(64);
        private readonly List<Move> _movesFromSelected = new List<Move>(28);
        private readonly StringBuilder _moveList = new StringBuilder(1024);

        private ChessAI _ai;
        private BoardView _boardView;
        private PiecesView _piecesView;
        private CameraRig _cameraRig;
        private AudioManager _audio;
        private UIManager _ui;
        private Light _keyLight;

        private PieceColor _humanColor = PieceColor.White;
        private GamePhase _phase = GamePhase.Menu;
        private GameResult _result = GameResult.InProgress;

        private int _selectedSquare = Squares.None;
        private Move _pendingPromotion;
        private float _aiEarliestPlayTime;
        private bool _aiSearchRunning;
        private int _aiSeed;
        private int _appliedThemeIndex = -1;

        private void Awake()
        {
            GameSettings.Load();
            Application.targetFrameRate = 60;

            BuildScene();
            HookUpInterface();

            ApplyTheme();
            _ui.Build();
            _ui.RefreshMenuFooter();

            _boardView.BuildCoordinateLabels(UIFactory.ResolveFont());

            // Show the opening position behind the menu rather than an empty board.
            Fen.Load(_board, Board.StartFen);
            RefreshLegalMoves();
            _piecesView.Rebuild(_board);
            _cameraRig.SetSide(PieceColor.White, true);
        }

        private void BuildScene()
        {
            _boardView = new GameObject("Board").AddComponent<BoardView>();
            _boardView.transform.SetParent(transform, false);

            _piecesView = new GameObject("PieceRoot").AddComponent<PiecesView>();
            _piecesView.transform.SetParent(transform, false);

            _audio = gameObject.AddComponent<AudioManager>();
            _audio.Build();

            _ui = gameObject.AddComponent<UIManager>();

            _ai = new ChessAI();

            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.fieldOfView = 45f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 200f;

            _cameraRig = camera.gameObject.GetComponent<CameraRig>();
            if (_cameraRig == null) _cameraRig = camera.gameObject.AddComponent<CameraRig>();
            _cameraRig.Configure(camera);

            _keyLight = FindFirstObjectByType<Light>();
            if (_keyLight == null || _keyLight.type != LightType.Directional)
            {
                var lightObject = new GameObject("Key Light");
                _keyLight = lightObject.AddComponent<Light>();
                _keyLight.type = LightType.Directional;
            }

            _keyLight.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            _keyLight.intensity = 1.15f;
            _keyLight.shadows = LightShadows.Soft;
            _keyLight.color = new Color(1f, 0.97f, 0.92f);
        }

        private void HookUpInterface()
        {
            _ui.PlayRequested += StartNewGame;
            _ui.SettingsOpened += () => _ui.OpenSettings(_phase != GamePhase.Menu);
            _ui.SettingsClosed += OnSettingsClosed;
            _ui.QuitRequested += QuitGame;
            _ui.PauseRequested += PauseGame;
            _ui.ResumeRequested += ResumeGame;
            _ui.RestartRequested += StartNewGame;
            _ui.MainMenuRequested += ReturnToMenu;
            _ui.UndoRequested += UndoMove;
            _ui.PromotionChosen += CompletePromotion;
            _ui.SettingsApplied += OnSettingsApplied;
        }

        /// <summary>
        /// Only a theme change is worth rebuilding geometry for. Dragging the volume slider fires
        /// this every frame, so everything else takes the cheap path.
        /// </summary>
        private void OnSettingsApplied()
        {
            if (_appliedThemeIndex != GameSettings.ThemeIndex) ApplyTheme();
            else RefreshHighlights();
        }

        private void ApplyTheme()
        {
            _appliedThemeIndex = GameSettings.ThemeIndex;
            BoardTheme theme = GameSettings.Theme;

            _boardView.Build(theme);
            _piecesView.ApplyTheme(theme);
            _piecesView.Rebuild(_board);
            _boardView.BuildCoordinateLabels(UIFactory.ResolveFont());

            Camera camera = _cameraRig.Camera;
            if (camera != null) camera.backgroundColor = theme.Table;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = theme.AmbientSky;
            RenderSettings.ambientEquatorColor = Color.Lerp(theme.AmbientSky, theme.AmbientGround, 0.5f);
            RenderSettings.ambientGroundColor = theme.AmbientGround;

            RefreshHighlights();
        }

        // ------------------------------------------------------- Observable state

        public GamePhase Phase => _phase;

        /// <summary>Re-applies the stored settings, for callers outside the settings screen.</summary>
        public void ApplyCurrentSettings() => OnSettingsApplied();

        public GameResult Result => _result;
        public PieceColor HumanColor => _humanColor;
        public int PlyCount => _board.PlyCount;
        public string CurrentFen => Fen.Save(_board);

        /// <summary>
        /// Plays a move on the player's behalf, exactly as tapping the two squares would.
        /// Returns false when it is not the player's turn or the move is not legal here.
        /// </summary>
        public bool TryPlayPlayerMove(int from, int to, PieceType promotion = PieceType.Queen)
        {
            if (_phase != GamePhase.PlayerTurn) return false;

            for (int i = 0; i < _legalMoves.Count; i++)
            {
                Move candidate = _legalMoves[i];
                if (candidate.From != from || candidate.To != to) continue;
                if (candidate.IsPromotion && candidate.Promotion != promotion) continue;

                _selectedSquare = from;
                _movesFromSelected.Clear();
                _movesFromSelected.Add(candidate);

                PlayMove(candidate);
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------ Game flow

        public void StartNewGame() => StartNewGame(Board.StartFen);

        /// <summary>Starts a game from any legal position, which also covers puzzles and tests.</summary>
        public void StartNewGame(string fen)
        {
            _ai.Cancel();
            _aiSearchRunning = false;
            _ai.ClearMemory();

            _humanColor = GameSettings.ResolveHumanColor();
            _aiSeed = Random.Range(1, int.MaxValue);

            if (!Fen.Load(_board, fen)) Fen.Load(_board, Board.StartFen);
            _moveList.Length = 0;
            _result = GameResult.InProgress;
            _selectedSquare = Squares.None;
            _movesFromSelected.Clear();

            RefreshLegalMoves();
            _piecesView.Rebuild(_board);
            _cameraRig.SetSide(_humanColor, false);

            _ui.ShowHud();
            _ui.SetMoveList(_moveList);
            _ui.SetPlayers(
                "You  -  " + (_humanColor == PieceColor.White ? "White" : "Black"),
                "Computer  -  " + ChessAI.DisplayName(GameSettings.Difficulty));

            RefreshHighlights();
            UpdateCaptureReadout();

            _phase = _board.SideToMove == _humanColor ? GamePhase.PlayerTurn : GamePhase.AiTurn;
            UpdateStatus();

            if (_phase == GamePhase.AiTurn) BeginAiTurn();
        }

        public void ReturnToMenu()
        {
            _ai.Cancel();
            _aiSearchRunning = false;
            _phase = GamePhase.Menu;
            _selectedSquare = Squares.None;
            _movesFromSelected.Clear();

            Fen.Load(_board, Board.StartFen);
            RefreshLegalMoves();
            _piecesView.Rebuild(_board);
            _cameraRig.SetSide(PieceColor.White, false);

            RefreshHighlights();
            _ui.ShowMainMenu();
        }

        private void PauseGame()
        {
            if (_phase == GamePhase.Menu || _phase == GamePhase.Finished) return;
            _ui.ShowPause();
        }

        private void ResumeGame()
        {
            _ui.HidePause();
        }

        private void OnSettingsClosed()
        {
            // Coming from the pause menu, settings sit on top of it; returning just reveals it again.
            if (!_ui.SettingsCameFromGame) _ui.RefreshMenuFooter();
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // --------------------------------------------------------------- Input

        private void Update()
        {
            SyncCameraMargins();

            if (_aiSearchRunning) PollAi();

            if (_phase != GamePhase.PlayerTurn) return;
            if (_ui.IsModalOpen) return;
            if (_piecesView.IsAnimating) return;

            if (TryReadPointerDown(out Vector2 screenPoint))
                HandleBoardTap(screenPoint);
        }

        /// <summary>
        /// Keeps the camera's safe area matched to the real HUD bars. Their height in pixels
        /// changes with resolution and orientation, and a fixed guess left the back rank hidden
        /// behind the button bar in landscape.
        /// </summary>
        private void SyncCameraMargins()
        {
            if (_ui == null || _cameraRig == null) return;

            float top = _phase == GamePhase.Menu ? 0.08f : _ui.TopHudFraction;
            float bottom = _phase == GamePhase.Menu ? 0.08f : _ui.BottomHudFraction;

            if (Mathf.Abs(_cameraRig.TopMargin - top) < 0.004f &&
                Mathf.Abs(_cameraRig.BottomMargin - bottom) < 0.004f)
                return;

            _cameraRig.TopMargin = top;
            _cameraRig.BottomMargin = bottom;
            _cameraRig.Refresh();
        }

        /// <summary>
        /// Reads a press from either a touchscreen or a mouse, skipping presses that landed on
        /// the interface.
        /// </summary>
        private static bool TryReadPointerDown(out Vector2 screenPoint)
        {
            screenPoint = Vector2.zero;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase != TouchPhase.Began) return false;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId)) return false;

                screenPoint = touch.position;
                return true;
            }

            if (!Input.GetMouseButtonDown(0)) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;

            screenPoint = Input.mousePosition;
            return true;
        }

        private void HandleBoardTap(Vector2 screenPoint)
        {
            if (!BoardView.TryScreenPointToSquare(_cameraRig.Camera, screenPoint, out int square))
            {
                ClearSelection();
                return;
            }

            if (_selectedSquare != Squares.None)
            {
                if (TryFindMove(_selectedSquare, square, out Move move))
                {
                    if (move.IsPromotion)
                    {
                        _pendingPromotion = move;
                        _phase = GamePhase.AwaitingPromotion;
                        _ui.ShowPromotion(_humanColor);
                        return;
                    }

                    PlayMove(move);
                    return;
                }

                if (square == _selectedSquare)
                {
                    ClearSelection();
                    return;
                }
            }

            byte piece = _board.Squares[square];
            if (piece != Piece.None && Piece.IsColor(piece, _humanColor))
            {
                SelectSquare(square);
                return;
            }

            if (_selectedSquare != Squares.None)
            {
                _audio.PlayIllegal();
                ClearSelection();
            }
        }

        private void SelectSquare(int square)
        {
            _selectedSquare = square;
            _movesFromSelected.Clear();

            for (int i = 0; i < _legalMoves.Count; i++)
                if (_legalMoves[i].From == square) _movesFromSelected.Add(_legalMoves[i]);

            _audio.PlayClick();
            RefreshHighlights();
        }

        private void ClearSelection()
        {
            if (_selectedSquare == Squares.None) return;

            _selectedSquare = Squares.None;
            _movesFromSelected.Clear();
            RefreshHighlights();
        }

        /// <summary>
        /// Finds the legal move joining two squares. Promotions share from/to across four
        /// entries, so the caller resolves the piece separately.
        /// </summary>
        private bool TryFindMove(int from, int to, out Move move)
        {
            for (int i = 0; i < _movesFromSelected.Count; i++)
            {
                Move candidate = _movesFromSelected[i];
                if (candidate.From == from && candidate.To == to)
                {
                    move = candidate;
                    return true;
                }
            }

            move = Move.Null;
            return false;
        }

        private void CompletePromotion(PieceType type)
        {
            if (_phase != GamePhase.AwaitingPromotion) return;

            Move chosen = _pendingPromotion;
            for (int i = 0; i < _movesFromSelected.Count; i++)
            {
                Move candidate = _movesFromSelected[i];
                if (candidate.From == _pendingPromotion.From &&
                    candidate.To == _pendingPromotion.To &&
                    candidate.Promotion == type)
                {
                    chosen = candidate;
                    break;
                }
            }

            _phase = GamePhase.PlayerTurn;
            PlayMove(chosen);
        }

        // ---------------------------------------------------------- Making moves

        private void PlayMove(Move move)
        {
            string san = Notation.ToSan(_board, move, _legalMoves, _generator);
            AppendMoveToList(san, _board.SideToMove, _board.FullmoveNumber);

            PieceType movedType = Piece.TypeOf(_board.Squares[move.From]);

            int capturedSquare = -1;
            if (move.IsCapture)
            {
                capturedSquare = move.IsEnPassant
                    ? (_board.SideToMove == PieceColor.White ? move.To - 8 : move.To + 8)
                    : move.To;
            }

            _board.MakeMove(move);

            float duration = Mathf.Clamp(0.28f / Mathf.Max(0.1f, GameSettings.AnimationSpeed), 0.05f, 1f);
            _piecesView.ApplyMove(move, movedType, capturedSquare, duration);

            _selectedSquare = Squares.None;
            _movesFromSelected.Clear();

            RefreshLegalMoves();
            PlayMoveSound(move);

            RefreshHighlights();
            UpdateCaptureReadout();
            _ui.SetMoveList(_moveList);

            _result = Arbiter.GetResult(_board, _legalMoves);

            if (Arbiter.IsGameOver(_result))
            {
                FinishGame();
                return;
            }

            _phase = _board.SideToMove == _humanColor ? GamePhase.PlayerTurn : GamePhase.AiTurn;
            UpdateStatus();

            if (_phase == GamePhase.AiTurn) BeginAiTurn();
        }

        private void PlayMoveSound(Move move)
        {
            if (_board.IsInCheck(_board.SideToMove)) _audio.PlayCheck();
            else if (move.IsPromotion) _audio.PlayPromotion();
            else if (move.IsCastle) _audio.PlayCastle();
            else if (move.IsCapture) _audio.PlayCapture();
            else _audio.PlayMove();
        }

        private void AppendMoveToList(string san, PieceColor mover, int fullmoveNumber)
        {
            if (mover == PieceColor.White)
            {
                if (_moveList.Length > 0) _moveList.Append('\n');
                _moveList.Append(fullmoveNumber).Append(". ").Append(san);
            }
            else
            {
                // Black opening the list happens when a game starts from a mid-game position.
                if (_moveList.Length == 0) _moveList.Append(fullmoveNumber).Append("...");
                _moveList.Append("   ").Append(san);
            }
        }

        private void RefreshLegalMoves()
        {
            _generator.GenerateLegalMoves(_board, _legalMoves);
        }

        // ------------------------------------------------------------------- AI

        private void BeginAiTurn()
        {
            _aiEarliestPlayTime = Time.time + MinimumThinkSeconds;
            _aiSearchRunning = true;
            _ai.StartThinking(_board, GameSettings.Difficulty, _aiSeed++);

            _ui.SetThinking(true, "Computer is thinking...");
            UpdateStatus();
        }

        private void PollAi()
        {
            if (!_ai.TryTakeResult(out SearchResult result))
            {
                if (_ai.LastFailure != null)
                {
                    Debug.LogError("Chess AI failed: " + _ai.LastFailure);
                    _aiSearchRunning = false;
                    _ui.SetThinking(false);
                    PlayFallbackMove();
                }
                return;
            }

            _aiSearchRunning = false;

            // Hold the answer back until the minimum think time has elapsed.
            if (Time.time < _aiEarliestPlayTime)
            {
                StartCoroutine(PlayAfterDelay(result.BestMove, _aiEarliestPlayTime - Time.time));
                return;
            }

            FinishAiTurn(result.BestMove);
        }

        private System.Collections.IEnumerator PlayAfterDelay(Move move, float delay)
        {
            yield return new WaitForSeconds(delay);
            FinishAiTurn(move);
        }

        private void FinishAiTurn(Move move)
        {
            _ui.SetThinking(false);

            if (_phase != GamePhase.AiTurn) return;

            if (move.IsNull)
            {
                PlayFallbackMove();
                return;
            }

            PlayMove(move);
        }

        /// <summary>Last resort if the search returned nothing: play any legal move rather than stall.</summary>
        private void PlayFallbackMove()
        {
            if (_legalMoves.Count == 0) return;
            PlayMove(_legalMoves[Random.Range(0, _legalMoves.Count)]);
        }

        // ---------------------------------------------------------------- Undo

        public void UndoMove()
        {
            if (_phase != GamePhase.PlayerTurn && _phase != GamePhase.Finished) return;
            if (_board.PlyCount == 0) return;

            _ai.Cancel();
            _aiSearchRunning = false;
            _ui.SetThinking(false);

            // Step back past the opponent's reply as well, so it stays the player's turn.
            _board.UnmakeMove();
            if (_board.SideToMove != _humanColor && _board.PlyCount > 0)
                _board.UnmakeMove();

            TrimMoveList();

            _result = GameResult.InProgress;
            _selectedSquare = Squares.None;
            _movesFromSelected.Clear();

            RefreshLegalMoves();
            _piecesView.Rebuild(_board);
            RefreshHighlights();
            UpdateCaptureReadout();
            _ui.SetMoveList(_moveList);

            _phase = GamePhase.PlayerTurn;
            UpdateStatus();
            _audio.PlayClick();
        }

        /// <summary>Rebuilds the notation list from the surviving history after an undo.</summary>
        private void TrimMoveList()
        {
            _moveList.Length = 0;

            var replay = new Board();
            Fen.Load(replay, Board.StartFen);

            var scratchGenerator = new MoveGenerator();
            var legal = new List<Move>(64);

            for (int ply = 0; ply < _board.PlyCount; ply++)
            {
                Move move = _board.GetHistoryMove(ply);
                scratchGenerator.GenerateLegalMoves(replay, legal);

                string san = Notation.ToSan(replay, move, legal, scratchGenerator);
                AppendMoveToList(san, replay.SideToMove, replay.FullmoveNumber);

                replay.MakeMove(move);
            }
        }

        // ------------------------------------------------------------ Presentation

        private void RefreshHighlights()
        {
            if (_boardView == null) return;

            _boardView.ClearHighlights();

            if (_phase == GamePhase.Menu) return;

            if (GameSettings.ShowLastMove && _board.PlyCount > 0)
            {
                Move last = _board.LastMove;
                _boardView.AddHighlight(last.From, HighlightKind.LastMove);
                _boardView.AddHighlight(last.To, HighlightKind.LastMove);
            }

            if (_board.IsInCheck(_board.SideToMove))
                _boardView.AddHighlight(_board.KingSquares[(int)_board.SideToMove], HighlightKind.Check);

            if (_selectedSquare != Squares.None)
            {
                _boardView.AddHighlight(_selectedSquare, HighlightKind.Selection);

                if (GameSettings.ShowLegalMoves)
                {
                    for (int i = 0; i < _movesFromSelected.Count; i++)
                    {
                        Move move = _movesFromSelected[i];
                        _boardView.AddHighlight(move.To, move.IsCapture ? HighlightKind.LegalCapture : HighlightKind.LegalMove);
                    }
                }
            }
        }

        private void UpdateStatus()
        {
            _ui.SetUndoEnabled(_board.PlyCount > 0);

            bool inCheck = _board.IsInCheck(_board.SideToMove);

            switch (_phase)
            {
                case GamePhase.PlayerTurn:
                    _ui.SetStatus(inCheck ? "Check - your move" : "Your turn",
                        inCheck ? UIPalette.Danger : UIPalette.Accent);
                    break;

                case GamePhase.AiTurn:
                    _ui.SetStatus(inCheck ? "Check - computer must answer" : "Computer's turn", UIPalette.Info);
                    break;

                case GamePhase.Finished:
                    _ui.SetStatus(Arbiter.Describe(_result), UIPalette.Text);
                    break;
            }
        }

        /// <summary>Shows each side's material lead, the way a chess app puts "+3" next to a player.</summary>
        private void UpdateCaptureReadout()
        {
            int white = 0;
            int black = 0;

            for (int square = 0; square < 64; square++)
            {
                byte piece = _board.Squares[square];
                if (piece == Piece.None) continue;

                PieceType type = Piece.TypeOf(piece);
                if (type == PieceType.King) continue;

                int value = Evaluation.PieceValue(type) / 100;
                if (Piece.IsColor(piece, PieceColor.White)) white += value;
                else black += value;
            }

            int humanScore = _humanColor == PieceColor.White ? white - black : black - white;

            _ui.SetCaptures(
                humanScore > 0 ? "+" + humanScore : string.Empty,
                humanScore < 0 ? "+" + (-humanScore) : string.Empty);
        }

        private void FinishGame()
        {
            _phase = GamePhase.Finished;
            _selectedSquare = Squares.None;
            _movesFromSelected.Clear();

            RefreshHighlights();
            UpdateStatus();

            PieceColor? winner = Arbiter.Winner(_result);

            string title;
            Color color;

            if (winner == null)
            {
                title = "Draw";
                color = UIPalette.Info;
                _audio.PlayDraw();
            }
            else if (winner.Value == _humanColor)
            {
                title = "You win!";
                color = UIPalette.Success;
                _audio.PlayWin();
            }
            else
            {
                title = "You lose";
                color = UIPalette.Danger;
                _audio.PlayLose();
            }

            _ui.ShowGameOver(title, Arbiter.Describe(_result), color);
        }

        private void OnDestroy()
        {
            _ai?.Dispose();
        }
    }
}
