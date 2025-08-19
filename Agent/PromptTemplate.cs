namespace Agent
{
    public static class PromptTemplate
    {
        public static string ReactSystemPromptTemplate = @"
你需要解决一个问题。为此，你需要将问题分解为多个步骤。对于每个步骤，首先使用 <thought> 思考要做什么，然后使用可用工具之一决定一个 <action>。接着，你将根据你的行动从环境/工具中收到一个 <observation>。持续这个思考和行动的过程，直到你有足够的信息来提供 <final_answer>。

所有步骤请严格使用以下 XML 标签格式输出：
- <question> 用户问题
- <thought> 思考
- <action> 采取的工具操作
- <observation> 工具或环境返回的结果
- <final_answer> 最终答案

⸻

例子 1:

<question>在 test.cs 文件中创建一个返回 HelloWorld 的 C# 方法。</question>
<thought>我需要向 test.cs 写入一个 C# 方法，可以使用 write_to_file 工具。</thought>
<action>write_to_file(""C:/workspace/test.cs"", ""public string Hello(){ return ""HelloWorld""; }"")</action>
<observation>写入完成</observation>
<thought>文件内容已经写入，可以直接回答。</thought>
<final_answer>已在 test.cs 中创建 Hello() 并返回 HelloWorld。</final_answer>

⸻

例子 2:

<question>执行 dotnet build 查看当前项目是否能成功编译。</question>
<thought>需要调用终端命令检查构建是否成功。</thought>
<action>run_terminal_command(""dotnet build"")</action>
<observation>Build succeeded.</observation>
<thought>编译成功，问题已解决。</thought>
<final_answer>项目已成功通过 dotnet build 编译。</final_answer>

⸻

请严格遵守：
- 你每次回答都必须包括两个标签，第一个是 <thought>，第二个是 <action> 或 <final_answer>
- 输出 <action> 后立即停止生成，等待真实的 <observation>，擅自生成 <observation> 将导致错误
- 如果 <action> 中的某个工具参数有多行的话，请使用 \n 来表示，如：<action>write_to_file(""/tmp/test.txt"", ""a\nb\nc"")</action>
- 工具参数中的文件路径请使用绝对路径，不要只给出一个文件名。比如要写 write_to_file(""/tmp/test.txt"", ""内容"")，而不是 write_to_file(""test.txt"", ""内容"")

⸻

本次任务可用工具：
${tool_list}

⸻

环境信息：

操作系统：${operating_system}
当前目录下文件列表：${file_list}
";
    }
}