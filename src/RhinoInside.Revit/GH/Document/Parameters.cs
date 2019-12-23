using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

using Autodesk.Revit.DB;

namespace RhinoInside.Revit.GH.Components
{
  public class DocumentParameters : DocumentComponent
  {
    public override Guid ComponentGuid => new Guid("189F0A94-D077-4B96-8A92-6D5334EF7157");
    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override ElementFilter ElementFilter => new Autodesk.Revit.DB.ElementClassFilter(typeof(ParameterElement));

    public DocumentParameters() : base
    (
      "Document.Parameters", "Parameters",
      "Gets a list of valid parameters for the specified category that can be used in a table view",
      "Revit", "Document"
    )
    {
    }

    protected override void RegisterInputParams(GH_InputParamManager manager)
    {
      manager.AddParameter(new Parameters.Category(), "Category", "C", "Category", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager manager)
    {
      manager.AddParameter(new Parameters.ParameterKey(), "ParameterKeys", "K", "Parameter definitions list", GH_ParamAccess.list);
    }

    protected override void TrySolveInstance(IGH_DataAccess DA)
    {
      var category = default(Category);
      if (!DA.GetData("Category", ref category))
        return;

      var doc = category.Document();
      if(doc is object)
      {
        var parameterKeys = TableView.GetAvailableParameters(doc, category.Id);
        DA.SetDataList("ParameterKeys", parameterKeys.Select(paramId => Types.ParameterKey.FromElementId(doc, paramId)));
      }
    }
  }
}
