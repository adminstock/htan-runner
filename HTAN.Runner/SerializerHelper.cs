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
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace HTAN.Runner
{

  static class SerializerHelper
  {

    public static void SaveXml(object source, string path, Type[] extraTypes = null)
    {
      if (String.IsNullOrEmpty(path))
      {
        throw new ArgumentNullException("path");
      }

      using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Inheritable))
      using (var x = new XmlTextWriter(stream, Encoding.UTF8))
      {
        x.Formatting = Formatting.Indented;

        XmlSerializer mySerializer = new XmlSerializer(source.GetType(), extraTypes);

        mySerializer.Serialize(x, source);
      }
    }

    public static T LoadXml<T>(string path, Type[] extraTypes = null) where T : new()
    {
      if (String.IsNullOrEmpty(path))
      {
        throw new ArgumentNullException("path");
      }

      XmlSerializer mySerializer = new XmlSerializer(typeof(T), extraTypes);

      using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
      {
        return (T)mySerializer.Deserialize(stream);
      }
    }


  }

}
