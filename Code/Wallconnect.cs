using System;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Structure;
using System.Collections.Generic;
using System.Linq;

namespace WorkCode
{
  [Transaction(TransactionMode.Manual)]
  public class Wallconnect : IExternalCommand
  {
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {

      UIApplication uiApp = commandData.Application;
      UIDocument uiDoc = uiApp.ActiveUIDocument;
      Document doc = uiApp.ActiveUIDocument.Document;
      Application app = uiApp.Application;

      Selection selection = uiDoc.Selection;

      Transaction ts = new Transaction(doc, "cut");
      ts.Start();

      IList<Element> _elements = uiDoc.Selection.PickElementsByRectangle(new WallFilter(), "请框选所有需要剪切的墙、板、柱");

      foreach (var element1 in _elements)
      {
      
        var collector2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilyInstance));
        var pipeIntersectFilter = new ElementIntersectsElementFilter(element1);

        List<FamilyInstance> pipes = collector2.WherePasses(pipeIntersectFilter).ToList().ConvertAll(x => x as FamilyInstance);

        foreach (var pipe in pipes)
        {
          InstanceVoidCutUtils.AddInstanceVoidCut(doc, element1, pipe);
        }
        //new BoundingBoxIsInsideFilter();
        
      }

      ts.Commit();
      return Result.Succeeded;
    }
  }

}
