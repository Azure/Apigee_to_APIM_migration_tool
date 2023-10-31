CREATE TABLE [dbo].[ProxyEndpoint]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY, 
    [ProxyEndpoint] VARCHAR(100) NOT NULL, 
    [ProxyId] INT NOT NULL, 
    [EndpointName] NVARCHAR(100) NOT NULL, 
    CONSTRAINT [FK_ProxyEndpoint_ToProxy] FOREIGN KEY ([ProxyId]) REFERENCES [Proxy]([Id])
)
