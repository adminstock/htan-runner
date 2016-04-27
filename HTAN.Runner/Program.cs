// ----------------------------------------------------------------------------
// Copyright © Aleksey Nemiro, 2016. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mono.Unix;
using Mono.Unix.Native;

namespace HTAN.Runner
{

  class Program
  {

    /// <summary>
    /// Exit status.
    /// </summary>
    private static bool Exit = false;

    /// <summary>
    /// List of started FastCGI processes.
    /// </summary>
    private static List<FastCgiProcess> FastCgiProcesses = null;

    /// <summary>
    /// The white space chars (\r, \n, \t and space).
    /// </summary>
    private static readonly char[] WhiteSpaceChars = new char[] { '\r', '\n', '\t', ' ' };

    private static readonly TimeSpan LogAccessTimeout = new TimeSpan(0, 0, 30);

    private static readonly TimeSpan LogAccessPause = new TimeSpan(0, 0, 0, 0, 100);

    static void Main(string[] args)
    {
      // check paths
      if (!Directory.Exists(Path.GetDirectoryName(Properties.Settings.Default.LogFile)))
      {
        Program.ExecuteCommand("mkdir", String.Format("-p '{0}'", Path.GetDirectoryName(Properties.Settings.Default.LogFile)));
      }

      if (!Directory.Exists(Properties.Settings.Default.AppAvailablePath))
      {
        Program.WriteLog(LogMessageType.Error, "Directory '{0}' not found.", Properties.Settings.Default.AppAvailablePath);
        throw new Exception(String.Format("Directory '{0}' not found.", Properties.Settings.Default.AppAvailablePath));
      }

      if (!Directory.Exists(Properties.Settings.Default.AppEnabledPath))
      {
        Program.WriteLog(LogMessageType.Error, "Directory '{0}' not found.", Properties.Settings.Default.AppEnabledPath);
        throw new Exception(String.Format("Directory '{0}' not found.", Properties.Settings.Default.AppEnabledPath));
      }

      try
      {
        // load configs and run applications
        Program.Start();

        // create signals handler
        var t = new Thread(Program.SignalHandlers);
        t.Start();

        // waiting for exit
        while (!Program.Exit)
        {
          Thread.Sleep(0);
          Thread.Sleep(5000);
          Program.CheckProcesses();
        }

        Environment.Exit(0);
      }
      catch (Exception ex)
      {
        Program.WriteLog(LogMessageType.Error, "Fall: {0}.", ex.Message);
        throw ex;
      }
    }

    /// <summary>
    /// Processes signals. 
    /// </summary>
    static void SignalHandlers()
    {
      // http://docs.go-mono.com/monodoc.ashx?link=T:Mono.Unix.Native.Signum
      // Interrupt from keyboard.
      var intr = new UnixSignal(Signum.SIGINT);
      // Termination signal.
      var term = new UnixSignal(Signum.SIGTERM);
      // Hangup detected on controlling terminal or death of controlling process.
      var hup = new UnixSignal(Signum.SIGHUP);
      // Quit from keyboard.
      var quit = new UnixSignal(Signum.SIGQUIT);
      // Kill signal.
      // var kill = new UnixSignal(Signum.SIGKILL);

      var signals = new UnixSignal[] { intr, term, hup, quit };

      while (!Program.Exit)
      {
        int idx = UnixSignal.WaitAny(signals);

        if (idx < 0 || idx >= signals.Length)
        {
          continue;
        }

        Program.WriteLog(LogMessageType.Info, "Received signal '{0}'.", signals[idx].Signum.ToString());

        if (intr.IsSet || term.IsSet || quit.IsSet)
        {
          // exit
          intr.Reset();
          term.Reset();

          Program.Stop();

          Program.Exit = true;
        }
        else if (hup.IsSet)
        {
          // reload
          hup.Reset();
          Program.Stop();
          Program.Start();
        }
      }
    }

    /// <summary>
    /// Reads configiration files and runs applications.
    /// </summary>
    static void Start()
    {
      Program.WriteLog(LogMessageType.Info, "Loading...");

      Program.FastCgiProcesses = new List<FastCgiProcess>();

      // load configs and create tasks
      foreach (var path in Directory.GetFiles(Properties.Settings.Default.AppEnabledPath))
      {
        var config = SerializerHelper.LoadXml<Config>(path, new Type[] { typeof(FastCgiItem), typeof(Command) });
        int index = 1;

        // fastCGI
        foreach (var fastCGI in config.FastCGI)
        {
          if (String.IsNullOrEmpty(fastCGI.Address))
          {
            Program.WriteLog(LogMessageType.Error, "Address of the fastCGI is empty. The item will be skipped.");
            continue;
          }

          if (String.IsNullOrEmpty(fastCGI.Command))
          {
            Program.WriteLog(LogMessageType.Warning, "Command of fastCGI item '{0}' name is empty.", fastCGI.Address);
          }

          // search or create command
          var command = config.Commands.FirstOrCommand(fastCGI.Command);

          // pid file
          string pidFile = String.Format("/tmp/{0}.pid", Guid.NewGuid().ToString().Replace("-", ""));

          // remove old files
          if (fastCGI.Address.StartsWith("unix:"))
          {
            var unixSocketPath = fastCGI.Address.Substring("unix:".Length);

            //if (File.Exists(unixSocketPath))
            //{
            Program.Remove(unixSocketPath);
            //}

            pidFile = String.Format("/tmp/{0}.pid", Path.GetFileName(unixSocketPath));
          }

          // command to start-stop-daemon
          string exec = Program.ReplaceFastCGIMarkers(command.Exec, fastCGI, command, pidFile);
          string arguments = Program.ReplaceFastCGIMarkers(command.Arguments, fastCGI, command, pidFile);

          // create process info
          var fcgCgiProcess = new FastCgiProcess();
          fcgCgiProcess.Name = String.Format("{0} #{1}", Path.GetFileNameWithoutExtension(path), index);
          fcgCgiProcess.Address = fastCGI.Address;
          fcgCgiProcess.StoppingTimeout = fastCGI.StoppingTimeout.GetValueOrDefault(10); // default: 10 sec.
          fcgCgiProcess.PidFile = pidFile;
          fcgCgiProcess.Command = new Command
          {
            Exec = exec,
            Arguments = arguments,
            User = command.User,
            Group = command.Group
          };

          // action
          fcgCgiProcess.Action = () =>
          {
            // before starting
            if (fcgCgiProcess.BeforeStartingCommand != null)
            {
              Program.WriteLog(LogMessageType.Info, "Execution <BeforeStartingCommand> of {0)...", fcgCgiProcess.Name);
              Program.ExecuteCommandAs(fcgCgiProcess.BeforeStartingCommand);
              Program.WriteLog(LogMessageType.Info, "Executed <BeforeStartingCommand> of {0).", fcgCgiProcess.Name);
            }

            // start
            Program.WriteLog(LogMessageType.Info, "Starting {0)...", fcgCgiProcess.Name);

            fcgCgiProcess.IsStarted = true;
            Program.StartDaemon(command.User, command.Group, pidFile, exec, arguments);

            Program.WriteLog(LogMessageType.Info, "Started {0).", fcgCgiProcess.Name);

            // waiting pid
            Program.WriteLog(LogMessageType.Info, "Waiting PID of {0}...", fcgCgiProcess.Name);

            while (!File.Exists(pidFile))
            {
              Thread.Sleep(250);
            }

            // get pid
            using (var f = new FileStream(pidFile, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(f))
            {
              fcgCgiProcess.PID = Convert.ToInt32(reader.ReadLine());
            }

            Program.WriteLog(LogMessageType.Info, "PID of workflow {1} is #{0}.", fcgCgiProcess.PID, fcgCgiProcess.Name);
          };

          // thread
          fcgCgiProcess.Thread = new Thread(() => fcgCgiProcess.Action()) { IsBackground = true };

          #region TODO: make common code

          if (!String.IsNullOrEmpty(fastCGI.AfterStartingCommand))
          {
            var afterStartingCommand = Program.CheckForWeb(config.Commands.FirstOrCommand(fastCGI.AfterStartingCommand));
            var afterStartingExec = Program.ReplaceFastCGIMarkers(afterStartingCommand.Exec, fastCGI, afterStartingCommand, pidFile);
            var afterStartingArguments = Program.ReplaceFastCGIMarkers(afterStartingCommand.Arguments, fastCGI, afterStartingCommand, pidFile);
            fcgCgiProcess.AfterStartingCommand = new Command
            {
              Exec = afterStartingExec,
              Arguments = afterStartingArguments,
              User = afterStartingCommand.User,
              Group = afterStartingCommand.Group
            };
          }

          if (!String.IsNullOrEmpty(fastCGI.BeforeStartingCommand))
          {
            var beforeStartingCommand = Program.CheckForWeb(config.Commands.FirstOrCommand(fastCGI.BeforeStartingCommand));
            var beforeStartingExec = Program.ReplaceFastCGIMarkers(beforeStartingCommand.Exec, fastCGI, beforeStartingCommand, pidFile);
            var beforeStartingArguments = Program.ReplaceFastCGIMarkers(beforeStartingCommand.Arguments, fastCGI, beforeStartingCommand, pidFile);
            fcgCgiProcess.BeforeStartingCommand = new Command
            {
              Exec = beforeStartingExec,
              Arguments = beforeStartingArguments,
              User = beforeStartingCommand.User,
              Group = beforeStartingCommand.Group
            };
          }

          if (!String.IsNullOrEmpty(fastCGI.AfterStoppingCommand))
          {
            var afterStoppingCommand = Program.CheckForWeb(config.Commands.FirstOrCommand(fastCGI.AfterStoppingCommand));
            var afterStoppingExec = Program.ReplaceFastCGIMarkers(afterStoppingCommand.Exec, fastCGI, afterStoppingCommand, pidFile);
            var afterStoppingArguments = Program.ReplaceFastCGIMarkers(afterStoppingCommand.Arguments, fastCGI, afterStoppingCommand, pidFile);
            fcgCgiProcess.AfterStoppingCommand = new Command
            {
              Exec = afterStoppingExec,
              Arguments = afterStoppingArguments,
              User = afterStoppingCommand.User,
              Group = afterStoppingCommand.Group
            };
          }

          if (!String.IsNullOrEmpty(fastCGI.BeforeStoppingCommand))
          {
            var beforeStoppingCommand = Program.CheckForWeb(config.Commands.FirstOrCommand(fastCGI.BeforeStoppingCommand));
            var beforeStoppingExec = Program.ReplaceFastCGIMarkers(beforeStoppingCommand.Exec, fastCGI, beforeStoppingCommand, pidFile);
            var beforeStoppingArguments = Program.ReplaceFastCGIMarkers(beforeStoppingCommand.Arguments, fastCGI, beforeStoppingCommand, pidFile);
            fcgCgiProcess.BeforeStoppingCommand = new Command
            {
              Exec = beforeStoppingExec,
              Arguments = beforeStoppingArguments,
              User = beforeStoppingCommand.User,
              Group = beforeStoppingCommand.Group
            };
          }

          #endregion

          // add process info to list
          Program.FastCgiProcesses.Add(fcgCgiProcess);

          // start thread
          fcgCgiProcess.Thread.Start();

          // after starting
          if (fcgCgiProcess.AfterStartingCommand != null)
          {
            // in a separate thread to continue running other tasks
            fcgCgiProcess.AfterStartingAction = () =>
            {
              // waiting
              while (!fcgCgiProcess.IsStarted)
              {
                // small pause
                Thread.Sleep(250);
              }

              Program.WriteLog(LogMessageType.Info, "Execution <AfterStartingCommand> of {0)...", fcgCgiProcess.Name);
              // TODO: normalize code
              fcgCgiProcess.AfterStartingCommand.Exec = Program.ReplaceFastCGIMarkersAfterStarting(fcgCgiProcess.AfterStartingCommand.Exec, fcgCgiProcess.PID.ToString());
              fcgCgiProcess.AfterStartingCommand.Arguments = Program.ReplaceFastCGIMarkersAfterStarting(fcgCgiProcess.AfterStartingCommand.Arguments, fcgCgiProcess.PID.ToString());
              Program.ExecuteCommandAs(fcgCgiProcess.AfterStartingCommand);
              Program.WriteLog(LogMessageType.Info, "Executed <AfterStartingCommand> of {0).", fcgCgiProcess.Name);
            };

            fcgCgiProcess.AfterStartingThread = new Thread(() => fcgCgiProcess.AfterStartingAction()) { IsBackground = true };

            fcgCgiProcess.AfterStartingThread.Start();
          }

          index++;
        }
      }

      Program.WriteLog(LogMessageType.Info, "Loaded.");
    }

    /// <summary>
    /// Shuts down the running processes.
    /// </summary>
    static void Stop()
    {
      Program.WriteLog(LogMessageType.Info, "Stopping...");

      if (Program.FastCgiProcesses != null)
      {
        foreach (var fcgCgiProcess in Program.FastCgiProcesses)
        {
          // before stopping
          if (fcgCgiProcess.BeforeStoppingCommand != null)
          {
            // TODO: timeout
            Program.WriteLog(LogMessageType.Info, "Execution <BeforeStoppingCommand> of {0)...", fcgCgiProcess.Name);
            // TODO: normalize code
            fcgCgiProcess.BeforeStoppingCommand.Exec = Program.ReplaceFastCGIMarkersAfterStarting(fcgCgiProcess.BeforeStoppingCommand.Exec, fcgCgiProcess.PID.ToString());
            fcgCgiProcess.BeforeStoppingCommand.Arguments = Program.ReplaceFastCGIMarkersAfterStarting(fcgCgiProcess.BeforeStoppingCommand.Arguments, fcgCgiProcess.PID.ToString());
            Program.ExecuteCommandAs(fcgCgiProcess.BeforeStoppingCommand);
            Program.WriteLog(LogMessageType.Info, "Executed <BeforeStoppingCommand> of {0).", fcgCgiProcess.Name);
          }

          // stop process
          Program.WriteLog(LogMessageType.Info, "Stopping {0), PID #{1}...", fcgCgiProcess.Name, fcgCgiProcess.PID);
          Program.StopDaemon(fcgCgiProcess.PidFile, fcgCgiProcess.StoppingTimeout);
          Program.WriteLog(LogMessageType.Info, "Stopped {0), PID #{1}.", fcgCgiProcess.Name, fcgCgiProcess.PID);

          // remove pid file
          Program.Remove(fcgCgiProcess.PidFile);

          // remove unix socket files
          if (fcgCgiProcess.Address.StartsWith("unix:"))
          {
            Program.Remove(fcgCgiProcess.Address.Substring("unix:".Length));
          }

          // after stopping
          if (fcgCgiProcess.AfterStoppingCommand != null)
          {
            // TODO: timeout
            Program.WriteLog(LogMessageType.Info, "Execution <AfterStoppingCommand> of {0)...", fcgCgiProcess.Name);
            // TODO: normalize code
            fcgCgiProcess.AfterStoppingCommand.Exec = Program.ReplaceFastCGIMarkersAfterStarting(fcgCgiProcess.AfterStoppingCommand.Exec, fcgCgiProcess.PID.ToString());
            fcgCgiProcess.AfterStoppingCommand.Arguments = Program.ReplaceFastCGIMarkersAfterStarting(fcgCgiProcess.AfterStoppingCommand.Arguments, fcgCgiProcess.PID.ToString());
            Program.ExecuteCommandAs(fcgCgiProcess.AfterStoppingCommand);
            Program.WriteLog(LogMessageType.Info, "Executed <AfterStoppingCommand> of {0).", fcgCgiProcess.Name);
          }

          // kill threads
          if (fcgCgiProcess.Thread != null && fcgCgiProcess.Thread.ThreadState != System.Threading.ThreadState.Stopped && fcgCgiProcess.Thread.ThreadState != System.Threading.ThreadState.Aborted)
          {
            try
            {
              Program.WriteLog(LogMessageType.Info, "Killing thread of {0).", fcgCgiProcess.Name);
              fcgCgiProcess.Thread.Abort();
            }
            catch (Exception ex)
            {
              Program.WriteLog(LogMessageType.Error, ex.Message);
            }
          }

          if (fcgCgiProcess.AfterStartingThread != null && fcgCgiProcess.AfterStartingThread.ThreadState != System.Threading.ThreadState.Stopped && fcgCgiProcess.AfterStartingThread.ThreadState != System.Threading.ThreadState.Aborted)
          {
            try
            {
              Program.WriteLog(LogMessageType.Info, "Killing <AfterStartingThread> thread of {0).", fcgCgiProcess.Name);
              fcgCgiProcess.AfterStartingThread.Abort();
            }
            catch (Exception ex)
            {
              Program.WriteLog(LogMessageType.Error, ex.Message);
            }
          }
        }
      }

      Program.WriteLog(LogMessageType.Info, "Stopped.");
    }

    /// <summary>
    /// Checks the existence of processes and restarting.
    /// </summary>
    static void CheckProcesses()
    {
      if (Program.Exit)
      {
        return;
      }

      // Program.WriteLog(LogMessageType.Info, "Checking processes {0}.", Program.FastCgiProcesses.Count);

      var processes = Process.GetProcesses();

      foreach (var p in Program.FastCgiProcesses)
      {
        if (p.PID == 0)
        {
          continue;
        }

        if (!processes.Any(itm => itm.Id == p.PID))
        {
          Program.WriteLog(LogMessageType.Info, "PID #{0} not found. Restarting '{1}'.", p.PID, p.Name);

          // stop daemon
          Program.StopDaemon(p.PidFile, p.StoppingTimeout);

          // reset PID
          p.PID = 0;

          // kill threads
          try { p.Thread.Abort(); } catch { }
          if (p.AfterStartingCommand != null && p.AfterStartingThread != null)
          {
            try { p.AfterStartingThread.Abort(); }
            catch { }
          }

          // start new process
          p.Thread = new Thread(() => { p.Action(); }) { IsBackground = true };
          p.Thread.Start();

          if (p.AfterStartingCommand != null)
          {
            p.AfterStartingThread = new Thread(() => p.AfterStartingAction()) { IsBackground = true };
          }
        }
      }
    }

    /// <summary>
    /// Executes command.
    /// </summary>
    /// <param name="exec">The file to run.</param>
    /// <param name="arguments">The additional arguments.</param>
    static string ExecuteCommand(string exec, string arguments)
    {
      return Program.ExecuteCommandAs(null, exec, arguments);
    }

    /// <summary>
    /// Executes the command from specific username.
    /// </summary>
    static string ExecuteCommandAs(Command command)
    {
      return Program.ExecuteCommandAs(command.User, command.Exec, command.Arguments);
    }

    /// <summary>
    /// Executes the command from specific username.
    /// </summary>
    /// <param name="exec">The file to run.</param>
    /// <param name="arguments">The additional arguments.</param>
    /// <param name="user">The username to run <paramref name="exec"/>.</param>
    static string ExecuteCommandAs(string user, string exec, string arguments)
    {
      exec = exec.Replace("'", @"'\''");
      string command = exec;

      if (!String.IsNullOrEmpty(arguments))
      {
        arguments = arguments.Replace("'", @"'\''");
        command += " " + arguments;
      }

      if (!String.IsNullOrEmpty(user))
      {
        user = user.Replace("'", @"'\''");
        command = String.Format("--login '{0}' --command '{1}'", user, command);
      }
      else
      {
        command = String.Format("--command '{0}'", command);
      }

      var startInfo = new ProcessStartInfo
      {
        FileName = "su",
        Arguments = command,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };

      return Program.ProcessStart(startInfo);
    }
    
    /// <summary>
    /// Starts daemon in the background.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="group"></param>
    /// <param name="pidFile"></param>
    /// <param name="exec"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    static int StartDaemon(string user, string group, string pidFile, string exec, string arguments)
    {
      if (!String.IsNullOrEmpty(arguments))
      {
        arguments = " -- " + arguments;
      }

      arguments = String.Format("--start --chuid {0}:{1} --background --pidfile {2} --make-pidfile --verbose --exec {3}{4}", user, group, pidFile, exec, arguments);

      var startInfo = new ProcessStartInfo
      {
        FileName = "start-stop-daemon",
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };

      return Program.ProcessStart2(startInfo);
    }

    /// <summary>
    /// Stops daemon.
    /// </summary>
    /// <param name="pidFile">The pid file to stop.</param>
    /// <returns></returns>
    static int StopDaemon(string pidFile, int timeout)
    {
      string arguments = String.Format("--stop --verbose --pidfile {0} --retry=TERM/{1}/KILL/5", pidFile, timeout); // --remove-pidfile

      var startInfo = new ProcessStartInfo
      {
        FileName = "start-stop-daemon",
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };

      return Program.ProcessStart2(startInfo);
    }

    /// <summary>
    /// Starts process and returns StandardOutput or StandardError.
    /// </summary>
    /// <param name="startInfo">The process start info.</param>
    /// <returns></returns>
    static string ProcessStart(ProcessStartInfo startInfo)
    {
      Program.WriteLog
      (
        LogMessageType.Info, 
        "CMD ( {0}{1}{2} )", 
        startInfo.FileName, 
        (!String.IsNullOrEmpty(startInfo.Arguments) ? " " : ""), 
        startInfo.Arguments
      );

      var p = Process.Start(startInfo);

      var result = p.StandardOutput.ReadToEnd();
      string error = p.StandardError.ReadToEnd();

      p.WaitForExit();

      if (!String.IsNullOrEmpty(result))
      {
        result = result.Trim(Program.WhiteSpaceChars);
        Program.WriteLog(LogMessageType.Info, "PID#{0} code {1}, StandardOutput: {2}", p.Id, p.ExitCode, result);
      }

      if (!String.IsNullOrEmpty(error))
      {
        error = error.Trim(Program.WhiteSpaceChars);
        Program.WriteLog(LogMessageType.Info, "PID#{0} code {1}, StandardError: {2}", p.Id, p.ExitCode, error);
      }

      return result ?? error; // TODO: bug or not to bug...
    }

    /// <summary>
    /// Starts process and returns ExitCode.
    /// </summary>
    /// <param name="startInfo">The process start info.</param>
    /// <returns></returns>
    static int ProcessStart2(ProcessStartInfo startInfo)
    {
      Program.WriteLog
      (
        LogMessageType.Info,
        "CMD ( {0}{1}{2} )",
        startInfo.FileName,
        (!String.IsNullOrEmpty(startInfo.Arguments) ? " " : ""),
        startInfo.Arguments
      );

      var p = Process.Start(startInfo);

      var result = p.StandardOutput.ReadToEnd();
      string error = p.StandardError.ReadToEnd();

      p.WaitForExit();

      if (!String.IsNullOrEmpty(result))
      {
        result = result.Trim(Program.WhiteSpaceChars);
        Program.WriteLog(LogMessageType.Info, "PID#{0} code {1}, StandardOutput: {2}", p.Id, p.ExitCode, result);
      }

      if (!String.IsNullOrEmpty(error))
      {
        error = error.Trim(Program.WhiteSpaceChars);
        Program.WriteLog(LogMessageType.Info, "PID#{0} code {1}, StandardError: {2}", p.Id, p.ExitCode, error);
      }

      return p.ExitCode;
    }

    /// <summary>
    /// Replaces the markers.
    /// </summary>
    /// <param name="value">The string to processing.</param>
    /// <param name="fastCGI">The <see cref="FastCgiItem"/> instance.</param>
    /// <param name="command">The <see cref="Command"/> instance.</param>
    /// <param name="pidFile">The path to PID file.</param>
    /// <returns>Returns processed <paramref name="value"/>.</returns>
    static string ReplaceFastCGIMarkers(string value, FastCgiItem fastCGI, Command command, string pidFile)
    {
      if (String.IsNullOrEmpty(value))
      {
        return null;
      }

      value = value.Replace("{socket}", fastCGI.Address);
      value = value.Replace("{address}", fastCGI.Address);
      value = value.Replace("{user}", command.User);
      value = value.Replace("{group}", command.Group);
      value = value.Replace("{pidFile}", pidFile);

      return value;
    }

    /// <summary>
    /// Replaces the markers.
    /// </summary>
    /// <returns>Returns processed <paramref name="value"/>.</returns>
    static string ReplaceFastCGIMarkersAfterStarting(string value, string pid)
    {
      if (String.IsNullOrEmpty(value))
      {
        return null;
      }

      value = value.Replace("{pid}", pid);

      return value;
    }

    static Command CheckForWeb(Command command)
    {
      if (command.Exec.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || command.Exec.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
      {
        if (String.IsNullOrEmpty(command.Arguments))
        {
          command.Arguments = "-q -S -O- " + command.Exec;
        }
        else
        {
          command.Arguments = command.Arguments + " " + command.Exec;
        }

        command.Exec = "wget";
      }

      return command;
    }

    /// <summary>
    /// Removes the specified file or directory.
    /// </summary>
    /// <param name="path">Path to remove.</param>
    static void Remove(string path)
    {
      Program.ExecuteCommand("rm", String.Format("--force \"{0}\"", path));
    }

    /// <summary>
    /// Adds record to the log file.
    /// </summary>
    /// <param name="type">Recored type.</param>
    /// <param name="message">Message text. Acceptable use of format strings. For example: "Message #{0}."</param>
    /// <param name="args">Additional arguments.</param>
    static void WriteLog(LogMessageType type, string message, params object[] args)
    {
      if (String.IsNullOrEmpty(message))
      {
        return;
      }

      var totalTime = new TimeSpan();

      while (totalTime < Program.LogAccessTimeout)
      {
        try
        {

          using (var stream = new FileStream(Properties.Settings.Default.LogFile, FileMode.Append, FileAccess.Write))
          using (var writer = new StreamWriter(stream, Encoding.UTF8))
          {
            if (args != null && args.Length > 0)
            {
              message = String.Format(message, args);
            }

            string line = String.Format("{0} [{1}]: {2}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), type.ToString().ToUpper(), message);

            Console.WriteLine(line);
            writer.WriteLine(line);
          }

          break;
        }
        catch
        {
          totalTime += Program.LogAccessPause;
        }
      }
    }

  }

}