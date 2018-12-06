using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Structure;



[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
class ReadCadCommand : IExternalCommand
{
  Application app;
  Document doc;
  UIDocument uidoc;
  /// <summary>
  /// 正常樑寬度
  /// </summary>
  const double NormBeamWidth = 1000;//1000mm


  /// <summary>
  /// 所有正常樑寬度集合
  /// </summary>
  List<double> WidthList = new List<double>();



  public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
  {
    uidoc = commandData.Application.ActiveUIDocument;
    app = commandData.Application.Application;
    doc = uidoc.Document;


    Reference r;
    try
    {
      r = uidoc.Selection.PickObject(ObjectType.PointOnElement,“選擇樑的一邊”);//選擇一個元素
    }
    catch (Exception ex)
    {
      message = "您取消了本次操作！";
      return Result.Failed;
    }

    //Reference r = uidoc.Selection.PickObject(ObjectType.PointOnElement);//選擇一個元素
    string ss = r.ConvertToStableRepresentation(doc);

    Element elem = doc.GetElement(r);
    GeometryElement geoElem = elem.get_Geometry(new Options());
    GeometryObject geoObj = elem.GetGeometryObjectFromReference(r);


    //獲取選中的cad圖層
    Category targetCategory = null;
    ElementId graphicsStyleId = null;


    if (geoObj.GraphicsStyleId != ElementId.InvalidElementId)
    {
      graphicsStyleId = geoObj.GraphicsStyleId;
      GraphicsStyle gs = doc.GetElement(geoObj.GraphicsStyleId) as GraphicsStyle;
      if (gs != null)
      {
        targetCategory = gs.GraphicsStyleCategory;
        var name = gs.GraphicsStyleCategory.Name;
      }
    }
    //隱藏選中的cad圖層
    Transaction trans = new Transaction(doc, "隱藏圖層");
    trans.Start();
    if (targetCategory != null)
      doc.ActiveView.SetCategoryHidden(targetCategory.Id, false);


    trans.Commit();

    if (geoElem == null || graphicsStyleId == null)
    {
      message = "幾何元素或ID不存在！";
      return Result.Failed;
    }

    List<CADModel> curveArray_List = getCurveArray(doc, geoElem, graphicsStyleId);
    List<CADModel> curveArray_List_copy = new List<CADModel>();//複製得到的模型
    foreach (var OrginCADModle in curveArray_List)
    {
      curveArray_List_copy.Add(OrginCADModle);
    }


    //取得的模型的線的總數量
    int LineNumber = curveArray_List.Count();




    //存放不匹配的樑的相關線
    List<CADModel> NotMatchCadModel = new List<CADModel>();
    //存放模型數組的數組
    List<List<CADModel>> CADModelList_List = new List<List<CADModel>>();
    //int i = 0;

    //篩選模型
    while (curveArray_List.Count() > 0)
    {

      //存放距離
      List<double> distanceList = new List<double>();
      //存放對應距離的CADModel
      List<CADModel> cADModel_B_List = new List<CADModel>();

      var CadModel_A = curveArray_List[0];
      curveArray_List.Remove(CadModel_A);//去除取出的樑的二段線段之一

      if (curveArray_List.Count() >= 1)
      {
        foreach (var CadModel_B in curveArray_List)
        {
          //樑的2個段線非同一長度最大誤差爲50mm，方向爲絕對值（然而sin120°=sin60°）
          if ((float)Math.Abs(CadModel_A.rotation) == (float)Math.Abs(CadModel_B.rotation) && Math.Abs(CadModel_A.length - CadModel_B.length) < 0.164)
          {
            double distance = CadModel_A.location.DistanceTo(CadModel_B.location);
            distanceList.Add(distance);
            cADModel_B_List.Add(CadModel_B);
          }
        }


        if (distanceList.Count() != 0 && cADModel_B_List.Count != 0)
        {
          double distanceTwoLine = distanceList.Min();
          //篩選不正常的寬度,如發現不正常，將CadModel_B繼續放入數組
          if (distanceTwoLine * 304.8 < NormBeamWidth && distanceTwoLine > 0)
          {
            //TaskDialog.Show("1", (distanceTwoLine * 304.8).ToString());

            var CadModel_shortDistance = cADModel_B_List[distanceList.IndexOf(distanceTwoLine)];
            curveArray_List.Remove(CadModel_shortDistance);
            //1對樑的模型裝入數組
            List<CADModel> cADModels = new List<CADModel>();
            cADModels.Add(CadModel_A);
            cADModels.Add(CadModel_shortDistance);
            CADModelList_List.Add(cADModels);
            //TaskDialog.Show("1", CadModel_A.location.ToString() + "\n" + CadModel_shortDistance.location.ToString());

          }
        }
        else
        {
          NotMatchCadModel.Add(CadModel_A);
        }

      }
      else
      {
        NotMatchCadModel.Add(CadModel_A);
      }

    }

    TaskDialog.Show("1", "未匹配的線有：" + NotMatchCadModel.Count().ToString() + " 條!\n" + "匹配上的有：" + CADModelList_List.Count().ToString() + " 對！\n" + "丟失：" + (LineNumber - NotMatchCadModel.Count() - CADModelList_List.Count() * 2).ToString() + " 條！\n");
    //樑類別
    FamilySymbol BeamTypeName = doc.GetElement(new ElementId(342873)) as FamilySymbol;
    //默認標高2
    Level level = LevelFilter(doc);
    int tranNumber = 0;//用於改變事務的ID
                       //生成樑
    foreach (var cadModelList in CADModelList_List)
    {
      CADModel cADModel_A = cadModelList[0];
      CADModel cADModel_B = cadModelList[1];

      //TaskDialog.Show("1", cADModel_A.location.ToString() + "\n" + cADModel_B.location.ToString());

      var cADModel_A_StratPoint = cADModel_A.curveArray.get_Item(0).GetEndPoint(0);
      var cADModel_A_EndPoint = cADModel_A.curveArray.get_Item(0).GetEndPoint(1);
      var cADModel_B_StratPoint = cADModel_B.curveArray.get_Item(0).GetEndPoint(0);
      var cADModel_B_EndPoint = cADModel_B.curveArray.get_Item(0).GetEndPoint(1);

      XYZ ChangeXYZ = new XYZ();

      var LineLength = (GetMiddlePoint(cADModel_A_StratPoint, cADModel_B_StratPoint)).DistanceTo(GetMiddlePoint(cADModel_A_EndPoint, cADModel_B_EndPoint));
      if (LineLength < 0.00328)//樑的2段線起點非同一端。2段線非同一長度，又非同一端的，中間點的誤差選擇爲1mm
      {
        ChangeXYZ = cADModel_B_StratPoint;
        cADModel_B_StratPoint = cADModel_B_EndPoint;
        cADModel_B_EndPoint = ChangeXYZ;
      }
      Curve curve = Line.CreateBound((GetMiddlePoint(cADModel_A_StratPoint, cADModel_B_StratPoint)), GetMiddlePoint(cADModel_A_EndPoint, cADModel_B_EndPoint));

      double distance = cADModel_A.location.DistanceTo(cADModel_B.location);

      distance = Math.Round(distance * 304.8, 1);//作爲樑_b的參數
      WidthList.Add(distance);//樑寬度集合

      string beamName = "ZBIM矩形樑 " + (float)(distance) + "*" + (float)(600) + "mm";//類型名 寬度*高度
      if (!familSymbol_exists(beamName, "ZBIM - 矩形樑", doc))
      {
        MakeBeamType(beamName, "ZBIM - 矩形樑");
        EditBeamType(beamName, (float)(distance), (float)(600));
      }

      //用於數據顯示和選擇，已註釋
      #region
      //List<string> columnTypes = new List<string>();
      //columnTypes = getBeamTypes(doc);
      //bool repeat = false;
      //foreach (string context in columnTypes)
      //{
      //    if (context == beamName)
      //    {
      //        repeat = true;
      //        break;
      //    }
      //}
      //if (!repeat)
      //{
      //    columnTypes.Add(beamName);
      //}
      #endregion

      using (Transaction transaction = new Transaction(doc))
      {
        transaction.Start("Beadm Strart Bulid" + tranNumber.ToString());
        FilteredElementCollector collector = new FilteredElementCollector(doc);
        collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming);


        foreach (FamilySymbol beamType in collector)
        {
          if (beamType.Name == beamName)
          {
            if (!beamType.IsActive)
            {
              beamType.Activate();
            }
            FamilyInstance beamInstance = doc.Create.NewFamilyInstance(curve, beamType, level, StructuralType.Beam);
            var Elevation = beamInstance.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);

            break;
          }
        }



        transaction.Commit();
      }


    }




    //取得未匹配的和丟失的CADModel
    foreach (var cadModelList in CADModelList_List)
    {
      CADModel cADModel_A = cadModelList[0];
      CADModel cADModel_B = cadModelList[1];
      curveArray_List_copy.Remove(cADModel_A);
      curveArray_List_copy.Remove(cADModel_B);
    }
    //一個方向的樑
    List<CADModel> UpBeamCAdModel = new List<CADModel>();
    //一個方向的樑
    List<CADModel> CrossBeamCAdModel = new List<CADModel>();

    //最大梁寬度
    double MaxBeamWidth;
    if (WidthList.Count() == 0)
    {
      MaxBeamWidth = 1000;//1000mm
    }
    else
    {
      MaxBeamWidth = WidthList.Max();
    }
    //判斷是否位空
    if (curveArray_List_copy.Count() > 1)
    {
      var OrginRotation = Math.Abs(curveArray_List_copy[0].rotation);
      //分流，橫在一起，豎在一起
      foreach (var cadModle in curveArray_List_copy)
      {
        if (Math.Abs(cadModle.rotation) == OrginRotation)
        {
          CrossBeamCAdModel.Add(cadModle);
        }
        else
        {
          UpBeamCAdModel.Add(cadModle);
        }
      }
    }

    //判單方向的數量
    if (CrossBeamCAdModel.Count() > 2)
    {
      var CrossMinLength = CrossBeamCAdModel.Select(c => c.length).ToList().Min();//取出最小的線段長度
                                                                                  //將大約2倍最小長度的CAd模型編組
      var LongCrossLenth = from n in CrossBeamCAdModel
                           where n.length > CrossMinLength * 2
                           select n;
      //降序
      var newA = from n in LongCrossLenth
                 orderby n.length descending
                 select n;
      List<CADModel> LongCrossLenth_list = newA.ToList();

      //存放模型數組的數組A組-第二次
      List<List<CADModel>> CADModelList_Second = new List<List<CADModel>>();
      ////查詢失敗
      List<List<CADModel>> FailCadModel = new List<List<CADModel>>();
      while (LongCrossLenth_list.Count() > 0)
      {
        //存放距離
        // List<double> distanceList = new List<double>();
        //存放對應距離的CADModel
        List<CADModel> cADModel_B_List = new List<CADModel>();

        var CadModel_Main = LongCrossLenth_list[0];//取出主模型
        XYZ StartPoint_Main = CadModel_Main.curveArray.get_Item(0).GetEndPoint(0);//取出線段2點
        XYZ EndPoint_Main = CadModel_Main.curveArray.get_Item(0).GetEndPoint(1);
        LongCrossLenth_list.Remove(CadModel_Main);
        CrossBeamCAdModel.Remove(CadModel_Main);
        foreach (var cadModelFirst in CrossBeamCAdModel)
        {
          Line line = CadModel_Main.curveArray.get_Item(0) as Line;//取出主模型的線

          if (cadModelFirst.length >= CadModel_Main.length)
          {
            continue;
          }
          double PointLineDistance = line.Distance(cadModelFirst.location);
          PointLineDistance = Math.Round(PointLineDistance * 304.8, 1);
          if (PointLineDistance > MaxBeamWidth || PointLineDistance == 0)
          {
            continue;
          }
          XYZ StartPoint_First = cadModelFirst.curveArray.get_Item(0).GetEndPoint(0);//子線段2點
          XYZ EndPoint_First = cadModelFirst.curveArray.get_Item(0).GetEndPoint(1);
          double A1 = StartPoint_First.DistanceTo(StartPoint_Main);
          double A2 = StartPoint_First.DistanceTo(EndPoint_Main);
          double B1 = EndPoint_First.DistanceTo(StartPoint_Main);
          double B2 = EndPoint_First.DistanceTo(EndPoint_Main);
          if (Math.Abs(PointLineDistance - A1 * 304.8) < 1 || Math.Abs(PointLineDistance - A2 * 304.8) < 1 || Math.Abs(PointLineDistance - B1 * 304.8) < 1 || Math.Abs(PointLineDistance - B2 * 304.8) < 1)
          {

            cADModel_B_List.Add(cadModelFirst);
          }


        }
        List<CADModel> FailedCadmodel = new List<CADModel>();
        if (cADModel_B_List.Count() == 1)
        {

          FailedCadmodel.Add(cADModel_B_List[0]);
          FailedCadmodel.Add(CadModel_Main);
          FailCadModel.Add(FailedCadmodel);
        }
        if (cADModel_B_List.Count() == 2)
        {

          cADModel_B_List.Add(CadModel_Main);
          CADModelList_Second.Add(cADModel_B_List);
        }
        else
        {
          CrossBeamCAdModel.Add(CadModel_Main);
        }

        //FailCadModel.Add(FailedCadmodel);
      }
      ////第二組A成功部分生成
      foreach (var CAdMidelList in CADModelList_Second)
      {
        CADModel cADModel_A = CAdMidelList[0];
        CADModel cADModel_B = CAdMidelList[1];
        CADModel cADModel_Main = CAdMidelList[2];

        Line line = cADModel_Main.curveArray.get_Item(0) as Line;

        List<double> MinDistance = new List<double>();//最短距離
        List<double> MinDistanceB = new List<double>();//最短距離B
        XYZ OnePoint = new XYZ();
        XYZ TwoPont = new XYZ();
        var cADModel_A_StratPoint = cADModel_A.curveArray.get_Item(0).GetEndPoint(0);
        var cADModel_A_EndPoint = cADModel_A.curveArray.get_Item(0).GetEndPoint(1);
        double distanceA = line.Distance(cADModel_A.location);

        var cADModel_B_StratPoint = cADModel_B.curveArray.get_Item(0).GetEndPoint(0);
        var cADModel_B_EndPoint = cADModel_B.curveArray.get_Item(0).GetEndPoint(1);
        double distanceB = line.Distance(cADModel_B.location);

        var cADModel_Main_StratPoint = cADModel_Main.curveArray.get_Item(0).GetEndPoint(0);
        var cADModel_Main_EndPoint = cADModel_Main.curveArray.get_Item(0).GetEndPoint(1);


        double A1 = cADModel_A_StratPoint.DistanceTo(cADModel_Main_StratPoint);
        MinDistance.Add(A1);
        double A2 = cADModel_A_StratPoint.DistanceTo(cADModel_Main_EndPoint);
        MinDistance.Add(A2);
        double A3 = cADModel_A_EndPoint.DistanceTo(cADModel_Main_StratPoint);
        MinDistance.Add(A3);
        double A4 = cADModel_A_EndPoint.DistanceTo(cADModel_Main_EndPoint);
        MinDistance.Add(A4);
        double MainDistance_A = MinDistance.Min();
        int index_A = MinDistance.IndexOf(MainDistance_A);
        switch (index_A)
        {
          case 0:
            OnePoint = cADModel_A_StratPoint;
            break;
          case 1:
            OnePoint = cADModel_A_StratPoint;
            break;
          case 2:
            OnePoint = cADModel_A_EndPoint;
            break;
          case 3:
            OnePoint = cADModel_A_EndPoint;
            break;
        }

        double B1 = cADModel_B_StratPoint.DistanceTo(cADModel_Main_StratPoint);
        MinDistanceB.Add(B1);
        double B2 = cADModel_B_StratPoint.DistanceTo(cADModel_Main_EndPoint);
        MinDistanceB.Add(B2);
        double B3 = cADModel_B_EndPoint.DistanceTo(cADModel_Main_StratPoint);
        MinDistanceB.Add(B3);
        double B4 = cADModel_B_EndPoint.DistanceTo(cADModel_Main_EndPoint);
        MinDistanceB.Add(B4);
        double MainDistance_b = MinDistanceB.Min();
        int index_B = MinDistanceB.IndexOf(MainDistance_b);
        switch (index_B)
        {
          case 0:
            TwoPont = cADModel_B_StratPoint;
            break;
          case 1:
            TwoPont = cADModel_B_StratPoint;
            break;
          case 2:
            TwoPont = cADModel_B_EndPoint;
            break;
          case 3:
            TwoPont = cADModel_B_EndPoint;
            break;
        }

        MinDistance.Clear();
        MinDistanceB.Clear();

        XYZ ChangeXYZ = new XYZ();

        var LineLength = (GetMiddlePoint(OnePoint, cADModel_Main_StratPoint)).DistanceTo(GetMiddlePoint(TwoPont, cADModel_Main_EndPoint));
        if (LineLength < 0.00328)//樑的2段線起點非同一端。2段線非同一長度，又非同一端的，中間點的誤差選擇爲1mm
        {
          ChangeXYZ = TwoPont;
          TwoPont = OnePoint;
          OnePoint = ChangeXYZ;
        }
        Curve curve = Line.CreateBound((GetMiddlePoint(OnePoint, cADModel_Main_StratPoint)), GetMiddlePoint(TwoPont, cADModel_Main_EndPoint));


        double distance = MainDistance_A;//cADModel_A.location.DistanceTo(cADModel_Main.location);

        distance = Math.Round(distance * 304.8, 1);//作爲樑_b的參數


        string beamName = "ZBIM矩形樑 " + (float)(distance) + "*" + (float)(600) + "mm";//類型名 寬度*高度
        if (!familSymbol_exists(beamName, "ZBIM - 矩形樑", doc))
        {
          MakeBeamType(beamName, "ZBIM - 矩形樑");
          EditBeamType(beamName, (float)(distance), (float)(600));
        }

        using (Transaction transaction = new Transaction(doc))
        {
          transaction.Start("Beadm Strart Bulid" + tranNumber.ToString());
          FilteredElementCollector collector = new FilteredElementCollector(doc);
          collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming);


          foreach (FamilySymbol beamType in collector)
          {
            if (beamType.Name == beamName)
            {
              if (!beamType.IsActive)
              {
                beamType.Activate();
              }
              FamilyInstance beamInstance = doc.Create.NewFamilyInstance(curve, beamType, level, StructuralType.Beam);
              var Elevation = beamInstance.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);

              break;
            }
          }

          //foreach()

          transaction.Commit();
        }

      }
      //第二組A的失敗部分生成
      foreach (var CAdMidelList in FailCadModel)
      {
        CADModel cADModel_A = CAdMidelList[0];
        CADModel cADModel_Main = CAdMidelList[1];

        Line line = cADModel_Main.curveArray.get_Item(0) as Line;

        List<double> MinDistance = new List<double>();

        XYZ OnePoint = new XYZ();
        XYZ TwoPoint;//用於編造第二點
        var cADModel_A_StratPoint = cADModel_A.curveArray.get_Item(0).GetEndPoint(0);
        var cADModel_A_EndPoint = cADModel_A.curveArray.get_Item(0).GetEndPoint(1);

        var cADModel_Main_StratPoint = cADModel_Main.curveArray.get_Item(0).GetEndPoint(0);
        var cADModel_Main_EndPoint = cADModel_Main.curveArray.get_Item(0).GetEndPoint(1);

        double A1 = cADModel_A_StratPoint.DistanceTo(cADModel_Main_StratPoint);
        MinDistance.Add(A1);
        double A2 = cADModel_A_StratPoint.DistanceTo(cADModel_Main_EndPoint);
        MinDistance.Add(A2);
        double A3 = cADModel_A_EndPoint.DistanceTo(cADModel_Main_StratPoint);
        MinDistance.Add(A3);
        double A4 = cADModel_A_EndPoint.DistanceTo(cADModel_Main_EndPoint);
        MinDistance.Add(A4);
        double MainDistance_A = MinDistance.Min();
        int index_A = MinDistance.IndexOf(MainDistance_A);
        switch (index_A)
        {
          case 0:
            OnePoint = cADModel_A_StratPoint;
            break;
          case 1:
            OnePoint = cADModel_A_StratPoint;
            break;
          case 2:
            OnePoint = cADModel_A_EndPoint;
            break;
          case 3:
            OnePoint = cADModel_A_EndPoint;
            break;
        }


        if ((OnePoint.X - cADModel_Main_StratPoint.X) < 0.001)
        {
          TwoPoint = new XYZ(cADModel_Main_EndPoint.X, OnePoint.Y, OnePoint.Z);

        }
        else if ((OnePoint.X - cADModel_Main_EndPoint.X) < 0.001)
        {
          TwoPoint = new XYZ(cADModel_Main_StratPoint.X, OnePoint.Y, OnePoint.Z);
        }
        else
        {
          continue;
        }


        XYZ ChangeXYZ = new XYZ();

        var LineLength = (GetMiddlePoint(OnePoint, cADModel_Main_StratPoint)).DistanceTo(GetMiddlePoint(TwoPoint, cADModel_Main_EndPoint));
        if (LineLength < 0.00328)//樑的2段線起點非同一端。2段線非同一長度，又非同一端的，中間點的誤差選擇爲1mm
        {
          ChangeXYZ = TwoPoint;
          TwoPoint = OnePoint;
          OnePoint = ChangeXYZ;
        }
        Curve curve = Line.CreateBound((GetMiddlePoint(OnePoint, cADModel_Main_StratPoint)), GetMiddlePoint(TwoPoint, cADModel_Main_EndPoint));


        double distance = MainDistance_A;//cADModel_A.location.DistanceTo(cADModel_Main.location);

        distance = Math.Round(distance * 304.8, 1);//作爲樑_b的參數


        string beamName = "ZBIM矩形樑 " + (float)(distance) + "*" + (float)(600) + "mm";//類型名 寬度*高度
        if (!familSymbol_exists(beamName, "ZBIM - 矩形樑", doc))
        {
          MakeBeamType(beamName, "ZBIM - 矩形樑");
          EditBeamType(beamName, (float)(distance), (float)(600));
        }

        using (Transaction transaction = new Transaction(doc))
        {
          transaction.Start("Beadm Strart Bulid" + tranNumber.ToString());
          FilteredElementCollector collector = new FilteredElementCollector(doc);
          collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming);


          foreach (FamilySymbol beamType in collector)
          {
            if (beamType.Name == beamName)
            {
              if (!beamType.IsActive)
              {
                beamType.Activate();
              }
              FamilyInstance beamInstance = doc.Create.NewFamilyInstance(curve, beamType, level, StructuralType.Beam);
              var Elevation = beamInstance.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);

              break;
            }
          }

          //foreach()

          transaction.Commit();
        }


      }
    }

    //判斷單方向數量
    if (UpBeamCAdModel.Count() > 2)
    {
      var UpMinLength = UpBeamCAdModel.Select(c => c.length).ToList().Min();
      var LongUpLength = from n in UpBeamCAdModel
                         where n.length > UpMinLength * 2
                         select n;
      //降序
      var newB = from n in LongUpLength
                 orderby n.length descending
                 select n;
      List<CADModel> LongUPLenth_list_B = newB.ToList();

      //存放模型數組的數組B組-第二次（豎着）
      List<List<CADModel>> CADModelList_Second_B = new List<List<CADModel>>();
      //查詢失敗B組
      List<List<CADModel>> FailCadModel_B = new List<List<CADModel>>();
      while (LongUPLenth_list_B.Count() > 0)
      {
        //存放距離
        // List<double> distanceList = new List<double>();
        //存放對應距離的CADModel
        List<CADModel> cADModel_B_List = new List<CADModel>();

        var CadModel_Main = LongUPLenth_list_B[0];//取出主模型
        XYZ StartPoint_Main = CadModel_Main.curveArray.get_Item(0).GetEndPoint(0);//取出線段2點
        XYZ EndPoint_Main = CadModel_Main.curveArray.get_Item(0).GetEndPoint(1);
        LongUPLenth_list_B.Remove(CadModel_Main);
        UpBeamCAdModel.Remove(CadModel_Main);
        foreach (var cadModelFirst in UpBeamCAdModel)
        {
          Line line = CadModel_Main.curveArray.get_Item(0) as Line;//取出主模型的線

          if (cadModelFirst.length >= CadModel_Main.length)
          {
            continue;
          }
          double PointLineDistance = line.Distance(cadModelFirst.location);
          PointLineDistance = Math.Round(PointLineDistance * 304.8, 1);
          if (PointLineDistance > MaxBeamWidth || PointLineDistance == 0)
          {
            continue;
          }
          XYZ StartPoint_First = cadModelFirst.curveArray.get_Item(0).GetEndPoint(0);//子線段2點
          XYZ EndPoint_First = cadModelFirst.curveArray.get_Item(0).GetEndPoint(1);
          double A1 = StartPoint_First.DistanceTo(StartPoint_Main);
          double A2 = StartPoint_First.DistanceTo(EndPoint_Main);
          double B1 = EndPoint_First.DistanceTo(StartPoint_Main);
          double B2 = EndPoint_First.DistanceTo(EndPoint_Main);
          if (Math.Abs(PointLineDistance - A1 * 304.8) < 1 || Math.Abs(PointLineDistance - A2 * 304.8) < 1 || Math.Abs(PointLineDistance - B1 * 304.8) < 1 || Math.Abs(PointLineDistance - B2 * 304.8) < 1)
          {

            cADModel_B_List.Add(cadModelFirst);
          }


        }
        List<CADModel> FailedCadmodel = new List<CADModel>();
        if (cADModel_B_List.Count() == 1)
        {

          FailedCadmodel.Add(cADModel_B_List[0]);
          FailedCadmodel.Add(CadModel_Main);
          FailCadModel_B.Add(FailedCadmodel);
        }
        if (cADModel_B_List.Count() == 2)
        {

          cADModel_B_List.Add(CadModel_Main);
          CADModelList_Second_B.Add(cADModel_B_List);
        }
        else
        {
          UpBeamCAdModel.Add(CadModel_Main);
        }


      }
      TaskDialog.Show("1", FailCadModel_B.Count().ToString());
      //第二組B成功部分生成
      foreach (var CAdMidelList in CADModelList_Second_B)
      {
        CADModel cADModel_A = CAdMidelList[0];
        CADModel cADModel_B = CAdMidelList[1];
        CADModel cADModel_Main = CAdMidelList[2];
        Line line = cADModel_Main.curveArray.get_Item(0) as Line;

        List<double> MinDistance = new List<double>();//最短距離
        List<double> MinDistanceB = new List<double>();//最短距離B
        XYZ OnePoint = new XYZ();
        XYZ TwoPont = new XYZ();
        var cADModel_A_StratPoint = cADModel_A.curveArray.get_Item(0).GetEndPoint(0);
        var cADModel_A_EndPoint = cADModel_A.curveArray.get_Item(0).GetEndPoint(1);
        double distanceA = line.Distance(cADModel_A.location);

        var cADModel_B_StratPoint = cADModel_B.curveArray.get_Item(0).GetEndPoint(0);
        var cADModel_B_EndPoint = cADModel_B.curveArray.get_Item(0).GetEndPoint(1);
        double distanceB = line.Distance(cADModel_B.location);

        var cADModel_Main_StratPoint = cADModel_Main.curveArray.get_Item(0).GetEndPoint(0);
        var cADModel_Main_EndPoint = cADModel_Main.curveArray.get_Item(0).GetEndPoint(1);


        double A1 = cADModel_A_StratPoint.DistanceTo(cADModel_Main_StratPoint);
        MinDistance.Add(A1);
        double A2 = cADModel_A_StratPoint.DistanceTo(cADModel_Main_EndPoint);
        MinDistance.Add(A2);
        double A3 = cADModel_A_EndPoint.DistanceTo(cADModel_Main_StratPoint);
        MinDistance.Add(A3);
        double A4 = cADModel_A_EndPoint.DistanceTo(cADModel_Main_EndPoint);
        MinDistance.Add(A4);
        double MainDistance_A = MinDistance.Min();
        int index_A = MinDistance.IndexOf(MainDistance_A);
        switch (index_A)
        {
          case 0:
            OnePoint = cADModel_A_StratPoint;
            break;
          case 1:
            OnePoint = cADModel_A_StratPoint;
            break;
          case 2:
            OnePoint = cADModel_A_EndPoint;
            break;
          case 3:
            OnePoint = cADModel_A_EndPoint;
            break;
        }

        double B1 = cADModel_B_StratPoint.DistanceTo(cADModel_Main_StratPoint);
        MinDistanceB.Add(B1);
        double B2 = cADModel_B_StratPoint.DistanceTo(cADModel_Main_EndPoint);
        MinDistanceB.Add(B2);
        double B3 = cADModel_B_EndPoint.DistanceTo(cADModel_Main_StratPoint);
        MinDistanceB.Add(B3);
        double B4 = cADModel_B_EndPoint.DistanceTo(cADModel_Main_EndPoint);
        MinDistanceB.Add(B4);
        double MainDistance_b = MinDistanceB.Min();
        int index_B = MinDistanceB.IndexOf(MainDistance_b);
        switch (index_B)
        {
          case 0:
            TwoPont = cADModel_B_StratPoint;
            break;
          case 1:
            TwoPont = cADModel_B_StratPoint;
            break;
          case 2:
            TwoPont = cADModel_B_EndPoint;
            break;
          case 3:
            TwoPont = cADModel_B_EndPoint;
            break;
        }

        MinDistance.Clear();
        MinDistanceB.Clear();

        XYZ ChangeXYZ = new XYZ();

        var LineLength = (GetMiddlePoint(OnePoint, cADModel_Main_StratPoint)).DistanceTo(GetMiddlePoint(TwoPont, cADModel_Main_EndPoint));
        if (LineLength < 0.00328)//樑的2段線起點非同一端。2段線非同一長度，又非同一端的，中間點的誤差選擇爲1mm
        {
          ChangeXYZ = TwoPont;
          TwoPont = OnePoint;
          OnePoint = ChangeXYZ;
        }
        Curve curve = Line.CreateBound((GetMiddlePoint(OnePoint, cADModel_Main_StratPoint)), GetMiddlePoint(TwoPont, cADModel_Main_EndPoint));

        //TaskDialog.Show("1", OnePoint.ToString() + "\n" + TwoPont.ToString());
        //TaskDialog.Show("1", curve.GetEndPoint(0).ToString() + "\n" + curve.GetEndPoint(1).ToString());


        double distance = MainDistance_A;//cADModel_A.location.DistanceTo(cADModel_Main.location);

        distance = Math.Round(distance * 304.8, 1);//作爲樑_b的參數


        string beamName = "ZBIM矩形樑 " + (float)(distance) + "*" + (float)(600) + "mm";//類型名 寬度*高度
        if (!familSymbol_exists(beamName, "ZBIM - 矩形樑", doc))
        {
          MakeBeamType(beamName, "ZBIM - 矩形樑");
          EditBeamType(beamName, (float)(distance), (float)(600));
        }

        using (Transaction transaction = new Transaction(doc))
        {
          transaction.Start("Beadm Strart Bulid" + tranNumber.ToString());
          FilteredElementCollector collector = new FilteredElementCollector(doc);
          collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming);


          foreach (FamilySymbol beamType in collector)
          {
            if (beamType.Name == beamName)
            {
              if (!beamType.IsActive)
              {
                beamType.Activate();
              }
              FamilyInstance beamInstance = doc.Create.NewFamilyInstance(curve, beamType, level, StructuralType.Beam);
              var Elevation = beamInstance.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
              //Elevation.Set(LevelFilter(doc,"標高 1").Id);
              //Parameter bottom = column.LookupParameter("底部偏移");
              //bottom.Set(UnitUtils.ConvertToInternalUnits(offsetElev, DisplayUnitType.DUT_MILLIMETERS));
              //Parameter top = column.LookupParameter("頂部偏移");
              //top.Set(UnitUtils.ConvertToInternalUnits(high, DisplayUnitType.DUT_MILLIMETERS));
              break;
            }
          }

          //foreach()

          transaction.Commit();
        }
        //i++;
      }
      //第二組B的失敗部分生成
      foreach (var CAdMidelList in FailCadModel_B)
      {
        CADModel cADModel_A = CAdMidelList[0];
        CADModel cADModel_Main = CAdMidelList[1];

        Line line = cADModel_Main.curveArray.get_Item(0) as Line;

        List<double> MinDistance = new List<double>();

        XYZ OnePoint = new XYZ();
        XYZ TwoPoint;//用於編造第二點
        var cADModel_A_StratPoint = cADModel_A.curveArray.get_Item(0).GetEndPoint(0);
        var cADModel_A_EndPoint = cADModel_A.curveArray.get_Item(0).GetEndPoint(1);

        var cADModel_Main_StratPoint = cADModel_Main.curveArray.get_Item(0).GetEndPoint(0);
        var cADModel_Main_EndPoint = cADModel_Main.curveArray.get_Item(0).GetEndPoint(1);

        double A1 = cADModel_A_StratPoint.DistanceTo(cADModel_Main_StratPoint);
        MinDistance.Add(A1);
        double A2 = cADModel_A_StratPoint.DistanceTo(cADModel_Main_EndPoint);
        MinDistance.Add(A2);
        double A3 = cADModel_A_EndPoint.DistanceTo(cADModel_Main_StratPoint);
        MinDistance.Add(A3);
        double A4 = cADModel_A_EndPoint.DistanceTo(cADModel_Main_EndPoint);
        MinDistance.Add(A4);
        double MainDistance_A = MinDistance.Min();
        int index_A = MinDistance.IndexOf(MainDistance_A);
        switch (index_A)
        {
          case 0:
            OnePoint = cADModel_A_StratPoint;
            break;
          case 1:
            OnePoint = cADModel_A_StratPoint;
            break;
          case 2:
            OnePoint = cADModel_A_EndPoint;
            break;
          case 3:
            OnePoint = cADModel_A_EndPoint;
            break;
        }


        if ((OnePoint.Y - cADModel_Main_StratPoint.Y) < 0.001)
        {
          TwoPoint = new XYZ(OnePoint.X, cADModel_Main_EndPoint.Y, OnePoint.Z);

        }
        else if ((OnePoint.Y - cADModel_Main_EndPoint.Y) < 0.001)
        {
          TwoPoint = new XYZ(OnePoint.X, cADModel_Main_StratPoint.Y, OnePoint.Z);
        }
        else
        {
          continue;
        }


        XYZ ChangeXYZ = new XYZ();

        var LineLength = (GetMiddlePoint(OnePoint, cADModel_Main_StratPoint)).DistanceTo(GetMiddlePoint(TwoPoint, cADModel_Main_EndPoint));
        if (LineLength < 0.00328)//樑的2段線起點非同一端。2段線非同一長度，又非同一端的，中間點的誤差選擇爲1mm
        {
          ChangeXYZ = TwoPoint;
          TwoPoint = OnePoint;
          OnePoint = ChangeXYZ;
        }
        Curve curve = Line.CreateBound((GetMiddlePoint(OnePoint, cADModel_Main_StratPoint)), GetMiddlePoint(TwoPoint, cADModel_Main_EndPoint));


        double distance = MainDistance_A;//cADModel_A.location.DistanceTo(cADModel_Main.location);

        distance = Math.Round(distance * 304.8, 1);//作爲樑_b的參數


        string beamName = "ZBIM矩形樑 " + (float)(distance) + "*" + (float)(600) + "mm";//類型名 寬度*高度
        if (!familSymbol_exists(beamName, "ZBIM - 矩形樑", doc))
        {
          MakeBeamType(beamName, "ZBIM - 矩形樑");
          EditBeamType(beamName, (float)(distance), (float)(600));
        }

        using (Transaction transaction = new Transaction(doc))
        {
          transaction.Start("Beadm Strart Bulid" + tranNumber.ToString());
          FilteredElementCollector collector = new FilteredElementCollector(doc);
          collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming);


          foreach (FamilySymbol beamType in collector)
          {
            if (beamType.Name == beamName)
            {
              if (!beamType.IsActive)
              {
                beamType.Activate();
              }
              FamilyInstance beamInstance = doc.Create.NewFamilyInstance(curve, beamType, level, StructuralType.Beam);
              var Elevation = beamInstance.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);

              break;
            }
          }

          transaction.Commit();
        }


      }
    }


    return Result.Succeeded;
  }

  /// <summary>
  /// 取得所有同一圖層的所有線
  /// </summary>
  /// <param name="doc">revit系統文檔</param>
  /// <param name="geoElem">幾何元素</param>
  /// <param name="graphicsStyleId">幾何元素ID</param>
  /// <returns></returns>
  private List<CADModel> getCurveArray(Document doc, GeometryElement geoElem, ElementId graphicsStyleId)
  {
    List<CADModel> curveArray_List = new List<CADModel>();
    TransactionGroup transGroup = new TransactionGroup(doc, "繪製模型線");
    transGroup.Start();



    //判斷元素類型
    foreach (var gObj in geoElem)
    {
      GeometryInstance geomInstance = gObj as GeometryInstance;
      //座標轉換。如果選擇的是“自動-中心到中心”，或者移動了importInstance，需要進行座標轉換
      Transform transform = geomInstance.Transform;


      if (null != geomInstance)
      {
        foreach (var insObj in geomInstance.SymbolGeometry)//取幾何得類別
        {
          if (insObj.GraphicsStyleId.IntegerValue != graphicsStyleId.IntegerValue)
            continue;


          if (insObj.GetType().ToString() == "Autodesk.Revit.DB.NurbSpline")
          {
            //不需要
          }
          if (insObj.GetType().ToString() == "Autodesk.Revit.DB.Line")
          {
            Line line = insObj as Line;
            XYZ normal = XYZ.BasisZ;
            XYZ point = line.GetEndPoint(0);
            point = transform.OfPoint(point);

            Line newLine = TransformLine(transform, line);

            CurveArray curveArray = new CurveArray();
            curveArray.Append(TransformLine(transform, line));

            XYZ startPoint = newLine.GetEndPoint(0);
            XYZ endPoint = newLine.GetEndPoint(1);
            XYZ MiddlePoint = GetMiddlePoint(startPoint, endPoint);
            double angle = (startPoint.Y - endPoint.Y) / startPoint.DistanceTo(endPoint);
            double rotation = Math.Asin(angle);

            CADModel cADModel = new CADModel();
            cADModel.curveArray = curveArray;
            cADModel.length = newLine.Length;
            cADModel.shape = "矩形樑";
            cADModel.width = 300 / 304.8;
            cADModel.location = MiddlePoint;
            cADModel.rotation = rotation;

            curveArray_List.Add(cADModel);
          }
          if (insObj.GetType().ToString() == "Autodesk.Revit.DB.Arc")
          {
            //不需要
          }
          //對於連續的折線
          if (insObj.GetType().ToString() == "Autodesk.Revit.DB.PolyLine")
          {

            PolyLine polyLine = insObj as PolyLine;
            IList<XYZ> points = polyLine.GetCoordinates();


            for (int i = 0; i < points.Count - 1; i++)
            {
              Line line = Line.CreateBound(points[i], points[i + 1]);
              line = TransformLine(transform, line);
              Line newLine = line;
              CurveArray curveArray = new CurveArray();
              curveArray.Append(newLine);

              XYZ startPoint = newLine.GetEndPoint(0);
              XYZ endPoint = newLine.GetEndPoint(1);
              XYZ MiddlePoint = GetMiddlePoint(startPoint, endPoint);
              double angle = (startPoint.Y - endPoint.Y) / startPoint.DistanceTo(endPoint);
              double rotation = Math.Asin(angle);

              CADModel cADModel = new CADModel();
              cADModel.curveArray = curveArray;
              cADModel.length = newLine.Length;
              cADModel.shape = "矩形樑";
              cADModel.width = 300 / 304.8;
              cADModel.location = MiddlePoint;
              cADModel.rotation = rotation;

              curveArray_List.Add(cADModel);

              //curveArray.Append(line);
            }


            //XYZ normal = XYZ.BasisZ;
            //XYZ point = points.First();
            //point = transform.OfPoint(point);


            //CreateModelCurveArray(curveArray, normal, point);
          }

        }
      }
    }

    transGroup.Assimilate();
    return curveArray_List;
  }

  /// <summary>
  /// 創建模型線組
  /// </summary>
  /// <param name="curveArray">曲線組</param>
  /// <param name="normal">法線</param>
  /// <param name="point">點</param>
  private void CreateModelCurveArray(CurveArray curveArray, XYZ normal, XYZ point)
  {
    if (curveArray.Size > 0)
    {
      Transaction transaction2 = new Transaction(doc);
      transaction2.Start("繪製模型線");
      try
      {
        SketchPlane modelSketch = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(normal, point));
        ModelCurveArray modelLine = doc.Create.NewModelCurveArray(curveArray, modelSketch);
      }
      catch
      {


      }
      transaction2.Commit();
      //curveArray.Clear();暫時不清除
    }
  }

  /// <summary>
  /// 翻轉指定線
  /// </summary>
  /// <param name="transform">矩陣</param>
  /// <param name="line">被翻轉的線</param>
  /// <returns></returns>
  private Line TransformLine(Transform transform, Line line)
  {
    XYZ startPoint = transform.OfPoint(line.GetEndPoint(0));
    XYZ endPoint = transform.OfPoint(line.GetEndPoint(1));
    Line newLine = Line.CreateBound(startPoint, endPoint);
    return newLine;
  }

  /// <summary>
  /// 查詢指定族二級名稱是否存在
  /// </summary>
  /// <param name="name">族類型二級名稱</param>
  /// <param name="familyName">族類型一級名稱</param>
  /// <param name="doc">revit文檔</param>
  /// <returns></returns>
  private bool familSymbol_exists(string name, string familyName, Document doc)
  {
    bool exists = false;

    FilteredElementCollector collector = new FilteredElementCollector(doc);
    collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming);
    foreach (FamilySymbol beamType in collector)
    {
      if (beamType.FamilyName == familyName)
      {
        if (beamType.Name == name)
        {
          exists = true;
          break;
        }
      }
    }
    return exists;
  }

  /// <summary>
  /// 創建新的樑類型
  /// </summary>
  /// <param name="name">族類型二級</param>
  /// <param name="familyname">族類型名稱</param>
  private void MakeBeamType(string name, string familyname)
  {
    //載入新的類型
    ChangeFamily(doc);

    FilteredElementCollector collector = new FilteredElementCollector(doc);
    collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming);
    Document familyDoc = null;
    foreach (FamilySymbol beamType in collector)
    {
      if (beamType.FamilyName == familyname)
      {
        familyDoc = doc.EditFamily(beamType.Family);
        break;
      }
    }

    FamilyManager familyManager = familyDoc.FamilyManager;
    Transaction trans = new Transaction(familyDoc, "UserMakeBeamType");
    trans.Start();
    FamilyType newFamilyType = familyManager.NewType(name);
    familyDoc.LoadFamily(doc, new MyFamilyLoadOptions());
    trans.Commit();
    familyDoc.Close(false);
    familyDoc.Dispose();
  }

  /// <summary>
  /// 編輯樑族類型，修改族類型二級名稱的參數
  /// </summary>
  /// <param name="name">族類型名字</param>
  /// <param name="b">寬度</param>
  /// <param name="h">高度</param>
  private void EditBeamType(string name, float b, float h)
  {
    FilteredElementCollector collector = new FilteredElementCollector(doc);
    collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming);
    Transaction trans = new Transaction(doc, "UserEditBeamType");
    trans.Start();
    foreach (FamilySymbol beamType in collector)
    {
      if (beamType.Name == name)
      {
        Parameter parab = beamType.LookupParameter("b");
        if (null != parab)
        {
          parab.Set(UnitUtils.ConvertToInternalUnits(b, DisplayUnitType.DUT_MILLIMETERS));
        }
        Parameter parah = beamType.LookupParameter("h");
        if (null != parah)
        {
          parah.Set(UnitUtils.ConvertToInternalUnits(h, DisplayUnitType.DUT_MILLIMETERS));
        }
      }
    }
    trans.Commit();
  }

  /// <summary>
  /// 取2點中間值
  /// </summary>
  /// <param name="startPoint">開始點</param>
  /// <param name="endPoint">結束點</param>
  /// <returns></returns>
  private XYZ GetMiddlePoint(XYZ startPoint, XYZ endPoint)
  {
    XYZ MiddlePoint = new XYZ((startPoint.X + endPoint.X) / 2, (startPoint.Y + endPoint.Y) / 2, (startPoint.Z + endPoint.Z) / 2);
    return MiddlePoint;
  }

  /// <summary>
  /// 獲取指定標高
  /// </summary>
  /// <param name="doc">系統文檔</param>
  /// <param name="name">標高名</param>
  /// <returns></returns>
  private Level LevelFilter(Document doc, string name = "標高 2")
  {
    FilteredElementCollector collector = new FilteredElementCollector(doc);
    ICollection<Element> collection = collector.OfClass(typeof(Level)).ToElements();
    Level level = null;
    foreach (Element element in collection)
    {
      Level level1 = element as Level;
      if (level1 != null && level1.Name == name)
      {
        level = level1;
      }
    }

    return level;
  }

  /// <summary>
  /// 獲取樑類型的名稱集合
  /// </summary>
  /// <param name="doc"></param>
  /// <returns></returns>
  private List<string> getBeamTypes(Document doc)
  {
    List<string> beamTypes = new List<string>();
    FilteredElementCollector collector = new FilteredElementCollector(doc);
    collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming);
    foreach (FamilySymbol beamType in collector)
    {
      beamTypes.Add(beamType.Name.ToString());
    }
    return beamTypes;
  }

  /// <summary>
  /// 重命名族名稱（一級），包括遇到沒有情況下的新的載入
  /// </summary>
  /// <param name="doc">revit文檔</param>
  private void ChangeFamily(Document doc)
  {
    Transaction changeFamily = new Transaction(doc, "ChangeFamily");
    bool type1 = false;//判斷是否存在族類型“ZBIM - 矩形樑”
    bool type2 = false;//判斷是否存在族類型“混凝土 - 矩形樑”
                       //bool type3 = false;
    changeFamily.Start();
    FilteredElementCollector collection_Origin = new FilteredElementCollector(doc);
    collection_Origin.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming);
    foreach (FamilySymbol familySymbol_beam in collection_Origin)//判斷是否存在“ZBIM - 矩形樑”
    {
      if (familySymbol_beam.Family.Name == "ZBIM - 矩形樑")
        type1 = true;

    }
    if (!type1)//如果不存在“ZBIM - 矩形樑”，判斷是否存在"混凝土 - 矩形樑",存在就將它的名稱改爲“ZBIM - 矩形樑”
    {
      foreach (FamilySymbol familySymbol_beam in collection_Origin)
      {
        if (familySymbol_beam.Family.Name == "混凝土 - 矩形樑")
        {
          familySymbol_beam.Family.Name = "ZBIM - 矩形樑";
          type1 = true;
          type2 = true;
        }
      }
    }


    changeFamily.Commit();
    //因爲原來有“混凝土 - 矩形樑”,你需要補上去。
    if (type2)
    {
      Transaction trans = new Transaction(doc, "loadfamily");
      trans.Start();
      doc.LoadFamily(@"C:\ProgramData\Autodesk\RVT 2018\Libraries\China\結構\框架\混凝土\混凝土 - 矩形樑.rfa");
      trans.Commit();
    }
    if (!type1 && !type2)//如果即不存在“ZBIM - 矩形樑”，也不存在“混凝土 - 矩形樑”,直接載入並改名
    {
      Transaction trans = new Transaction(doc, "loadfamilyBeam");
      trans.Start();
      doc.LoadFamily(@"C:\ProgramData\Autodesk\RVT 2018\Libraries\China\結構\框架\混凝土\混凝土 - 矩形樑.rfa");
      trans.Commit();


      FilteredElementCollector collector = new FilteredElementCollector(doc);
      collector.OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_StructuralFraming);


      Transaction tran = new Transaction(doc, "changedfamilyBeam");
      tran.Start();
      if (!type1)
      {
        foreach (FamilySymbol familySymbol_column in collector)
        {
          if (familySymbol_column.Family.Name == "混凝土 - 矩形樑")
            familySymbol_column.Family.Name = "ZBIM - 矩形樑";
        }

      }
      tran.Commit();

    }
    //doc.Regenerate();
  }

  /// <summary>
  /// 判斷2點是否重合
  /// </summary>
  /// <param name="A">A點</param>
  /// <param name="B">B點</param>
  /// <returns>結果</returns>
  private bool IsCoincide(XYZ A, XYZ B)
  {
    if (A.X == B.X && A.Y == B.Y && A.Z == B.Z)
    {
      return true;
    }
    else
    {
      return false;
    }
  }
}





上邊爲主程序，配套的其他類爲

using Autodesk.Revit.DB;

public class CADModel
{
  public CADModel()
  {
    curveArray = null;
    shape = "";
    length = 0;
    width = 0;
    familySymbol = "";
    location = new XYZ(0, 0, 0);
    rotation = 0;
  }
  /// <summary>
  /// 曲線陣列
  /// </summary>
  public CurveArray curveArray { get; set; }
  /// <summary>
  /// 形狀、模型
  /// </summary>
  public string shape { get; set; }
  /// <summary>
  /// 長度
  /// </summary>
  public double length { get; set; }
  /// <summary>
  /// 寬度
  /// </summary>
  public double width { get; set; }
  /// <summary>
  /// 族類型
  /// </summary>
  public string familySymbol { get; set; }
  /// <summary>
  /// 三維地址,直線取中點
  /// </summary>
  public XYZ location { get; set; }
  /// <summary>
  /// 水平角度,會出現負值，建議|X|
  /// </summary>
  public double rotation { get; set; }

  public static explicit operator CADModel(CurveArray v)
  {
    throw new NotImplementedException();
  }

}



還有

using Autodesk.Revit.DB;

 

public class MyFamilyLoadOptions : IFamilyLoadOptions
{
  public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
  {
    overwriteParameterValues = false;
    return true;
  }

  public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
  {
    source = FamilySource.Project;
    overwriteParameterValues = true;
    return true;
  }
}