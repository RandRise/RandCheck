namespace UaeDocumentIntelligence.Domain.Model;

public enum ActorKind { Human, System }
public enum DocumentProcessingStatus { Stored, Processing, ReadyForReview, ExtractionFailed, ProcessingCancelled }
public enum ExtractionRunStatus { Queued, Running, Succeeded, Partial, Failed, Cancelled }
public enum FieldValueType { String, Decimal, Date, Currency, Identifier }
public enum ExtractionWarningSeverity { Info, Degraded }
public enum ReviewStatus { Pending, InProgress, Completed, Cancelled }
public enum ReviewActionType { Confirmed, Corrected, Added, Cleared }
