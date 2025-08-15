using System;
using System.IO;
using System.Threading.Tasks;

namespace MinimalAgent
{
	public static class FileTools
	{
		private static string _projectDirectory = Directory.GetCurrentDirectory();

		public static void SetProjectDirectory(string projectDirectory)
		{
			_projectDirectory = projectDirectory;
		}

		public static string GetProjectDirectory()
		{
			return _projectDirectory;
		}

		public static async Task<string> ReadFileAsync(string filePath)
		{
			try
			{
				var absolutePath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(GetProjectDirectory(), filePath);

				if (!File.Exists(absolutePath))
				{
					return $"文件不存在：{absolutePath}";
				}

				var fileInfo = new FileInfo(absolutePath);
				if (fileInfo.Length > 1024 * 1024)
				{
					return $"文件过大（{fileInfo.Length} bytes），请使用其他方式查看大文件";
				}

				var content = await File.ReadAllTextAsync(absolutePath, System.Text.Encoding.UTF8);
				return content;
			}
			catch (UnauthorizedAccessException)
			{
				return $"无权限访问文件：{filePath}";
			}
			catch (Exception ex)
			{
				return $"读取文件失败：{ex.Message}";
			}
		}

		public static async Task<string> WriteToFileAsync(string filePath, string content)
		{
			try
			{
				var absolutePath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(GetProjectDirectory(), filePath);

				var directory = Path.GetDirectoryName(absolutePath);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				content = content.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r");

				// 检查文件是否已存在
				var fileExists = File.Exists(absolutePath);
				
				await File.WriteAllTextAsync(absolutePath, content, System.Text.Encoding.UTF8);
				
				if (fileExists)
				{
					return $"✅ 文件已成功修改：{absolutePath}";
				}
				else
				{
					return $"✅ 文件已成功创建：{absolutePath}";
				}
			}
			catch (UnauthorizedAccessException)
			{
				return $"❌ 无权限写入文件：{filePath}";
			}
			catch (Exception ex)
			{
				return $"❌ 写入文件失败：{ex.Message}";
			}
		}

		public static string ListDirectory(string directoryPath)
		{
			try
			{
				var absolutePath = Path.IsPathRooted(directoryPath) ? directoryPath : Path.Combine(GetProjectDirectory(), directoryPath);

				if (!Directory.Exists(absolutePath))
				{
					return $"目录不存在：{absolutePath}";
				}

				var items = Directory.GetFileSystemEntries(absolutePath);
				var result = new System.Text.StringBuilder();
				result.AppendLine($"📁 目录内容：{absolutePath}");
				result.AppendLine(new string('-', 50));

				foreach (var item in items)
				{
					var name = Path.GetFileName(item);
					var isDirectory = Directory.Exists(item);
					var icon = isDirectory ? "📁" : "📄";
					result.AppendLine($"{icon} {name}");
				}

				return result.ToString();
			}
			catch (UnauthorizedAccessException)
			{
				return $"无权限访问目录：{directoryPath}";
			}
			catch (Exception ex)
			{
				return $"列出目录失败：{ex.Message}";
			}
		}

		public static string CreateDirectory(string directoryPath)
		{
			try
			{
				var absolutePath = Path.IsPathRooted(directoryPath) ? directoryPath : Path.Combine(GetProjectDirectory(), directoryPath);

				if (Directory.Exists(absolutePath))
				{
					return $"📁 目录已存在：{absolutePath}";
				}

				Directory.CreateDirectory(absolutePath);
				return $"✅ 目录已成功创建：{absolutePath}";
			}
			catch (UnauthorizedAccessException)
			{
				return $"❌ 无权限创建目录：{directoryPath}";
			}
			catch (Exception ex)
			{
				return $"❌ 创建目录失败：{ex.Message}";
			}
		}

		public static string SearchFiles(string searchPattern, string directoryPath = "")
		{
			try
			{
				var searchDir = string.IsNullOrEmpty(directoryPath) ? GetProjectDirectory() :
					(Path.IsPathRooted(directoryPath) ? directoryPath : Path.Combine(GetProjectDirectory(), directoryPath));

				if (!Directory.Exists(searchDir))
				{
					return $"搜索目录不存在：{searchDir}";
				}

				var files = Directory.GetFiles(searchDir, searchPattern, SearchOption.AllDirectories);
				var result = new System.Text.StringBuilder();
				result.AppendLine($"🔍 搜索结果：在 {searchDir} 中搜索 {searchPattern}");
				result.AppendLine(new string('-', 50));

				if (files.Length == 0)
				{
					result.AppendLine("未找到匹配的文件");
				}
				else
				{
					foreach (var file in files)
					{
						var relativePath = Path.GetRelativePath(searchDir, file);
						result.AppendLine($"📄 {relativePath}");
					}
					result.AppendLine($"\n共找到 {files.Length} 个文件");
				}

				return result.ToString();
			}
			catch (UnauthorizedAccessException)
			{
				return $"无权限搜索目录：{directoryPath}";
			}
			catch (Exception ex)
			{
				return $"搜索文件失败：{ex.Message}";
			}
		}

		public static async Task<string> RunTerminalCommandAsync(string command)
		{
			try
			{
				var process = new System.Diagnostics.Process
				{
					StartInfo = new System.Diagnostics.ProcessStartInfo
					{
						FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "/bin/bash",
						Arguments = Environment.OSVersion.Platform == PlatformID.Win32NT ? $"/c {command}" : $"-c \"{command}\"",
						WorkingDirectory = GetProjectDirectory(),
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						CreateNoWindow = true
					}
				};

				var output = new System.Text.StringBuilder();
				var error = new System.Text.StringBuilder();

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

				var result = new System.Text.StringBuilder();
				result.AppendLine($"🚀 命令执行完成：{command}");
				result.AppendLine($"工作目录：{GetProjectDirectory()}");
				result.AppendLine($"退出代码：{process.ExitCode}");
				
				if (output.Length > 0)
				{
					result.AppendLine("\n📤 标准输出：");
					result.AppendLine(output.ToString());
				}
				
				if (error.Length > 0)
				{
					result.AppendLine("\n❌ 错误输出：");
					result.AppendLine(error.ToString());
				}

				return result.ToString();
			}
			catch (Exception ex)
			{
				return $"执行命令失败：{ex.Message}";
			}
		}
	}
} 