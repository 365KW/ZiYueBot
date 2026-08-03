using System.IO;
using System.Text.Json;
using MySql.Data.MySqlClient;
using ZiYueReviewer.Models;

namespace ZiYueReviewer.Services;

public class DatabaseService
{
    private readonly DatabaseConfig _config;

    public DatabaseService(DatabaseConfig config) => _config = config;

    private MySqlConnection OpenConnection()
    {
        MySqlConnection connection = new(
            $"Server={_config.Source};Port={_config.Port};Database={_config.Database};" +
            $"User={_config.User};Password={_config.Password};Charset=utf8mb4;AllowUserVariables=True;Pooling=true;");
        connection.Open();
        return connection;
    }

    public async Task<List<BottleItem>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        return await QueryAsync(
            "SELECT queue_id, userid, username, created, content, 0 AS views " +
            "FROM driftbottles_queue WHERE reviewed = 0 ORDER BY queue_id DESC",
            (row, content) => new BottleItem
            {
                Id = row.GetInt32("queue_id"),
                UserId = row.GetUInt64("userid"),
                Username = row.IsDBNull("username") ? "" : row.GetString("username"),
                Created = row.GetDateTime("created"),
                Views = row.GetInt32("views"),
                Content = content,
            },
            cancellationToken);
    }

    public async Task<List<BottleItem>> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        return await QueryAsync(
            "SELECT id, userid, username, created, content, views " +
            "FROM driftbottles WHERE pickable = 1 ORDER BY id DESC LIMIT 300",
            (row, content) => new BottleItem
            {
                Id = row.GetInt32("id"),
                UserId = row.GetUInt64("userid"),
                Username = row.IsDBNull("username") ? "" : row.GetString("username"),
                Created = row.GetDateTime("created"),
                Views = row.GetInt32("views"),
                Content = content,
            },
            cancellationToken);
    }

    private async Task<List<BottleItem>> QueryAsync(
        string sql,
        Func<RowReader, string, BottleItem> factory,
        CancellationToken cancellationToken,
        string? paramName = null,
        object? paramValue = null)
    {
        var items = new List<BottleItem>();

        await using MySqlConnection connection = OpenConnection();
        await using MySqlCommand command = new(sql, connection);
        if (paramName is not null)
        {
            command.Parameters.AddWithValue(paramName, paramValue);
        }
        await using MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync(cancellationToken);

        RowReader row = new(reader);
        int contentIndex = reader.GetOrdinal("content");

        while (await reader.ReadAsync(cancellationToken))
        {
            string content = reader.IsDBNull(contentIndex) ? "" : reader.GetString(contentIndex);
            BottleItem item = factory(row, content);
            item.Segments.AddRange(ContentSegment.Parse(content));
            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// 查询某用户近六个月提交的云瓶及其审核状态（公开的“我的云瓶”页面数据）。
    /// 通过/驳回通过 driftbottles 表按 (created, content) 匹配，移交通过 straitbottles 表匹配。
    /// </summary>
    public async Task<UserQueueData> GetUserQueueAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var records = new List<UserQueueRecord>();

        await using MySqlConnection connection = OpenConnection();

        var dictionary = new Dictionary<string, (int id, bool pickable)>();
        await using (MySqlCommand command = new(
            "SELECT id, created, content, pickable FROM driftbottles WHERE userid = @userId", connection))
        {
            command.Parameters.AddWithValue("@userId", userId);
            await using MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync(cancellationToken);
            int contentIndex = reader.GetOrdinal("content");
            while (await reader.ReadAsync(cancellationToken))
            {
                string key = reader.GetDateTime("created").ToString("yyyy-MM-dd HH:mm:ss") + "|" +
                             (reader.IsDBNull(contentIndex) ? "" : reader.GetString(contentIndex));
                dictionary[key] = (reader.GetInt32("id"), reader.GetBoolean("pickable"));
            }
        }

        var straitKeys = new HashSet<string>();
        await using (MySqlCommand command = new(
            "SELECT created, content FROM straitbottles WHERE userid = @userId", connection))
        {
            command.Parameters.AddWithValue("@userId", userId);
            await using MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync(cancellationToken);
            int contentIndex = reader.GetOrdinal("content");
            while (await reader.ReadAsync(cancellationToken))
            {
                straitKeys.Add(reader.GetDateTime("created").ToString("yyyy-MM-dd HH:mm:ss") + "|" +
                               (reader.IsDBNull(contentIndex) ? "" : reader.GetString(contentIndex)));
            }
        }

        await using (MySqlCommand command = new(
            "SELECT queue_id, created, content, reviewed, remark FROM driftbottles_queue " +
            "WHERE userid = @userId AND created >= DATE_SUB(NOW(), INTERVAL 6 MONTH) ORDER BY queue_id DESC",
            connection))
        {
            command.Parameters.AddWithValue("@userId", userId);
            await using MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync(cancellationToken);
            int contentIndex = reader.GetOrdinal("content");
            int remarkIndex = reader.GetOrdinal("remark");
            while (await reader.ReadAsync(cancellationToken))
            {
                string content = reader.IsDBNull(contentIndex) ? "" : reader.GetString(contentIndex);
                string key = reader.GetDateTime("created").ToString("yyyy-MM-dd HH:mm:ss") + "|" + content;

                string status = "pending";
                string? assignedId = null;
                if (reader.GetBoolean("reviewed"))
                {
                    if (straitKeys.Contains(key))
                    {
                        status = "transferred";
                        assignedId = "海峡云瓶";
                    }
                    else if (dictionary.TryGetValue(key, out (int id, bool pickable) target))
                    {
                        status = target.pickable ? "approved" : "rejected";
                        assignedId = target.id.ToString();
                    }
                }

                var record = new UserQueueRecord
                {
                    QueueId = reader.GetInt32("queue_id"),
                    Created = reader.GetDateTime("created"),
                    AssignedId = assignedId,
                    Status = status,
                    Remark = reader.IsDBNull(remarkIndex) ? null : reader.GetString(remarkIndex),
                };
                record.Segments.AddRange(ContentSegment.Parse(content));
                records.Add(record);            }
        }

        // 今日审核进度：统计今天提交的记录按状态分布
        DateTime today = DateTime.Today;
        int todayTotal = records.Count(r => r.Created.Date == today);
        var todayStats = new TodayStats
        {
            Total = todayTotal,
            Pending = records.Count(r => r.Created.Date == today && r.Status == "pending"),
            Approved = records.Count(r => r.Created.Date == today && r.Status == "approved"),
            Rejected = records.Count(r => r.Created.Date == today && r.Status == "rejected"),
            Transferred = records.Count(r => r.Created.Date == today && r.Status == "transferred"),
        };

        return new UserQueueData { UserId = userId, Records = records, Today = todayStats };
    }

    /// <summary>
    /// 全部云瓶审核记录（审核页“审核记录”表）。含分配到编号/海峡云瓶与状态。
    /// </summary>
    public async Task<List<UserQueueRecord>> GetAllReviewRecordsAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<UserQueueRecord>();

        await using MySqlConnection connection = OpenConnection();

        var dictionary = new Dictionary<(ulong, string), (int id, bool pickable)>();
        await using (MySqlCommand command = new(
            "SELECT userid, id, created, content, pickable FROM driftbottles", connection))
        {
            await using MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync(cancellationToken);
            int uidIndex = reader.GetOrdinal("userid");
            int contentIndex = reader.GetOrdinal("content");
            while (await reader.ReadAsync(cancellationToken))
            {
                string key = reader.GetDateTime("created").ToString("yyyy-MM-dd HH:mm:ss") + "|" +
                             (reader.IsDBNull(contentIndex) ? "" : reader.GetString(contentIndex));
                dictionary[(reader.GetUInt64(uidIndex), key)] = (reader.GetInt32("id"), reader.GetBoolean("pickable"));
            }
        }

        var straitKeys = new HashSet<(ulong, string)>();
        await using (MySqlCommand command = new(
            "SELECT userid, created, content FROM straitbottles", connection))
        {
            await using MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync(cancellationToken);
            int uidIndex = reader.GetOrdinal("userid");
            int contentIndex = reader.GetOrdinal("content");
            while (await reader.ReadAsync(cancellationToken))
            {
                straitKeys.Add((reader.GetUInt64(uidIndex),
                    reader.GetDateTime("created").ToString("yyyy-MM-dd HH:mm:ss") + "|" +
                    (reader.IsDBNull(contentIndex) ? "" : reader.GetString(contentIndex))));
            }
        }

        await using (MySqlCommand command = new(
            "SELECT queue_id, userid, username, created, content, reviewed, remark FROM driftbottles_queue " +
            "ORDER BY queue_id DESC",
            connection))
        {
            await using MySqlDataReader reader = (MySqlDataReader)await command.ExecuteReaderAsync(cancellationToken);
            int uidIndex = reader.GetOrdinal("userid");
            int usernameIndex = reader.GetOrdinal("username");
            int contentIndex = reader.GetOrdinal("content");
            int remarkIndex = reader.GetOrdinal("remark");
            while (await reader.ReadAsync(cancellationToken))
            {
                string content = reader.IsDBNull(contentIndex) ? "" : reader.GetString(contentIndex);
                string key = reader.GetDateTime("created").ToString("yyyy-MM-dd HH:mm:ss") + "|" + content;
                ulong uid = reader.GetUInt64(uidIndex);

                string status = "pending";
                string? assignedId = null;
                if (reader.GetBoolean("reviewed"))
                {
                    if (straitKeys.Contains((uid, key)))
                    {
                        status = "transferred";
                        assignedId = "海峡云瓶";
                    }
                    else if (dictionary.TryGetValue((uid, key), out (int id, bool pickable) target))
                    {
                        status = target.pickable ? "approved" : "rejected";
                        assignedId = target.id.ToString();
                    }
                }

                var record = new UserQueueRecord
                {
                    QueueId = reader.GetInt32("queue_id"),
                    UserId = uid,
                    Username = reader.IsDBNull(usernameIndex) ? "" : reader.GetString(usernameIndex),
                    Created = reader.GetDateTime("created"),
                    AssignedId = assignedId,
                    Status = status,
                    Remark = reader.IsDBNull(remarkIndex) ? null : reader.GetString(remarkIndex),
                };
                record.Segments.AddRange(ContentSegment.Parse(content));
                records.Add(record);
            }
        }

        return records;
    }

    /// <summary>
    /// 执行审核：将队列中的云瓶移入目标表并标记为已审核。
    /// </summary>
    public async Task ReviewAsync(ReviewRequest request, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = OpenConnection();
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        string insertSql = request.Action switch
        {
            ReviewAction.Approve =>
                "INSERT INTO driftbottles(userid, username, created, content, pickable) " +
                "SELECT userid, username, created, content, TRUE FROM driftbottles_queue WHERE queue_id = @queueId",
            ReviewAction.Reject =>
                "INSERT INTO driftbottles(userid, username, created, content, pickable) " +
                "SELECT userid, username, created, content, FALSE FROM driftbottles_queue WHERE queue_id = @queueId",
            ReviewAction.Transfer =>
                "INSERT INTO straitbottles(userid, username, created, content, fromDiscord) " +
                "SELECT userid, username, created, content, FALSE FROM driftbottles_queue WHERE queue_id = @queueId",
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };

        await using MySqlCommand insert = new(insertSql, connection, transaction);
        insert.Parameters.AddWithValue("@queueId", request.QueueId);
        int inserted = await insert.ExecuteNonQueryAsync(cancellationToken);

        if (inserted == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException($"队列中不存在编号 {request.QueueId} 的云瓶");
        }

        await using MySqlCommand markReviewed = new(
            "UPDATE driftbottles_queue SET reviewed = 1, remark = @remark WHERE queue_id = @queueId",
            connection, transaction);
        markReviewed.Parameters.AddWithValue("@queueId", request.QueueId);
        markReviewed.Parameters.AddWithValue("@remark", (object?)request.Remark ?? DBNull.Value);
        await markReviewed.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public static bool TryLoadConfig(out DatabaseConfig config, out string error, out string baseDir)
    {
        error = "";
        baseDir = Directory.GetCurrentDirectory();
        var candidates = new List<string>();

        DirectoryInfo? dir = new(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            candidates.Add(Path.Combine(dir.FullName, "config.json"));
            candidates.Add(Path.Combine(dir.FullName, "ZiYueBot", "config.json"));
            dir = dir.Parent;
        }
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "config.json"));

        foreach (string path in candidates)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                JsonDocument doc = JsonDocument.Parse(stream);
                JsonElement root = doc.RootElement;

                config = new DatabaseConfig
                {
                    Source = root.GetProperty("DatabaseSource").GetString() ?? "localhost",
                    Port = root.GetProperty("DatabasePort").GetUInt32(),
                    Database = root.GetProperty("DatabaseName").GetString() ?? "ziyuebot",
                    User = root.GetProperty("DatabaseUser").GetString() ?? "",
                    Password = root.GetProperty("DatabasePassword").GetString() ?? "",
                };
                baseDir = Path.GetDirectoryName(Path.GetFullPath(path))!;
                return true;
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                continue;
            }
        }

        config = new DatabaseConfig();
        error = "未找到 config.json（已尝试：" + string.Join("、", candidates.Take(10)) + "）";
        return false;
    }
}
