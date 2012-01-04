/**
* Copyright 2011 Microsoft Corporation
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*   http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

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
