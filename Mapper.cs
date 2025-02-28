﻿using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SQLServerGraphEFCore;

/// <summary>
/// Mapper <see cref="DbDataReader"/> to model of type <see cref="T"/>
/// </summary>
/// <typeparam name="T">Model type</typeparam>
internal class Mapper<T> where T : new()
{
    /// <summary>
    /// Contains different columns set information mapped to type <typeparamref name="T"/>.
    /// </summary>
    private static readonly ConcurrentDictionary<int, Prop[]> PropertiesCache = new();

    private readonly Prop[] _properties;
    private readonly DbDataReader _reader;
    public Mapper(DbDataReader reader)
    {
        _reader = reader;
        _properties = MapColumnsToProperties();
    }

    /// <summary>
    /// Map <see cref="DbDataReader"/> to a T and apply an action on it for each row
    /// </summary>
    /// <param name="action">Action to apply to each row</param>
    public void Map(Action<T> action)
    {
        while (_reader.Read())
        {
            T row = MapNextRow();
            action(row);
        }
    }

    /// <summary>
    /// Map <see cref="DbDataReader"/> to a T and apply an action on it for each row
    /// </summary>
    /// <param name="action">Action to apply to each row</param>
    public Task MapAsync(Action<T> action) =>
        MapAsync(action, CancellationToken.None);

    /// <summary>
    /// Map <see cref="DbDataReader"/> to a T and apply an action on it for each row
    /// </summary>
    /// <param name="action">Action to apply to each row</param>
    /// <param name="cancellationToken">The cancellation instruction, which propagates a notification that operations should be canceled</param>
    public async Task MapAsync(Action<T> action, CancellationToken cancellationToken)
    {
        while (await _reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            T row = await MapNextRowAsync(cancellationToken).ConfigureAwait(false);
            action(row);
        }
    }
    public async IAsyncEnumerable<T> MapAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            T row = await MapNextRowAsync(cancellationToken).ConfigureAwait(false);
            yield return row;
        }
    }

    public T MapNextRow()
    {
        T row = new T();
        for (int i = 0; i < _properties.Length; ++i)
        {
            object value = _reader.IsDBNull(_properties[i].ColumnOrdinal) ? null : _reader.GetValue(_properties[i].ColumnOrdinal);
            _properties[i].Setter(row, value);
        }
        return row;
    }

    public Task<T> MapNextRowAsync() => MapNextRowAsync(CancellationToken.None);

    public async Task<T> MapNextRowAsync(CancellationToken cancellationToken)
    {
        T row = new T();
        for (int i = 0; i < _properties.Length; ++i)
        {
            object value = await _reader.IsDBNullAsync(_properties[i].ColumnOrdinal, cancellationToken).ConfigureAwait(false)
                ? null
                : _reader.GetValue(_properties[i].ColumnOrdinal);
            _properties[i].Setter(row, value);
        }
        return row;
    }

    internal static int ComputePropertyKey(IEnumerable<string> columns)
    {
        unchecked
        {
            int hashCode = 17;
            foreach (string column in columns)
            {
                hashCode = (hashCode * 31) + column.GetHashCode();
            }
            return hashCode;
        }
    }

    private Prop[] MapColumnsToProperties()
    {
        Type modelType = typeof(T);

        string[] columns = new string[_reader.FieldCount];
        for (int i = 0; i < _reader.FieldCount; ++i)
            columns[i] = _reader.GetName(i);

        int propKey = ComputePropertyKey(columns);
        if (PropertiesCache.TryGetValue(propKey, out Prop[] s))
            return s;

        var properties = new List<Prop>(columns.Length);
        for (int i = 0; i < columns.Length; i++)
        {
            string name = columns[i].Replace("_", "");
            PropertyInfo prop = modelType.GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
                continue;

            ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
            ParameterExpression value = Expression.Parameter(typeof(object), "value");

            // "x as T" is faster than "(T) x" if x is a reference type
            UnaryExpression instanceCast = prop.DeclaringType.IsValueType
                ? Expression.Convert(instance, prop.DeclaringType)
                : Expression.TypeAs(instance, prop.DeclaringType);

            UnaryExpression valueCast = prop.PropertyType.IsValueType
                ? Expression.Convert(value, prop.PropertyType)
                : Expression.TypeAs(value, prop.PropertyType);

            MethodCallExpression setterCall = Expression.Call(instanceCast, prop.GetSetMethod(), valueCast);
            var setter = (Action<object, object>)Expression.Lambda(setterCall, instance, value).Compile();

            properties.Add(new Prop
            {
                ColumnOrdinal = i,
                Setter = setter,
            });
        }
        Prop[] propertiesArray = [.. properties];
        PropertiesCache[propKey] = propertiesArray;
        return propertiesArray;
    }
}
