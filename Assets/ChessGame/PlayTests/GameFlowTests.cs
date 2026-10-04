using System.Collections;
using Chess.AI;
using Chess.Core;
using Chess.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Chess.PlayTests
{
    /// <summary>
    /// End-to-end checks that run the real scene: the board and interface are built at startup,
    /// a tapped move is answered by the opponent, and undo puts the player back on move. Any
    /// Debug.LogError raised along the way fails the test, so these also catch silent breakage
    /// in the scene wiring.
    /// </summary>
    public class GameFlowTests
    {
        private GameManager _manager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Written before the scene loads, for the first run in a fresh domain...
            PlayerPrefs.SetInt("chess.difficulty", (int)Difficulty.Easy);
            PlayerPrefs.SetInt("chess.side", (int)PlayerSide.White);
            PlayerPrefs.SetInt("chess.sound", 0);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Chess", LoadSceneMode.Single);
            yield return null;

            // ...and again afterwards, because the settings cache only loads once per domain.
            GameSettings.Difficulty = Difficulty.Easy;
            GameSettings.PreferredSide = PlayerSide.White;
            GameSettings.SoundEnabled = false;

            _manager = Object.FindFirstObjectByType<GameManager>();
            Assert.IsNotNull(_manager, "The Chess scene has no GameManager.");
        }

        private IEnumerator WaitFor(System.Func<bool> condition, float timeoutSeconds, string description)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail("Timed out waiting for " + description);

                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Scene_BuildsBoardPiecesAndInterface()
        {
            Assert.IsNotNull(Object.FindFirstObjectByType<BoardView>(), "board was not built");
            Assert.IsNotNull(Object.FindFirstObjectByType<Canvas>(), "interface canvas was not built");
            Assert.IsNotNull(Camera.main, "no main camera");

            var pieces = Object.FindObjectsByType<PieceView>(FindObjectsSortMode.None);
            Assert.AreEqual(32, pieces.Length, "expected a full set of 32 pieces on the board");

            yield return null;
        }

        [UnityTest]
        public IEnumerator NewGame_StartsWithThePlayerOnMove()
        {
            _manager.StartNewGame();
            yield return null;

            Assert.AreEqual(PieceColor.White, _manager.HumanColor);
            Assert.AreEqual(GamePhase.PlayerTurn, _manager.Phase);
            Assert.AreEqual(0, _manager.PlyCount);
            Assert.AreEqual(Board.StartFen, _manager.CurrentFen);
        }

        [UnityTest]
        public IEnumerator PlayerMove_IsAnsweredByTheComputer()
        {
            _manager.StartNewGame();
            yield return null;

            int from = Squares.FromAlgebraic("e2");
            int to = Squares.FromAlgebraic("e4");

            Assert.IsTrue(_manager.TryPlayPlayerMove(from, to), "e2e4 should be playable at the start");
            Assert.AreEqual(GamePhase.AiTurn, _manager.Phase);

            yield return WaitFor(() => _manager.Phase == GamePhase.PlayerTurn, 20f, "the computer to reply");

            Assert.AreEqual(2, _manager.PlyCount, "expected one move from each side");
        }

        [UnityTest]
        public IEnumerator IllegalMove_IsRejected()
        {
            _manager.StartNewGame();
            yield return null;

            // e2 to e5 is two squares too far for a pawn.
            Assert.IsFalse(_manager.TryPlayPlayerMove(Squares.FromAlgebraic("e2"), Squares.FromAlgebraic("e5")));
            Assert.AreEqual(0, _manager.PlyCount);
        }

        [UnityTest]
        public IEnumerator Undo_TakesBackBothPliesAndReturnsTheTurn()
        {
            _manager.StartNewGame();
            yield return null;

            _manager.TryPlayPlayerMove(Squares.FromAlgebraic("d2"), Squares.FromAlgebraic("d4"));
            yield return WaitFor(() => _manager.Phase == GamePhase.PlayerTurn, 20f, "the computer to reply");

            Assert.AreEqual(2, _manager.PlyCount);

            _manager.UndoMove();
            yield return null;

            Assert.AreEqual(0, _manager.PlyCount, "undo should retract the reply as well as the move");
            Assert.AreEqual(GamePhase.PlayerTurn, _manager.Phase);
            Assert.AreEqual(Board.StartFen, _manager.CurrentFen);
        }

        [UnityTest]
        public IEnumerator Promotion_TurnsThePawnIntoTheChosenPiece()
        {
            // White pawn one square from the eighth rank, with both kings safely out of the way.
            _manager.StartNewGame("7k/P7/8/8/8/8/8/K7 w - - 0 1");
            yield return null;

            Assert.AreEqual(GamePhase.PlayerTurn, _manager.Phase);

            int from = Squares.FromAlgebraic("a7");
            int to = Squares.FromAlgebraic("a8");

            Assert.IsTrue(_manager.TryPlayPlayerMove(from, to, PieceType.Rook), "the promotion should be playable");

            var board = new Board();
            Assert.IsTrue(Fen.Load(board, _manager.CurrentFen));

            byte promoted = board.Squares[to];
            Assert.AreEqual(PieceType.Rook, Piece.TypeOf(promoted), "the pawn should have become the rook that was asked for");
            Assert.AreEqual(PieceColor.White, Piece.ColorOf(promoted));

            // The visible piece has to follow the logical one.
            var pieces = Object.FindObjectsByType<PieceView>(FindObjectsSortMode.None);
            bool foundRook = false;
            foreach (PieceView piece in pieces)
                if (piece.Square == to && piece.Type == PieceType.Rook) foundRook = true;

            Assert.IsTrue(foundRook, "the model on a8 was not swapped for a rook");
        }

        [UnityTest]
        public IEnumerator SeveralMoves_KeepBoardAndViewInStep()
        {
            _manager.StartNewGame();
            yield return null;

            string[] openings = { "e2e4", "g1f3", "f1c4" };

            foreach (string uci in openings)
            {
                int from = Squares.FromAlgebraic(uci.Substring(0, 2));
                int to = Squares.FromAlgebraic(uci.Substring(2, 2));

                if (!_manager.TryPlayPlayerMove(from, to))
                {
                    // The computer may have occupied the square; any legal continuation is fine.
                    continue;
                }

                yield return WaitFor(() => _manager.Phase == GamePhase.PlayerTurn || _manager.Phase == GamePhase.Finished,
                    20f, "the computer to reply");

                if (_manager.Phase == GamePhase.Finished) break;
            }

            var board = new Board();
            Assert.IsTrue(Fen.Load(board, _manager.CurrentFen), "the game produced an unreadable position");

            int visible = 0;
            foreach (PieceView piece in Object.FindObjectsByType<PieceView>(FindObjectsSortMode.None))
                if (piece.isActiveAndEnabled) visible++;

            int onBoard = 0;
            for (int square = 0; square < 64; square++)
                if (board.Squares[square] != Piece.None) onBoard++;

            Assert.AreEqual(onBoard, visible, "the number of rendered pieces drifted from the position");
        }

        [UnityTest]
        public IEnumerator ChangingTheme_RebuildsWithoutLosingThePosition()
        {
            _manager.StartNewGame();
            yield return null;

            _manager.TryPlayPlayerMove(Squares.FromAlgebraic("e2"), Squares.FromAlgebraic("e4"));
            yield return WaitFor(() => _manager.Phase == GamePhase.PlayerTurn, 20f, "the computer to reply");

            string before = _manager.CurrentFen;

            for (int theme = 0; theme < Visual.BoardTheme.All.Length; theme++)
            {
                GameSettings.ThemeIndex = theme;
                _manager.ApplyCurrentSettings();
                yield return null;
            }

            Assert.AreEqual(before, _manager.CurrentFen, "switching theme must not disturb the game");

            // Settings are static, so leave the default in place for whatever test runs next.
            GameSettings.ThemeIndex = 0;
            _manager.ApplyCurrentSettings();
        }

        [UnityTest]
        public IEnumerator ReturningToMenu_StopsTheGameCleanly()
        {
            _manager.StartNewGame();
            yield return null;

            _manager.TryPlayPlayerMove(Squares.FromAlgebraic("e2"), Squares.FromAlgebraic("e4"));

            // Bail out while the opponent is still searching: the search must be cancelled, not orphaned.
            _manager.ReturnToMenu();
            yield return null;
            yield return null;

            Assert.AreEqual(GamePhase.Menu, _manager.Phase);
            Assert.AreEqual(Board.StartFen, _manager.CurrentFen);

            yield return new WaitForSeconds(1f);
            Assert.AreEqual(GamePhase.Menu, _manager.Phase, "a late search result must not resume the game");
        }
    }
}
