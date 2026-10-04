using System;
using System.Collections.Generic;
using System.Threading;
using Chess.Core;

namespace Chess.AI
{
    public struct SearchSettings
    {
        public int MaxDepth;

        /// <summary>Wall-clock budget in milliseconds. Zero or less means depth is the only limit.</summary>
        public int TimeLimitMs;

        /// <summary>Centipawn noise added to each root move, which is what makes the easy levels beatable.</summary>
        public int RandomSpread;

        public int Seed;

        public static SearchSettings Create(int maxDepth, int timeLimitMs, int randomSpread, int seed)
        {
            return new SearchSettings
            {
                MaxDepth = Math.Max(1, maxDepth),
                TimeLimitMs = timeLimitMs,
                RandomSpread = Math.Max(0, randomSpread),
                Seed = seed
            };
        }
    }

    public struct SearchResult
    {
        public Move BestMove;
        public int Score;
        public int Depth;
        public long Nodes;
        public long ElapsedMs;
    }

    /// <summary>
    /// Negamax with alpha-beta, iterative deepening, a transposition table, killer and history
    /// move ordering, and a quiescence search that resolves captures before evaluating.
    /// One instance is single-threaded: it owns its own board copy while searching.
    /// </summary>
    public sealed class SearchEngine
    {
        private const int MaxQuiescencePly = 16;
        private const int ListCount = SearchConstants.MaxPly + MaxQuiescencePly + 2;

        private readonly MoveGenerator _generator = new MoveGenerator();
        private readonly TranspositionTable _transpositions;
        private readonly List<Move>[] _moveLists = new List<Move>[ListCount];
        private readonly Move[,] _killers = new Move[SearchConstants.MaxPly, 2];
        private readonly int[,] _history = new int[64, 64];
        private readonly int[] _orderScores = new int[256];

        private Board _board;
        private System.Diagnostics.Stopwatch _timer;
        private CancellationToken _token;
        private int _timeLimitMs;
        private int _randomSpread;
        private Random _random;
        private bool _abort;
        private long _nodes;

        private Move _bestMoveThisIteration;
        private int _bestScoreThisIteration;

        public SearchEngine(int transpositionSizeMb = 16)
        {
            _transpositions = new TranspositionTable(transpositionSizeMb);
            for (int i = 0; i < _moveLists.Length; i++) _moveLists[i] = new List<Move>(64);
        }

        public void ClearMemory()
        {
            _transpositions.Clear();
            Array.Clear(_history, 0, _history.Length);
            Array.Clear(_killers, 0, _killers.Length);
        }

        /// <summary>
        /// Searches a private copy of <paramref name="board"/>, so the caller's position is
        /// never touched and this can safely run on a worker thread.
        /// </summary>
        public SearchResult FindBestMove(Board board, SearchSettings settings, CancellationToken token)
        {
            _board = board.Clone();
            _token = token;
            _timeLimitMs = settings.TimeLimitMs;
            _randomSpread = settings.RandomSpread;
            _random = new Random(settings.Seed);
            _abort = false;
            _nodes = 0;
            _timer = System.Diagnostics.Stopwatch.StartNew();

            Array.Clear(_killers, 0, _killers.Length);
            Array.Clear(_history, 0, _history.Length);

            var result = new SearchResult { BestMove = Move.Null, Score = 0, Depth = 0 };

            var rootMoves = _generator.GenerateLegalMoves(_board);
            if (rootMoves.Count == 0)
            {
                result.ElapsedMs = _timer.ElapsedMilliseconds;
                return result;
            }

            // Guarantee a playable move even if the very first iteration runs out of time.
            result.BestMove = rootMoves[0];

            for (int depth = 1; depth <= settings.MaxDepth; depth++)
            {
                _bestMoveThisIteration = Move.Null;
                _bestScoreThisIteration = -SearchConstants.Infinity;

                SearchRoot(depth);

                if (_abort) break;

                result.BestMove = _bestMoveThisIteration;
                result.Score = _bestScoreThisIteration;
                result.Depth = depth;

                // A forced mate is already the final answer, so stop burning time on it.
                if (SearchConstants.IsMateScore(_bestScoreThisIteration)) break;
            }

            result.Nodes = _nodes;
            result.ElapsedMs = _timer.ElapsedMilliseconds;
            return result;
        }

        private void SearchRoot(int depth)
        {
            var moves = _moveLists[0];
            _generator.GenerateLegalMoves(_board, moves);
            if (moves.Count == 0) return;

            OrderMoves(moves, 0, _transpositions.GetMove(_board.ZobristKey));

            int alpha = -SearchConstants.Infinity;
            const int beta = SearchConstants.Infinity;

            Move bestMove = moves[0];
            int bestScore = -SearchConstants.Infinity;

            for (int i = 0; i < moves.Count; i++)
            {
                Move move = moves[i];

                _board.MakeMove(move);
                int score = -Negamax(depth - 1, 1, -beta, -alpha);
                _board.UnmakeMove();

                // Discard a half-finished iteration; the previous depth's answer still stands.
                if (_abort) return;

                if (_randomSpread > 0)
                    score += _random.Next(-_randomSpread, _randomSpread + 1);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                    if (score > alpha) alpha = score;
                }
            }

            _bestMoveThisIteration = bestMove;
            _bestScoreThisIteration = bestScore;

            _transpositions.Store(_board.ZobristKey, depth, 0, bestScore, TranspositionTable.Exact, bestMove);
        }

        private int Negamax(int depth, int ply, int alpha, int beta)
        {
            if (_abort) return 0;

            _nodes++;
            if ((_nodes & 1023) == 0) CheckBudget();

            if (ply > 0)
            {
                // A single repetition inside the search is already enough to claim the draw.
                if (_board.RepetitionCount() >= 2) return 0;
                if (_board.HalfmoveClock >= 100) return 0;
                if (Arbiter.HasInsufficientMaterial(_board)) return 0;

                // Mate distance pruning: nothing better than mating right now is reachable.
                int mateAlpha = Math.Max(alpha, -SearchConstants.MateScore + ply);
                int mateBeta = Math.Min(beta, SearchConstants.MateScore - ply);
                if (mateAlpha >= mateBeta) return mateAlpha;
                alpha = mateAlpha;
                beta = mateBeta;
            }

            ulong key = _board.ZobristKey;

            if (ply > 0)
            {
                int cached = _transpositions.Lookup(key, depth, ply, alpha, beta);
                if (cached != TranspositionTable.LookupFailed) return cached;
            }

            // Hard ceiling on recursion. Check extensions do not shorten a line, so without
            // this a perpetual-check sequence would never unwind.
            if (ply >= SearchConstants.MaxPly) return Evaluation.Evaluate(_board);

            bool inCheck = _board.IsInCheck(_board.SideToMove);

            // Resolve the forcing line one ply deeper. This has to happen before the drop into
            // quiescence, which only searches captures and would misjudge a position in check.
            if (inCheck) depth++;

            if (depth <= 0) return Quiescence(ply, alpha, beta, 0);

            var moves = _moveLists[ply];
            _generator.GenerateLegalMoves(_board, moves);

            if (moves.Count == 0)
            {
                // Mate scores shrink with distance so the engine prefers the quickest mate.
                return inCheck ? -SearchConstants.MateScore + ply : 0;
            }

            OrderMoves(moves, ply, _transpositions.GetMove(key));

            byte flag = TranspositionTable.UpperBound;
            Move bestMove = Move.Null;
            int bestScore = -SearchConstants.Infinity;

            for (int i = 0; i < moves.Count; i++)
            {
                Move move = moves[i];

                _board.MakeMove(move);
                int score = -Negamax(depth - 1, ply + 1, -beta, -alpha);
                _board.UnmakeMove();

                if (_abort) return 0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }

                if (score > alpha)
                {
                    alpha = score;
                    flag = TranspositionTable.Exact;
                }

                if (alpha >= beta)
                {
                    flag = TranspositionTable.LowerBound;

                    if (!move.IsCapture && ply < SearchConstants.MaxPly)
                    {
                        _killers[ply, 1] = _killers[ply, 0];
                        _killers[ply, 0] = move;
                        _history[move.From, move.To] += depth * depth;
                    }

                    break;
                }
            }

            _transpositions.Store(key, depth, ply, bestScore, flag, bestMove);
            return bestScore;
        }

        /// <summary>
        /// Plays out the remaining captures and promotions so the evaluation is never taken in
        /// the middle of an exchange.
        /// </summary>
        private int Quiescence(int ply, int alpha, int beta, int quiescenceDepth)
        {
            if (_abort) return 0;

            _nodes++;
            if ((_nodes & 1023) == 0) CheckBudget();

            int standPat = Evaluation.Evaluate(_board);
            if (standPat >= beta) return beta;
            if (standPat > alpha) alpha = standPat;

            if (quiescenceDepth >= MaxQuiescencePly) return alpha;

            int listIndex = ply < ListCount ? ply : ListCount - 1;
            var moves = _moveLists[listIndex];
            _generator.GenerateLegalMoves(_board, moves, true);

            OrderMoves(moves, -1, Move.Null);

            for (int i = 0; i < moves.Count; i++)
            {
                _board.MakeMove(moves[i]);
                int score = -Quiescence(ply + 1, -beta, -alpha, quiescenceDepth + 1);
                _board.UnmakeMove();

                if (_abort) return 0;

                if (score >= beta) return beta;
                if (score > alpha) alpha = score;
            }

            return alpha;
        }

        private void CheckBudget()
        {
            if (_token.IsCancellationRequested)
            {
                _abort = true;
                return;
            }

            if (_timeLimitMs > 0 && _timer.ElapsedMilliseconds >= _timeLimitMs)
                _abort = true;
        }

        /// <summary>
        /// Sorts in place, best guess first: hash move, then promotions, then captures by
        /// most-valuable-victim / least-valuable-attacker, then killers, then history.
        /// </summary>
        private void OrderMoves(List<Move> moves, int ply, Move hashMove)
        {
            int count = moves.Count;
            if (count > _orderScores.Length) count = _orderScores.Length;

            for (int i = 0; i < count; i++)
                _orderScores[i] = ScoreMove(moves[i], hashMove, ply);

            for (int i = 1; i < count; i++)
            {
                int score = _orderScores[i];
                Move move = moves[i];
                int j = i - 1;

                while (j >= 0 && _orderScores[j] < score)
                {
                    _orderScores[j + 1] = _orderScores[j];
                    moves[j + 1] = moves[j];
                    j--;
                }

                _orderScores[j + 1] = score;
                moves[j + 1] = move;
            }
        }

        private int ScoreMove(Move move, Move hashMove, int ply)
        {
            if (!hashMove.IsNull && move == hashMove) return 10000000;

            if (move.IsPromotion)
                return 9000000 + Evaluation.PieceValue(move.Promotion);

            if (move.IsCapture)
            {
                int victim = move.IsEnPassant
                    ? Evaluation.PawnValue
                    : Evaluation.PieceValue(Piece.TypeOf(_board.Squares[move.To]));
                int attacker = Evaluation.PieceValue(Piece.TypeOf(_board.Squares[move.From]));
                return 8000000 + victim * 16 - attacker;
            }

            if (ply >= 0 && ply < SearchConstants.MaxPly)
            {
                if (_killers[ply, 0] == move) return 7000000;
                if (_killers[ply, 1] == move) return 6000000;
            }

            return _history[move.From, move.To];
        }
    }
}
