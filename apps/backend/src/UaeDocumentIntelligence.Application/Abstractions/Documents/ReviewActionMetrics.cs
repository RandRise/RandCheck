namespace UaeDocumentIntelligence.Application.Abstractions;

public sealed record ReviewActionMetrics(int SessionCount, int CompletedSessionCount,
    int ActionCount, int ConfirmedCount, int CorrectedCount, int AddedCount,
    int ClearedCount, double? MachineChangeRate);
