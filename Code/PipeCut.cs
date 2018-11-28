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
  public class WallCutPipe : IExternalCommand
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

      //Reference refWall = selection.PickObject(ObjectType.Element, "choise");
      //Element elem_1 = doc.GetElement(refWall);

      //Reference refPipe = selection.PickObject(ObjectType.Element, "choise");
      //Element elem_2 = doc.GetElement(refPipe);

      ////SolidSolidCutUtils.RemoveCutBetweenSolids(doc, elem_1, elem_2);
      //InstanceVoidCutUtils.AddInstanceVoidCut(doc, elem_1, elem_2);


      //SolidSolidCutUtils.AddCutBetweenSolids(doc, elem_1, elem_2);
      //JoinGeometryUtils.JoinGeometry(doc, e1, e2);

      //FilteredElementCollector collector = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType();

      IList<Element> _elements = uiDoc.Selection.PickElementsByRectangle(new WallFilter(), "请框选所有需要剪切管道的墙");

      foreach (var awall in _elements)
      {
        //FilteredElementCollector collector2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeAccessory).WhereElementIsNotElementType();
        var collector2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeAccessory).OfClass(typeof(FamilyInstance));
        varz pipeIntersectFilter = new ElementIntersectsElementFilter(awall);
        //在所有Pipe中过滤出和每一个墙相交的Pipe然后组成集合
        List<FamilyInstance> pipes = collector2.WherePasses(pipeIntersectFilter).ToList().ConvertAll(x => x as FamilyInstance);

        foreach (var pipe in pipes)
        {
          InstanceVoidCutUtils.AddInstanceVoidCut(doc, awall, pipe);
        }

      }

      //try
      //{
      //  foreach (var element in selection.GetElementIds())
      //  {

      //    InstanceVoidCutUtils.AddInstanceVoidCut(doc, beam, cuttingInstance);
      //  }
      //}

      //catch (Exception e)
      //{
      //  message = e.Message;
      //  return Result.Failed;
      //}

      //TaskDialog.Show("Hello", "Hello Revit!");
      ts.Commit();
      return Result.Succeeded;
    }
  }


  public class WallFilter : ISelectionFilter
  {
    public bool AllowElement(Element element)
    {
      if (element is Wall)
      {
        return true;
      }
      return false;
    }
    public bool AllowReference(Reference refer, XYZ point)
    {
      return false;
    }
  }
}


