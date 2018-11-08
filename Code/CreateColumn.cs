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

namespace Code

{
  [Transaction(TransactionMode.Manual)]
  [RegenerationAttribute(RegenerationOption.Manual)]
  public class CreateColumn : IExternalCommand
  {
    const string _family_name = "混凝土 - 矩形 - 柱";
    const string _extension = ".rfa";
    const string _directory = "C:/ProgramData/Autodesk/RVT 2016/Libraries/China/结构/柱混凝土/";
    const string _path = _directory + _family_name + _extension;

    StructuralType _structural = StructuralType.NonStructural;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
      Result rc = Result.Failed;

      UIApplication app = commandData.Application;
      UIDocument uiDoc = app.ActiveUIDocument;
      Document doc = app.ActiveUIDocument.Document;

      Family f = Util.GetFirstElementOfTypeNamed(doc, typeof(Family), _family_name) as Family;

      using (Transaction t = new Transaction(doc))
      {
        t.Start("Create New Column Type and Instance");

        if (null != f)
        {
          FamilySymbol s = null;
          foreach (ElementId id in f.GetFamilySymbolIds()) // 2015
          {
            s = doc.GetElement(id) as FamilySymbol;
            break;
          }
          ElementType s1 = s.Duplicate("Nuovo simbolo");
          s = s1 as FamilySymbol;

          s.LookupParameter("b").Set(250 / 304.8); // 
          s.LookupParameter("h").Set(400 / 304.8); // 


          //var topLevelid = s.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId();
          //Level topLevel = doc.GetElement(topLevelid) as Level;
          var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).ToArray();
          Level baseLevel = Util.GetFirstElementOfTypeNamed(doc, typeof(Level), "STR_2F（5.760）") as Level;
          Level topLevel = Util.GetFirstElementOfTypeNamed(doc, typeof(Level), "ARC_3F（8.800）") as Level;
          
          //if (topLevel.StorageType == StorageType.ElementId)
          //{
          //ElementId sValue = topLevel.AsElementId();
          //topLevel.Set(new ElementId(398741));//Level one
          // Level top = doc.GetElement(sValue) as Level;
          //}


          // s.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).SetValueString("STR_1F");// 
          // s.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).Set("STR_2F（5.760）"); // 
          //s.LookupParameter("底部标高").Set("STR_1F");// 

          //var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).ToArray();
          //doc.Create.NewFamilyInstance(XYZ.Zero,s,levels[0],StructuralType.Column);
          // doc.Create.NewFamilyInstance(XYZ.Zero,s,doc.ActiveView);



          // We can change the symbol name at any time:

          s.Name = "250x400mmTest";

          // Insert an instance of our new symbol:

          //IList<Element> _elements = uiDoc.Selection.PickElementsByRectangle(new WallFilter(), "请框选所有需要剪切管道的墙");
          IList<Element> _lines = uiDoc.Selection.PickElementsByRectangle("请选择详图线");

          List<XYZ> _points = new List<XYZ>();
          foreach (var element in _lines)
          {
            var line = (element as DetailLine).GeometryCurve as Line;
            if (line != null)
            {
              XYZ p1 = line.GetEndPoint(0);
              XYZ p2 = line.GetEndPoint(1);
              if (!_points.Contains(p1))
              {
                _points.Add(p1);
              }
              if (!_points.Contains(p2))
              {
                _points.Add(p2);
              }
            }
          }
                  
          XYZ p = new XYZ();
          foreach (var item in _points)
          {
            p += item;
          }
          p = p / _points.Count;

          //XYZ p = XYZ.Zero;
          var _familyInstance = doc.Create.NewFamilyInstance(
            p, s,baseLevel,_structural);
          _familyInstance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).Set(topLevel.Id);
          //s.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).Set(topLevel.Id);
          //Parameter topLevel = s.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
          //if (topLevel.StorageType == StorageType.ElementId)
          //{
          //  var sValue = topLevel.AsElementId();
          //  Level top = doc.GetElement(sValue) as Level;
          //}

          // For a column, the reference direction is ignored:

          //XYZ normal = new XYZ( 1, 2, 3 );
          //doc.Create.NewFamilyInstance(
          //  p, s, normal, null, nonStructural );

          // _familyInstance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).SetValueString("STR_1F");
          //_familyInstance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).SetValueString("STR_2F（5.760）");

          TaskDialog.Show("Hello", "OK");

          rc = Result.Succeeded;
        }
        t.Commit();
      }
      return rc;
    }
  }
}
