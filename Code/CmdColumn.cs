using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Structure;
using System.Collections.Generic;

namespace Code
{
  class CmdColumn : IExternalCommand
  {
    //在指定的族样板中创建拉伸体
    //static void CreateColumnExtrusion(Document doc)
    //{
    //  using (Transaction tx = new Transaction(doc))
    //  {
    //    tx.Start("Create Column");
    //    //拾取闭合二维轮廓
    //    ReferenceArray refer = new ReferenceArray();

    //  }
    //}
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
      UIApplication uiapp = commandData.Application;
      UIDocument uidoc = uiapp.ActiveUIDocument;
      Document doc = uidoc.Document;
      Application app = doc.Application;

      Result rc = Result.Failed;
      string templateFileName = @"C:\ProgramData\Autodesk\RVT 2016\Family Templates\Chinese\公制结构柱.rft";
      Document familyDocument = app.NewFamilyDocument(templateFileName);
      if (null == familyDocument)
      {
        throw new System.Exception("未找到族文档");
      }
      //CreateColumnExtrusion(familyDocument);
      IList<Element> _lines = uidoc.Selection.PickElementsByRectangle("请框选闭合的柱轮廓线，支持详图线");
      ReferenceArray refer = new ReferenceArray();

      foreach (var elem in _lines)
      {
        ModelCurve c = elem as ModelCurve;
        refer.Append(c.GeometryCurve.Reference);
      }
      XYZ direction = new XYZ(0, 0, 10);

      Form form = familyDocument.FamilyCreate.NewExtrusionForm(true, refer, direction);

      //储存族文档并导入到项目中再修改相关的材质和标高以及名称
      return rc;

    }


  }
}
