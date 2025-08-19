using System.Diagnostics;

namespace Agent
{
    public static class Tools
    {
        public static string ReadFile(string[] args)
        {
            if (args.Length == 0)
                throw new ArgumentException("需要提供文件路径参数");

            var filePath = args[0];
            return File.ReadAllText(filePath);
        }

        public static string WriteToFile(string[] args)
        {
            if (args.Length < 2)
                throw new ArgumentException("需要提供文件路径和内容参数");

            var filePath = args[0];
            var content = args[1].Replace("\\n", "\n");
            
            File.WriteAllText(filePath, content);
            return "写入成功";
        }

        public static string RunTerminalCommand(string[] args)
        {
            if (args.Length == 0)
                throw new ArgumentException("需要提供命令参数");

            var command = args[0];
            
            var startInfo = new ProcessStartInfo
            {
                FileName = GetShell(),
                Arguments = GetShellArguments(command),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return "执行失败";

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 ? output : error;
        }

        private static string GetShell()
        {
            return OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash";
        }

        private static string GetShellArguments(string command)
        {
            return OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command}\"";
        }
    }
}