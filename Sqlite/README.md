# TinyBase SQLite Persister for C#

A comprehensive C# implementation recreating the TypeScript SQLite persister functionality from TinyBase, using Entity Framework Core and Microsoft.Data.Sqlite.

## 📋 Overview

This C# implementation recreates the SQLite persister from the original TypeScript module, featuring:

- **SQLite Database Integration** using Microsoft.Data.Sqlite
- **Polling-based Change Detection** (SQLite doesn't support PostgreSQL-style triggers)
- **Support for both JSON and tabular persistence** modes
- **File-based and in-memory database** support
- **Entity Framework Core** for data access
- **Repository Pattern** with Unit of Work
- **Dependency Injection** support
- **Comprehensive async/await** patterns

## 🔄 SQLite vs PostgreSQL Differences

### Key Architectural Differences

| Feature | PostgreSQL | SQLite | Impact |
|---------|------------|--------|---------|
| **Change Detection** | NOTIFY/LISTEN with triggers | Polling-based detection | Different implementation approach |
| **Database Triggers** | Advanced DDL/DML triggers | Limited trigger support | Simpler trigger creation |
| **Connection Pattern** | Client-server with connection pooling | File-based, embedded | Different resource management |
| **Schema Management** | Advanced schema introspection | SQLite PRAGMA commands | Different schema queries |
| **Event System** | Database-level events | Application-level polling | Different event handling |

### TypeScript to C# Mapping

**TypeScript (SQLite):**
```typescript
export const createSqlite3Persister = ((
  store: Store | MergeableStore,
  db: Database,
  configOrStoreTableName?: DatabasePersisterConfig | string,
  onSqlCommand?: (sql: string, params?: any[]) => void,
  onIgnoredError?: (error: any) => void,
): Sqlite3Persister =>
  createCustomSqlitePersister(
    store,
    configOrStoreTableName,
    async (sql: string, params: any[] = []): Promise<IdObj<any>[]> =>
      await promiseNew((resolve, reject) =>
        db.all(sql, params, (error, rows: IdObj<any>[]) =>
          error ? reject(error) : resolve(rows),
        ),
      ),
    (listener: DatabaseChangeListener): Observer => {
      const observer = (_: any, _2: any, tableName: string) =>
        listener(tableName);
      db.on(CHANGE, observer);
      return observer;
    },
    (observer: Observer): any => db.off(CHANGE, observer),
    // ... more parameters
  ) as Sqlite3Persister) as typeof createSqlite3PersisterDecl;
```

**C# Equivalent:**
```csharp
public class SqlitePersisterFactory : ISqlitePersisterFactory
{
    public async Task<ISqlitePersister> CreateSqlitePersisterAsync(
        string storeId,
        string? configOrTableName = null,
        Action<string, object[]>? onSqlCommand = null,
        Action<Exception>? onIgnoredError = null,
        CancellationToken cancellationToken = default)
    {
        var config = await ParseConfigurationAsync(storeId, configOrTableName, cancellationToken);
        var unitOfWork = new SqliteUnitOfWork(_context);
        var connection = new SqliteConnection(_connectionString);
        
        var persisterConfig = new SqlitePersisterConfig
        {
            StoreTableName = config.StoreTableName,
            ManagedTableNames = config.ManagedTableNames,
            IsJson = config.IsJson,
            AutoLoadIntervalSeconds = config.AutoLoadIntervalSeconds,
            OnSqlCommand = onSqlCommand,
            OnIgnoredError = onIgnoredError
        };

        return new SqlitePersister(unitOfWork, connection, persisterConfig);
    }
}
```

## 🏗️ Project Structure

```
TinyBaseSqlitePersister/
├── TinyBaseSqlitePersister.csproj          # .NET 8 project file
│
├── Models/
│   ├── Entities/
│   │   ├── SqliteStore.cs                  # Store entity model (SQLite)
│   │   ├── SqliteTable.cs                  # Table entity model (SQLite)
│   │   └── SqliteCell.cs                   # Cell entity model (SQLite)
│   └── Configuration/
│       └── SqlitePersisterConfig.cs        # SQLite configuration models
│
├── Data/
│   ├── Contexts/
│   │   └── SqliteDbContext.cs              # EF Core SQLite DbContext
│   └── Repositories/
│       ├── IRepository.cs                  # Generic repository interface
│       ├── Repository.cs                   # Generic repository implementation
│       ├── ISqliteStoreRepository.cs       # Store repository interface
│       ├── SqliteStoreRepository.cs        # Store repository implementation
│       ├── ISqliteTableRepository.cs       # Table repository interface
│       ├── SqliteTableRepository.cs        # Table repository implementation
│       ├── ISqliteCellRepository.cs        # Cell repository interface
│       ├── SqliteCellRepository.cs         # Cell repository implementation
│       ├── ISqliteUnitOfWork.cs            # Unit of Work interface
│       └── SqliteUnitOfWork.cs             # Unit of Work implementation
│
├── Services/
│   ├── Persisters/
│   │   ├── ISqlitePersister.cs            # SQLite persister interface
│   │   └── SqlitePersister.cs             # Core SQLite persister implementation
│   ├── SqlitePersisterFactory.cs          # Factory for creating persisters
│   └── SqliteConfigurationUtilities.cs    # SQLite-specific utilities
│
├── Extensions/
│   └── ServiceCollectionExtensions.cs     # DI extension methods for SQLite
│
├── Configuration/
│   └── SqlitePersisterOptions.cs          # Configuration options
│
└── Examples/
    ├── SqliteProgram.cs                    # Complete usage examples
    └── appsettings.json                    # Configuration file
```

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 or later
- Entity Framework Core 8.0
- Microsoft.Data.Sqlite 8.0

### Installation

1. **Add NuGet packages** (already included in project):
   ```xml
   <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />
   <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
   ```

2. **Configure your SQLite connection string**:
   ```json
   {
     "ConnectionStrings": {
       "Sqlite": "Data Source=tinybase.db;Mode=ReadWriteCreate;Cache=Private;"
     }
   }
   ```

### Basic Usage

#### 1. In-Memory Database (Testing)

```csharp
using TinyBaseSqlitePersister.Extensions;

var services = new ServiceCollection();

// Use in-memory SQLite
services.AddInMemorySqlitePersister("TestDatabase");

var serviceProvider = services.BuildServiceProvider();
var persisterFactory = serviceProvider.GetRequiredService<ISqlitePersisterFactory>();
```

#### 2. File-Based Database

```csharp
using TinyBaseSqlitePersister.Extensions;

var services = new ServiceCollection();

// Add SQLite persister services
services.AddSqlitePersister(
    "Data Source=myapp.db;Mode=ReadWriteCreate;Cache=Private;",
    options =>
    {
        options.AutoLoadIntervalSeconds = 5;
        options.EnableSqlLogging = true;
    });

var serviceProvider = services.BuildServiceProvider();
```

#### 3. Create and Use Persister

```csharp
var persisterFactory = serviceProvider.GetRequiredService<ISqlitePersisterFactory>();

// Simple table persistence
using var persister = await persisterFactory.CreateSqlitePersisterAsync(
    "my_store",
    "my_table");

// Load existing data
await persister.LoadAsync();

// Set up change tracking (polling-based)
persister.DataChanged += (sender, args) =>
{
    Console.WriteLine($"Data changed: {args.TableName}, Event: {args.EventType}");
};

// Start listening (polling will begin)
await persister.StartListeningAsync();

// Save changes
await persister.SaveAsync();

// Stop listening
await persister.StopListeningAsync();
```

#### 4. File-Based Database Creation

```csharp
// Create persister with file-based database
var persisterFactory = serviceProvider.GetRequiredService<ISqlitePersisterFactory>();
var databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "myapp", "data.db");

using var persister = await persisterFactory.CreateFileBasedSqlitePersisterAsync(
    "file_store",
    databasePath,
    @"
    {
        ""isJson"": true,
        ""autoLoadIntervalSeconds"": 3,
        ""managedTableNames"": [""users"", ""products"", ""orders""]
    }");
```

## 🔧 SQLite-Specific Configuration

### Auto-Load Interval

Unlike PostgreSQL's event-based notifications, SQLite uses polling to detect changes:

```csharp
var config = new SqlitePersisterConfig
{
    IsJson = false,
    ManagedTableNames = new List<string> { "table1", "table2" },
    AutoLoadIntervalSeconds = 5 // Poll every 5 seconds (0 = disabled)
};
```

### Database File Management

```csharp
// Create connection string for file
var connectionString = SqliteConfigurationUtilities.CreateFileConnectionString("data/myapp.db");

// Ensure directory exists
SqliteConfigurationUtilities.EnsureDatabaseDirectory("data/myapp.db");

// In-memory database
var inMemoryConnection = SqliteConfigurationUtilities.CreateInMemoryConnectionString();
```

## 📊 SQLite Schema

The implementation automatically creates the following tables:

### Stores Table
```sql
CREATE TABLE stores (
    id TEXT PRIMARY KEY,
    name TEXT,
    is_mergeable INTEGER,
    configuration TEXT,
    created_at DATETIME,
    updated_at DATETIME,
    config_hash TEXT,
    is_persisted INTEGER,
    auto_load_interval_seconds INTEGER
);
```

### Tables Table
```sql
CREATE TABLE tables (
    id TEXT PRIMARY KEY,
    store_id TEXT REFERENCES stores(id),
    name TEXT,
    is_managed INTEGER,
    schema TEXT,
    created_at DATETIME,
    updated_at DATETIME,
    is_json_table INTEGER
);
```

### Cells Table
```sql
CREATE TABLE cells (
    id TEXT PRIMARY KEY,
    table_id TEXT REFERENCES tables(id),
    row_id TEXT,
    column_id TEXT,
    value TEXT,
    created_at DATETIME,
    updated_at DATETIME
);
```

## 🕰️ Change Detection in SQLite

### Polling-Based Approach

Since SQLite doesn't have PostgreSQL's NOTIFY/LISTEN functionality, the implementation uses polling:

```csharp
private async Task PollForChanges()
{
    using var command = new SqliteCommand(@"
        SELECT 
            CAST(user_version AS INTEGER) as data_version,
            CAST(schema_version AS INTEGER) as schema_version,
            total_changes() as total_changes
        FROM pragma_user_version, pragma_schema_version", _connection);

    using var reader = await command.ExecuteReaderAsync();
    
    if (await reader.ReadAsync())
    {
        var currentVersion = new SqliteVersionInfo
        {
            DataVersion = reader.GetInt32(0),
            SchemaVersion = reader.GetInt32(1),
            TotalChanges = reader.GetInt32(2)
        };

        if (HasVersionChanged(currentVersion))
        {
            // Trigger change event
            var args = new SqlitePersisterEventArgs
            {
                Message = "version_change",
                EventType = "version_change",
                TableName = "all_tables"
            };
            DataChanged?.Invoke(this, args);
        }
    }
}
```

### SQLite PRAGMA Commands

The implementation uses SQLite PRAGMA commands for schema introspection:

```sql
-- Get table schema information
SELECT t.name as table_name, c.name as column_name 
FROM sqlite_master t, pragma_table_info(t.name) c 
WHERE t.type IN ('table','view') AND t.name IN (?) 
ORDER BY t.name, c.ordinal_position;

-- Get version information
SELECT CAST(user_version AS INTEGER), CAST(schema_version AS INTEGER), total_changes() 
FROM pragma_user_version, pragma_schema_version;
```

## 💻 Complete Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using TinyBaseSqlitePersister.Extensions;
using TinyBaseSqlitePersister.Services;

class Program
{
    static async Task Main()
    {
        // Setup services
        var services = new ServiceCollection();
        services.AddSqlitePersister(
            "Data Source=example.db;Mode=ReadWriteCreate;Cache=Private;",
            options => {
                options.AutoLoadIntervalSeconds = 3;
                options.EnableSqlLogging = true;
            });
        
        var serviceProvider = services.BuildServiceProvider();
        var persisterFactory = serviceProvider.GetRequiredService<ISqlitePersisterFactory>();

        // Create persister
        using var persister = await persisterFactory.CreateSqlitePersisterAsync(
            "my_store",
            @"
            {
                ""isJson"": true,
                ""managedTableNames"": [""users"", ""products"", ""orders""],
                ""autoLoadIntervalSeconds"": 5
            }");

        // Set up event handlers
        persister.DataChanged += (sender, args) =>
        {
            Console.WriteLine($"Change detected in {args.TableName}");
        };

        // Start working with data
        await persister.LoadAsync();
        await persister.StartListeningAsync();

        // Simulate some work
        await Task.Delay(10000);
        
        await persister.SaveAsync();
        await persister.StopListeningAsync();
    }
}
```

## 🛠️ SQLite vs PostgreSQL Comparison

### Advantages of SQLite Implementation

1. **No Server Required**: File-based, embedded database
2. **Simpler Deployment**: Single file database
3. **Better for Single-User Applications**: No connection management overhead
4. **Built-in Change Events**: SQLite Change event support
5. **Easier Testing**: In-memory database support

### Limitations of SQLite Implementation

1. **No Real-time Change Detection**: Requires polling instead of event-based
2. **Limited Schema Management**: Cannot create triggers for DDL events
3. **No Advanced SQL Features**: Missing some PostgreSQL-specific functionality
4. **Single Writer at a Time**: SQLite's write lock limitations

### When to Use Which

**Use SQLite when:**
- Building desktop applications
- Need simple deployment (single file)
- Single-user or low-concurrency scenarios
- Testing and development
- Mobile applications

**Use PostgreSQL when:**
- Building web applications with multiple users
- Need real-time change notifications
- Require advanced SQL features
- High-concurrency scenarios
- Enterprise applications

## 📚 API Reference

### ISqlitePersisterFactory

```csharp
public interface ISqlitePersisterFactory
{
    Task<ISqlitePersister> CreateSqlitePersisterAsync(
        string storeId,
        string? configOrTableName = null,
        Action<string, object[]>? onSqlCommand = null,
        Action<Exception>? onIgnoredError = null,
        CancellationToken cancellationToken = default);

    Task<ISqlitePersister> CreateFileBasedSqlitePersisterAsync(
        string storeId,
        string databasePath,
        string? configOrTableName = null,
        Action<string, object[]>? onSqlCommand = null,
        Action<Exception>? onIgnoredError = null,
        CancellationToken cancellationToken = default);
}
```

### ISqlitePersister

```csharp
public interface ISqlitePersister : IDisposable
{
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task StartListeningAsync(CancellationToken cancellationToken = default);
    Task StopListeningAsync(CancellationToken cancellationToken = default);
    
    event EventHandler<SqlitePersisterEventArgs> DataChanged;
    event EventHandler<SqlitePersisterEventArgs> TableCreated;
    
    string ConfigHash { get; }
    bool IsListening { get; }
}
```

## 🐛 Troubleshooting

### Common SQLite Issues

1. **Database File Locked**
   ```csharp
   // Check if file exists and permissions
   var fileInfo = new FileInfo(databasePath);
   if (fileInfo.Exists)
   {
       Console.WriteLine($"File size: {fileInfo.Length} bytes");
   }
   ```

2. **In-Memory Database Persistence**
   ```csharp
   // For in-memory databases, keep connection open
   await using var connection = new SqliteConnection("Data Source=:memory:;Mode=Memory;Cache=Shared;");
   await connection.OpenAsync();
   ```

3. **Migration Issues**
   ```csharp
   // Ensure database is initialized
   await context.Database.EnsureCreatedAsync();
   ```

### SQLite-Specific Performance Tips

1. **Use WAL Mode for Concurrent Access**
   ```sql
   PRAGMA journal_mode=WAL;
   ```

2. **Enable Foreign Key Constraints**
   ```sql
   PRAGMA foreign_keys=ON;
   ```

3. **Set Appropriate Cache Size**
   ```sql
   PRAGMA cache_size=10000;
   ```

## 📈 Performance Considerations

### SQLite Optimization

1. **Connection Management**: Use connection pooling
2. **Transaction Usage**: Wrap multiple operations in transactions
3. **Index Creation**: Create indexes for frequently queried columns
4. **WAL Mode**: Enable Write-Ahead Logging for better concurrency

### Memory Management

- All operations use async/await patterns
- Proper disposal of connections and commands
- Event handlers properly unsubscribed
- Cancellation token support throughout

## 🧪 Testing

### Unit Testing with In-Memory SQLite

```csharp
[Test]
public async Task ShouldCreateSqlitePersister()
{
    var services = new ServiceCollection();
    services.AddInMemorySqlitePersister("TestDB");
    var serviceProvider = services.BuildServiceProvider();
    
    var factory = serviceProvider.GetRequiredService<ISqlitePersisterFactory>();
    var persister = await factory.CreateSqlitePersisterAsync("test_store");
    
    Assert.IsNotNull(persister);
    Assert.IsFalse(persister.IsListening);
}
```

### Integration Testing

```csharp
[Test]
public async Task ShouldHandlePollingChanges()
{
    var persister = await factory.CreateSqlitePersisterAsync("test_store");
    var eventRaised = false;
    
    persister.DataChanged += (s, e) => eventRaised = true;
    
    await persister.StartListeningAsync();
    // Simulate database change
    await Task.Delay(6000); // Wait for polling cycle
    
    Assert.IsTrue(eventRaised);
}
```

## 🔄 Migration from TypeScript

### Key Changes for SQLite

| TypeScript SQLite | C# SQLite | Notes |
|------------------|----------|-------|
| `sqlite3.Database` | `SqliteConnection` | ADO.NET connection |
| `db.all()` | `SqliteCommand.ExecuteReader()` | ADO.NET execution |
| `db.on('change')` | `SqliteConnection.Change` | Built-in .NET support |
| `db.off('change')` | Event unsubscribe | Standard .NET events |
| `promiseNew()` | `async/await` | Native C# async patterns |
| Polling via `setInterval` | `System.Threading.Timer` | .NET timer implementation |

### Integration Pattern

**TypeScript:**
```typescript
import { createSqlite3Persister } from 'tinybase-persister-sqlite3';

const persister = await createSqlite3Persister(
    store, 
    db, 
    config,
    onSqlCommand,
    onIgnoredError
);
```

**C#:**
```csharp
using var persister = await persisterFactory.CreateSqlitePersisterAsync(
    storeId: "my_store",
    configOrTableName: config,
    onSqlCommand: (sql, parameters) => { /* logging */ },
    onIgnoredError: (error) => { /* error handling */ }
);
```

## 📚 Documentation Files

1. **SQLite README.md**: This comprehensive guide
2. **Configuration**: appsettings.json examples
3. **Examples**: Complete usage demonstrations
4. **XML Documentation**: Inline code documentation

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
- SQLite development team for the robust embedded database
- Microsoft for the Microsoft.Data.Sqlite library