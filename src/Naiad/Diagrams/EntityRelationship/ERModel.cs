namespace Naiad.Diagrams.EntityRelationship;

public class ErModel : DiagramBase
{
    public List<Entity> Entities { get; } = [];
    public List<Relationship> Relationships { get; } = [];
}