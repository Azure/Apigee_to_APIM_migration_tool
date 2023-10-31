using ApigeeToAzureApimMigrationTool.Core.Dto;

namespace ApigeeToApimMigrationTool.Core.Interface
{
    public interface IProxyMetaDataDataAccess
    {
        Task<ApiProxyRevisionMetadata> GetProxyRevisionMetaDataByPath(string path);
        Task InsertProxyRevisionMetaData(ApiProxyRevisionMetadata metaData);
        Task<bool> IsProxyMetadataTablePopulted();
    }
}