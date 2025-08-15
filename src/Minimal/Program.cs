using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MinimalAgent
{
	class Program
	{
		static async Task Main(string[] args)
		{
			try
			{
				Console.WriteLine("🤖 ReAct智能体 - 最简版本");
				Console.WriteLine("========================");
				Console.WriteLine();

				// 获取项目目录
				string projectDirectory;
				if (args.Length > 0)
				{
					projectDirectory = args[0];
				}
				else
				{
					Console.Write("请输入项目目录路径（或按回车使用默认的'proj'目录）：");
					var input = Console.ReadLine();
					if (string.IsNullOrWhiteSpace(input))
					{
						// 创建默认的proj目录
						projectDirectory = Path.Combine(Directory.GetCurrentDirectory(), "proj");
						if (!Directory.Exists(projectDirectory))
						{
							Directory.CreateDirectory(projectDirectory);
							Console.WriteLine($"✅ 已创建默认项目目录：{projectDirectory}");
						}
					}
					else
					{
						projectDirectory = input;
					}
				}

				// 验证目录是否存在，如果不存在则创建（针对proj目录）
				if (!Directory.Exists(projectDirectory))
				{
					// 如果是默认的proj目录，则创建它
					if (Path.GetFileName(projectDirectory) == "proj")
					{
						Directory.CreateDirectory(projectDirectory);
						Console.WriteLine($"✅ 已创建项目目录：{projectDirectory}");
					}
					else
					{
						Console.WriteLine($"❌ 目录不存在：{projectDirectory}");
						return;
					}
				}

				projectDirectory = Path.GetFullPath(projectDirectory);
				Console.WriteLine($"📁 工作目录：{projectDirectory}");
				Console.WriteLine();

				// 设置工具的项目目录
				FileTools.SetProjectDirectory(projectDirectory);

				// 创建工具字典
				var tools = new Dictionary<string, Delegate>
				{
					{ "read_file", FileTools.ReadFileAsync },
					{ "write_to_file", FileTools.WriteToFileAsync },
					{ "list_directory", FileTools.ListDirectory },
					{ "search_files", FileTools.SearchFiles },
					{ "create_directory", FileTools.CreateDirectory },
					{ "run_terminal_command", FileTools.RunTerminalCommandAsync }
				};

				// 创建ReAct智能体
				var model = "qwen2.5-coder:7b"; // 默认模型
				var ollamaBaseUrl = "http://localhost:11434"; // 默认Ollama地址

				var agent = new ReActAgent(
					tools: tools,
					model: model,
					projectDirectory: projectDirectory,
					ollamaBaseUrl: ollamaBaseUrl
				);

				Console.WriteLine("✅ 智能体初始化完成！");
				Console.WriteLine("💡 使用说明：");
				Console.WriteLine($"   - 确保Ollama服务正在运行 ({ollamaBaseUrl})");
				Console.WriteLine($"   - 确保已下载 {model} 模型");
				Console.WriteLine("   - 输入任务描述，智能体会自动执行");
				Console.WriteLine("   - 支持的文件操作：读取、写入、列目录、搜索、创建目录");
				Console.WriteLine();

				// 主循环
				while (true)
				{
					Console.Write("🎯 请输入任务（输入'exit'退出）：");
					var task = Console.ReadLine();

					if (string.IsNullOrWhiteSpace(task))
						continue;

					if (task.ToLower() == "exit")
						break;

					try
					{
						Console.WriteLine("\n🚀 开始执行任务...");
						var finalAnswer = await agent.RunAsync(task);
						Console.WriteLine($"\n✅ 任务完成！");
						Console.WriteLine($"📝 最终答案：{finalAnswer}");
					}
					catch (Exception ex)
					{
						Console.WriteLine($"\n❌ 任务执行失败：{ex.Message}");
						if (ex.InnerException != null)
						{
							Console.WriteLine($"   详细错误：{ex.InnerException.Message}");
						}
					}

					Console.WriteLine("\n" + new string('=', 50) + "\n");
				}

				Console.WriteLine("👋 再见！");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ 程序启动失败：{ex.Message}");
				Console.WriteLine("请检查：");
				Console.WriteLine("1. Ollama服务是否正在运行");
				Console.WriteLine("2. 网络连接是否正常");
				Console.WriteLine("3. 项目目录路径是否正确");
			}
		}
	}
} 