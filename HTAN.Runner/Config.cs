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
using System.Xml;
using System.Xml.Serialization;

namespace HTAN.Runner
{

  [Serializable]
  [XmlRoot("configuration")]
  public class Config
  {

    /// <summary>
    /// Gets or sets list of FastCGI.
    /// </summary>
    [XmlArray("fastCGI")]
    [XmlArrayItem("add")]
    public List<FastCgiItem> FastCGI { get; set; }

    /// <summary>
    /// Gets or sets commands list.
    /// </summary>
    [XmlArray("commands")]
    [XmlArrayItem("add")]
    public CommandCollection Commands { get; set; }

  }

}
