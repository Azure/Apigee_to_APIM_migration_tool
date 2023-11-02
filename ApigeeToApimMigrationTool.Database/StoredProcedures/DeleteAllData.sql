CREATE PROCEDURE [dbo].[DeleteAllData]
AS
	delete from ProxyPolicy
	delete from ProxyBasePath
	delete from ProxyEndpoint
	delete from Proxy
RETURN 0
