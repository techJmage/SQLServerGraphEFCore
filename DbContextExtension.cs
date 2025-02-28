using System.Data;
using Microsoft.EntityFrameworkCore;

namespace SQLServerGraphEFCore;

public static class DbContextExtension
{
    public static IQueryBuilder BuildEdgeQuery<F, T, E>(this DbContext ctx, string queryText, F fromParameters, T toParameters, E parameters, CommandType commandType = CommandType.Text)
    {
        var proc = ctx.LoadQuery(queryText, commandType);
        proc = AddParams(fromParameters, proc);
        proc = AddParams(toParameters, proc);
        proc = AddParams(parameters, proc);
        return proc;
    }

    public static IQueryBuilder BuildQuery<T>(this DbContext ctx, string queryText, T parameters, CommandType commandType = CommandType.Text)
    {
        var proc = ctx.LoadQuery(queryText, commandType);
        proc = AddParams(parameters, proc);
        return proc;
    }

    public static async IAsyncEnumerable<T> ExecuteEnumAsync<TP, T>(this DbContext ctx, string query, TP parameters,
            CommandType commandType = CommandType.Text, CancellationToken? cancellationToken = null) where T : new()
    {
        var q = ctx.BuildQuery(query, parameters, commandType);
        cancellationToken ??= CancellationToken.None;
        var r = await q.ExecReaderAsync(cancellationToken.Value);
        await foreach (T p in r.ToEnumAsync<T>(cancellationToken.Value).ConfigureAwait(false))
            yield return p;
    }

    public static IEnumerable<T> ExecuteEnumerable<TP, T>(this DbContext ctx, string queryText, TP parameters, CommandType commandType = CommandType.Text)
    {
        IEnumerable<T> result = [];
        ctx.BuildQuery(queryText, parameters, commandType).Exec(r => result = r.OfType<T>());
        return result;
    }

    public static IEnumerable<T> ExecuteEnumerable<T>(this DbContext ctx, string queryText, CommandType commandType = CommandType.Text)
    {
        IEnumerable<T> result = [];
        ctx.LoadQuery(queryText, commandType).Exec(r => result = r.OfType<T>());
        return result;
    }

    public static List<T> ExecuteList<TP, T>(this DbContext ctx, string query, TP parameters, CommandType commandType = CommandType.Text) where T : new()
    {
        var result = new List<T>();
        ctx.BuildQuery(query, parameters, commandType).Exec(r => result = r.ToList<T>());
        return result;
    }

    public static List<T> ExecuteList<T>(this DbContext ctx, string queryText, CommandType commandType = CommandType.Text) where T : new()
    {
        var result = new List<T>();
        ctx.LoadQuery(queryText, commandType).Exec(r => result = r.ToList<T>());
        return result;
    }

    public static async Task<List<T>> ExecuteListAsync<TP, T>(this DbContext ctx, string query, TP parameters, CommandType commandType = CommandType.Text) where T : new()
    {
        var result = new List<T>();
        var q = ctx.BuildQuery(query, parameters, commandType);
        await q.ExecAsync(async r => result = await r.ToListAsync<T>()).ConfigureAwait(false);
        return result;
    }

    public static int ExecuteNonQuery<T>(this DbContext ctx, string queryText, T parameters, CommandType commandType = CommandType.Text) =>
            ctx.BuildQuery(queryText, parameters, commandType).ExecNonQuery();

    public static async Task<int> ExecuteNonQueryAsync<T>(this DbContext ctx, string queryText, T parameters, CommandType commandType = CommandType.Text) =>
        await ctx.BuildQuery(queryText, parameters, commandType).ExecNonQueryAsync().ConfigureAwait(false);

    public static T ExecuteScalar<TP, T>(this DbContext ctx, string queryText, TP parameters)
    {
        ctx.BuildQuery(queryText, parameters).ExecScalar(out T result);
        return result;
    }

    public static T FirstOrDefault<TP, T>(this DbContext ctx, string queryText, TP parameters, CommandType commandType = CommandType.Text) where T : class, new()
    {
        var result = new T();
        ctx.BuildQuery(queryText, parameters, commandType).Exec(r => result = r.FirstOrDefault<T>());
        return result;
    }

    /// <summary>
    /// Load a stored procedure
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="name">Procedure's name</param>
    /// <returns></returns>
    public static IQueryBuilder LoadQuery(this DbContext ctx, string name, CommandType commandType) => new QueryBuilder(ctx, name, commandType);
    public static T SingleOrDefault<TP, T>(this DbContext ctx, string queryText, TP parameters, CommandType commandType = CommandType.Text) where T : class, new()
    {
        var result = new T();
        ctx.BuildQuery(queryText, parameters, commandType).Exec(r => result = r.SingleOrDefault<T>());
        return result;
    }

    public static async Task<T> SingleOrDefaultAsync<TP, T>(this DbContext ctx, string queryText, TP parameters, CommandType commandType = CommandType.Text) where T : class, new()
    {
        var result = new T();
        var q = ctx.BuildQuery(queryText, parameters, commandType);
        await q.ExecAsync(async r => result = await r.SingleOrDefaultAsync<T>()).ConfigureAwait(false);
        return result;
    }

    private static IQueryBuilder AddParams<E>(E parameters, IQueryBuilder proc)
    {
        if (parameters == null)
            return proc;
        if (parameters is IDictionary<string, object> parDict)
            foreach (var p in parDict)
                proc = proc.AddParam(p.Key, p.Value, p.Value.GetType());
        else
        {
            var properties = parameters.GetType().GetProperties();//typeof T will not work for anonymous
            foreach (var p in properties)
                proc = proc.AddParam(p.Name, p.GetValue(parameters), p.PropertyType);
        }
        return proc;
    }
}
