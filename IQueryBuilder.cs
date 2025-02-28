using System.Data.Common;
using System.Runtime.CompilerServices;

namespace SQLServerGraphEFCore;

/// <summary>
/// Stored procedure builder.
/// </summary>
public interface IQueryBuilder : IDisposable
{
    /// <summary>
    /// Add input parameter.
    /// </summary>
    /// <typeparam name="T">Type of the parameter. Can be nullable.</typeparam>
    /// <param name="name">Name of the parameter.</param>
    /// <param name="val">Value of the parameter.</param>
    IQueryBuilder AddParam<T>(string name, T val);

    /// <summary>
    /// Add input/output parameter.
    /// </summary>
    /// <typeparam name="T">Type of the parameter. Can be nullable.</typeparam>
    /// <param name="name">Name of the parameter.</param>
    /// <param name="val">Value of the parameter.</param>
    /// <param name="outParam">Created parameter. Value will be populated after calling <see cref="Exec(Action{DbDataReader})"/>.</param>
    /// <param name="size">Number of decimal places to which <see cref="IOutParam{T}.Value"/> is resolved.</param>
    /// <param name="precision">Number of digits used to represent the <see cref="IOutParam{T}.Value"/> property.</param>
    /// <param name="scale">Maximum size, in bytes, of the data within the column.</param>
    IQueryBuilder AddParam<T>(string name, T val, out IOutParam<T> outParam, int size = 0, byte precision = 0, byte scale = 0);

    /// <summary>
    /// Add output parameter.
    /// </summary>
    /// <typeparam name="T">Type of the parameter. Can be nullable.</typeparam>
    /// <param name="name">Name of the parameter.</param>
    /// <param name="outParam">Created parameter. Value will be populated after calling <see cref="Exec(Action{DbDataReader})"/>.</param>
    /// <param name="size">Number of decimal places to which <see cref="IOutParam{T}.Value"/> is resolved.</param>
    /// <param name="precision">Number of digits used to represent the <see cref="IOutParam{T}.Value"/> property.</param>
    /// <param name="scale">Maximum size, in bytes, of the data within the column.</param>
    IQueryBuilder AddParam<T>(string name, out IOutParam<T> outParam, int size = 0, byte precision = 0, byte scale = 0);

    /// <summary>
    /// Add pre configured DB query execution parameter directly command.
    /// </summary>
    /// <param name="parameter">DB query execution parameter <see cref="DbParameter"/>.</param>
    IQueryBuilder AddParam(DbParameter parameter);

    IQueryBuilder AddParam(string name, object value, Type type);

    /// <summary>
    /// Execute the stored procedure.
    /// </summary>
    /// <param name="action">Actions to do with the result sets.</param>
    void Exec(Action<DbDataReader> action);

    /// <summary>
    /// Execute the stored procedure.
    /// </summary>
    /// <param name="action">Actions to do with the result sets.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="TaskCanceledException">When <paramref name="cancellationToken"/> was cancelled.</exception>
    Task ExecAsync(Func<DbDataReader, Task> action, CancellationToken cancellationToken = default);

    IAsyncEnumerable<T> ExecAsync<T>(Func<DbDataReader, IAsyncEnumerable<T>> action, CancellationToken cancellationToken) where T : new();

    /// <summary>
    /// Execute the stored procedure.
    /// </summary>
    /// <returns>The number of rows affected.</returns>
    int ExecNonQuery();

    /// <summary>
    /// Execute the stored procedure.
    /// </summary>
    /// <returns>The number of rows affected.</returns>
    Task<int> ExecNonQueryAsync();

    /// <summary>
    /// Execute the stored procedure.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="TaskCanceledException">When <paramref name="cancellationToken"/> was cancelled.</exception>
    /// <returns>The number of rows affected.</returns>
    Task<int> ExecNonQueryAsync(CancellationToken cancellationToken);

    Task<DbDataReader> ExecReaderAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Execute the stored procedure and return the first column of the first row.
    /// </summary>
    /// <typeparam name="T">Type of the scalar value.</typeparam>
    /// <param name="val">The first column of the first row in the result set.</param>
    void ExecScalar<T>(out T val);

    /// <summary>
    /// Execute the stored procedure and return the first column of the first row.
    /// </summary>
    /// <typeparam name="T">Type of the scalar value.</typeparam>
    /// <param name="action">Action applied to the first column of the first row in the result set.</param>
    Task ExecScalarAsync<T>(Action<T> action);

    /// <summary>
    /// Execute the stored procedure and return the first column of the first row.
    /// </summary>
    /// <typeparam name="T">Type of the scalar value.</typeparam>.
    /// <param name="action">Action applied to the first column of the first row in the result set.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="TaskCanceledException">When <paramref name="cancellationToken"/> was cancelled.</exception>
    Task ExecScalarAsync<T>(Action<T> action, CancellationToken cancellationToken);

    /// <summary>
    /// Add return value parameter.
    /// </summary>
    /// <typeparam name="T">Type of the parameter. Can be nullable.</typeparam>
    /// <param name="retParam">Created parameter. Value will be populated after calling <see cref="Exec(Action{DbDataReader})"/>.</param>
    IQueryBuilder ReturnValue<T>(out IOutParam<T> retParam);

    /// <summary>
    /// Set the wait time before terminating the attempt to execute the stored procedure and generating an error.
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    IQueryBuilder SetTimeout(int timeout);
}
