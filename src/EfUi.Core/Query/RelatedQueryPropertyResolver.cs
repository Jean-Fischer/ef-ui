using EfUi.Core.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.Core.Query;

internal static class RelatedQueryPropertyResolver
{
    private static readonly string[] PreferredPropertyNames = ["Name", "Title", "Email"];

    public static IProperty? Find(IEntityType dependentEntityType, EntityPropertyMetadata property)
    {
        var foreignKey = dependentEntityType.GetForeignKeys()
            .Where(candidate => candidate.Properties.Count == 1)
            .SingleOrDefault(candidate => candidate.Properties[0].Name == property.Name);
        if (foreignKey is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(property.RelatedDisplayPropertyName))
        {
            return foreignKey.PrincipalEntityType.FindProperty(property.RelatedDisplayPropertyName);
        }

        foreach (var preferredPropertyName in PreferredPropertyNames)
        {
            var preferredProperty = foreignKey.PrincipalEntityType.FindProperty(preferredPropertyName);
            if (preferredProperty is not null)
            {
                return preferredProperty;
            }
        }

        return foreignKey.PrincipalEntityType.FindPrimaryKey()?.Properties.SingleOrDefault();
    }
}
