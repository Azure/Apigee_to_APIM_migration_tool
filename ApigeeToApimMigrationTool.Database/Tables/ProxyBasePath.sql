CREATE TABLE [dbo].[ProxyBasePath]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY, 
    [Path] VARCHAR(1000) NOT NULL, 
    [ProxyId] INT NOT NULL, 
    CONSTRAINT [FK_ProxyBasePath_ToProxy] FOREIGN KEY ([ProxyId]) REFERENCES [Proxy]([Id])
)
