namespace ZiYueBot.ShellUse;

public class ShellRun
{
    public static async Task Run()
    {
        while (true)
        {
            Console.Write("> ");
            string? input = Console.ReadLine();
            if (input is null) break;
            if (input.Trim() is "exit" or "quit" or "q")
            {
                Console.WriteLine("控制台已退出。");
                break;
            }
            try
            {
                await ShellInvoke.Execute(input);
            }
            catch (Exception e)
            {
                Console.WriteLine($"命令执行失败：{e.Message}");
            }
        }
    }
}
