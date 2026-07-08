-- Demo "Shop" database for the sql-agent sample. Idempotent: safe to re-run.
IF DB_ID('Shop') IS NULL
    CREATE DATABASE Shop;
GO
USE Shop;
GO

IF OBJECT_ID('dbo.OrderItems') IS NOT NULL DROP TABLE dbo.OrderItems;
IF OBJECT_ID('dbo.Orders') IS NOT NULL DROP TABLE dbo.Orders;
IF OBJECT_ID('dbo.Products') IS NOT NULL DROP TABLE dbo.Products;
IF OBJECT_ID('dbo.Customers') IS NOT NULL DROP TABLE dbo.Customers;
GO

CREATE TABLE dbo.Customers (
    Id INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Email NVARCHAR(200) NOT NULL,
    Country NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Products (
    Id INT IDENTITY PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Category NVARCHAR(50) NOT NULL,
    Price DECIMAL(10, 2) NOT NULL
);

CREATE TABLE dbo.Orders (
    Id INT IDENTITY PRIMARY KEY,
    CustomerId INT NOT NULL REFERENCES dbo.Customers(Id),
    OrderedAt DATETIME2 NOT NULL,
    Status NVARCHAR(20) NOT NULL
);

CREATE TABLE dbo.OrderItems (
    Id INT IDENTITY PRIMARY KEY,
    OrderId INT NOT NULL REFERENCES dbo.Orders(Id),
    ProductId INT NOT NULL REFERENCES dbo.Products(Id),
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(10, 2) NOT NULL
);
GO

INSERT INTO dbo.Customers (Name, Email, Country) VALUES
('Anna Berg', 'anna.berg@example.com', 'Norway'),
('Bjorn Dahl', 'bjorn.dahl@example.com', 'Norway'),
('Clara Lund', 'clara.lund@example.com', 'Sweden'),
('David Holm', 'david.holm@example.com', 'Denmark'),
('Eva Nilsen', 'eva.nilsen@example.com', 'Norway'),
('Filip Marek', 'filip.marek@example.com', 'Poland'),
('Greta Voss', 'greta.voss@example.com', 'Germany'),
('Henrik Aas', 'henrik.aas@example.com', 'Norway');

INSERT INTO dbo.Products (Name, Category, Price) VALUES
('Trail Running Shoes', 'Footwear', 129.90),
('Waterproof Jacket', 'Clothing', 199.50),
('Merino Base Layer', 'Clothing', 79.00),
('Headlamp 400lm', 'Equipment', 49.90),
('Trekking Poles', 'Equipment', 89.00),
('Down Sleeping Bag', 'Equipment', 349.00),
('Wool Hiking Socks', 'Footwear', 19.90),
('Thermos 1L', 'Accessories', 34.50);

INSERT INTO dbo.Orders (CustomerId, OrderedAt, Status) VALUES
(1, '2026-05-02T10:15:00', 'Delivered'),
(1, '2026-06-11T14:30:00', 'Delivered'),
(2, '2026-05-20T09:05:00', 'Delivered'),
(3, '2026-06-01T16:45:00', 'Shipped'),
(3, '2026-06-25T11:20:00', 'Processing'),
(4, '2026-06-18T13:00:00', 'Delivered'),
(5, '2026-06-28T08:40:00', 'Processing'),
(6, '2026-07-01T17:10:00', 'Processing'),
(7, '2026-06-05T12:00:00', 'Cancelled'),
(8, '2026-07-03T10:00:00', 'Shipped');

INSERT INTO dbo.OrderItems (OrderId, ProductId, Quantity, UnitPrice) VALUES
(1, 1, 1, 129.90), (1, 7, 3, 19.90),
(2, 4, 1, 49.90),
(3, 2, 1, 199.50), (3, 3, 2, 79.00),
(4, 6, 1, 349.00),
(5, 5, 1, 89.00), (5, 8, 2, 34.50),
(6, 1, 1, 129.90), (6, 2, 1, 199.50),
(7, 3, 1, 79.00), (7, 7, 5, 19.90),
(8, 8, 1, 34.50),
(9, 6, 1, 349.00),
(10, 4, 2, 49.90), (10, 5, 1, 89.00);
GO

-- Read-only login for the agent (defense in depth on top of SELECT-only validation).
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'agent_reader')
    CREATE LOGIN agent_reader WITH PASSWORD = 'AgentReader1!';
GO
USE Shop;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'agent_reader')
    CREATE USER agent_reader FOR LOGIN agent_reader;
GO
ALTER ROLE db_datareader ADD MEMBER agent_reader;
GO
PRINT 'Shop database seeded.';
GO
