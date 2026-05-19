using System;

namespace EfUi.Core.Metadata;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class EfUiDisplayColumnAttribute : Attribute
{
    public EfUiDisplayColumnAttribute(string propertyName)
    {
        PropertyName = propertyName;
    }

    public string PropertyName { get; }
}
