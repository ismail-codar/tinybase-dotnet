# TypeScript to C# Mapping Guide

This document provides a detailed mapping between the original TypeScript PostgreSQL persister implementation and the C# version, explaining how each component was translated and adapted for the .NET ecosystem.

## 📋 Table of Contents

1. [Core TypeScript Components](#core-typescript-components)
2. [C# Architecture Mapping](#c-architecture-mapping)
3. [Function-by-Function Mapping](#function-by-function-mapping)
4. [Data Structure Mapping](#data-structure-mapping)
5. [Database Operations](#database-operations)
6. [Event System](#event-system)
7. [Error Handling](#error-handling)

## 🔄 Core TypeScript Components

### Original TypeScript Structure

```typescript
// Main entry point
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
    configOrTableName,
    commandSql?.unsafe,
    // PostgreSQL-specific listeners
    async (channel, listener) => sql.listen(channel, listener),
    (notifyListener) => tryCatch(notifyListener.unlisten, onIgnoredError),
    onSqlCommand,
    onIgnoredError,
    () => commandSql?.release?.(),
    3, // StoreOrMergeableStore
    sql,
    'getSql'
  ) as PostgresPersister;
});
```

## 🏗️ C# Architecture Mapping

### 1. Service Layer Pattern

**TypeScript Approach:**
- Single function returning a promise
- Direct PostgreSQL client usage
- Embedded configuration parsing

**C# Approach:**
```csharp
// Factory pattern for better testability and DI
public interface IPostgresPersisterFactory
{
    Task<IPostgresPersister> CreatePostgresPersisterAsync(
        string storeId,
        string? configOrTableName = null,
        Action<string, object[]>? onSqlCommand = null,
        Action<Exception>? onIgnoredError = null,
        CancellationToken cancellationToken = default);
}

public class PostgresPersisterFactory : IPostgresPersisterFactory
{
    public async Task<IPostgresPersister> CreatePostgresPersisterAsync(...)
    {
        // Parse configuration
        var config = await ParseConfigurationAsync(storeId, configOrTableName, cancellationToken);
        
        // Create dependencies
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

### 2. Entity Framework Integration

**TypeScript Approach:**
```typescript
// Raw SQL client
const commandSql = await sql.reserve?.();
// Direct database operations
await executeCommand(sql);
```

**C# Approach:**
```csharp
// Entity Framework DbContext
public class TinyBaseDbContext : DbContext
{
    public DbSet<Store> Stores { get; set; }
    public DbSet<Table> Tables { get; set; }
    public DbSet<Cell> Cells { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure entity relationships and constraints
        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(e => e.Tables)
                  .WithOne(t => t.Store)
                  .HasForeignKey(t => t.StoreId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        // ... more configurations
    }
}

// Repository pattern for data access
public class StoreRepository : IStoreRepository
{
    public async Task<Store?> GetByConfigHashAsync(string configHash, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(s => s.ConfigHash == configHash, cancellationToken);
    }
}
```

## 🔍 Function-by-Function Mapping

### 1. Configuration Parsing

**TypeScript:**
```typescript
const [isJson, , defaultedConfig, managedTableNamesSet] = getConfigStructures(
  configOrStoreTableName,
);
const configHash = EMPTY_STRING + getHash(jsonStringWithUndefined(defaultedConfig));
const channel = TINYBASE + '_' + configHash;
```

**C#:**
```csharp
public static (bool isJson, PersisterConfig? rawConfig, PersisterConfig defaultedConfig, HashSet<string> managedTableNamesSet) 
    GetConfigStructures(string? configOrTableName = null)
{
    var isJson = false;
    PersisterConfig? rawConfig = null;

    if (!string.IsNullOrEmpty(configOrTableName))
    {
        if (configOrTableName.StartsWith("{"))
        {
            // JSON configuration
            isJson = true;
            try
            {
                rawConfig = JsonSerializer.Deserialize<PersisterConfig>(configOrTableName, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Invalid JSON configuration: {ex.Message}", nameof(configOrTableName), ex);
            }
        }
        else
        {
            // Simple table name
            rawConfig = new PersisterConfig
            {
                IsJson = false,
                ManagedTableNames = new List<string> { configOrTableName }
            };
        }
    }

    var defaultedConfig = new PersisterConfig
    {
        IsJson = rawConfig?.IsJson ?? false,
        StoreTableName = rawConfig?.StoreTableName ?? "tinybase_store",
        ManagedTableNames = new List<string>(rawConfig?.ManagedTableNames ?? new List<string> { "default_table" }),
        TableConfigs = new List<TableConfig>(rawConfig?.TableConfigs ?? new List<TableConfig>())
    };

    var managedTableNamesSet = new HashSet<string>(defaultedConfig.ManagedTableNames);
    
    return (isJson, rawConfig, defaultedConfig, managedTableNamesSet);
}
```

### 2. Database Function Creation

**TypeScript:**
```typescript
const createFunction = async (
  name: string,
  body: string,
  returnPrefix = '',
  declarations = '',
): Promise<string> => {
  const escapedFunctionName = escapeIds(TINYBASE, name, configHash);
  await executeCommand(
    CREATE +
      OR_REPLACE +
      FUNCTION +
      escapedFunctionName +
      `()RETURNS ${returnPrefix}trigger ` +
      `AS $$ ${declarations}BEGIN ${body}END;$$ LANGUAGE plpgsql;`,
  );
  return escapedFunctionName;
};
```

**C#:**
```csharp
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

    var command = new NpgsqlCommand(sql, _connection);
    await command.ExecuteNonQueryAsync(cancellationToken);
    return functionName;
}
```

### 3. Trigger Creation

**TypeScript:**
```typescript
const addDataChangedTriggers = (
  tableName: string,
  dataChangedFunction: string,
) =>
  promiseAll(
    arrayMap([INSERT, DELETE, UPDATE], (action, newOrOldOrBoth) =>
      createTrigger(
        OR_REPLACE,
        escapeIds(TINYBASE, DATA_CHANGED, configHash, tableName, action),
        `AFTER ${action} ON${escapeId(tableName)}FOR EACH ROW WHEN(${when(
          tableName,
          newOrOldOrBoth as 0 | 1 | 2,
        )})`,
        dataChangedFunction,
      ),
    ),
  );
```

**C#:**
```csharp
private async Task CreateDataChangedTriggersAsync(string tableName, string functionName, CancellationToken cancellationToken = default)
{
    var escapedTableName = EscapeIdentifier(tableName);
    
    // Create triggers for INSERT, UPDATE, DELETE
    var operations = new[] { "INSERT", "UPDATE", "DELETE" };
    
    foreach (var operation in operations)
    {
        var triggerName = EscapeIdentifier($"{TINYBASE_PREFIX}_{DATA_CHANGED}_{_configHash}_{tableName}_{operation}");
        var sql = $@"
            CREATE OR REPLACE TRIGGER {triggerName}
            AFTER {operation} ON {escapedTableName}
            FOR EACH ROW
            EXECUTE FUNCTION {functionName}();";

        var command = new NpgsqlCommand(sql, _connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
```

## 📊 Data Structure Mapping

### Store/MergeableStore

**TypeScript:**
```typescript
interface Store {
  // TinyBase store interface
}

interface MergeableStore extends Store {
  // Additional mergeable functionality
}
```

**C#:**
```csharp
public class Store
{
    [Key]
    [MaxLength(255)]
    public string Id { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Name { get; set; }

    public bool IsMergeable { get; set; }

    [Column(TypeName = "jsonb")]
    public string Configuration { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string ConfigHash { get; set; } = string.Empty;

    public bool IsPersisted { get; set; }

    public virtual ICollection<Table> Tables { get; set; } = new List<Table>();
}
```

### Table Structure

**TypeScript:**
```typescript
// Implicit in TypeScript - just table names and configurations
const managedTableNamesSet: Set<string> = new Set();
```

**C#:**
```csharp
public class Table
{
    [Key]
    [MaxLength(255)]
    public string Id { get; set; } = string.Empty;

    [MaxLength(255)]
    public string StoreId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Name { get; set; }

    public bool IsManaged { get; set; }

    [Column(TypeName = "jsonb")]
    public string Schema { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsJsonTable { get; set; }

    public virtual ICollection<Cell> Cells { get; set; } = new List<Cell>();

    [ForeignKey(nameof(StoreId))]
    public virtual Store Store { get; set; } = null!;
}
```

### Cell Data

**TypeScript:**
```typescript
// Cells are stored in the TinyBase store, not directly in the database
// They get serialized/deserialized during persistence operations
```

**C#:**
```csharp
public class Cell
{
    [MaxLength(255)]
    public string TableId { get; set; } = string.Empty;

    [MaxLength(255)]
    public string RowId { get; set; } = string.Empty;

    [MaxLength(255)]
    public string ColumnId { get; set; } = string.Empty;

    [Column(TypeName = "text")]
    public string Value { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Key]
    [Column(Order = 1)]
    [MaxLength(255)]
    public string Id => $"{TableId}_{RowId}_{ColumnId}";

    [ForeignKey(nameof(TableId))]
    public virtual Table Table { get; set; } = null!;
}
```

## 🗄️ Database Operations

### Connection Management

**TypeScript:**
```typescript
const commandSql = await sql.reserve?.();
// ... operations
() => commandSql?.release?.(),
```

**C#:**
```csharp
public async Task StartListeningAsync(CancellationToken cancellationToken = default)
{
    if (_isListening) return;

    try
    {
        await _connection.OpenAsync(cancellationToken);
        var command = new NpgsqlCommand($"LISTEN {_channelName}", _connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
        
        _connection.Notification += OnNotificationReceived;
        _isListening = true;
    }
    catch (Exception ex)
    {
        _config.OnIgnoredError?.Invoke(ex);
        throw;
    }
}

public async Task StopListeningAsync(CancellationToken cancellationToken = default)
{
    if (!_isListening) return;

    try
    {
        _connection.Notification -= OnNotificationReceived;
        var command = new NpgsqlCommand($"UNLISTEN {_channelName}", _connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await _connection.CloseAsync(cancellationToken);
        _isListening = false;
    }
    catch (Exception ex)
    {
        _config.OnIgnoredError?.Invoke(ex);
    }
}
```

### Change Detection

**TypeScript:**
```typescript
const listenerHandle = await addChangeListener(
  channel,
  (prefixAndTableName) =>
    ifNotUndefined(
      strMatch(prefixAndTableName, EVENT_REGEX),
      async ([, eventType, tableName]) => {
        if (collHas(managedTableNamesSet, tableName)) {
          if (eventType == 'c:') {
            await addDataChangedTriggers(
              tableName,
              dataChangedFunctionName,
            );
          }
          listener();
        }
      },
    ),
);
```

**C#:**
```csharp
private void OnNotificationReceived(object? sender, NpgsqlNotificationEventArgs e)
{
    try
    {
        var message = e.Payload;
        if (message.StartsWith($"{TABLE_CREATED}:"))
        {
            var tableName = message.Substring(2);
            var args = new PersisterEventArgs
            {
                Channel = e.Channel,
                Message = message,
                EventType = TABLE_CREATED,
                TableName = tableName
            };
            TableCreated?.Invoke(this, args);
        }
        else if (message.StartsWith($"{DATA_CHANGED}:"))
        {
            var tableName = message.Substring(2);
            var args = new PersisterEventArgs
            {
                Channel = e.Channel,
                Message = message,
                EventType = DATA_CHANGED,
                TableName = tableName
            };
            DataChanged?.Invoke(this, args);
        }
    }
    catch (Exception ex)
    {
        _config.OnIgnoredError?.Invoke(ex);
    }
}
```

## 🎯 Event System

### TypeScript Event Handling

**TypeScript:**
```typescript
// Direct event listener registration
persister.onDataChange = (listener) => { /* handle change */ };
```

**C# Event System:**
```csharp
public class PersisterEventArgs : EventArgs
{
    public string? Channel { get; set; }
    public string? Message { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
}

public interface IPostgresPersister : IDisposable
{
    event EventHandler<PersisterEventArgs> DataChanged;
    event EventHandler<PersisterEventArgs> TableCreated;
    // ... other members
}

// Usage:
persister.DataChanged += (sender, args) =>
{
    Console.WriteLine($"Data changed in table: {args.TableName}");
};

persister.TableCreated += (sender, args) =>
{
    Console.WriteLine($"Table created: {args.TableName}");
};
```

## ⚠️ Error Handling

### TypeScript Approach

**TypeScript:**
```typescript
const tryCatch = (fn: Function, onError: Function) => {
  try {
    return fn();
  } catch (error) {
    onError(error);
  }
};

// Usage
(notifyListener: ListenMeta) =>
  tryCatch(notifyListener.unlisten, onIgnoredError),
```

**C# Approach**

**C#:**
```csharp
// C# uses built-in exception handling
public async Task StopListeningAsync(CancellationToken cancellationToken = default)
{
    if (!_isListening) return;

    try
    {
        _connection.Notification -= OnNotificationReceived;
        // ... other cleanup
        _isListening = false;
    }
    catch (Exception ex)
    {
        _config.OnIgnoredError?.Invoke(ex);
    }
}

// Callback-based error handling
var persisterConfig = new PostgresPersisterConfig
{
    OnSqlCommand = (sql, parameters) => 
    {
        logger.LogDebug("Executing SQL: {SQL}", sql);
    },
    OnIgnoredError = (error) => 
    {
        logger.LogWarning("Ignored error: {Error}", error.Message);
    }
};
```

## 🔄 Integration Differences

### 1. Store Integration

**TypeScript:**
```typescript
// Store is passed directly and methods are called on it
export const createPostgresPersister = (async (
  store: Store | MergeableStore, // Direct store reference
  // ...
) => {
  // Store methods are called directly
  return persister;
})();
```

**C#:**
```csharp
// C# version uses identifiers and callbacks
public async Task<IPostgresPersister> CreatePostgresPersisterAsync(
    string storeId, // Identifier instead of object
    // ...
)
{
    // Integration is done through events and callbacks
    persister.DataChanged += async (sender, args) =>
    {
        // Handle data changes - call back to store if needed
        // store?.OnDataChanged(args.TableName);
    };
    
    return persister;
}
```

### 2. Configuration Flexibility

**TypeScript:**
```typescript
// Configuration is object-based with type safety
const config: DatabasePersisterConfig = {
  storeTableName: "optional_name",
  // ... other options
};
```

**C#:**
```csharp
// C# uses classes and interfaces for type safety
public class PostgresPersisterConfig
{
    public string? StoreTableName { get; set; }
    public List<string> ManagedTableNames { get; set; } = new();
    public bool IsJson { get; set; }
    public List<TableConfig> TableConfigs { get; set; } = new();
    public Action<string, object[]>? OnSqlCommand { get; set; }
    public Action<Exception>? OnIgnoredError { get; set; }
}
```

## 📈 Performance Considerations

### 1. Database Connection Pooling

**TypeScript:**
- Connection pooling handled by the `postgres` library
- Manual connection management with `reserve()`/`release()`

**C#:**
- Built-in connection pooling with NpgsqlConnection
- Automatic connection management through DI
- Entity Framework handles connection lifecycle

### 2. Async/Await Pattern

**TypeScript:**
```typescript
// Promises with async/await
const result = await executeCommand(sql);
```

**C#:**
```csharp
// Native async/await with CancellationToken support
public async Task<Store> AddAsync(Store entity, CancellationToken cancellationToken = default)
{
    return await _dbSet.AddAsync(entity, cancellationToken);
}
```

### 3. Memory Management

**TypeScript:**
- JavaScript garbage collection
- Manual cleanup with try-catch blocks

**C#:**
- IDisposable pattern for proper resource cleanup
- Using statements for automatic disposal
- Strong typing prevents memory leaks

## 🎯 Key Benefits of C# Implementation

1. **Type Safety**: Strong typing throughout the application
2. **Entity Framework**: Powerful ORM with migration support
3. **Dependency Injection**: Better testability and modularity
4. **Cancellation Support**: Proper async cancellation patterns
5. **Event System**: Standard .NET event patterns
6. **Error Handling**: Comprehensive exception handling
7. **Performance**: Connection pooling and optimization
8. **Maintainability**: Clean architecture and separation of concerns

This mapping provides a comprehensive guide for understanding how the sophisticated TypeScript PostgreSQL persister was successfully recreated in C# while maintaining the same core functionality and improving upon the architecture for the .NET ecosystem.