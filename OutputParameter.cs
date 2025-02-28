using System.Data.Common;

namespace SQLServerGraphEFCore;

internal class OutputParam<T>(DbParameter param) : IOutParam<T>
{
    public T Value
    {
        get
        {
            if (param.Value is DBNull)
            {
                if (default(T) == null)
                    return default;
                else
                    throw new InvalidOperationException($"{param.ParameterName} is null and can't be assigned to a non-nullable type");
            }

            var nullableUnderlyingType = Nullable.GetUnderlyingType(typeof(T));
            if (nullableUnderlyingType != null)
                return (T)Convert.ChangeType(param.Value, nullableUnderlyingType);
            return (T)Convert.ChangeType(param.Value, typeof(T));
        }
    }

    public override string ToString() => param.Value.ToString();
}
