namespace EfUi.SampleHost.Models;

public sealed class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<User> Users { get; set; } = new();
}
