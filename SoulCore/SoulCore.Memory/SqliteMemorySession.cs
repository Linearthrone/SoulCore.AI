using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Sqlite;
namespace SoulCore.Memory;
public sealed class SqliteMemorySession : IAsyncDisposable, IDisposable {
  private readonly ILogger<SqliteMemorySession> _logger; private readonly SqliteConnection _connection; private bool _disposed;
  public SqliteMemorySession(IOptions<MemoryOptions> options, ILogger<SqliteMemorySession> logger) {
    ArgumentNullException.ThrowIfNull(options); _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    DatabasePath = options.Value.ResolveDbPath(); DbGate = SqlitePathGate.ForPath(DatabasePath);
    var dir = Path.GetDirectoryName(DatabasePath); if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Mode = SqliteOpenMode.ReadWriteCreate, Cache = SqliteCacheMode.Shared, DefaultTimeout = SqlitePathGate.DefaultBusyTimeoutMs / 1000 }.ToString());
    _connection.Open(); ApplyBusyTimeout(); ApplyMigrations(); IsDatabaseOpen = true; _logger.LogInformation("SqliteMemorySession ready at {DbPath}", DatabasePath);
  }
  public SqliteMemorySession(string dbPath, ILogger<SqliteMemorySession>? logger=null): this(Microsoft.Extensions.Options.Options.Create(new MemoryOptions{DbPath=dbPath}), logger??Microsoft.Extensions.Logging.Abstractions.NullLogger<SqliteMemorySession>.Instance){}
  public bool IsDatabaseOpen { get; private set; } public string DatabasePath { get; } internal SemaphoreSlim DbGate { get; } internal SqliteConnection Connection => _connection;
  internal Task RunDbAsync(Func<CancellationToken,Task> w, CancellationToken ct)=>SqliteDbGate.RunAsync(DbGate,_disposed,w,ct);
  internal Task<T> RunDbAsync<T>(Func<CancellationToken,Task<T>> w, CancellationToken ct)=>SqliteDbGate.RunAsync(DbGate,_disposed,w,ct);
  public void Dispose(){ if(_disposed)return; DbGate.Wait(); try{ if(!_disposed){_connection.Dispose();IsDatabaseOpen=false;_disposed=true;}} finally{DbGate.Release();}}
  public ValueTask DisposeAsync(){Dispose();return ValueTask.CompletedTask;}
  private void ApplyBusyTimeout(){using var c=_connection.CreateCommand();c.CommandText=$"PRAGMA busy_timeout = {SqlitePathGate.DefaultBusyTimeoutMs};";c.ExecuteNonQuery();}
  private void ApplyMigrations(){using(var p=_connection.CreateCommand()){p.CommandText="PRAGMA foreign_keys = ON;";p.ExecuteNonQuery();}
    ApplyIfMissing("001","SoulCore.Memory.Schema.001_schema.sql","SoulCore.Memory.Migrations.001_initial.sql");
    ApplyIfMissing("002","SoulCore.Memory.Migrations.002_embedding_vectors.sql");
    ApplyIfMissing("003","SoulCore.Memory.Migrations.003_victoria_tasks.sql");
    ApplyIfMissing("004","SoulCore.Memory.Migrations.004_victoria_workflows.sql");
    ApplyIfMissing("005","SoulCore.Memory.Migrations.005_episodic_source_model.sql");
    ApplyIfMissing("006","SoulCore.Memory.Migrations.006_victoria_journals.sql");}
  private void ApplyIfMissing(string v, params string[] scripts){ if(IsMigrationApplied(v))return; foreach(var s in scripts) ExecuteScript(ReadEmbedded(s)); _logger.LogInformation("Applied Memory migration {Version} to {DbPath}", v, DatabasePath);} 
  private bool IsMigrationApplied(string v){ try{ using var c=_connection.CreateCommand(); c.CommandText="SELECT 1 FROM schema_migrations WHERE version = $version LIMIT 1;"; c.Parameters.AddWithValue("$version", v); var r=c.ExecuteScalar(); return r is not null and not DBNull;} catch(SqliteException){return false;}}
  private void ExecuteScript(string sql){using var c=_connection.CreateCommand();c.CommandText=sql;c.ExecuteNonQuery();}
  private static string ReadEmbedded(string n){using var s=typeof(SqliteMemorySession).Assembly.GetManifestResourceStream(n)??throw new InvalidOperationException($"Embedded SQL not found: {n}"); using var r=new StreamReader(s); return r.ReadToEnd();}
}
