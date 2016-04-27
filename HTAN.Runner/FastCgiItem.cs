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
using System.Xml.Serialization;

namespace HTAN.Runner
{

  [Serializable]
  public class FastCgiItem
  {

    /// <summary>
    /// Gets or sets server address.
    /// For example: <c>unix:/tmp/example.socket</c>, <c>tcp:127.0.0.1:9000</c>.
    /// </summary>
    [XmlAttribute("address")]
    public string Address { get; set; }

    /// <summary>
    /// The command or command name to execution.
    /// </summary>
    [XmlAttribute("command")]
    public string Command { get; set; }

    /// <summary>
    /// Command or command name, which to be executed before starting.
    /// </summary>
    [XmlAttribute("beforeStartingCommand")]
    public string BeforeStartingCommand { get; set; }

    /// <summary>
    /// Command or command name, which to be executed after starting.
    /// </summary>
    [XmlAttribute("afterStartingCommand")]
    public string AfterStartingCommand { get; set; }

    /// <summary>
    /// Command or command name, which to be executed before stopping.
    /// </summary>
    [XmlAttribute("beforeStoppingCommand")]
    public string BeforeStoppingCommand { get; set; }

    /// <summary>
    /// Command or command name, which to be executed after stopping.
    /// </summary>
    [XmlAttribute("afterStoppingCommand")]
    public string AfterStoppingCommand { get; set; }

    /// <summary>
    /// Maximum waiting time stopping the process (in seconds). Default: 10 seconds.
    /// </summary>
    [XmlIgnore]
    public int? StoppingTimeout { get; set; }

    /// <summary>
    /// Use <see cref="StoppingTimeout"/>.
    /// </summary>
    [XmlAttribute("stoppingTimeout")]
    public string StoppingTimeoutString
    {
      get 
      {
        return (this.StoppingTimeout.HasValue ? this.StoppingTimeout.Value.ToString() : null);
      }
      set 
      {
        this.StoppingTimeout = (!String.IsNullOrEmpty(value) ? int.Parse(value) : default(int?));
      }
    }

  }

}
