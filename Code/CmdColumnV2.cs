using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Structure;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Command
{
  [Transaction(TransactionMode.Manual)]

  class CmdColumnFamily : IExternalCommand
  {

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
      UIApplication uiapp = commandData.Application;
      UIDocument uidoc = uiapp.ActiveUIDocument;
      Document doc = uidoc.Document;
      Application app = doc.Application;
      Result rc = Result.Failed;

      const string _family_name = "柱19";
      const string _family_path = "D:/" + _family_name + ".rfa";

      string templateFileName = @"C:\ProgramData\Autodesk\RVT 2016\Family Templates\Chinese\公制柱.rft";
      //string templateFileName = @"C:\ProgramData\Autodesk\RVT 2016\Family Templates\Chinese\概念体量\公制体量.rft";
      Document familyDocument = app.NewFamilyDocument(templateFileName);

      //拾取详图线后再创建体量文档的模型线参照轮廓，并建立拉伸体
      IList<Element> _lines = uidoc.Selection.PickElementsByRectangle("请框选闭合的柱轮廓线，支持详图线");

      //ReferenceArray refer = new ReferenceArray();
      //using (Transaction trans = new Transaction(familyDocument))
      //{
      //  trans.Start("ModelLine");
      //  foreach (var line in _lines)
      //  {
      //    XYZ p1 = ((line as DetailLine).GeometryCurve as Line).GetEndPoint(0);
      //    XYZ p2 = ((line as DetailLine).GeometryCurve as Line).GetEndPoint(1);
      //    ModelCurve mc = MakeLine(familyDocument, p1, p2);
      //    refer.Append(mc.GeometryCurve.Reference);
      //  }
      //  trans.Commit();
      //}

      using (Transaction ts = new Transaction(familyDocument))
      {
        ts.Start("Create Column");
        XYZ norm = new XYZ(10, 0, 0).CrossProduct(new XYZ(0, 10, 0));
        Plane plane = new Plane(norm, new XYZ(0, 0, 0));
        SketchPlane sp = SketchPlane.Create(familyDocument, plane);
        ts.Commit();
        CreateExtrusion(familyDocument, sp);
      }

      //Saveas family document
      SaveAsOptions opt = new SaveAsOptions();
      opt.OverwriteExistingFile = true;

      familyDocument.SaveAs(_family_path, opt);

      using (Transaction tx = new Transaction(doc))
      {
        tx.Start("Create Column");

        if (!doc.LoadFamily(_family_path))
        {
          throw new Exception("没有加载族");
        }
        //if (File.Exists(_family_path))
        //  File.Delete(_family_path);
        List<XYZ> points = new List<XYZ>();

        foreach (var elem in _lines)
        {

          XYZ p1 = ((elem as DetailLine).GeometryCurve as Line).GetEndPoint(0);
          XYZ p2 = ((elem as DetailLine).GeometryCurve as Line).GetEndPoint(1);
          //Line line = Line.CreateBound(p1, p2);
          //XYZ normal = p1.CrossProduct(p2);
          //Plane plane = new Plane(normal, p1);
          //SketchPlane skPlane = SketchPlane.Create(doc, plane);
          //ModelCurve c = doc.Create.NewModelCurve(line, skPlane);
          //refer.Append(c.GeometryCurve.Reference);

          points.Add(p1);
          points.Add(p2);
        }

        XYZ p = new XYZ();
        //数组求和新方法
        foreach (var item in points)
        {
          p += item;
        }
        p = p / points.Count;

        //通过族名过滤在项目中创建的族
        Family family = new FilteredElementCollector(doc).OfClass(typeof(Family)).Where(x => x.Name.Equals(_family_name)).Cast<Family>().FirstOrDefault();
        FamilySymbol fs = doc.GetElement(family.GetFamilySymbolIds().First()) as FamilySymbol;

        //创建族实例
        Level level = doc.ActiveView.GenLevel;
        if (!fs.IsActive)
        {
          fs.Activate();
        }
        FamilyInstance fi = doc.Create.NewFamilyInstance(p, fs, level, StructuralType.NonStructural);
        //doc.Regenerate();
        rc = Result.Succeeded;

        tx.Commit();
      }
      //步骤：储存族文档并导入到项目中前修改相关的材质和标高以及名称 
      return rc;
    }

    private Extrusion CreateExtrusion(Document document, SketchPlane sketchPlane)
    {
      Extrusion rectExtrusion = null;

      // make sure we have a family document
      if (true == document.IsFamilyDocument)
      {
        // define the profile for the extrusion
        CurveArrArray curveArrArray = new CurveArrArray();
        CurveArray curveArray1 = new CurveArray();
        CurveArray curveArray2 = new CurveArray();
        CurveArray curveArray3 = new CurveArray();

        // create a rectangular profile
        XYZ p0 = XYZ.Zero;
        XYZ p1 = new XYZ(10 / 304.8, 0, 0);
        XYZ p2 = new XYZ(10 / 304.8, 10 / 304.8, 0);
        XYZ p3 = new XYZ(0, 10 / 304.8, 0);
        Line line1 = Line.CreateBound(p0, p1);
        Line line2 = Line.CreateBound(p1, p2);
        Line line3 = Line.CreateBound(p2, p3);
        Line line4 = Line.CreateBound(p3, p0);
        curveArray1.Append(line1);
        curveArray1.Append(line2);
        curveArray1.Append(line3);
        curveArray1.Append(line4);

        curveArrArray.Append(curveArray1);

        using (Transaction ts = new Transaction(document))
        {
          ts.Start("Create");
          // create solid rectangular extrusion
          rectExtrusion = document.FamilyCreate.NewExtrusion(true, curveArrArray, sketchPlane, 1000 / 304.8);

          if (null != rectExtrusion)
          {
            // move extrusion to proper place
            XYZ transPoint1 = new XYZ(2000 / 304.8, 0, 0);
            ElementTransformUtils.MoveElement(document, rectExtrusion.Id, transPoint1);
          }
          else
          {
            throw new Exception("Create new Extrusion failed.");
          }
          ts.Commit();
        }
      }

      else
      {
        throw new Exception("Please open a Family document.");
      }

      return rectExtrusion;
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

