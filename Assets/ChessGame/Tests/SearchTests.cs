using System.Collections.Generic;
using System.Threading;
using Chess.AI;
using Chess.Core;
using NUnit.Framework;

namespace Chess.Tests
{
    public class SearchTests
    {
        private static Board Position(string fen)
        {
            var board = new Board();
            Assert.IsTrue(Fen.Load(board, fen), "FEN failed to load: " + fen);
            return board;
        }

        private static SearchResult Search(Board board, int depth, int timeMs = 4000)
        {
            var engine = new SearchEngine();
            return engine.FindBestMove(board, SearchSettings.Create(depth, timeMs, 0, 1), CancellationToken.None);
        }

        private static bool IsLegal(Board board, Move move)
        {
            var generator = new MoveGenerator();
            foreach (Move legal in generator.GenerateLegalMoves(board))
                if (legal == move) return true;
            return false;
        }

        [Test]
        public void Search_ReturnsALegalMoveFromTheOpeningPosition()
        {
            Board board = Board.CreateStartPosition();
            SearchResult result = Search(board, 4);

            Assert.IsFalse(result.BestMove.IsNull, "engine returned no move");
            Assert.IsTrue(IsLegal(board, result.BestMove), "engine returned an illegal move: " + result.BestMove.ToUci());
        }

        [Test]
        public void Search_DoesNotMutateTheCallersBoard()
        {
            Board board = Board.CreateStartPosition();
            string before = Fen.Save(board);

            Search(board, 4);

            Assert.AreEqual(before, Fen.Save(board));
        }

        [Test]
        public void Search_FindsMateInOne()
        {
            Board board = Position("6k1/5ppp/8/8/8/8/8/K3R3 w - - 0 1");
            SearchResult result = Search(board, 3);

            Assert.AreEqual("e1e8", result.BestMove.ToUci());
            Assert.IsTrue(SearchConstants.IsMateScore(result.Score), "expected a mate score, got " + result.Score);
        }

        [Test]
        public void Search_FindsMateInTwo()
        {
            // 1.Ra8+ forces 1...Rc8 as the only interposition, then 2.Rxc8 mates on the
            // back rank. The black rook sits on c6 rather than the eighth rank, where it
            // would simply block the checking ray instead of having to answer it.
            Board board = Position("6k1/5ppp/2r5/8/8/8/8/R5K1 w - - 0 1");
            SearchResult result = Search(board, 5);

            Assert.IsTrue(SearchConstants.IsMateScore(result.Score), "expected a forced mate, got " + result.Score);
            Assert.LessOrEqual(SearchConstants.MateDistanceInMoves(result.Score), 2);
        }

        [Test]
        public void Search_TakesAHangingQueen()
        {
            Board board = Position("4k3/8/8/8/7q/8/8/K6R w - - 0 1");
            SearchResult result = Search(board, 4);

            Assert.AreEqual("h1h4", result.BestMove.ToUci(), "the free queen should be taken");
        }

        [Test]
        public void Search_AvoidsStalemateWhenItCanStillWin()
        {
            // White is overwhelmingly ahead; the engine must not shuffle into a draw.
            Board board = Position("7k/8/8/8/8/8/5Q2/K5R1 w - - 0 1");
            SearchResult result = Search(board, 4);

            board.MakeMove(result.BestMove);

            var generator = new MoveGenerator();
            var replies = generator.GenerateLegalMoves(board);
            GameResult outcome = Arbiter.GetResult(board, replies);

            Assert.AreNotEqual(GameResult.Stalemate, outcome, "engine walked into stalemate");
        }

        [Test]
        public void Search_RespectsItsTimeBudget()
        {
            Board board = Board.CreateStartPosition();
            var engine = new SearchEngine();

            var timer = System.Diagnostics.Stopwatch.StartNew();
            SearchResult result = engine.FindBestMove(board, SearchSettings.Create(64, 500, 0, 1), CancellationToken.None);
            timer.Stop();

            Assert.IsFalse(result.BestMove.IsNull);
            Assert.Less(timer.ElapsedMilliseconds, 3000, "search overran its 500ms budget by too much");
        }

        [Test]
        public void Search_StopsWhenCancelled()
        {
            Board board = Board.CreateStartPosition();
            var engine = new SearchEngine();

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.CancelAfter(150);

                var timer = System.Diagnostics.Stopwatch.StartNew();
                SearchResult result = engine.FindBestMove(board, SearchSettings.Create(64, 0, 0, 1), cancellation.Token);
                timer.Stop();

                Assert.IsFalse(result.BestMove.IsNull, "a cancelled search must still return a playable move");
                Assert.Less(timer.ElapsedMilliseconds, 3000, "cancellation was not honoured promptly");
            }
        }

        [Test]
        public void EasyDifficulty_StillPlaysLegalMoves()
        {
            Board board = Board.CreateStartPosition();
            var engine = new SearchEngine();
            var seen = new HashSet<string>();

            // The easy level adds score noise, so repeated searches should not always agree.
            for (int seed = 0; seed < 6; seed++)
            {
                SearchResult result = engine.FindBestMove(
                    board, ChessAI.SettingsFor(Difficulty.Easy, seed), CancellationToken.None);

                Assert.IsTrue(IsLegal(board, result.BestMove), "illegal move at easy difficulty");
                seen.Add(result.BestMove.ToUci());
            }

            Assert.Greater(seen.Count, 1, "easy difficulty should vary its opening choice");
        }
    }
}
