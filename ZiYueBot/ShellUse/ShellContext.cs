using System.Text;
using ZiYueBot.Core;

namespace ZiYueBot.ShellUse;

public class ShellContext(EventType eventType, bool adminEnable) : Context
{
    public override Platform Platform => Platform.Console;
    public override EventType EventType { get; } = eventType;
    public override string UserName => "控制台";
    public override ulong UserId => ZiYueBot.Instance.Config.ConsoleUserId;
    public override bool HasChannelAdmin { get; } = adminEnable;
    public override Task SendMessage(MessageChain messageChain)
    {
        var outMessage = new StringBuilder();

        foreach (var message in messageChain)
        {
            switch (message.Type)
            {
                case MessageEntityType.Text:
                    outMessage.Append(((TextMessageEntity)message).Text);
                    break;
                case MessageEntityType.Image:
                    outMessage.Append(((ImageMessageEntity)message).Path);
                    break;
                case MessageEntityType.Ping:
                    outMessage.Append(((PingMessageEntity)message).UserId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        Console.WriteLine(outMessage);
        return Task.CompletedTask;
    }

    public override Task<string> FetchUserName(ulong userId)
    {
        return Task.FromResult("Console");
    }
}