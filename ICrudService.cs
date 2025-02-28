namespace SQLServerGraphEFCore;

public interface ICrudService: IDisposable, IAsyncDisposable
{
    bool AnyEdge<F, T, E>(string edgeName, string fromNode, string toNode, F fromParams, T toParams, E parameters);

    bool AnyNode<T>(string nodeName, T parameters);

    Task<int> DeleteByIdAsync(string entity, object id, bool isNode = true);

    Task<int> DeleteEdgeAsync<E>(string edgeName, E parameters, object fromId, object toId);

    Task<int> DeleteEdgeAsync<F, T, E>(string edgeName, string fromNode, string toNode, F fromParams, T toParams, E parameters);

    Task<int> DeleteNodeAsync<T>(string nodeName, T parameters);

    Task<int> InsertEdgeAsync<F, T, E>(string edgeName, string fromNode, string toNode, F fromParams, T toParams, E parameters);

    int InsertNode<T>(string nodeName, T parameters);

    Task<int> InsertNodeAsync<T>(string nodeName, T parameters);
}
