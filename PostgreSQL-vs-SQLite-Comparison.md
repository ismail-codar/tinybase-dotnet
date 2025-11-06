# PostgreSQL vs SQLite Persister Comparison

This document provides a detailed comparison between the PostgreSQL and SQLite implementations of the TinyBase persister, explaining the architectural differences and when to use each approach.

## 📊 Feature Comparison Matrix

| Feature | PostgreSQL Implementation | SQLite Implementation | Impact |
|---------|---------------------------|----------------------|---------|
| **Database Type** | Client-server | File-based, embedded | Deployment complexity |
| **Connection Management** | Connection pooling, multiple clients | Single file, embedded | Resource management |
| **Change Detection** | NOTIFY/LISTEN with triggers | Polling-based detection | Real-time vs periodic |
| **Schema Management** | Advanced introspection, triggers | SQLite PRAGMA commands | API differences |
| **Event System** | Database-level notifications | Application-level polling | Event timing |
| **Concurrency** | Multi-user, read/write | Single writer, multiple readers | Use case suitability |
| **Deployment** | Server required | Single file | Operational overhead |
| **Performance** | Optimized for concurrency | Optimized for simplicity | Workload dependent |

## 🏗️ Architecture Comparison

### PostgreSQL Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Application   │    │  PostgreSQL      │    │   PostgreSQL    │
│                 │    │     Server       │    │   Client Lib    │
│ ┌─────────────┐ │    │                  │    │ ┌─────────────┐ │
│ │PostgresPersister│───▶│ ┌─────────────┐ │    │ │ Npgsql      │ │
│ └─────────────┘ │    │ │Database     │ │    │ └─────────────┘ │
│                 │    │ │Triggers &   │ │    │                 │
│ ┌─────────────┐ │    │ │Functions    │ │    │ ┌─────────────┐ │
│ │Entity Framework│◀────│ │NOTIFY/LISTEN│ │◀───│ │ Connection  │ │
│ └─────────────┘ │    │ └─────────────┘ │    │ │ Pooling     │ │
└─────────────────┘    └──────────────────┘    └─────────────┘
```

**Key PostgreSQL Features:**
- Real-time change detection via `LISTEN/NOTIFY`
- Database-level triggers for DDL/DML events
- Connection pooling across multiple clients
- Advanced SQL features and schema introspection
- Support for stored procedures and functions

### SQLite Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Application   │    │  SQLite         │    │   Microsoft.    │
│                 │    │  Database       │    │  Data.Sqlite    │
│ ┌─────────────┐ │    │                 │    │ ┌─────────────┐ │
│ │SqlitePersister│───▶│ ┌─────────────┐ │    │ │ Sqlite      │ │
│ └─────────────┘ │    │ │File-based   │ │    │ │ Connection  │ │
│                 │    │ │Database     │ │    │ │ (Embedded)  │ │
│ ┌─────────────┐ │    │ │             │ │    │ └─────────────┘ │
│ │Entity Framework│◀────│ │Polling for │ │    │                 │
│ └─────────────┘ │    │ │Changes      │ │    │ ┌─────────────┐ │
└─────────────────┘    │ └─────────────┘ │    │ │ No Pooling  │ │
                       └─────────────────┘    │ │ (Single     │ │
                                               │ │  Connection)│ │
                                               └─────────────┘
```

**Key SQLite Features:**
- Polling-based change detection
- File-based, embedded database
- SQLite PRAGMA commands for introspection
- No connection pooling (single connection)
- Limited schema management capabilities

## 🔄 Implementation Differences

### 1. Change Detection Strategy

#### PostgreSQL (Event-Based)
```typescript
// TypeScript
const listenerHandle = await addChangeListener(
  channel,
  (prefixAndTableName) =>
    ifNotUndefined(
      strMatch(prefixAndTableName, EVENT_REGEX),
      async ([, eventType, tableName]) => {
        if (collHas(managedTableNamesSet, tableName)) {
          if (eventType == 'c:') {
            await addDataChangedTriggers(tableName, dataChangedFunctionName);
          }
          listener();
        }
      },
    ),
);
```

**C# PostgreSQL:**
```csharp
// Database triggers and NOTIFY/LISTEN
private async Task SetupDatabaseTriggersAsync(CancellationToken cancellationToken = default)
{
    // Create trigger function
    var functionName = await CreateTableCreatedFunctionAsync(cancellationToken);
    await CreateTableCreatedTriggerAsync(functionName, cancellationToken);
    
    // Set up LISTEN for notifications
    var command = new NpgsqlCommand($"LISTEN {_channelName}", _connection);
    await command.ExecuteNonQueryAsync(cancellationToken);
    
    _connection.Notification += OnNotificationReceived;
}
```

#### SQLite (Polling-Based)
```typescript
// TypeScript
const startPolling = () =>
  (interval = startInterval(
    () =>
      tryCatch(async () => {
        const [{d, s, c}] = await executeCommand(
          SELECT + ` ${DATA_VERSION} d,${SCHEMA_VERSION} s,TOTAL_CHANGES() c FROM ${PRAGMA}${DATA_VERSION} JOIN ${PRAGMA}${SCHEMA_VERSION}`,
        ) as [IdObj<number>];
        if (d != dataVersion || s != schemaVersion || c != totalChanges) {
          if (dataVersion != null) {
            listener();
          }
          dataVersion = d;
          schemaVersion = s;
          totalChanges = c;
        }
      }),
    autoLoadIntervalSeconds as number,
  ));
```

**C# SQLite:**
```csharp
// Timer-based polling
private Timer? _pollingTimer;

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
            var args = new SqlitePersisterEventArgs
            {
                Message = "version_change",
                EventType = "version_change"
            };
            DataChanged?.Invoke(this, args);
        }
    }
}
```

### 2. Database Connection Management

#### PostgreSQL
```csharp
// Connection pooling and multiple clients
public class PostgresPersisterFactory
{
    private readonly string _connectionString;
    
    public async Task<IPostgresPersister> CreatePostgresPersisterAsync(...)
    {
        var connection = new NpgsqlConnection(_connectionString);
        // PostgreSQL supports connection pooling
        await connection.OpenAsync();
        return new PostgresPersister(unitOfWork, connection, config);
    }
}
```

#### SQLite
```csharp
// Single connection, file-based
public class SqlitePersisterFactory
{
    public async Task<ISqlitePersister> CreateSqlitePersisterAsync(...)
    {
        var connection = new SqliteConnection(_connectionString);
        // SQLite is embedded, single file
        await connection.OpenAsync();
        return new SqlitePersister(unitOfWork, connection, config);
    }
    
    public async Task<ISqlitePersister> CreateFileBasedSqlitePersisterAsync(
        string storeId, string databasePath, ...)
    {
        var connectionString = $"Data Source={databasePath};Mode=ReadWriteCreate;Cache=Private;";
        var connection = new SqliteConnection(connectionString);
        // File-based database
        return new SqlitePersister(unitOfWork, connection, config);
    }
}
```

### 3. Schema Management

#### PostgreSQL
```csharp
// Advanced schema introspection
private async Task SetupDatabaseTriggersAsync(CancellationToken cancellationToken = default)
{
    // Create functions and triggers for DDL events
    await CreateTableCreatedFunctionAsync(cancellationToken);
    await CreateDataChangedFunctionAsync(cancellationToken);
    
    // Manage table creation
    await EnsureTableExistsAsync(tableName, cancellationToken);
    await CreateDataChangedTriggersAsync(tableName, functionName, cancellationToken);
}

private async Task<string> CreateTableCreatedFunctionAsync(CancellationToken cancellationToken = default)
{
    var functionName = EscapeIdentifier($"{TINYBASE_PREFIX}_{TABLE_CREATED}_{_configHash}");
    var sql = $@"
        CREATE OR REPLACE FUNCTION {functionName}()
        RETURNS event_trigger AS $$
        DECLARE
            row record;
        BEGIN
            FOR row IN SELECT object_identity FROM pg_event_trigger_ddl_commands() 
            WHERE command_tag = 'CREATE TABLE' LOOP 
                PERFORM pg_notify('{_channelName}', 'c:' || SPLIT_PART(row.object_identity, '.', 2));
            END LOOP;
        END;
        $$ LANGUAGE plpgsql;";
}
```

#### SQLite
```csharp
// Limited schema management using PRAGMA
public static string GetTableSchemaQuery(IEnumerable<string> tableNames)
{
    var tableArray = tableNames.ToArray();
    var placeholders = GetPlaceholders(tableArray.Length);
    
    return $"{SELECT} t.name as table_name, c.name as column_name FROM sqlite_master t, pragma_table_info(t.name) c {WHERE} t.type IN ('table','view') AND t.name IN ({placeholders}) ORDER BY t.name, c.ordinal_position";
}

public static string GetVersionQuery()
{
    return @"
        SELECT 
            CAST(user_version AS INTEGER) as data_version,
            CAST(schema_version AS INTEGER) as schema_version,
            total_changes() as total_changes
        FROM pragma_user_version, pragma_schema_version";
}
```

## 📈 Performance Characteristics

### PostgreSQL Performance
- **Strengths:** Optimized for concurrent read/write operations
- **Best for:** Multi-user applications, high concurrency
- **Scalability:** Can handle thousands of concurrent connections
- **Network overhead:** Client-server communication
- **Latency:** Network-dependent, but optimized for performance

### SQLite Performance
- **Strengths:** Fast for single-user scenarios, no network overhead
- **Best for:** Desktop applications, mobile apps, single-user scenarios
- **Scalability:** Excellent for small to medium datasets
- **File I/O:** Direct file system operations
- **Latency:** No network latency, very fast local operations

## 🛠️ Use Case Recommendations

### Choose PostgreSQL When:

1. **Multi-User Applications**
   - Web applications with multiple concurrent users
   - Server-side applications with high concurrency
   - Distributed systems

2. **Real-Time Requirements**
   - Need immediate change notifications
   - Real-time data synchronization
   - Live dashboards and monitoring

3. **Enterprise Features**
   - Advanced SQL features required
   - Complex schema management
   - Stored procedures and functions

4. **High Availability**
   - Need database replication
   - Backup and recovery features
   - Point-in-time recovery

### Choose SQLite When:

1. **Single-User Applications**
   - Desktop applications
   - Mobile applications
   - Local data storage

2. **Simple Deployment**
   - Need single-file deployment
   - No database server infrastructure
   - Embedded applications

3. **Rapid Development**
   - Quick prototyping
   - Development and testing
   - Simple data persistence

4. **Resource Constraints**
   - Limited memory and CPU
   - Portable applications
   - Offline-capable applications

## 🔄 Migration Between Implementations

### Configuration Compatibility

Both implementations support similar configuration formats:

```csharp
// Same JSON configuration works for both
var config = @"
{
    ""isJson"": true,
    ""managedTableNames"": [""users"", ""products"", ""orders""],
    ""autoLoadIntervalSeconds"": 5
}";

// PostgreSQL
using var pgPersister = await pgFactory.CreatePostgresPersisterAsync("store", config);

// SQLite  
using var sqlitePersister = await sqliteFactory.CreateSqlitePersisterAsync("store", config);
```

### API Compatibility

The core interfaces are designed to be compatible:

```csharp
// Similar interface, different implementations
public interface IPostgresPersister : IDisposable { ... }
public interface ISqlitePersister : IDisposable { ... }

// Same methods, different underlying technology
await persister.LoadAsync();
await persister.SaveAsync();
await persister.StartListeningAsync();
await persister.StopListeningAsync();
```

### Data Structure Compatibility

Both use the same entity models (with namespace differences):

```csharp
// PostgreSQL
public class Store { ... }
public class Table { ... } 
public class Cell { ... }

// SQLite
public class SqliteStore { ... }
public class SqliteTable { ... }
public class SqliteCell { ... }
```

## 🎯 Decision Matrix

| Criteria | PostgreSQL | SQLite | Winner |
|----------|------------|---------|---------|
| **Setup Complexity** | High (server setup) | Low (file creation) | 🏆 SQLite |
| **Deployment** | Complex (infrastructure) | Simple (single file) | 🏆 SQLite |
| **Concurrent Users** | Excellent | Limited | 🏆 PostgreSQL |
| **Real-time Changes** | Excellent | Good (polling) | 🏆 PostgreSQL |
| **Performance** | Network-dependent | Very fast | 🏆 SQLite |
| **Feature Richness** | Excellent | Limited | 🏆 PostgreSQL |
| **Backup/Recovery** | Advanced | Basic | 🏆 PostgreSQL |
| **Development Speed** | Medium | Fast | 🏆 SQLite |
| **Production Ready** | Excellent | Good | 🏆 PostgreSQL |
| **Learning Curve** | Steep | Gentle | 🏆 SQLite |

## 💡 Hybrid Approach

For applications that need both PostgreSQL and SQLite:

```csharp
public class HybridPersisterFactory
{
    private readonly ISqlitePersisterFactory _sqliteFactory;
    private readonly IPostgresPersisterFactory _postgresFactory;
    
    public async Task<IPersister> CreatePersisterAsync(
        string storeId, 
        DatabaseType type, 
        string connectionString)
    {
        return type switch
        {
            DatabaseType.PostgreSQL => await _postgresFactory.CreatePostgresPersisterAsync(
                storeId, connectionString),
            DatabaseType.Sqlite => await _sqliteFactory.CreateSqlitePersisterAsync(
                storeId, connectionString),
            _ => throw new ArgumentException("Unknown database type")
        };
    }
}

public enum DatabaseType
{
    PostgreSQL,
    Sqlite
}
```

## 📚 Summary

Both PostgreSQL and SQLite implementations provide robust TinyBase persistence, but serve different use cases:

- **PostgreSQL** is the choice for enterprise applications, multi-user scenarios, and real-time requirements
- **SQLite** excels in simplicity, single-user applications, and rapid development

The choice depends on your specific requirements for concurrency, deployment complexity, and feature needs. Both implementations maintain API compatibility to make migration between them straightforward when requirements change.