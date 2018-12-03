using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Structure;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Code
{
  [Transaction(TransactionMode.Manual)]
  class CmdColumn : IExternalCommand
  {
    ReferenceArray refer = new ReferenceArray();

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
      UIApplication uiapp = commandData.Application;
      UIDocument uidoc = uiapp.ActiveUIDocument;
      Document doc = uidoc.Document;
      Application app = doc.Application;
      Result rc = Result.Failed;

      const string _family_name = "柱10";
      const string _family_path = "D:/" + _family_name + ".rfa";
      //if (File.Exists(_family_path))
      //{
      //  File.Delete(_family_path);
      //}

      //string templateFileName = @"C:\ProgramData\Autodesk\RVT 2016\Family Templates\Chinese\公制结构柱.rft";
      string templateFileName = @"C:\ProgramData\Autodesk\RVT 2016\Family Templates\Chinese\概念体量\公制体量.rft";
      Document familyDocument = app.NewFamilyDocument(templateFileName);



      //Saveas family document
      SaveAsOptions opt = new SaveAsOptions();
      opt.OverwriteExistingFile = true;

      familyDocument.SaveAs(_family_path, opt);

      //ReferenceArray refer = new ReferenceArray();

      using (Transaction tx = new Transaction(doc))
      {
        tx.Start("Create Column");

        if (!doc.LoadFamily(_family_path))
        {
          throw new Exception("没有加载族");
        }

        //if (null == familyDocument)
        //{
        //  throw new System.Exception("未找到族文档");
        //}
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

        CreateExtrusion(familyDocument);

        Family family = new FilteredElementCollector(doc).OfClass(typeof(Family)).Where(x => x.Name.Equals(_family_name)).Cast<Family>().FirstOrDefault();
        FamilySymbol fs = doc.GetElement(family.GetFamilySymbolIds().First()) as FamilySymbol;

        //创建族实例
        Level level = doc.ActiveView.GenLevel;
        if (!fs.IsActive)
        {
          fs.Activate();
        }
        FamilyInstance fi = doc.Create.NewFamilyInstance(p, fs, level, StructuralType.NonStructural);
        doc.Regenerate();
        rc = Result.Succeeded;

        tx.Commit();
      }
      //步骤：储存族文档并导入到项目中再修改相关的材质和标高以及名称 
      return rc;
    }
    public void CreateExtrusion(Document doc)
    {

      using (Transaction tx = new Transaction(doc))
      {
        tx.Start("Create Column");
        // Create profile

        //ReferenceArray refar = new ReferenceArray();

        //XYZ[] pts = new XYZ[] { new XYZ(-10, -10, 0), new XYZ(+10, -10, 0), new XYZ(+10, +10, 0), new XYZ(-10, +10, 0) };

        //int j, n = pts.Length;


        //for (int i = 0; i < n; ++i)
        //{
        //  j = i + 1;

        //  if (j >= n) { j = 0; }

        //  ModelCurve c = MakeLine(doc, pts[i], pts[j]);

        //  refar.Append(c.GeometryCurve.Reference);
        //}
        XYZ direction = new XYZ( /*-6*/ 0, 0, 1000 / 304.8);

        Form form = doc.FamilyCreate.NewExtrusionForm(true, refer, direction);

        tx.Commit();
      }
    }

    //Create ModelCurve
    static ModelCurve MakeLine(Document doc, XYZ p, XYZ q)
    {
      // Create plane by the points

      Line line = Line.CreateBound(p, q);
      XYZ norm = p.CrossProduct(q);
      if (norm.GetLength() == 0) { norm = XYZ.BasisZ; }

      Plane plane = new Plane(norm, q); // 2016

      //Plane plane = Plane.CreateByNormalAndOrigin(norm, q); // 2017

      SketchPlane skplane = SketchPlane.Create(doc, plane);

      // Create line

      return doc.FamilyCreate.NewModelCurve(
        line, skplane);
    }
  }
}
