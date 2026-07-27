namespace SoulCore.Core.Safety;

/// <summary>
/// Token/cost tracking meter for Phase 3. Records per-call usage and computes
/// cumulative spend against a monthly USD cap and an optional token ceiling.
/// CapExceeded is computed here; enforcement (refusing inference) is the caller's
/// responsibility via <see cref="GetSummary"/>. Pure in-memory logic; no external
/// dependencies.
/// </summary>
public sealed class SpendMeter
{
    private readonly decimal _inputRatePer1K;
    private readonly decimal _outputRatePer1K;
    private readonly decimal _monthlyCap;
    private readonly long _monthlyTokenCap;
    private long _totalTokensIn;
    private long _totalTokensOut;
    private decimal _estimatedCost;
    private readonly object _gate = new();

    /// <param name="inputRatePer1K">USD cost per 1K input tokens. Default $0.</param>
    /// <param name="outputRatePer1K">USD cost per 1K output tokens. Default $0.</param>
    /// <param name="monthlyCapUsd">Monthly spend cap in USD. Default $30.</param>
    /// <param name="monthlyTokenCap">
    /// Optional monthly token ceiling (in+out). Default 0 = disabled.
    /// </param>
    public SpendMeter(
        decimal inputRatePer1K = 0m,
        decimal outputRatePer1K = 0m,
        decimal monthlyCapUsd = 30m,
        long monthlyTokenCap = 0)
    {
        if (inputRatePer1K < 0m)
            throw new ArgumentOutOfRangeException(nameof(inputRatePer1K), "Rate must be non-negative.");
        if (outputRatePer1K < 0m)
            throw new ArgumentOutOfRangeException(nameof(outputRatePer1K), "Rate must be non-negative.");
        if (monthlyCapUsd <= 0m)
            throw new ArgumentOutOfRangeException(nameof(monthlyCapUsd), "Cap must be positive.");
        if (monthlyTokenCap < 0)
            throw new ArgumentOutOfRangeException(nameof(monthlyTokenCap), "Token cap must be non-negative.");

        _inputRatePer1K = inputRatePer1K;
        _outputRatePer1K = outputRatePer1K;
        _monthlyCap = monthlyCapUsd;
        _monthlyTokenCap = monthlyTokenCap;
    }

    /// <summary>
    /// Records token usage for a single inference call. Token counts must be non-negative.
    /// </summary>
    /// <param name="provider">The inference provider name (e.g. "ollama", "hermes").</param>
    /// <param name="tokensIn">Number of input/prompt tokens consumed.</param>
    /// <param name="tokensOut">Number of output/completion tokens generated.</param>
    public void RecordUsage(string provider, long tokensIn, long tokensOut)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider must be non-empty.", nameof(provider));
        if (tokensIn < 0)
            throw new ArgumentOutOfRangeException(nameof(tokensIn), "Token counts must be non-negative.");
        if (tokensOut < 0)
            throw new ArgumentOutOfRangeException(nameof(tokensOut), "Token counts must be non-negative.");

        var costIn = (decimal)tokensIn / 1000m * _inputRatePer1K;
        var costOut = (decimal)tokensOut / 1000m * _outputRatePer1K;
        var callCost = costIn + costOut;

        lock (_gate)
        {
            _totalTokensIn += tokensIn;
            _totalTokensOut += tokensOut;
            _estimatedCost += callCost;
        }
    }

    /// <summary>
    /// Returns the current cumulative spend summary. <c>CapExceeded</c> is true when
    /// <c>EstimatedCost</c> reaches the monthly USD cap, or when a configured token
    /// ceiling is reached (MonthlyTokenCap &gt; 0 and TotalTokens &gt;= MonthlyTokenCap).
    /// </summary>
    public SpendSummary GetSummary()
    {
        lock (_gate)
        {
            return BuildSummaryUnlocked();
        }
    }

    /// <summary>
    /// Resets all counters to zero. Intended for the start of a new billing cycle
    /// or for tests. Returns the summary just before reset.
    /// </summary>
    public SpendSummary Reset()
    {
        lock (_gate)
        {
            var before = BuildSummaryUnlocked();

            _totalTokensIn = 0;
            _totalTokensOut = 0;
            _estimatedCost = 0m;

            return before;
        }
    }

    private SpendSummary BuildSummaryUnlocked()
    {
        var costExceeded = _estimatedCost >= _monthlyCap;
        var tokenExceeded = _monthlyTokenCap > 0
            && (_totalTokensIn + _totalTokensOut) >= _monthlyTokenCap;

        return new SpendSummary(
            _totalTokensIn,
            _totalTokensOut,
            _estimatedCost,
            _monthlyCap,
            costExceeded || tokenExceeded,
            _monthlyTokenCap);
    }
}
