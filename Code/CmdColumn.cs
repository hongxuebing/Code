using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Structure;
using System.Collections.Generic;
using System.Linq;

namespace Code
{
  [Transaction(TransactionMode.Manual)]

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
    Document _doc;
    Autodesk.Revit.Creation.Application _creapp;
    Autodesk.Revit.Creation.Document _credoc;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
      UIApplication uiapp = commandData.Application;
      UIDocument uidoc = uiapp.ActiveUIDocument;
      Document doc = uidoc.Document;
      Application app = doc.Application;

      const string _family_name = "柱";
      const string _family_path = "D:/" + _family_name + ".rfa";

      string templateFileName = @"C:\ProgramData\Autodesk\RVT 2016\Family Templates\Chinese\公制结构柱.rft";
      Document familyDocument = app.NewFamilyDocument(templateFileName);
      ReferenceArray refer = new ReferenceArray();
      XYZ direction = new XYZ(0, 0, 10);


      Result rc = Result.Failed;
      using (Transaction tx = new Transaction(doc))
      {
        tx.Start("Create Column");


        if (null == familyDocument)
        {
          throw new System.Exception("未找到族文档");
        }
        //CreateColumnExtrusion(familyDocument);
        IList<Element> _lines = uidoc.Selection.PickElementsByRectangle("请框选闭合的柱轮廓线，支持详图线");
        List<XYZ> points = new List<XYZ>();

        foreach (var elem in _lines)
        {
          XYZ p1 = ((elem as DetailLine).GeometryCurve as Line).GetEndPoint(0);
          XYZ p2 = ((elem as DetailLine).GeometryCurve as Line).GetEndPoint(1);
          Line line = Line.CreateBound(p1, p2);
          XYZ normal = p1.CrossProduct(p2);
          Plane plane = new Plane(normal, p1);
          SketchPlane skPlane = SketchPlane.Create(doc, plane);
          ModelCurve c = doc.Create.NewModelCurve(line, skPlane);
          refer.Append(c.GeometryCurve.Reference);

          points.Add(p1);
          points.Add(p2);
        }
        //XYZ direction = new XYZ(0, 0, 10);
        XYZ p = new XYZ();
        //数组求和新方法
        foreach (var item in points)
        {
          p += item;
        }
        p = p / points.Count;

        tx.Commit();
      }

      CreateExtrusion(familyDocument, refer, direction);
      //Form form = familyDocument.FamilyCreate.NewExtrusionForm(true, refer, direction);

      //步骤：储存族文档并导入到项目中再修改相关的材质和标高以及名称
      using (Transaction t2 = new Transaction(doc))
      {
        t2.Start("div");

        SaveAsOptions opt = new SaveAsOptions();
        opt.OverwriteExistingFile = true;

        familyDocument.SaveAs(_family_path, opt);

        if (!doc.LoadFamily(_family_path))
        {
          throw new Exception("没有加载族");
        }
        Family family = new FilteredElementCollector(doc).OfClass(typeof(Family)).Where(x => x.Name.Equals(_family_name)).Cast<Family>().FirstOrDefault();
        FamilySymbol fs = doc.GetElement(family.GetFamilySymbolIds().First()) as FamilySymbol;

        //创建族实例
        Level level = doc.ActiveView.GenLevel;
        FamilyInstance fi = doc.Create.NewFamilyInstance(XYZ.Zero, fs, level, StructuralType.NonStructural);
        doc.Regenerate();
        rc = Result.Succeeded;
        t2.Commit();
      }
      return rc;
    }
    static void CreateExtrusion(Document doc, ReferenceArray refer, XYZ direction)
    {

      using (Transaction tx = new Transaction(doc))
      {
        tx.Start("Create Column");
        Form form = doc.FamilyCreate.NewExtrusionForm(true, refer, direction);
        tx.Commit();
      }
    }
  }
}