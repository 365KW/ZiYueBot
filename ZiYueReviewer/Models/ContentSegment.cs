using System.Text;

namespace ZiYueReviewer.Models;

/// <summary>图片在数据库中的存储形式。</summary>
public enum ImageSourceKind
{
    Remote,
    Local,
    Base64,
}

/// <summary>
/// 云瓶内容的一段：纯文本或图片。文本以 Text 保存内容，图片以 Text 保存其本地路径、远程 URL 或 base64 数据。
/// </summary>
public class ContentSegment
{
    public bool IsImage { get; init; }
    public string Text { get; init; } = "";
    public ImageSourceKind ImageKind { get; init; }

    /// <summary>
    /// 将数据库字符串解析为内容段。\u2408…\u2409 为本地图片路径，\uE000…\uE001 为远程图片 URL。
    /// </summary>
    public static List<ContentSegment> Parse(string message)
    {
        var segments = new List<ContentSegment>();
        if (string.IsNullOrEmpty(message)) return segments;

        const char localStart = '\u2408', localEnd = '\u2409';
        const char remoteStart = '\uE000', remoteEnd = '\uE001';

        var text = new StringBuilder();

        void FlushText()
        {
            if (text.Length == 0) return;
            string s = text.ToString().Trim();
            text.Clear();
            if (s.Length > 0) segments.Add(new ContentSegment { IsImage = false, Text = s });
        }

        for (int i = 0; i < message.Length;)
        {
            char c = message[i];
            if (c is localStart or remoteStart)
            {
                int end = message.IndexOf(c == localStart ? localEnd : remoteEnd, i + 1);
                if (end < 0)
                {
                    text.Append(message[i..]);
                    break;
                }
                FlushText();
                string value = message[(i + 1)..end];
                segments.Add(c == localStart
                    ? new ContentSegment { IsImage = true, Text = value, ImageKind = ImageSourceKind.Local }
                    : new ContentSegment { IsImage = true, Text = value, ImageKind = ImageSourceKind.Remote });
                i = end + 1;
            }
            else
            {
                text.Append(c);
                i++;
            }
        }
        FlushText();
        return segments;
    }
}

public class BottleItem
{
    public int Id { get; init; }
    public ulong UserId { get; init; }
    public string Username { get; init; } = "";
    public DateTime Created { get; init; }
    public int Views { get; init; }
    public string Content { get; init; } = "";
    public List<ContentSegment> Segments { get; } = new();
}

/// <summary>“我的云瓶”页面数据：近六个月的提交记录与今日进度统计。</summary>
public class UserQueueData
{
    public ulong UserId { get; init; }
    public List<UserQueueRecord> Records { get; init; } = new();
    public TodayStats Today { get; init; } = new();
}

/// <summary>一条提交记录及其审核状态。</summary>
public class UserQueueRecord
{
    /// <summary>审核页“审核记录”表使用；队列页队列编号。</summary>
    public int QueueId { get; init; }
    public ulong UserId { get; init; }
    public string Username { get; init; } = "";
    public DateTime Created { get; init; }
    public List<ContentSegment> Segments { get; } = new();
    /// <summary>通过/驳回时的 driftbottles 编号，移交时为“海峡云瓶”，待审时为 null。</summary>
    public string? AssignedId { get; init; }
    /// <summary>pending / approved / rejected / transferred。</summary>
    public string Status { get; init; } = "pending";
    /// <summary>审核意见（审核员填写），可为 null。</summary>
    public string? Remark { get; init; }
}

/// <summary>今日审核进度统计。</summary>
public class TodayStats
{
    public int Total { get; init; }
    public int Pending { get; init; }
    public int Approved { get; init; }
    public int Rejected { get; init; }
    public int Transferred { get; init; }
}
