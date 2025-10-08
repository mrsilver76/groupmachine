/*
 * ProgressBar.cs
 * An extremely simple console progress bar that displays progress, percentage complete,
 * and estimated time remaining. To use, set ProgressBar.Total to the total number of items
 * you will be processing, then call ProgressBar.Start() before starting, and ProgressBar.Stop()
 * when done. Update the value of ProgressBar.Completed as you process each item.
 */

namespace GroupMachine
{
    /// <summary>
    /// Provides functionality for displaying and managing a progress bar in the console.
    /// </summary>
    /// <remarks>The <see cref="ProgressBar"/> class is designed to visually represent the progress of a task
    /// in the console. It supports updating the progress bar dynamically as tasks are completed and provides an
    /// estimated time of completion (ETA) based on the progress rate. The progress bar is displayed only when the
    /// console output is not redirected, and it automatically adjusts its display to fit within the console's buffer
    /// width.  To use the progress bar: <list type="number"> <item>Set the <see cref="Total"/> property to the total
    /// number of items to process.</item> <item>Call <see cref="Start"/> to initialize and display the progress
    /// bar.</item> <item>Update the <see cref="Completed"/> property as tasks are completed.</item> <item>Call <see
    /// cref="Stop"/> to finalize and remove the progress bar from the console.</item> </list> The progress bar is
    /// thread-safe and ensures proper cleanup of resources when stopped.</remarks>
    internal static class ProgressBar
    {
        private static Timer? _timer;
        private static readonly object _lock = new();
        private static DateTime? _startTime;

        /// <summary>The total number of items to process.</summary>
        public static int Total { get; set; }
        /// <summary>The number of items that have been processed.</summary>
        public static int Completed { get; set; }

        /// <summary>
        /// Starts the timer and initializes the progress tracking state.
        /// </summary>
        /// <remarks>This method ensures that the timer is started only once. It initializes the progress
        /// tracking by resetting the completion count and setting the start time. If the console output is redirected,
        /// the timer will not be started, and the console cursor visibility will remain unchanged.</remarks>
        public static void Start()
        {
            lock (_lock)
            {
                if (_timer != null)
                    return;

                Completed = 0;
                _startTime = DateTime.UtcNow;

                if (Console.IsOutputRedirected)
                    return;

                Console.CursorVisible = false;
                _timer = new Timer(Update, null, 0, 3000);
            }
        }

        /// <summary>
        /// Stops the timer and restores the console state.
        /// </summary>
        /// <remarks>This method disposes of the timer if it is active, clears the current console line, 
        /// and makes the console cursor visible. If the timer is already null, the method does nothing.</remarks>
        public static void Stop()
        {
            lock (_lock)
            {
                if (_timer == null)
                    return;

                _timer.Dispose();
                _timer = null;
                ClearLine();
                Console.CursorVisible = true;
            }
        }

        /// <summary>
        /// Updates the progress bar display in the console.
        /// </summary>
        /// <param name="_"></param>
        private static void Update(object? _)
        {
            lock (_lock)
            {
                if (Total == 0 || _startTime == null)
                    return;

                double pct = (double)Completed / Total;
                int barWidth = 20;

                int filled = (int)(pct * barWidth);
                int empty = barWidth - filled;

                string bar = "[" + new string('●', filled) + new string('○', empty) + "]";

                string eta = "";
                if (Completed >= 5 && pct >= 0.01)  // Only show ETA with enough data
                    eta = CalculateEta();

                string line = $"   Status: {bar} {pct * 100:F1}% complete {eta}";
                if (line.Length < 70)
                    line = line.PadRight(70); // pad to prevent leftover chars

                Console.Write("\r" + line);
                Console.Out.Flush();

                if (Completed >= Total)
                    Stop();
            }
        }

        /// <summary>
        /// Calculates the estimated time of arrival (ETA) based on the current progress.
        /// </summary>
        /// <returns></returns>
        private static string CalculateEta()
        {
            TimeSpan elapsed = DateTime.UtcNow - _startTime!.Value;
            double remainingItems = Total - Completed;
            double secondsPerItem = elapsed.TotalSeconds / Completed;
            double remainingSeconds = remainingItems * secondsPerItem;

            TimeSpan remaining = TimeSpan.FromSeconds(remainingSeconds);

            // Rounding rules
            if (remaining.TotalSeconds < 60)
                return $"(<{Math.Ceiling(remaining.TotalSeconds / 5) * 5} secs left)";
            if (remaining.TotalMinutes < 5)
                return $"(~{Math.Ceiling(remaining.TotalMinutes)} mins left)";
            if (remaining.TotalMinutes < 60)
                return $"(~{Math.Ceiling(remaining.TotalMinutes / 5) * 5} mins left)";
            return $"(~{Math.Ceiling(remaining.TotalMinutes / 15) * 15} mins left)";
        }


        /// <summary>
        /// Clears the current console line by overwriting it with spaces.
        /// </summary>
        /// <remarks>This method resets the content of the current console line by filling it with spaces 
        /// and moving the cursor back to the start of the line. It assumes the console buffer width  is sufficient to
        /// overwrite the entire line.</remarks>
        private static void ClearLine()
        {
            int width = Console.BufferWidth;
            if (width > 70) width = 70;

            Console.Write($"\r{new string(' ', width - 1)}\r");
        }
    }
}
