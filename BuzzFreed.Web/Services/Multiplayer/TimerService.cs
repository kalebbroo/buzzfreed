using System.Collections.Concurrent;
using BuzzFreed.Web.Utils;

namespace BuzzFreed.Web.Services.Multiplayer;

/// <summary>
/// Service for managing game timers (turn timeouts, countdown timers, etc.)
///
/// DESIGN:
/// - Uses System.Threading.Timer for efficient background timing
/// - Thread-safe timer management with ConcurrentDictionary
/// - Supports multiple concurrent timers per session
/// - Automatic cleanup when timers complete or are cancelled
///
/// TIMER TYPES:
/// - TurnTimer: Counts down during player's turn, triggers timeout
/// - ResultsTimer: Delay between turn end and next turn start
/// - CountdownTimer: Pre-game countdown (3, 2, 1, GO!)
/// - WarningTimer: Triggers warning notification before timeout
///
/// USAGE:
/// 1. StartTurnTimer(sessionId, timeLimit, onTimeout)
/// 2. Timer fires warning at 5 seconds remaining
/// 3. Timer fires timeout callback when time expires
/// 4. CancelTimer(sessionId) to stop early (player answered)
/// </summary>
public class TimerService : IDisposable
{
    private readonly ConcurrentDictionary<string, GameTimer> _timers = new();
    private readonly ILogger<TimerService> _logger;
    private bool _disposed;

    public TimerService(ILogger<TimerService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Start a turn timer for a game session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="durationSeconds">Turn duration in seconds</param>
    /// <param name="onTimeout">Callback when timer expires</param>
    /// <param name="onWarning">Callback for warning (5 seconds remaining)</param>
    /// <returns>Timer ID</returns>
    public string StartTurnTimer(
        string sessionId,
        int durationSeconds,
        Action<string> onTimeout,
        Action<string, int>? onWarning = null)
    {
        string timerId = $"turn:{sessionId}";

        // Cancel any existing timer for this session
        CancelTimer(timerId);

        var timer = new GameTimer
        {
            TimerId = timerId,
            SessionId = sessionId,
            Type = TimerType.Turn,
            StartTime = DateTime.UtcNow,
            DurationSeconds = durationSeconds,
            OnTimeout = onTimeout,
            OnWarning = onWarning
        };

        // Create warning timer (5 seconds before timeout)
        if (onWarning != null && durationSeconds > 5)
        {
            int warningDelay = (durationSeconds - 5) * 1000;
            timer.WarningTimer = new Timer(
                _ => TriggerWarning(timerId, 5),
                null,
                warningDelay,
                Timeout.Infinite);
        }

        // Create main timeout timer
        timer.MainTimer = new Timer(
            _ => TriggerTimeout(timerId),
            null,
            durationSeconds * 1000,
            Timeout.Infinite);

        _timers[timerId] = timer;

        Logs.Debug($"Turn timer started: {timerId} ({durationSeconds}s)");

        return timerId;
    }

    /// <summary>
    /// Start a results display timer (delay before next turn)
    /// </summary>
    public string StartResultsTimer(
        string sessionId,
        int durationSeconds,
        Action<string> onComplete)
    {
        string timerId = $"results:{sessionId}";

        CancelTimer(timerId);

        var timer = new GameTimer
        {
            TimerId = timerId,
            SessionId = sessionId,
            Type = TimerType.Results,
            StartTime = DateTime.UtcNow,
            DurationSeconds = durationSeconds,
            OnTimeout = onComplete
        };

        timer.MainTimer = new Timer(
            _ => TriggerTimeout(timerId),
            null,
            durationSeconds * 1000,
            Timeout.Infinite);

        _timers[timerId] = timer;

        Logs.Debug($"Results timer started: {timerId} ({durationSeconds}s)");

        return timerId;
    }

    /// <summary>
    /// Start a countdown timer (pre-game countdown)
    /// </summary>
    public string StartCountdownTimer(
        string sessionId,
        int countdownSeconds,
        Action<string, int> onTick,
        Action<string> onComplete)
    {
        string timerId = $"countdown:{sessionId}";

        CancelTimer(timerId);

        var timer = new GameTimer
        {
            TimerId = timerId,
            SessionId = sessionId,
            Type = TimerType.Countdown,
            StartTime = DateTime.UtcNow,
            DurationSeconds = countdownSeconds,
            OnTimeout = onComplete,
            RemainingSeconds = countdownSeconds
        };

        // Tick every second
        timer.MainTimer = new Timer(
            _ => HandleCountdownTick(timerId, onTick),
            null,
            1000,
            1000);

        _timers[timerId] = timer;

        Logs.Debug($"Countdown timer started: {timerId} ({countdownSeconds}s)");

        return timerId;
    }

    /// <summary>
    /// Cancel a timer
    /// </summary>
    public bool CancelTimer(string timerId)
    {
        if (_timers.TryRemove(timerId, out GameTimer? timer))
        {
            timer.Dispose();
            Logs.Debug($"Timer cancelled: {timerId}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Cancel all timers for a session
    /// </summary>
    public void CancelSessionTimers(string sessionId)
    {
        var sessionTimers = _timers.Keys
            .Where(k => k.Contains(sessionId))
            .ToList();

        foreach (string timerId in sessionTimers)
        {
            CancelTimer(timerId);
        }

        Logs.Debug($"All timers cancelled for session: {sessionId}");
    }

    /// <summary>
    /// Get remaining time for a timer
    /// </summary>
    public int? GetRemainingSeconds(string timerId)
    {
        if (_timers.TryGetValue(timerId, out GameTimer? timer))
        {
            var elapsed = DateTime.UtcNow - timer.StartTime;
            var remaining = timer.DurationSeconds - (int)elapsed.TotalSeconds;
            return Math.Max(0, remaining);
        }

        return null;
    }

    /// <summary>
    /// Check if a timer is active
    /// </summary>
    public bool IsTimerActive(string timerId)
    {
        return _timers.ContainsKey(timerId);
    }

    /// <summary>
    /// Get timer for a session's turn
    /// </summary>
    public string GetTurnTimerId(string sessionId) => $"turn:{sessionId}";

    /// <summary>
    /// Get timer for a session's results display
    /// </summary>
    public string GetResultsTimerId(string sessionId) => $"results:{sessionId}";

    // Private methods

    private void TriggerWarning(string timerId, int secondsRemaining)
    {
        try
        {
            if (_timers.TryGetValue(timerId, out GameTimer? timer))
            {
                Logs.Debug($"Timer warning: {timerId} ({secondsRemaining}s remaining)");
                timer.OnWarning?.Invoke(timer.SessionId, secondsRemaining);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering timer warning for {TimerId}", timerId);
        }
    }

    private void TriggerTimeout(string timerId)
    {
        try
        {
            if (_timers.TryRemove(timerId, out GameTimer? timer))
            {
                Logs.Info($"Timer expired: {timerId}");
                timer.OnTimeout?.Invoke(timer.SessionId);
                timer.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering timer timeout for {TimerId}", timerId);
        }
    }

    private void HandleCountdownTick(string timerId, Action<string, int> onTick)
    {
        try
        {
            if (_timers.TryGetValue(timerId, out GameTimer? timer))
            {
                timer.RemainingSeconds--;

                if (timer.RemainingSeconds > 0)
                {
                    onTick(timer.SessionId, timer.RemainingSeconds);
                }
                else
                {
                    // Countdown complete
                    if (_timers.TryRemove(timerId, out _))
                    {
                        timer.OnTimeout?.Invoke(timer.SessionId);
                        timer.Dispose();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling countdown tick for {TimerId}", timerId);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        foreach (var timer in _timers.Values)
        {
            timer.Dispose();
        }

        _timers.Clear();

        Logs.Debug("TimerService disposed");
    }
}

/// <summary>
/// Types of game timers
/// </summary>
public enum TimerType
{
    Turn,       // Player turn timeout
    Results,    // Results display delay
    Countdown,  // Pre-game countdown
    Warning     // Warning before timeout
}

/// <summary>
/// Internal timer state
/// </summary>
internal class GameTimer : IDisposable
{
    public string TimerId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public TimerType Type { get; set; }
    public DateTime StartTime { get; set; }
    public int DurationSeconds { get; set; }
    public int RemainingSeconds { get; set; }

    public Timer? MainTimer { get; set; }
    public Timer? WarningTimer { get; set; }

    public Action<string>? OnTimeout { get; set; }
    public Action<string, int>? OnWarning { get; set; }

    public void Dispose()
    {
        MainTimer?.Dispose();
        WarningTimer?.Dispose();
    }
}
