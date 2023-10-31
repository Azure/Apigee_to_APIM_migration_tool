using ApigeeToApimMigrationTool.Core;
using ApigeeToApimMigrationTool.Core.Interface;
using ApigeeToAzureApimMigrationTool.Core.Dto;
using ApigeeToAzureApimMigrationTool.Core.Interface;
using Dapper;
using Microsoft.Data.SqlClient;
using System.IO;

namespace ApigeeToApimMigrationTool.DataAccess
{

    public class ProxyMetaDataDataAccess : IProxyMetaDataDataAccess
    {
        private readonly SqlConnection _connection;
        public ProxyMetaDataDataAccess(string connectionString)
        {
            _connection = new SqlConnection(connectionString);
        }

        public async Task<bool> IsProxyMetadataTablePopulted()
        {
            bool result = await _connection.QuerySingleOrDefaultAsync<bool>("if EXISTS (SELECT 1 FROM Proxy) begin select 1 end else begin select 0 end\r\n");
            return result;
        }

        public async Task<ApiProxyRevisionMetadata> GetProxyRevisionMetaDataByPath(string path)
        {
            var result = await _connection.QuerySingleOrDefaultAsync<ApiProxyRevisionMetadata>($@"select p.Id, p.ProxyName, p.Revision, p.Description from Proxy p
                   join ProxyBasePath bp on p.Id = bp.ProxyId where bp.Path = '{path}'");
            return result;
        }

        public async Task InsertProxyRevisionMetaData(ApiProxyRevisionMetadata metaData)
        {
            if (_connection.State == System.Data.ConnectionState.Closed)
                await _connection.OpenAsync();

            SqlTransaction transaction = _connection.BeginTransaction();
            try
            {
                SqlCommand apiProxyInsertCommand = new SqlCommand($@"INSERT INTO [Proxy]([ProxyName],[Revision],[Description]) 
                        VALUES (@Name,@Revision, @Description); select  SCOPE_IDENTITY()", _connection, transaction);
                apiProxyInsertCommand.Parameters.AddWithValue("Name", metaData.Name);
                apiProxyInsertCommand.Parameters.AddWithValue("Revision", metaData.Revision);
                apiProxyInsertCommand.Parameters.AddWithValue("Description", metaData.Description?? "");
                var proxyId = await apiProxyInsertCommand.ExecuteScalarAsync();

                if (proxyId == null)
                    throw new ArgumentNullException("proxyId", "proxyId is null after inserting the metadata into sql table Proxy");

                foreach (var path in metaData.BasePaths)
                {
                    SqlCommand apiProxyPathInsertCommand = new SqlCommand($"INSERT INTO [ProxyBasePath]([Path],[ProxyId]) VALUES (@Path ,@ProxyId)", _connection, transaction);
                    apiProxyPathInsertCommand.Parameters.AddWithValue("Path", path?? "");
                    apiProxyPathInsertCommand.Parameters.AddWithValue("ProxyId", proxyId);
                    await apiProxyPathInsertCommand.ExecuteNonQueryAsync();
                }
                foreach (var policy in metaData.Policies)
                {
                    SqlCommand apiProxyPolicyInsertCommand = new SqlCommand($"INSERT INTO [ProxyPolicy]([PolicyName],[ProxyId]) VALUES (@PolicyName ,@ProxyId)", _connection, transaction);
                    apiProxyPolicyInsertCommand.Parameters.AddWithValue("PolicyName", policy ?? "");
                    apiProxyPolicyInsertCommand.Parameters.AddWithValue("ProxyId", proxyId);

                    await apiProxyPolicyInsertCommand.ExecuteNonQueryAsync();
                }
                foreach (var endpoint in metaData.ProxyEndpointPaths)
                {
                    SqlCommand apiProxyProxyEndpointsInsertCommand = new SqlCommand($"INSERT INTO [ProxyEndpoint]([ProxyEndpoint],[ProxyId],[EndpointName]) VALUES (@ProxyEndpoint ,@ProxyId, @EndpointName)", _connection, transaction);
                    apiProxyProxyEndpointsInsertCommand.Parameters.AddWithValue("EndpointName", endpoint.Key ?? "");
                    apiProxyProxyEndpointsInsertCommand.Parameters.AddWithValue("ProxyEndpoint", endpoint.Value ?? "");
                    apiProxyProxyEndpointsInsertCommand.Parameters.AddWithValue("ProxyId", proxyId);
                    await apiProxyProxyEndpointsInsertCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}