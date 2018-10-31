#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
//using WinForms = System.Windows.Forms;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Collections;
#endregion

namespace Code
{
  class Util
  {

    /// <summary>
    /// Return the First element of the given type and name.
    /// </summary>
    /// <param name="doc"></param>
    /// <param name="type"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static Element GetFirstElementOfTypeNamed(Document doc, Type type, string name)
    {
      FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(type);

      Func<Element, bool> nameEquals = e => e.Name.Equals(name);

      return collector.Any<Element>(nameEquals) ? collector.First<Element>(nameEquals) : null;

    }
  }
}