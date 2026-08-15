namespace Reviewer.Models;

/// <summary>审核操作。</summary>
public enum ReviewAction
{
    /// <summary>通过，进入可捞池。</summary>
    Approve,

    /// <summary>驳回，进入废弃池，保留但不可捞。</summary>
    Reject,

    /// <summary>移交，进入 QQ 可捞的海峡云瓶。</summary>
    Transfer,
}

public record ReviewRequest(int QueueId, ReviewAction Action, string? Remark = null);
