

public interface IEditCommand
{
    string Name { get; }
    void Execute(ChartEditService service);
    void Undo(ChartEditService service);
}