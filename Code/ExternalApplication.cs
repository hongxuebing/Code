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
      RibbonPanel myPanel = application.CreateRibbonPanel("MYAPP", "放柱");
      string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
      PushButtonData pushButtonData = new PushButtonData("innamecreateColumn", "放柱",thisAssemblyPath, "Code.CreateColumn");
      PushButton pushButton = myPanel.AddItem(pushButtonData) as PushButton;
      pushButton.ToolTip = "Create Column.";

      Uri uriImage = new Uri(@"C:\Users\xuebing\source\repos\HelloWorld\HelloWorld\bin\Debug\1-GlobeA_32x32.png");
      BitmapImage largeImage = new BitmapImage(uriImage);
      pushButton.LargeImage = largeImage;
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
