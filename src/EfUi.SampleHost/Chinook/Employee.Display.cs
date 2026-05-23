using EfUi.Core.Metadata;

namespace EfUi.SampleHost.Chinook;

[EfUiDisplayColumn(nameof(FullName))]
public partial class Employee
{
    public string FullName => $"{FirstName} {LastName}";
}
