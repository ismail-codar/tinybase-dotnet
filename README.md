# TinyBase PostgreSQL Persister for C#

A comprehensive C# implementation recreating the TypeScript PostgreSQL persister functionality from TinyBase, using Entity Framework Core for data persistence.

## 📋 Overview

This C# implementation recreates the sophisticated PostgreSQL persister from the original TypeScript module, featuring:

- **PostgreSQL Change Detection** using LISTEN/NOTIFY pattern
- **Database Triggers and Functions** for monitoring DDL/DML events
- **Support for both JSON and tabular persistence** modes
- **Real-time data synchronization** with TinyBase stores
- **Entity Framework Core** for robust data access
- **Repository Pattern** with Unit of Work
- **Dependency Injection** support
- **Comprehensive error handling** and logging

## 🏗️ Architecture

### Project Structure

```
├── Models/
│   ├── Entities/           # Entity models (Store, Table, Cell)
│   └── Configuration/      # Configuration models
├── Data/
│   ├── Contexts/           # Entity Framework DbContext
│   └── Repositories/       # Repository pattern implementations
├── Services/
│   └── Persisters/         # PostgreSQL persister implementation
├── Extensions/             # DI extension methods
├── Configuration/          # Configuration options
├── Examples/              # Usage examples
```

### Key Components

#### 1. Entity Models
- **Store**: Represents a TinyBase store
- **Table**: Represents a table within a store
- **Cell**: Represents individual data cells
- All entities support both JSON and tabular data

#### 2. Data Access Layer
- **Repository Pattern**: Generic and specific repositories
- **Unit of Work**: Coordinates multiple repository operations
- **Entity Framework Core**: PostgreSQL provider integration

#### 3. PostgreSQL Persister
- **IPostgresPersister**: Main interface
- **PostgresPersister**: Core implementation
- **PostgresPersisterFactory**: Factory for creating instances
- **ConfigurationUtilities**: Helper methods

## 🔄 TypeScript to C# Mapping

### Original TypeScript Structure

```typescript
// TypeScript
export const createPostgresPersister = (async (
  store: Store | MergeableStore,
  sql: Sql,
  configOrStoreTableName?: DatabasePersisterConfig | string,
  onSqlCommand?: (sql: string, params?: any[]) => void,
  onIgnoredError?: (error: any) => void,
): Promise<PostgresPersister> => {
  
  const commandSql = await sql.reserve?.();
  return createCustomPostgreSqlPersister(
    store,
    configOrStoreTableName,
    commandSql?.unsafe,
    // ... more parameters
  ) as PostgresPersister;
});
```

### C# Equivalent

```csharp
// C#
public class PostgresPersisterFactory : IPostgresPersisterFactory
{
    public async Task<IPostgresPersister> CreatePostgresPersisterAsync(
        string storeId,
        string? configOrTableName = null,
        Action<string, object[]>? onSqlCommand = null,
        Action<Exception>? onIgnoredError = null,
        CancellationToken cancellationToken = default)
    {
        var config = await ParseConfigurationAsync(storeId, configOrTableName, cancellationToken);
        var unitOfWork = new UnitOfWork(_context);
        var connection = new NpgsqlConnection(_connectionString);
        
        var persisterConfig = new PostgresPersisterConfig
        {
            StoreTableName = config.StoreTableName,
            ManagedTableNames = config.ManagedTableNames,
            IsJson = config.IsJson,
            OnSqlCommand = onSqlCommand,
            OnIgnoredError = onIgnoredError
        };

        return new PostgresPersister(unitOfWork, connection, persisterConfig);
    }
}
```

### Key Mapping Differences

| TypeScript | C# | Description |
|------------|----|-------------|
| `Store \| MergeableStore` | `string storeId` | Store identifier instead of object |
| `Sql` | `NpgsqlConnection` | ADO.NET connection instead of library-specific client |
| `createCustomPostgreSqlPersister` | `PostgresPersister` | Direct implementation with EF Core |
| Database triggers and functions | Custom function creation | Same functionality, different API |
| NOTIFY/LISTEN pattern | NpgsqlNotification | Same PostgreSQL feature |
| JSON/TS interfaces | C# classes | Type-safe configuration models |

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 or later
- PostgreSQL 12+ database
- Entity Framework Core 8.0

### Installation

1. **Add NuGet packages** (already included in project):
   ```xml
   <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
   <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
   <PackageReference Include="Npgsql" Version="8.0.0" />
   ```

2. **Configure your PostgreSQL connection string**:
   ```json
   {
     "ConnectionStrings": {
       "Postgres": "Host=localhost;Database=tinybase;Username=postgres;Password=password"
     }
   }
   ```

### Basic Usage

#### 1. Dependency Injection Setup

```csharp
using TinyBasePostgresPersister.Extensions;

var services = new ServiceCollection();

// Add PostgreSQL persister services
services.AddPostgresPersister(
    "Host=localhost;Database=tinybase;Username=postgres;Password=password",
    options =>
    {
        options.AutoCreateSchema = true;
        options.EnableSqlLogging = true;
    });

var serviceProvider = services.BuildServiceProvider();
```

#### 2. Create and Use Persister

```csharp
var persisterFactory = serviceProvider.GetRequiredService<IPostgresPersisterFactory>();

// Simple table persistence
using var persister = await persisterFactory.CreatePostgresPersisterAsync(
    "my_store",
    "my_table");

// Load existing data
await persister.LoadAsync();

// Set up change tracking
persister.DataChanged += (sender, args) =>
{
    Console.WriteLine($"Data changed: {args.TableName}");
};

persister.TableCreated += (sender, args) =>
{
    Console.WriteLine($"Table created: {args.TableName}");
};

// Start listening for changes
await persister.StartListeningAsync();

// Save changes
await persister.SaveAsync();

// Stop listening
await persister.StopListeningAsync();
```

#### 3. JSON Configuration

```csharp
var jsonConfig = @"
{
    ""isJson"": true,
    ""managedTableNames"": [""users"", ""products"", ""orders""],
    ""tableConfigs"": [
        {
            ""tableName"": ""users"",
            ""columnNames"": [""name"", ""email"", ""age""],
            ""columnTypes"": [""text"", ""text"", ""integer""]
        }
    ]
}";

using var persister = await persisterFactory.CreatePostgresPersisterAsync(
    "store_with_config",
    jsonConfig);
```

## 🛠️ Configuration Options

### PostgresPersisterOptions

```csharp
public class PostgresPersisterOptions
{
    public string ConnectionString { get; set; }
    public string DefaultTableName { get; set; } = "tinybase_store";
    public bool AutoCreateSchema { get; set; } = true;
    public int CommandTimeout { get; set; } = 30;
    public bool EnableSqlLogging { get; set; } = false;
    public string DatabaseProvider { get; set; } = "PostgreSQL";
}
```

### Configuration Methods

#### 1. Simple Table Name
```csharp
await persisterFactory.CreatePostgresPersisterAsync("store_id", "table_name");
```

#### 2. JSON Configuration
```csharp
await persisterFactory.CreatePostgresPersisterAsync("store_id", jsonConfig);
```

#### 3. Configuration Object
```csharp
var config = new PostgresPersisterConfig
{
    IsJson = false,
    ManagedTableNames = new List<string> { "table1", "table2" }
};
```

## 📊 Database Schema

The implementation automatically creates the following tables:

### Stores Table
```sql
CREATE TABLE stores (
    id text PRIMARY KEY,
    name text,
    is_mergeable boolean,
    configuration jsonb,
    created_at timestamptz,
    updated_at timestamptz,
    config_hash text,
    is_persisted boolean
);
```

### Tables Table
```sql
CREATE TABLE tables (
    id text PRIMARY KEY,
    store_id text REFERENCES stores(id),
    name text,
    is_managed boolean,
    schema jsonb,
    created_at timestamptz,
    updated_at timestamptz,
    is_json_table boolean
);
```

### Cells Table
```sql
CREATE TABLE cells (
    id text PRIMARY KEY,
    table_id text REFERENCES tables(id),
    row_id text,
    column_id text,
    value text,
    created_at timestamptz,
    updated_at timestamptz
);
```

## 🔧 Advanced Features

### Database Triggers

The implementation creates PostgreSQL triggers to monitor:

1. **DDL Events**: Table creation events
2. **DML Events**: INSERT, UPDATE, DELETE operations

```sql
-- Example trigger function
CREATE OR REPLACE FUNCTION tinybase_data_changed_hash()
RETURNS trigger AS $$
BEGIN
    PERFORM pg_notify('TINYBASE_hash', 'd:' || TG_TABLE_NAME);
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_name
AFTER INSERT OR UPDATE OR DELETE ON table_name
FOR EACH ROW
EXECUTE FUNCTION tinybase_data_changed_hash();
```

### Change Notification System

```csharp
// Set up comprehensive change tracking
persister.DataChanged += (sender, args) =>
{
    switch (args.EventType)
    {
        case "c:": // Table created
            Console.WriteLine($"Table {args.TableName} was created");
            break;
        case "d:": // Data changed
            Console.WriteLine($"Data in {args.TableName} was modified");
            break;
    }
};
```

### Error Handling

```csharp
using var persister = await persisterFactory.CreatePostgresPersisterAsync(
    "store_id",
    onSqlCommand: (sql, parameters) => 
    {
        logger.LogDebug("Executing SQL: {SQL}", sql);
    },
    onIgnoredError: (error) => 
    {
        logger.LogWarning("Ignored error: {Error}", error.Message);
    });
```

## 🔍 Migration from TypeScript

### Key Concepts Mapping

| TypeScript Concept | C# Implementation | Notes |
|-------------------|------------------|-------|
| `Store` object | `string storeId` | Identifier-based approach |
| `MergeableStore` | `Store.IsMergeable` | Property on Store entity |
| `Sql.reserve()` | Connection management | Automated in NpgsqlConnection |
| `sql.listen()` | `NpgsqlNotification` | Built-in .NET support |
| `tryCatch()` | `try-catch` blocks | Standard C# exception handling |
| `createCustomPostgreSqlPersister` | `PostgresPersister` | Direct implementation |

### Integration Pattern

**TypeScript:**
```typescript
import { createPostgresPersister } from 'tinybase-persister-postgres';

const persister = await createPostgresPersister(
    store, 
    sql, 
    config,
    onSqlCommand,
    onIgnoredError
);
```

**C#:**
```csharp
using var persister = await persisterFactory.CreatePostgresPersisterAsync(
    storeId: "my_store",
    configOrTableName: config,
    onSqlCommand: (sql, parameters) => { /* logging */ },
    onIgnoredError: (error) => { /* error handling */ }
);
```

## 📈 Performance Considerations

### Database Optimization

1. **Indexes**: Automatically created for common query patterns
2. **Connection Pooling**: Built into NpgsqlConnection
3. **Transaction Support**: Unit of Work pattern coordinates transactions
4. **Batched Operations**: Repository pattern supports bulk operations

### Memory Management

- All operations use async/await patterns
- Disposal pattern for connection management
- Event handlers properly unsubscribed
- Cancellation token support throughout

## 🧪 Testing

### Unit Testing

```csharp
[Test]
public async Task ShouldCreatePersister()
{
    // Arrange
    var mockContext = new Mock<TinyBaseDbContext>();
    var factory = new PostgresPersisterFactory(mockContext.Object, connectionString);
    
    // Act
    var persister = await factory.CreatePostgresPersisterAsync("test_store");
    
    // Assert
    Assert.IsNotNull(persister);
    Assert.IsFalse(persister.IsListening);
}
```

### Integration Testing

```csharp
[Test]
public async Task ShouldHandleDataChanges()
{
    var persister = await factory.CreatePostgresPersisterAsync("test_store");
    var eventRaised = false;
    
    persister.DataChanged += (s, e) => eventRaised = true;
    
    await persister.StartListeningAsync();
    // Simulate database change
    
    Assert.IsTrue(eventRaised);
}
```

## 🐛 Troubleshooting

### Common Issues

1. **Connection Issues**
   ```csharp
   // Check connection string
   var connectionString = configuration.GetConnectionString("Postgres");
   
   // Test connection
   using var connection = new NpgsqlConnection(connectionString);
   await connection.OpenAsync();
   ```

2. **Permission Errors**
   ```sql
   -- Grant necessary permissions
   GRANT CONNECT ON DATABASE tinybase TO username;
   GRANT USAGE ON SCHEMA public TO username;
   GRANT CREATE ON SCHEMA public TO username;
   ```

3. **Trigger Creation Failures**
   ```csharp
   // Enable detailed logging
   options.EnableSqlLogging = true;
   ```

### Debug Logging

```csharp
services.AddLogging(builder =>
{
    builder.AddSerilog();
    builder.SetMinimumLevel(LogLevel.Debug);
});
```

## 📚 API Reference

### IPostgresPersister

```csharp
public interface IPostgresPersister : IDisposable
{
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task StartListeningAsync(CancellationToken cancellationToken = default);
    Task StopListeningAsync(CancellationToken cancellationToken = default);
    
    event EventHandler<PersisterEventArgs> DataChanged;
    event EventHandler<PersisterEventArgs> TableCreated;
    
    string ConfigHash { get; }
    bool IsListening { get; }
}
```

### IPostgresPersisterFactory

```csharp
public interface IPostgresPersisterFactory
{
    Task<IPostgresPersister> CreatePostgresPersisterAsync(
        string storeId,
        string? configOrTableName = null,
        Action<string, object[]>? onSqlCommand = null,
        Action<Exception>? onIgnoredError = null,
        CancellationToken cancellationToken = default);
}
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgments

- Original TypeScript implementation by [tinyplex/tinybase](https://github.com/tinyplex/tinybase)
- Entity Framework Core team for the excellent ORM
- PostgreSQL community for the robust database features