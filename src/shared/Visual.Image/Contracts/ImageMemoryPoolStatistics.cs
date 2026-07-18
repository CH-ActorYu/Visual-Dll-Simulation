namespace Visual.Image.Contracts;

public readonly record struct ImageMemoryPoolStatistics(
    long TotalRented,
    long TotalReturned,
    long Outstanding,
    long PeakOutstanding);
