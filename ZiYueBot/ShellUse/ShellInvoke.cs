using ZiYueBot.Core;

namespace ZiYueBot.ShellUse;

public static class ShellInvoke
{
    public static Task Execute(string input)
    {
        try
        {
            var context = new ShellContext(EventType.GroupMessage, true);
            return EventHandler(context, input);
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }


    private static async Task EventHandler(ShellContext context, string input)
    {
        MessageChain chain = [new TextMessageEntity(input)];
        var explicitInvoke = false;
        var commandName =
            input.Contains(' ') ? input[..input.IndexOf(' ')] : input;
        explicitInvoke = commandName.StartsWith('/') || explicitInvoke;
        commandName = commandName.TrimStart('/');

        if (Commands.GetCommand(Platform.Console, commandName) is null)
        {
            if (Commands.CheckAlias(commandName, out var prompt))
            {
                await context.SendMessage($"命令未找到，你是否在找 /{prompt}？");
                return;
            }

            if (explicitInvoke)
            {
                await context.SendMessage("未知命令。请使用 /help 查看命令列表。");
            }
            return;
        }

        chain.RemoveAt(0);
        if (input.Contains(' ') && input.IndexOf(' ') != input.Length - 1)
            chain.Insert(0, new TextMessageEntity(input[(input.IndexOf(' ') + 1)..]));

        if (await Commands.CheckBlacklist(context, commandName)) return;

        await Commands.GetCommand(Platform.Console, commandName)!.Invoke(context, chain);

    }
}