using FlowDesk.Ai.Entities;
using FlowDesk.Data;
using FlowDesk.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FlowDesk.CharacterizationTests.Ai;

public sealed class WorkItemAiDraftModelTests
{
    [Fact]
    public void Model_WorkItemIdIndex_IsUnique()
    {
        IEntityType entityType = GetEntityType();

        var index = Assert.Single(entityType.GetIndexes(), candidate =>
            candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(WorkItemAiDraft.WorkItemId)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Model_RowVersion_IsConcurrencyToken()
    {
        IEntityType entityType = GetEntityType();
        IProperty property = entityType.FindProperty(
            nameof(WorkItemAiDraft.RowVersion))!;

        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    [Fact]
    public void Model_WorkItemForeignKey_IsRequiredCascadeRelationship()
    {
        IEntityType entityType = GetEntityType();
        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(typeof(WorkItem), foreignKey.PrincipalEntityType.ClrType);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    private static IEntityType GetEntityType()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new AppDbContext(options);

        return context.Model.FindEntityType(typeof(WorkItemAiDraft))!;
    }
}
