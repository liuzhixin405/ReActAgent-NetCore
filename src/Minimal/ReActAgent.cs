using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json.Serialization;

namespace MinimalAgent
{
	public class ReActAgent
	{
		private readonly Dictionary<string, Delegate> _tools;
		private readonly string _model;
		private readonly string _projectDirectory;
		private readonly HttpClient _httpClient;
		private readonly string _ollamaBaseUrl;

		public ReActAgent(Dictionary<string, Delegate> tools, string model, string projectDirectory, string ollamaBaseUrl = "http://localhost:11434")
		{
			_tools = tools;
			_model = model;
			_projectDirectory = projectDirectory;
			_ollamaBaseUrl = ollamaBaseUrl;
			_httpClient = new HttpClient();
			_httpClient.Timeout = TimeSpan.FromMinutes(60);
		}

		public async Task<string> RunAsync(string userInput)
		{
			var messages = new List<ChatMessage>
			{
				new ChatMessage { Role = "system", Content = RenderSystemPrompt() },
				new ChatMessage { Role = "user", Content = $"<task>{userInput}</task>" }
			};

			int iterationCount = 0;
			const int maxIterations = 20;

			while (iterationCount < maxIterations)
			{
				iterationCount++;
				Console.WriteLine($"\n🔄 迭代次数: {iterationCount}/{maxIterations}");

				// 调用模型
				var response = await CallModelAsync(messages);
				if (string.IsNullOrEmpty(response))
				{
					return "❌ 模型响应为空";
				}

				Console.WriteLine($"🤖 AI响应：\n{response}");

				// 提取思考
				var thoughtMatch = Regex.Match(response, @"<thought>(.*?)</thought>", RegexOptions.Singleline);
				if (thoughtMatch.Success)
				{
					var thought = thoughtMatch.Groups[1].Value.Trim();
					Console.WriteLine($"\n🤔 思考: {thought}");
				}

				// 先提取并执行所有行动
				var actionMatches = Regex.Matches(response, @"<action>(.*?)</action>", RegexOptions.Singleline);
				if (actionMatches.Count > 0)
				{
					Console.WriteLine($"🔍 找到 {actionMatches.Count} 个行动");
					
					foreach (Match actionMatch in actionMatches)
					{
						var action = actionMatch.Groups[1].Value.Trim();
						Console.WriteLine($"🔍 提取到行动: {action}");
						
						var (toolName, args) = ParseAction(action);
						Console.WriteLine($"🔧 解析工具: {toolName}, 参数: [{string.Join(", ", args)}]");

						try
						{
							var observation = await ExecuteToolAsync(toolName, args);
							Console.WriteLine($"📋 工具执行结果: {observation}");

							// 添加观察结果到消息历史
							messages.Add(new ChatMessage { Role = "assistant", Content = response });
							messages.Add(new ChatMessage { Role = "user", Content = $"<observation>{observation}</observation>" });
						}
						catch (Exception ex)
						{
							Console.WriteLine($"❌ 工具执行失败: {ex.Message}");
							messages.Add(new ChatMessage { Role = "assistant", Content = response });
							messages.Add(new ChatMessage { Role = "user", Content = $"<error>工具执行失败: {ex.Message}</error>" });
						}
					}
				}

				// 检查是否有最终答案
				var finalAnswerMatch = Regex.Match(response, @"<final_answer>(.*?)</final_answer>", RegexOptions.Singleline);
				if (finalAnswerMatch.Success)
				{
					var finalAnswer = finalAnswerMatch.Groups[1].Value.Trim();
					Console.WriteLine($"\n✅ 最终答案: {finalAnswer}");
					
					// 从最终答案中提取并执行命令
					var executedCommands = await ExtractAndExecuteCommands(finalAnswer);
					if (!string.IsNullOrEmpty(executedCommands))
					{
						finalAnswer += "\n\n" + executedCommands;
					}
					
					return finalAnswer;
				}

				// 如果没有行动也没有最终答案，返回错误
				if (actionMatches.Count == 0)
				{
					Console.WriteLine("❌ 未找到 <action> 标签，完整响应:");
					Console.WriteLine(response);
					return "❌ 模型响应格式错误，未找到 <action> 标签";
				}
			}

			return "❌ 达到最大迭代次数，任务可能未完成";
		}

		private async Task<string> CallModelAsync(List<ChatMessage> messages)
		{
			try
			{
				var request = new
				{
					model = _model,
					messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
					stream = false
				};

				var json = JsonSerializer.Serialize(request);
				var content = new StringContent(json, Encoding.UTF8, "application/json");

				Console.WriteLine($"🔗 正在调用Ollama...");
				var response = await _httpClient.PostAsync($"{_ollamaBaseUrl}/api/chat", content);
				var responseContent = await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
				{
					Console.WriteLine($"❌ API错误: {response.StatusCode}");
					return $"❌ Ollama API 错误: {response.StatusCode}";
				}

				Console.WriteLine($"📄 原始响应: {responseContent}");
				
				Console.WriteLine($"🔍 尝试反序列化 JSON...");
				var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
				
				if (ollamaResponse == null)
				{
					Console.WriteLine($"❌ JSON 反序列化失败，尝试手动解析...");
					// 手动解析 JSON
					var jsonDoc = JsonDocument.Parse(responseContent);
					var messageElement = jsonDoc.RootElement.GetProperty("message");
					var contentElement = messageElement.GetProperty("content");
					var result = contentElement.GetString() ?? "";
					
					// 处理 Unicode 转义字符
					result = result.Replace("\\u003c", "<").Replace("\\u003e", ">");
					Console.WriteLine($"✅ 手动解析成功: {result}");
					return result;
				}
				
				var modelContent = ollamaResponse.Message?.Content ?? "";
				
				if (string.IsNullOrEmpty(modelContent))
				{
					Console.WriteLine($"❌ 响应内容为空");
					return $"❌ Ollama 响应为空";
				}

				// 处理 Unicode 转义字符
				modelContent = modelContent.Replace("\\u003c", "<").Replace("\\u003e", ">");
				Console.WriteLine($"✅ 模型响应: {modelContent}");
				return modelContent;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ 调用异常: {ex.Message}");
				return $"❌ 调用Ollama失败: {ex.Message}";
			}
		}

		private (string toolName, string[] args) ParseAction(string action)
		{
			var match = Regex.Match(action, @"(\w+)\s*\(\s*(.*?)\s*\)");
			if (!match.Success)
			{
				throw new ArgumentException($"无法解析行动: {action}");
			}

			var toolName = match.Groups[1].Value;
			var argsString = match.Groups[2].Value;

			var args = ParseArguments(argsString);
			return (toolName, args);
		}

		private string[] ParseArguments(string argsString)
		{
			if (string.IsNullOrWhiteSpace(argsString))
				return new string[0];

			var args = new List<string>();
			var current = new StringBuilder();
			var inQuotes = false;
			var parenDepth = 0;

			for (int i = 0; i < argsString.Length; i++)
			{
				var ch = argsString[i];

				if (ch == '"' && (i == 0 || argsString[i - 1] != '\\'))
				{
					inQuotes = !inQuotes;
					continue;
				}

				if (!inQuotes)
				{
					if (ch == '(') parenDepth++;
					else if (ch == ')') parenDepth--;
					else if (ch == ',' && parenDepth == 0)
					{
						args.Add(current.ToString().Trim().Trim('"'));
						current.Clear();
						continue;
					}
				}

				current.Append(ch);
			}

			if (current.Length > 0)
				args.Add(current.ToString().Trim().Trim('"'));

			return args.ToArray();
		}

		private async Task<string> ExecuteToolAsync(string toolName, string[] args)
		{
			if (!_tools.TryGetValue(toolName, out var tool))
			{
				return $"❌ 未知工具: {toolName}";
			}

			var parameters = tool.Method.GetParameters();
			if (args.Length != parameters.Length)
			{
				return $"❌ 参数数量不匹配: 期望 {parameters.Length}，实际 {args.Length}";
			}

			var convertedArgs = new object[parameters.Length];
			for (int i = 0; i < parameters.Length; i++)
			{
				try
				{
					var converter = TypeDescriptor.GetConverter(parameters[i].ParameterType);
					convertedArgs[i] = converter.ConvertFromString(args[i]);
				}
				catch
				{
					return $"❌ 参数转换失败: {args[i]} -> {parameters[i].ParameterType.Name}";
				}
			}

			var result = tool.DynamicInvoke(convertedArgs);
			if (result is Task<string> task)
			{
				return await task;
			}
			else if (result is string str)
			{
				return str;
			}
			else
			{
				return result?.ToString() ?? "";
			}
		}

		private async Task<string> ExtractAndExecuteCommands(string finalAnswer)
		{
			var results = new List<string>();
			
			// 提取 dotnet 命令
			var dotnetCommands = Regex.Matches(finalAnswer, @"dotnet\s+(\w+)\s+([^\n\r]+)", RegexOptions.IgnoreCase);
			foreach (Match match in dotnetCommands)
			{
				var command = match.Value;
				Console.WriteLine($"🔧 执行命令: {command}");
				
				try
				{
					var result = await ExecuteCommandAsync(command);
					results.Add($"✅ 执行 {command}: {result}");
				}
				catch (Exception ex)
				{
					results.Add($"❌ 执行 {command} 失败: {ex.Message}");
				}
			}
			
			// 提取其他可能的命令
			var otherCommands = Regex.Matches(finalAnswer, @"```bash\s*\n([^`]+)\n```", RegexOptions.IgnoreCase);
			foreach (Match match in otherCommands)
			{
				var command = match.Groups[1].Value.Trim();
				Console.WriteLine($"🔧 执行命令: {command}");
				
				try
				{
					var result = await ExecuteCommandAsync(command);
					results.Add($"✅ 执行 {command}: {result}");
				}
				catch (Exception ex)
				{
					results.Add($"❌ 执行 {command} 失败: {ex.Message}");
				}
			}
			
			return string.Join("\n", results);
		}

		private async Task<string> ExecuteCommandAsync(string command)
		{
			try
			{
				var process = new System.Diagnostics.Process
				{
					StartInfo = new System.Diagnostics.ProcessStartInfo
					{
						FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "/bin/bash",
						Arguments = Environment.OSVersion.Platform == PlatformID.Win32NT ? $"/c {command}" : $"-c \"{command}\"",
						WorkingDirectory = _projectDirectory,
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						CreateNoWindow = true
					}
				};

				var output = new StringBuilder();
				var error = new StringBuilder();

				process.OutputDataReceived += (sender, e) => 
				{
					if (e.Data != null) output.AppendLine(e.Data);
				};
				process.ErrorDataReceived += (sender, e) => 
				{
					if (e.Data != null) error.AppendLine(e.Data);
				};

				process.Start();
				process.BeginOutputReadLine();
				process.BeginErrorReadLine();

				await process.WaitForExitAsync();

				var result = new StringBuilder();
				if (output.Length > 0)
				{
					result.AppendLine("输出:");
					result.AppendLine(output.ToString());
				}
				
				if (error.Length > 0)
				{
					result.AppendLine("错误:");
					result.AppendLine(error.ToString());
				}

				return result.ToString();
			}
			catch (Exception ex)
			{
				return $"执行失败: {ex.Message}";
			}
		}

		private string RenderSystemPrompt()
		{
			return $@"你是一个智能编程助手，能够帮助用户完成各种编程任务。

可用工具：
- read_file(filePath): 读取文件内容
- write_to_file(filePath, content): 写入文件内容
- list_directory(directoryPath): 列出目录内容
- search_files(searchPattern, directoryPath): 搜索文件
- create_directory(directoryPath): 创建目录
- run_terminal_command(command): 执行终端命令

工作目录：{_projectDirectory}

你需要将任务分解为多个步骤。对于每个步骤，首先使用<thought>思考要做什么然后使用 <action>调用一个工具，工具的执行结果会通过<observation> 返回给你。持续这个思考和行动的过程，直到你有足够的信息来提供<final_answer>。

有步骤请严格使用以下XML标签格式输出：
- <task>：用户提出的任务
- <thought>：思考
- <action>：采取的工具操作
- <observation>：工具或环境返回的结果
- <final_answer>：最终答案

示例：
用户：创建一个netcorewebapi项目
助手：
<thought>用户想要创建一个.NET Core Web API项目。我需要使用 dotnet CLI 来创建项目。</thought>
<action>run_terminal_command(""dotnet new webapi -n MyWebApi"")</action>

<final_answer>已成功创建.NET Core Web API项目。</final_answer>

请确保每个步骤都包含 <thought> 和 <action> 标签，当任务完成时使用 <final_answer> 标签。";
		}
	}

	public class ChatMessage
	{
		public string Role { get; set; } = "";
		public string Content { get; set; } = "";
	}

	public class OllamaResponse
	{
		[JsonPropertyName("model")]
		public string Model { get; set; } = "";
		
		[JsonPropertyName("created_at")]
		public string CreatedAt { get; set; } = "";
		
		[JsonPropertyName("message")]
		public OllamaMessage? Message { get; set; }
		
		[JsonPropertyName("done_reason")]
		public string DoneReason { get; set; } = "";
		
		[JsonPropertyName("done")]
		public bool Done { get; set; }
	}

	public class OllamaMessage
	{
		[JsonPropertyName("role")]
		public string Role { get; set; } = "";
		
		[JsonPropertyName("content")]
		public string Content { get; set; } = "";
	}
} 