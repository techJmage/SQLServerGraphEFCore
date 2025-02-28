using System.Data;
using Microsoft.EntityFrameworkCore;
using Utility;

namespace SQLServerGraphEFCore;

public class CrudService(string connectionString) : DisposableAsync, ICrudService
{
    protected static readonly string[] sysColumns = ["node_id", "edge_id", "from_id", "to_id"];
    protected readonly string connectionString = connectionString;
    protected HashSet<IAsyncDisposable> asyncDisposables = [];
    protected HashSet<IDisposable> disposables = [];
    public virtual DbContext CreateDbContext()
    {
        DbContextOptionsBuilder builder = new();
        builder.UseSqlServer(connectionString);
        return new DbContext(builder.Options);
    }

    public override void ReleaseResources()
    {
        foreach (var item in disposables)
            item.Dispose();
    }
    public override async ValueTask ReleaseResourcesAsync()
    {
        var tasks = asyncDisposables.Select(p => p.DisposeAsync().AsTask());
        await Task.WhenAll(tasks).ConfigureAwait(false);
        await base.ReleaseResourcesAsync();
    }
    #region RETRIEVE METHODS
    public bool AnyEdge<F, T, E>(string edgeName, string fromNode, string toNode, F fromParams, T toParams, E parameters)
    {
        if (parameters == null)
            return default;
        var fromId = GetNodeId(fromNode, fromParams);
        var toId = GetNodeId(toNode, toParams);
        if (fromId == null && toId == null)
            return default;
        var wh = GetWhClauseForEdge(fromId, toId, parameters);
        var q = $"SELECT COUNT(*) FROM (SELECT TOP 1 * FROM {edgeName} {wh}) d";
        using var ctx = CreateDbContext();
        return ctx.ExecuteScalar<dynamic, int>(q, parameters) > 0;
    }

    public bool AnyNode<T>(string nodeName, T parameters)
    {
        if (parameters == null || parameters.GetType().GetProperties().Length == 0)
            return false;
        using var ctx = CreateDbContext();
        return ctx.ExecuteScalar<dynamic, int>($"SELECT COUNT(*) FROM (SELECT TOP 1 * FROM {nodeName} {BuildWhereClause(parameters)}) d", parameters) > 0;
    }
    #endregion

    #region INSERT METHODS

    public async Task<int> InsertEdgeAsync<F, T, E>(string edgeName, string fromNode, string toNode, F fromParams, T toParams, E parameters)
    {
        if (parameters == null)
            return 0;
        string q = GetInsertEdgeQuery(edgeName, fromNode, toNode, fromParams, toParams, parameters);
        using var ctx = CreateDbContext();
        return await ctx.ExecuteNonQueryAsync(q, parameters).ConfigureAwait(false);
    }

    public int InsertNode<T>(string nodeName, T parameters)
    {
        if (parameters == null)
            return 0;
        var valueNames = GetValueNames(parameters);
        using var ctx = CreateDbContext();
        var ret = ctx.ExecuteNonQuery($"INSERT INTO {nodeName} VALUES({valueNames})", parameters);
        return ret;
    }

    public async Task<int> InsertNodeAsync<T>(string nodeName, T parameters)
    {
        if (parameters == null)
            return 0;
        var columnNames = GetColumnNames(parameters);
        var valueNames = GetValueNames(parameters);
        using var ctx = CreateDbContext();
        return await ctx.ExecuteNonQueryAsync($"INSERT INTO {nodeName}({columnNames}) VALUES({valueNames})", parameters).ConfigureAwait(false);
    }
    #endregion

    #region UPDATE METHODS

    public async Task<int> UpdateEdgeAsync<F, T, E, S>(string edgeName, string fromNode, string toNode, F fromParams, T toParams, E parameters, S whereClauseParameters)
    {
        if (parameters == null)
            return 0;
        string q = GetUpdateEdgeQuery(edgeName, fromNode, toNode, fromParams, toParams, parameters, whereClauseParameters);
        using var ctx = CreateDbContext();
        return await ctx.ExecuteNonQueryAsync(q, parameters).ConfigureAwait(false);
    }

    public async Task<int> UpdateNodeAsync<T, S>(string nodeName, T parameters, S whereClauseParameters)
    {
        if (parameters == null)
            return 0;
        using var ctx = CreateDbContext();
        return await ctx.ExecuteNonQueryAsync($"UPDATE {nodeName} SET {string.Join(" , ", AssignParamValue(parameters))} {BuildWhereClause(whereClauseParameters)};", parameters).ConfigureAwait(false);
    }
    public async Task<int> UpdateNodeByNodeIdAsync<T>(string nodeName, T parameters, object nodeId)
    {
        if (parameters == null)
            return 0;
        var q = $"UPDATE {nodeName} SET {string.Join(" , ",
            AssignParamValue(parameters, isUpdateClause: true).Where(p => !string.IsNullOrWhiteSpace(p)))} WHERE {GetWhereClauseForNodeId(nodeId, "node")};";
        using var ctx = CreateDbContext();
        return await ctx.ExecuteNonQueryAsync(q, parameters).ConfigureAwait(false);
    }
    #endregion

    #region DELETE METHODS

    public async Task<int> DeleteByIdAsync(string entity, object id, bool isNode = true)
    {
        var idField = isNode ? "node" : "edge";
        var q = $"DELETE FROM {entity} WHERE ${idField}_id = '{id}'";
        using var ctx = CreateDbContext();
        return await ctx.ExecuteNonQueryAsync(q, new { }).ConfigureAwait(false);
    }

    public async Task<int> DeleteEdgeAsync<F, T, E>(string edgeName, string fromNode, string toNode, F fromParams, T toParams, E parameters)
    {
        if (parameters == null)
            return 0;
        var fromId = GetNodeId(fromNode, fromParams);
        var toId = GetNodeId(toNode, toParams);
        if (fromId == null || toId == null) return 0;
        return await DeleteEdgeAsync(edgeName, parameters, fromId, toId);
    }

    public async Task<int> DeleteEdgeAsync<E>(string edgeName, E parameters, object fromId, object toId)
    {
        var wh = GetWhClauseForEdge(fromId, toId, parameters);
        if (string.IsNullOrWhiteSpace(wh))
            return 0;
        var q = $"DELETE FROM {edgeName} {wh}";
        using var ctx = CreateDbContext();
        return await ctx.ExecuteNonQueryAsync(q, parameters).ConfigureAwait(false);
    }

    public async Task<int> DeleteNodeAsync<T>(string nodeName, T parameters)
    {
        if (parameters == null)
            return 0;
        using var ctx = CreateDbContext();
        return await ctx.ExecuteNonQueryAsync($"DELETE FROM {nodeName} {BuildWhereClause(parameters)}", parameters).ConfigureAwait(false);
    }
    #endregion

    #region UTILITIES

    public static string BuildWhereClause<T>(T parameters, bool prependWhere = true)
    {
        return parameters == null || parameters.GetType().GetProperties().Length == 0 ? string.Empty : f(parameters, prependWhere);

        static string f<K>(K parameters, bool prependWhere) =>
            (prependWhere ? " WHERE " : string.Empty) + string.Join(" AND ", AssignParamValue(parameters));
    }

    public static string GetWhClauseForEdge<E>(object fromId, object toId, E parameters)
    {
        if (fromId == null && toId == null)
            return string.Empty;
        var wh = new List<string>();
        if (fromId != null)
            wh.Add(GetWhereClauseForNodeId(fromId, "from"));
        if (toId != null)
            wh.Add(GetWhereClauseForNodeId(toId, "to"));
        wh.Add(BuildWhereClause(parameters, false));
        if (wh.Count > 0)
            return " WHERE " + string.Join(" AND ", wh.Where(c => !string.IsNullOrWhiteSpace(c)));
        return string.Empty;
    }

    protected static string GetFromMatchQPart(string fromNode, string toNode, string edgeName) => $" FROM {fromNode}, {edgeName}, {toNode} WHERE MATCH({fromNode}-({edgeName})->{toNode})";

    protected string GetInsertEdgeQuery<F, T, E>(string edgeName, string fromNode, string toNode, F fromParams, T toParams, E parameters)
    {
        var fromId = GetNodeId(fromNode, fromParams);
        var toId = GetNodeId(toNode, toParams);
        var valueNames = GetValueNames(parameters);
        valueNames = string.IsNullOrWhiteSpace(valueNames) ? string.Empty : ", " + valueNames;
        var q = $"INSERT INTO {edgeName} VALUES('{fromId}', '{toId}'{valueNames})";
        return q;
    }

    protected string GetUpdateEdgeQuery<F, T, E, S>(string edgeName, string fromNode, string toNode, F fromParams, T toParams, E parameters, S whereClauseParameters)
    {
        var fromId = GetNodeId(fromNode, fromParams);
        var toId = GetNodeId(toNode, toParams);
        if (fromId == null || toId == null) return string.Empty;
        var assignments = string.Join(" , ", AssignParamValue(parameters));
        var wh = GetWhClauseForEdge(fromId, toId, whereClauseParameters);
        var q = $"UPDATE {edgeName} SET {assignments} {wh};";
        return q;
    }

    private static IEnumerable<string> AssignParamValue<T>(T parameters, bool isUpdateClause = false)
    {
        return parameters?.GetType().GetProperties().Select(p => p.GetValue(parameters) == null ? f(isUpdateClause, p) : $"{FormatParamName(p.Name)} = @{p.Name}") ?? [];

        static string f(bool isUpdateClause, System.Reflection.PropertyInfo p) =>
            isUpdateClause ? string.Empty : $"{FormatParamName(p.Name)} IS NULL";
    }

    private static string FormatParamName(string parameterName) => sysColumns.Contains(parameterName, StringComparer.InvariantCultureIgnoreCase) ? $"${parameterName}" : parameterName;

    private static string GetColumnNames<T>(T parameters) => parameters == null ? string.Empty : string.Join(",", parameters.GetType().GetProperties().Select(p => $"{p.Name}"));

    private static string GetValueNames<T>(T parameters) => parameters == null ? string.Empty : string.Join(",", parameters.GetType().GetProperties().Select(p => $"@{p.Name}"));

    private static string GetWhereClauseForNodeId(object nodeId, string nodeType) => nodeId != null ? $" ${nodeType}_id = '{nodeId}'" : string.Empty;

    private object GetNodeId<F>(string fromNode, F fromParams)
    {
        if (fromParams == null)
            return null;
        var properties = fromParams.GetType().GetProperties();
        if (properties.Length == 0)
            return null;
        foreach (var propName in sysColumns)
        {            
            var propInfo = Array.Find(properties, p => p.Name.Equals(propName, StringComparison.InvariantCultureIgnoreCase));
            if (propInfo != null)
                return propInfo.GetValue(fromParams);
        }
        using var ctx = CreateDbContext();
        return ctx.ExecuteScalar<F, object>($"SELECT $node_id FROM {fromNode} {BuildWhereClause(fromParams)}", fromParams);
    }
    #endregion    
}
