#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
//using WinForms = System.Windows.Forms;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Collections;
using Autodesk.Revit.DB.Events;
#endregion

namespace Code
{
  class Utils
  {
    //响应处理函数
    public void application_DocumentOpened(object sender,DocumentOpenedEventArgs args)
    {
      //get document object from Event
      Document doc = args.Document;
      using (Transaction trans = new Transaction(doc))
      {
        trans.Start("Edit Address");
        TaskDialog.Show("Infomation", "Event is working");
        //to do...
        trans.Commit();
      }
    }
    public Result OnStarup(UIControlledApplication application)
    {
      try
      {
        application.ControlledApplication.DocumentOpened += new EventHandler<DocumentOpenedEventArgs>(application_DocumentOpened);
      }
      catch(Exception)
      {
        return Result.Failed;
      }
      return Result.Succeeded;
    }

    public Result Onshutdown(UIControlledApplication application)
    {
      application.ControlledApplication.DocumentOpened -= application_DocumentOpened;
      return Result.Succeeded;
    }
    
  }
}


