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
using System.Threading;

namespace HTAN.Runner
{

  /// <summary>
  /// Represents FastCGI process info.
  /// </summary>
  class FastCgiProcess
  {

    /// <summary>
    /// Gets or sets friendly name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets ProcessID.
    /// </summary>
    public int PID { get; set; }

    /// <summary>
    /// Gets or sets PID file path.
    /// </summary>
    public string PidFile { get; set; }

    /// <summary>
    /// Gets or sets server address.
    /// For example: <c>unix:/tmp/example.socket</c>, <c>tcp:127.0.0.1:9000</c>.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// The command or command name to execution.
    /// </summary>
    public Command Command { get; set; }

    /// <summary>
    /// Gets or sets command which to be executed before starting <see cref="Command"/>.
    /// </summary>
    public Command BeforeStartingCommand { get; set; }

    /// <summary>
    /// Gets or sets command which to be executed after starting <see cref="Command"/>.
    /// </summary>
    public Command AfterStartingCommand { get; set; }

    /// <summary>
    /// Command which to be executed before stopping.
    /// </summary>
    public Command BeforeStoppingCommand { get; set; }

    /// <summary>
    /// Command which to be executed after stopping.
    /// </summary>
    public Command AfterStoppingCommand { get; set; }

    /// <summary>
    /// Maximum waiting time stopping the process (in seconds).
    /// </summary>
    public int StoppingTimeout { get; set; }

    public Thread Thread { get; set; }

    /// <summary>
    /// Action to <see cref="Thread"/>.
    /// </summary>
    public Action Action { get; set; }

    public Thread AfterStartingThread { get; set; }

    /// <summary>
    /// Action to <see cref="AfterStartingThread"/>.
    /// </summary>
    public Action AfterStartingAction { get; set; }

    /// <summary>
    /// Started status.
    /// </summary>
    public bool IsStarted { get; set; }

  }

}