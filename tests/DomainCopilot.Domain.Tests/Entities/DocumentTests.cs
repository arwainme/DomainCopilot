using DomainCopilot.Domain.Entities;
using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Domain.Tests.Entities;

public class DocumentTests
{
    [Fact]
    public void CreateDocument_ShouldSetRequiredProperties()
    {
        // Arrange
        var title = "Citizen Services Guide";
        var source = "Government Portal";
        var version = "2026.1";

        // Act
        var document = new Document(title, source, version);

        // Assert
        Assert.NotEqual(Guid.Empty, document.Id);
        Assert.Equal(title, document.Title);
        Assert.Equal(source, document.Source);
        Assert.Equal(version, document.Version);
        Assert.Equal(DocumentStatus.Pending, document.Status);
        Assert.Null(document.FailureReason);
        Assert.NotEqual(default, document.CreatedAt);
        Assert.NotEqual(default, document.UpdatedAt);
    }

    [Fact]
    public void CreateDocument_ShouldGenerateUniqueIds()
    {
        var firstDocument = new Document(
            "Citizen Services Guide",
            "Government Portal",
            "2026.1");

        var secondDocument = new Document(
            "Citizen Services Guide",
            "Government Portal",
            "2026.1");

        Assert.NotEqual(firstDocument.Id, secondDocument.Id);
    }

    [Fact]
    public void MarkAsProcessing_ShouldSetProcessingStatus()
    {
        var document = new Document(
            "Citizen Services Guide",
            "Government Portal");

        document.MarkAsProcessing();

        Assert.Equal(DocumentStatus.Processing, document.Status);
        Assert.Null(document.FailureReason);
    }

    [Fact]
    public void MarkAsCompleted_ShouldSetCompletedStatus()
    {
        var document = new Document(
            "Citizen Services Guide",
            "Government Portal");

        document.MarkAsProcessing();
        document.MarkAsCompleted();

        Assert.Equal(DocumentStatus.Completed, document.Status);
        Assert.Null(document.FailureReason);
    }

    [Fact]
    public void MarkAsFailed_ShouldSetFailedStatusAndReason()
    {
        var document = new Document(
            "Citizen Services Guide",
            "Government Portal");

        document.MarkAsProcessing();
        document.MarkAsFailed("Failed to extract document text.");

        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal(
            "Failed to extract document text.",
            document.FailureReason);
    }
}