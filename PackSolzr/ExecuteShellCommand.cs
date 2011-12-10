using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace PackSolzr
{
    public static class ExecuteShellCommand
    {
        public static Process Execute(String command, bool waitForExit, String workingDir = null)
        {
            Process processToExecuteCommand = new Process();

            processToExecuteCommand.StartInfo.FileName = "cmd.exe";
            if (workingDir != null)
            {
                processToExecuteCommand.StartInfo.WorkingDirectory = workingDir;
            }

            processToExecuteCommand.StartInfo.Arguments = @"/C " + command;
            processToExecuteCommand.StartInfo.RedirectStandardInput = true;
            processToExecuteCommand.StartInfo.RedirectStandardError = true;
            processToExecuteCommand.StartInfo.RedirectStandardOutput = true;
            processToExecuteCommand.StartInfo.UseShellExecute = false;
            processToExecuteCommand.StartInfo.CreateNoWindow = true;
            processToExecuteCommand.EnableRaisingEvents = false;
            processToExecuteCommand.Start();

            processToExecuteCommand.OutputDataReceived += new DataReceivedEventHandler(processToExecuteCommand_OutputDataReceived);
            processToExecuteCommand.ErrorDataReceived += new DataReceivedEventHandler(processToExecuteCommand_ErrorDataReceived);
            processToExecuteCommand.BeginOutputReadLine();
            processToExecuteCommand.BeginErrorReadLine();


            if (waitForExit == true)
            {
                processToExecuteCommand.WaitForExit();
                processToExecuteCommand.Close();
                processToExecuteCommand.Dispose();
                processToExecuteCommand = null;
            }

            return processToExecuteCommand;
        }

        private static void processToExecuteCommand_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private static void processToExecuteCommand_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }
    }
}
