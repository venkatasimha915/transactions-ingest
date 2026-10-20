namespace Transactions.Core.Sources;

/// <summary>A transaction exactly as published by a source system (the sample JSON contract).</summary>
public sealed record SourceTransaction(
    string SourceSystem,
    int TransactionId,
    string? CardNumber,
    string LocationCode,
    string ProductName,
    decimal Amount,
    DateTime TransactionTime);
