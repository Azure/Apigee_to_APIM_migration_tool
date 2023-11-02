CREATE TABLE [dbo].[ProxyPolicy]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY, 
    [PolicyName] VARCHAR(100) NOT NULL, 
    [ProxyId] INT NOT NULL, 
    CONSTRAINT [FK_ProxyPolicy_ToProxy] FOREIGN KEY ([ProxyId]) REFERENCES [Proxy]([Id])
)
