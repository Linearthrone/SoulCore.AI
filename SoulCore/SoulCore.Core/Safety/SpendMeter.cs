namespace SoulCore.Core.Safety;

/// <summary>
/// Token/cost tracking meter for Phase 3. Records per-call usage and computes
/// cumulative spend against a monthly cap. Does NOT enforce the cap yet — that
/// is a future gate. Pure in-memory logic; no external dependencies.
/// </summary>
public sealed class SpendMeter
{
    private readonly decimal _inputRatePer1K;
    private readonly decimal _outputRatePer1K;
    private readonly decimal _monthlyCap;
    private long _totalTokensIn;
    private long _totalTokensOut;
    private decimal _estimatedCost;
    private readonly object _gate = new();

    /// <param name="inputRatePer1K">USD cost per 1K input tokens. Default $0.</param>
    /// <param name="outputRatePer1K">USD cost per 1K output tokens. Default $0.</param>
    /// <param name="monthlyCapUsd">Monthly spend cap in USD. Default $30.</param>
    public SpendMeter(
        decimal inputRatePer1K = 0m,
        decimal outputRatePer1K = 0m,
        decimal monthlyCapUsd = 30m)
    {
        if (inputRatePer1K < 0m)
            throw new ArgumentOutOfRangeException(nameof(inputRatePer1K), "Rate must be non-negative.");
        if (outputRatePer1K < 0m)
            throw new ArgumentOutOfRangeException(nameof(outputRatePer1K), "Rate must be non-negative.");
        if (monthlyCapUsd <= 0m)
            throw new ArgumentOutOfRangeException(nameof(monthlyCapUsd), "Cap must be positive.");

        _inputRatePer1K = inputRatePer1K;
        _outputRatePer1K = outputRatePer1K;
        _monthlyCap = monthlyCapUsd;
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
    /// Returns the current cumulative spend summary. <c>CapExceeded</c> is true
    /// when <c>EstimatedCost</c> reaches (or exceeds) the monthly cap.
    /// </summary>
    public SpendSummary GetSummary()
    {
        lock (_gate)
        {
            return new SpendSummary(
                _totalTokensIn,
                _totalTokensOut,
                _estimatedCost,
                _monthlyCap,
                _estimatedCost >= _monthlyCap);
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
            var before = new SpendSummary(
                _totalTokensIn,
                _totalTokensOut,
                _estimatedCost,
                _monthlyCap,
                _estimatedCost >= _monthlyCap);

            _totalTokensIn = 0;
            _totalTokensOut = 0;
            _estimatedCost = 0m;

            return before;
        }
    }
}
