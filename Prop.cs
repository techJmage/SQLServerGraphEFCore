namespace SQLServerGraphEFCore;

internal struct Prop
{
    public int ColumnOrdinal { get; set; }
    public Action<object, object> Setter { get; set; }
}
