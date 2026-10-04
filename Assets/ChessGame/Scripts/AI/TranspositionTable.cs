using Chess.Core;

namespace Chess.AI
{
    public static class SearchConstants
    {
        public const int Infinity = 1000000;
        public const int MateScore = 100000;

        /// <summary>Scores beyond this magnitude are forced mates rather than ordinary evaluations.</summary>
        public const int MateThreshold = MateScore - 1000;

        public const int MaxPly = 64;

        public static bool IsMateScore(int score)
        {
            int abs = score < 0 ? -score : score;
            return abs > MateThreshold;
        }

        /// <summary>Plies until mate, for display as "mate in N moves".</summary>
        public static int MateDistanceInMoves(int score)
        {
            int abs = score < 0 ? -score : score;
            return (MateScore - abs + 1) / 2;
        }
    }

    /// <summary>
    /// Fixed-size, always-replace hash table keyed by Zobrist. Mate scores are stored relative to
    /// the node they were found at, so an entry stays valid when reached by a different path.
    /// </summary>
    public sealed class TranspositionTable
    {
        public const int Exact = 0;
        public const int LowerBound = 1;
        public const int UpperBound = 2;

        public const int LookupFailed = int.MinValue;

        private struct Entry
        {
            public ulong Key;
            public int Score;
            public int Depth;
            public byte Flag;
            public bool HasValue;
            public Move Move;
        }

        private readonly Entry[] _entries;
        private readonly ulong _mask;

        public TranspositionTable(int sizeMegabytes = 16)
        {
            int entrySize = 32;
            long wanted = (long)sizeMegabytes * 1024 * 1024 / entrySize;

            int count = 1;
            while (count * 2L <= wanted && count < (1 << 22)) count *= 2;

            _entries = new Entry[count];
            _mask = (ulong)(count - 1);
        }

        public void Clear()
        {
            for (int i = 0; i < _entries.Length; i++) _entries[i] = default;
        }

        private int IndexOf(ulong key) => (int)(key & _mask);

        public Move GetMove(ulong key)
        {
            Entry entry = _entries[IndexOf(key)];
            return entry.HasValue && entry.Key == key ? entry.Move : Move.Null;
        }

        public int Lookup(ulong key, int depth, int ply, int alpha, int beta)
        {
            Entry entry = _entries[IndexOf(key)];
            if (!entry.HasValue || entry.Key != key || entry.Depth < depth) return LookupFailed;

            int score = ScoreFromTable(entry.Score, ply);

            switch (entry.Flag)
            {
                case Exact: return score;
                case LowerBound: return score >= beta ? score : LookupFailed;
                case UpperBound: return score <= alpha ? score : LookupFailed;
                default: return LookupFailed;
            }
        }

        public void Store(ulong key, int depth, int ply, int score, byte flag, Move move)
        {
            int index = IndexOf(key);
            Entry existing = _entries[index];

            // Keep the deeper result unless the slot belongs to a different position.
            if (existing.HasValue && existing.Key == key && existing.Depth > depth) return;

            _entries[index] = new Entry
            {
                Key = key,
                Score = ScoreToTable(score, ply),
                Depth = depth,
                Flag = flag,
                Move = move,
                HasValue = true
            };
        }

        private static int ScoreToTable(int score, int ply)
        {
            if (score > SearchConstants.MateThreshold) return score + ply;
            if (score < -SearchConstants.MateThreshold) return score - ply;
            return score;
        }

        private static int ScoreFromTable(int score, int ply)
        {
            if (score > SearchConstants.MateThreshold) return score - ply;
            if (score < -SearchConstants.MateThreshold) return score + ply;
            return score;
        }
    }
}
