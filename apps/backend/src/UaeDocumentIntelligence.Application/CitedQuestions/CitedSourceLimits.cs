namespace UaeDocumentIntelligence.Application.CitedQuestions;

public static class CitedSourceLimits
{
    // Keep the review picker bounded, independently of the number of saved reviews.
    public const int PageSize = 10;
    public const int MaximumOffset = 10000;
    public const int MaximumSearchLength = 200;
}
