using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Core;

namespace Chess.AI
{
    public enum Difficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2,
        Expert = 3
    }

    /// <summary>
    /// Runs <see cref="SearchEngine"/> on a worker thread and hands the answer back to the main
    /// thread through polling, so the game never blocks a frame while the opponent thinks.
    /// </summary>
    public sealed class ChessAI : IDisposable
    {
        private readonly SearchEngine _engine = new SearchEngine();
        private CancellationTokenSource _cancellation;
        private Task<SearchResult> _search;
        private Exception _failure;

        public bool IsThinking => _search != null && !_search.IsCompleted;

        /// <summary>Set when the worker threw; the caller surfaces it once, on the main thread.</summary>
        public Exception LastFailure => _failure;

        public static SearchSettings SettingsFor(Difficulty difficulty, int seed)
        {
            switch (difficulty)
            {
                case Difficulty.Easy:
                    // Shallow and deliberately noisy, so a beginner can win.
                    return SearchSettings.Create(2, 400, 90, seed);
                case Difficulty.Medium:
                    return SearchSettings.Create(4, 1000, 35, seed);
                case Difficulty.Hard:
                    return SearchSettings.Create(6, 2000, 0, seed);
                default:
                    return SearchSettings.Create(12, 4000, 0, seed);
            }
        }

        public static string DisplayName(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return "Easy";
                case Difficulty.Medium: return "Medium";
                case Difficulty.Hard: return "Hard";
                default: return "Expert";
            }
        }

        public void StartThinking(Board board, Difficulty difficulty, int seed)
        {
            Cancel();

            _failure = null;
            SearchSettings settings = SettingsFor(difficulty, seed);

            // Clone on the calling thread so the worker never touches the live board.
            Board snapshot = board.Clone();

            _cancellation = new CancellationTokenSource();
            CancellationToken token = _cancellation.Token;

            _search = Task.Run(() =>
            {
                try
                {
                    return _engine.FindBestMove(snapshot, settings, token);
                }
                catch (Exception exception)
                {
                    _failure = exception;
                    return new SearchResult { BestMove = Move.Null };
                }
            }, token);
        }

        /// <summary>Returns true exactly once per search, on the frame the answer becomes available.</summary>
        public bool TryTakeResult(out SearchResult result)
        {
            result = default;

            if (_search == null || !_search.IsCompleted) return false;

            Task<SearchResult> finished = _search;
            _search = null;

            if (finished.IsFaulted)
            {
                _failure = finished.Exception;
                return false;
            }

            if (finished.IsCanceled) return false;

            result = finished.Result;
            return true;
        }

        public void Cancel()
        {
            if (_cancellation != null)
            {
                _cancellation.Cancel();
                _cancellation.Dispose();
                _cancellation = null;
            }

            _search = null;
        }

        public void ClearMemory() => _engine.ClearMemory();

        public void Dispose() => Cancel();
    }
}
