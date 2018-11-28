
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace ClassLibrary1
{
  [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
  public class Class: IExternalCommand
  {
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {

      UIDocument uidoc = commandData.Application.ActiveUIDocument;
      Document document = uidoc.Document;
      FilteredElementCollector columnCollecter = new FilteredElementCollector(document);
      columnCollecter.OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance));
      List<FamilyInstance> columnlist = columnCollecter.ToList().ConvertAll(x => x as FamilyInstance);
      foreach (FamilyInstance column in columnlist)
      {
        ElementIntersectsElementFilter columnIntersectsFilter = new ElementIntersectsElementFilter(column);
        FilteredElementCollector filteredElementCollector = new FilteredElementCollector(document);
        List<Element> columnsIntersectList = filteredElementCollector.WherePasses(columnIntersectsFilter).ToList();

        var coulmnIntersectList = from elem in columnsIntersectList
                                  where elem.Category.Id == new ElementId(-2001320)//梁
                                  ||
                                  elem.Category.Id == new ElementId(-2001330)//柱
                                  ||
                                   elem.Category.Id == new ElementId(-2000011) && (elem as Wall).WallType.Kind == WallKind.Basic//基本墙
                                  ||
                                  elem.Category.Id == new ElementId(-2000032)//板
                                  select elem;
        foreach (Element elem in columnsIntersectList)
        {
          Transaction ts = new Transaction(document);
          ts.Start("cut");
          if (JoinGeometryUtils.AreElementsJoined(document, column, elem) == false)
          {
            try
            {
              JoinGeometryUtils.JoinGeometry(document, elem, column);
            }
            catch
            {

            }


          }
          else
          {
            if (JoinGeometryUtils.IsCuttingElementInJoin(document, column, elem) == false)
            {
              JoinGeometryUtils.SwitchJoinOrder(document, column, elem);
            }

          }
          ts.Commit();
        }
      }
      return Result.Succeeded;
    }
  }
}
