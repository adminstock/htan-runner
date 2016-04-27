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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace HTAN.Runner
{

  /// <summary>
  /// Represents command.
  /// </summary>
  [Serializable]
  public class Command
  {

    /// <summary>
    /// Gets or sets command name.
    /// </summary>
    [XmlAttribute("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets username to run process.
    /// </summary>
    [XmlAttribute("user")]
    public string User { get; set; }

    /// <summary>
    /// Gets or sets group to run process.
    /// </summary>
    [XmlAttribute("group")]
    public string Group { get; set; }

    /// <summary>
    /// Program to start.
    /// </summary>
    [XmlAttribute("exec")]
    public string Exec { get; set; }

    /// <summary>
    /// Arguments to start program.
    /// </summary>
    [XmlAttribute("arguments")]
    public string Arguments { get; set; }

  }

}
