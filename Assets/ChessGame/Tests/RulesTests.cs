using System.Collections.Generic;
using Chess.Core;
using NUnit.Framework;

namespace Chess.Tests
{
    /// <summary>
    /// Perft compares the number of leaf nodes reachable at a given depth against values
    /// published for these standard positions. Matching them exercises castling rights,
    /// en passant (including the pinned-capture edge case), promotions, pins and check
    /// evasion, so an exact match is strong evidence the rule set is complete.
    /// </summary>
    public class PerftTests
    {
        private const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        private const string Kiwipete = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
        private const string EndgamePosition = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1";
        private const string PromotionPosition = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
        private const string TalkchessPosition = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";
        private const string SteadyPosition = "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10";

        private static Board Position(string fen)
        {
            var board = new Board();
            Assert.IsTrue(Fen.Load(board, fen), "FEN failed to load: " + fen);
            return board;
        }

        [TestCase(StartPosition, 1, 20L)]
        [TestCase(StartPosition, 2, 400L)]
        [TestCase(StartPosition, 3, 8902L)]
        [TestCase(StartPosition, 4, 197281L)]
        [TestCase(Kiwipete, 1, 48L)]
        [TestCase(Kiwipete, 2, 2039L)]
        [TestCase(Kiwipete, 3, 97862L)]
        [TestCase(EndgamePosition, 1, 14L)]
        [TestCase(EndgamePosition, 2, 191L)]
        [TestCase(EndgamePosition, 3, 2812L)]
        [TestCase(EndgamePosition, 4, 43238L)]
        [TestCase(EndgamePosition, 5, 674624L)]
        [TestCase(PromotionPosition, 1, 6L)]
        [TestCase(PromotionPosition, 2, 264L)]
        [TestCase(PromotionPosition, 3, 9467L)]
        [TestCase(PromotionPosition, 4, 422333L)]
        [TestCase(TalkchessPosition, 1, 44L)]
        [TestCase(TalkchessPosition, 2, 1486L)]
        [TestCase(TalkchessPosition, 3, 62379L)]
        [TestCase(SteadyPosition, 1, 46L)]
        [TestCase(SteadyPosition, 2, 2079L)]
        [TestCase(SteadyPosition, 3, 89890L)]
        public void Perft_MatchesPublishedNodeCount(string fen, int depth, long expected)
        {
            Board board = Position(fen);
            Assert.AreEqual(expected, Perft.Run(board, depth));
        }

        [Test]
        public void MakeUnmake_RestoresPositionExactly()
        {
            Board board = Position(Kiwipete);
            string fenBefore = Fen.Save(board);
            ulong keyBefore = board.ZobristKey;

            var generator = new MoveGenerator();
            var moves = generator.GenerateLegalMoves(board);

            foreach (Move move in moves)
            {
                board.MakeMove(move);
                board.UnmakeMove();

                Assert.AreEqual(fenBefore, Fen.Save(board), "Position changed after " + move.ToUci());
                Assert.AreEqual(keyBefore, board.ZobristKey, "Zobrist key changed after " + move.ToUci());
            }
        }

        [Test]
        public void ZobristKey_StaysConsistentWithFullRecompute()
        {
            Board board = Position(Kiwipete);
            var generator = new MoveGenerator();
            var moves = generator.GenerateLegalMoves(board);

            foreach (Move move in moves)
            {
                board.MakeMove(move);
                Assert.AreEqual(Zobrist.Compute(board), board.ZobristKey, "Incremental key drifted after " + move.ToUci());
                board.UnmakeMove();
            }
        }

        [Test]
        public void Fen_RoundTripsEveryTestPosition()
        {
            foreach (string fen in new[] { StartPosition, Kiwipete, EndgamePosition, PromotionPosition, TalkchessPosition, SteadyPosition })
                Assert.AreEqual(fen, Fen.Save(Position(fen)));
        }
    }

    public class RuleDetailTests
    {
        private readonly MoveGenerator _generator = new MoveGenerator();

        private Board Position(string fen)
        {
            var board = new Board();
            Assert.IsTrue(Fen.Load(board, fen));
            return board;
        }

        private List<Move> Moves(Board board) => _generator.GenerateLegalMoves(board);

        private bool HasMove(Board board, string uci)
        {
            foreach (Move move in Moves(board))
                if (move.ToUci() == uci) return true;
            return false;
        }

        [Test]
        public void EnPassant_IsOfferedRightAfterDoublePush()
        {
            Board board = Position("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3");
            Assert.IsTrue(HasMove(board, "e5f6"), "en passant capture e5xf6 should be available");
        }

        [Test]
        public void EnPassant_IsIllegalWhenItExposesTheKing()
        {
            // White king on e5 with a black rook on h5: capturing d6 en passant would clear
            // two pawns off the fifth rank at once and leave the king in check.
            Board board = Position("8/8/8/K2pP2r/8/8/8/7k w - d6 0 1");
            Assert.IsFalse(HasMove(board, "e5d6"), "pinned en passant capture must be rejected");
        }

        [Test]
        public void Castling_IsBlockedThroughAnAttackedSquare()
        {
            // Black rook on f8 attacks f1, the square the white king would cross.
            Board board = Position("5r1k/8/8/8/8/8/8/4K2R w K - 0 1");
            Assert.IsFalse(HasMove(board, "e1g1"), "king may not castle through check");
        }

        [Test]
        public void Castling_IsBlockedWhileInCheck()
        {
            Board board = Position("4r2k/8/8/8/8/8/8/4K2R w K - 0 1");
            Assert.IsFalse(HasMove(board, "e1g1"), "king may not castle out of check");
        }

        [Test]
        public void Castling_IsAllowedWhenOnlyTheRookPathIsAttacked()
        {
            // Queenside: b1 may be attacked, only e1/d1/c1 matter for the king.
            Board board = Position("1r5k/8/8/8/8/8/8/R3K3 w Q - 0 1");
            Assert.IsTrue(HasMove(board, "e1c1"), "b1 being attacked does not prevent queenside castling");
        }

        [Test]
        public void CastlingRights_AreLostWhenTheRookIsCaptured()
        {
            Board board = Position("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            Assert.AreEqual(Board.CastleAll, board.CastlingRights);

            // Rook a1 takes rook a8, so Black loses the queenside right.
            board.MakeMove(new Move(Squares.FromAlgebraic("a1"), Squares.FromAlgebraic("a8"), MoveFlags.Capture));

            Assert.AreEqual(0, board.CastlingRights & Board.CastleBlackQueen, "black queenside right should be gone");
            Assert.AreEqual(0, board.CastlingRights & Board.CastleWhiteQueen, "white queenside right should be gone");
            Assert.AreNotEqual(0, board.CastlingRights & Board.CastleBlackKing, "black kingside right should survive");
        }

        [Test]
        public void Promotion_OffersAllFourPieces()
        {
            Board board = Position("8/P6k/8/8/8/8/8/K7 w - - 0 1");
            var promotions = new List<string>();

            foreach (Move move in Moves(board))
                if (move.IsPromotion) promotions.Add(move.ToUci());

            Assert.AreEqual(4, promotions.Count);
            CollectionAssert.AreEquivalent(new[] { "a7a8q", "a7a8r", "a7a8b", "a7a8n" }, promotions);
        }

        [Test]
        public void Checkmate_IsDetected()
        {
            // Fool's mate.
            Board board = Position("rnb1kbnr/pppp1ppp/8/4p3/6Pq/5P2/PPPPP2P/RNBQKBNR w KQkq - 1 3");
            var moves = Moves(board);

            Assert.AreEqual(0, moves.Count);
            Assert.AreEqual(GameResult.WhiteCheckmated, Arbiter.GetResult(board, moves));
            Assert.AreEqual(PieceColor.Black, Arbiter.Winner(GameResult.WhiteCheckmated));
        }

        [Test]
        public void Stalemate_IsDetected()
        {
            Board board = Position("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");
            var moves = Moves(board);

            Assert.AreEqual(0, moves.Count);
            Assert.AreEqual(GameResult.Stalemate, Arbiter.GetResult(board, moves));
            Assert.IsTrue(Arbiter.IsDraw(GameResult.Stalemate));
        }

        [Test]
        public void FiftyMoveRule_IsDetected()
        {
            Board board = Position("7k/8/8/8/8/8/8/K5R1 w - - 100 60");
            Assert.AreEqual(GameResult.FiftyMoveRule, Arbiter.GetResult(board, Moves(board)));
        }

        [Test]
        public void ThreefoldRepetition_IsDetected()
        {
            Board board = Position("7k/8/8/8/8/8/8/K5R1 w - - 0 1");

            // Shuffle both kings back and forth twice to reach the start position a third time.
            string[] cycle = { "a1a2", "h8h7", "a2a1", "h7h8" };
            for (int repeat = 0; repeat < 2; repeat++)
            {
                foreach (string uci in cycle)
                {
                    Move move = FindMove(board, uci);
                    Assert.AreNotEqual(Move.Null, move, "expected move " + uci + " to be legal");
                    board.MakeMove(move);
                }
            }

            Assert.AreEqual(3, board.RepetitionCount());
            Assert.AreEqual(GameResult.ThreefoldRepetition, Arbiter.GetResult(board, Moves(board)));
        }

        [Test]
        public void InsufficientMaterial_CoversTheDeadPositions()
        {
            Assert.IsTrue(Arbiter.HasInsufficientMaterial(Position("7k/8/8/8/8/8/8/K7 w - - 0 1")), "K vs K");
            Assert.IsTrue(Arbiter.HasInsufficientMaterial(Position("7k/8/8/8/8/8/8/KN6 w - - 0 1")), "K+N vs K");
            Assert.IsTrue(Arbiter.HasInsufficientMaterial(Position("7k/8/8/8/8/8/8/KB6 w - - 0 1")), "K+B vs K");

            // The bishops sit on b1 and h7, both light squares, so mate is impossible.
            Assert.IsTrue(Arbiter.HasInsufficientMaterial(Position("7k/7b/8/8/8/8/8/KB6 w - - 0 1")), "same-coloured bishops");

            Assert.IsFalse(Arbiter.HasInsufficientMaterial(Position("7k/8/8/8/8/8/8/KR6 w - - 0 1")), "a rook can mate");
            Assert.IsFalse(Arbiter.HasInsufficientMaterial(Position("7k/P7/8/8/8/8/8/K7 w - - 0 1")), "a pawn can promote");
        }

        [Test]
        public void San_RendersCastlingCapturesPromotionAndMate()
        {
            Board board = Position("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            Assert.AreEqual("O-O", San(board, "e1g1"));
            Assert.AreEqual("O-O-O", San(board, "e1c1"));

            // Two knights on d2 and f3 can both reach e4, so the file must disambiguate.
            board = Position("7k/8/8/8/8/8/3N1N2/K7 w - - 0 1");
            Assert.AreEqual("Nde4", San(board, "d2e4"));
            Assert.AreEqual("Nfe4", San(board, "f2e4"));

            board = Position("7k/P7/8/8/8/8/8/K7 w - - 0 1");
            Assert.AreEqual("a8=Q+", San(board, "a7a8q"));

            board = Position("6k1/5ppp/8/8/8/8/8/K3R3 w - - 0 1");
            Assert.AreEqual("Re8#", San(board, "e1e8"));
        }

        private string San(Board board, string uci)
        {
            var legal = Moves(board);
            Move move = FindMove(board, uci);
            Assert.AreNotEqual(Move.Null, move, "expected move " + uci + " to be legal");
            return Notation.ToSan(board, move, legal, _generator);
        }

        private Move FindMove(Board board, string uci)
        {
            foreach (Move move in Moves(board))
                if (move.ToUci() == uci) return move;
            return Move.Null;
        }
    }
}
