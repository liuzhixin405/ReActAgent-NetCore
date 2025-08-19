using Agent;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("请提供项目目录路径");
            return;
        }

        var projectDirectory = Path.GetFullPath(args[0]);
        if (!Directory.Exists(projectDirectory))
        {
            Console.WriteLine($"目录不存在: {projectDirectory}");
            return;
        }

        // 定义工具
        var tools = new Dictionary<string, Func<string[], object>>
        {
            { "read_file", Tools.ReadFile },
            { "write_to_file", Tools.WriteToFile },
            { "run_terminal_command", Tools.RunTerminalCommand }
        };

        // 创建Agent实例，使用本地Ollama的qwen2.5-coder:7b模型
        var agent = new ReActAgent(tools, "qwen2.5-coder:7b", projectDirectory);

        Console.Write("请输入任务：");
        var task = Console.ReadLine();

        if (string.IsNullOrEmpty(task))
        {
            Console.WriteLine("任务不能为空");
            return;
        }

        try
        {
            var finalAnswer = await agent.RunAsync(task);
            Console.WriteLine($"\n\n✅ Final Answer：{finalAnswer}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n\n❌ 错误：{ex.Message}");
        }
    }
}