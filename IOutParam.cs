namespace SQLServerGraphEFCore;

public interface IOutParam<T>
{
    T Value { get; }
}
