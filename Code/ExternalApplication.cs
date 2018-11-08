using System;
using System;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Structure;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Code
{

  
  class ExternalApplication : IExternalApplication
  {
   
    public Result OnShutdown(UIControlledApplication application)
    {
      return Result.Succeeded;
    }

    public Result OnStartup(UIControlledApplication application)
    {
      application.CreateRibbonTab("MYAPP");
      RibbonPanel myPanel = application.CreateRibbonPanel("MYAPP", "ARC");
      RibbonPanel myPanel2 = application.CreateRibbonPanel("MYAPP", "NEEDS");

      string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
      PushButtonData pushButtonData = new PushButtonData("CreateColumn", "结构柱",thisAssemblyPath, "Code.CreateColumn");
      PushButtonData pushButtonData2 = new PushButtonData("PipeCut", "墙套管剪切",thisAssemblyPath, "Code.WallCutPipe");
      PushButton pushButton = myPanel.AddItem(pushButtonData) as PushButton;
      PushButton pushButton2 = myPanel.AddItem(pushButtonData2) as PushButton;
      pushButton.ToolTip = "框选详图线创建结构柱。";

      Uri uriImage = new Uri(@"G:\Resource\01icon\_1.png");
      Uri uriImage2 = new Uri(@"G:\Resource\01icon\_2.png");
      BitmapImage largeImage = new BitmapImage(uriImage);
      BitmapImage largeImage2 = new BitmapImage(uriImage2);
      pushButton.LargeImage = largeImage;
      pushButton2.LargeImage = largeImage2;

      return Result.Succeeded;
    }

    //private PushButton CreatePushButton(RibbonPanel panel,string name,string text)
    //{
    //  if (null == panel)
    //  {
    //    return null;
    //  }
    //  PushButtonData pushButtonData = CreatePushButtonData(name, text);
    //  PushButton pushButton = panel.AddItem(pushButtonData) as PushButton;
    //  pushButton.LargeImage = LoadImage(name);
    //  return pushButton;
    //}
  }

}
