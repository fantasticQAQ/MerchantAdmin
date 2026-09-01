-- =====================================================
-- 自动注入：库不存在则创建 + 切换上下文
-- 生成时间：2026-09-01 21:18:15
-- -----------------------------------------------------
-- 说明：
--  1) 先把 QUOTED_IDENTIFIER / ANSI_NULLS 等 EF Core 要求的 SET 选项打开。
--     sqlcmd 默认 QUOTED_IDENTIFIER=OFF，EF 生成的建表脚本若用到主键索引
--     超长键、XML 类型方法、筛选索引等会报 Msg 1934。
--  2) 库不存在则 CREATE DATABASE。
--  3) USE [DatabaseName]。
-- =====================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF DB_ID(N'MerchantAdmin.Merchant') IS NULL
BEGIN
    CREATE DATABASE [MerchantAdmin.Merchant];
END;
GO
USE [MerchantAdmin.Merchant];
GO
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901121213_InitialCreate'
)
BEGIN
    CREATE TABLE [IntegrationEventLog] (
        [EventId] uniqueidentifier NOT NULL,
        [EventTypeName] nvarchar(max) NOT NULL,
        [State] int NOT NULL,
        [TimesSent] int NOT NULL,
        [CreationTime] datetime2 NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [TransactionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_IntegrationEventLog] PRIMARY KEY ([EventId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901121213_InitialCreate'
)
BEGIN
    CREATE TABLE [OperationLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [UserName] nvarchar(max) NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Detail] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OperationLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901121213_InitialCreate'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] int NOT NULL IDENTITY,
        [CreatedAt] datetime2 NOT NULL,
        [OrderStatus] nvarchar(30) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901121213_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [Stock] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901121213_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [ProductName] nvarchar(max) NOT NULL,
        [Quantity] decimal(18,2) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [OrderId] int NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901121213_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260901121213_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260901121213_InitialCreate', N'8.0.27');
END;
GO

COMMIT;
GO

