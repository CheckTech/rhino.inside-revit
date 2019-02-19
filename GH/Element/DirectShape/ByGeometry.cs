using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using Autodesk.Revit.DB;
using Grasshopper.Kernel.Special;

namespace RhinoInside.Revit.GH.Components
{
  public class DirectShapeByGeometry : GH_TransactionalComponentItem
  {
    public override Guid ComponentGuid => new Guid("0bfbda45-49cc-4ac6-8d6d-ecd2cfed062a");
    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override System.Drawing.Bitmap Icon => ImageBuilder.BuildIcon("DS");

    public DirectShapeByGeometry() : base
    (
      "DirectShape.ByGeometry", "ByGeometry",
      "Create a DirectShape element from geometry",
      "Revit", "Model"
    )
    { }

    protected override void RegisterInputParams(GH_InputParamManager manager)
    {
      manager.AddGeometryParameter("Geometry", "G", string.Empty, GH_ParamAccess.list);
      manager[manager.AddParameter(new Parameters.Category(), "Category", "C", string.Empty, GH_ParamAccess.item)].Optional = true;
      manager[manager.AddTextParameter("Name", "N", string.Empty, GH_ParamAccess.item)].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager manager)
    {
      manager.AddParameter(new Parameters.Element(), "DirectShape", "DS", "New DirectShape", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      var geometry = new List<IGH_GeometricGoo>();
      DA.GetDataList("Geometry", geometry);

      Autodesk.Revit.DB.Category category = null;
      if (!DA.GetData("Category", ref category) && Params.Input[1].Sources.Count == 0)
        category = Autodesk.Revit.DB.Category.GetCategory(Revit.ActiveDBDocument, BuiltInCategory.OST_GenericModel);

      string name = null;
      if (!DA.GetData("Name", ref name) && geometry.Count == 1 && (geometry[0]?.IsReferencedGeometry ?? false))
        name = Rhino.RhinoDoc.ActiveDoc.Objects.FindId(geometry[0].ReferenceID)?.Name;

      DA.DisableGapLogic();
      int Iteration = DA.Iteration;
      Revit.EnqueueAction((doc) => CommitInstance(doc, DA, Iteration, geometry, category, name));
    }

    Rhino.Geometry.GeometryBase AsGeometryBase(IGH_GeometricGoo obj)
    {
      var scriptVariable = obj.ScriptVariable();
      switch (scriptVariable)
      {
        case Rhino.Geometry.Point3d g0: return new Rhino.Geometry.Point(g0);
        case Rhino.Geometry.Line    g1: return new Rhino.Geometry.LineCurve(g1);
        case Rhino.Geometry.Plane   g2: return new Rhino.Geometry.PlaneSurface(g2, new Rhino.Geometry.Interval(0.0, g2.XAxis.Length), new Rhino.Geometry.Interval(0.0, g2.YAxis.Length));
      }

      return scriptVariable as Rhino.Geometry.GeometryBase;
    }

    void CommitInstance
    (
      Document doc, IGH_DataAccess DA, int Iteration,
      IEnumerable<IGH_GeometricGoo> geometries,
      Autodesk.Revit.DB.Category category,
      string name
    )
    {
      var element = PreviousElement(doc, Iteration);
      try
      {
        if (element?.Pinned ?? true)
        {
          if (geometries != null)
          {
            if (category == null || !DirectShape.IsValidCategoryId(category.Id, doc))
            {
              AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, string.Format("Parameter '{0}' is not valid for DirectShape.", Params.Input[1].Name));
              category = Autodesk.Revit.DB.Category.GetCategory(doc, BuiltInCategory.OST_GenericModel);
            }

            var shape = new List<GeometryObject>();

            foreach (var geometry in geometries.Select((x) => AsGeometryBase(x)).ToHost())
            {
              // DirectShape only accepts those types and no nulls
              foreach (var g in geometry)
              {
                switch (g)
                {
                  case Point p: shape.Add(p); break;
                  case Curve c: shape.Add(c); break;
                  case Solid s: shape.Add(s); break;
                  case Mesh m: shape.Add(m); break;
                }
              }
            }

            if (shape.Count > 0)
            {
              var ds = Autodesk.Revit.DB.DirectShape.CreateElement(doc, category.Id);
              ds.SetShape(shape);
              ds.Name = name ?? string.Empty;
              element = ds;
            }
          }
        }
      }
      catch (Exception e)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
      }
      finally
      {
        ReplaceElement(doc, DA, Iteration, element);
      }
    }
  }

  public class DirectShapeCategories : GH_ValueList
  {
    public override Guid ComponentGuid => new Guid("7BAFE137-332B-481A-BE22-09E8BD4C86FC");
    public override GH_Exposure Exposure => GH_Exposure.secondary;
    protected override System.Drawing.Bitmap Icon => ImageBuilder.BuildIcon("DSC");

    public DirectShapeCategories()
    {
      Category = "Revit";
      SubCategory = "Model";
      Name = "DirectShape.Categories";
      NickName = "Categories";
      Description = "Provide a picker of a valid DirectShape category";

      ListItems.Clear();

      var categories = new List<Category>();

      if (Revit.ActiveDBDocument != null)
      {
        foreach (var item in Revit.ActiveDBDocument.Settings.Categories)
        {
          if (item is Category category)
          {
            if (!DirectShape.IsValidCategoryId(category.Id, Revit.ActiveDBDocument))
              continue;

            categories.Add(category);
          }
        }

        categories = categories.OrderBy(c => c.Name).ToList();

        var genericModel = Autodesk.Revit.DB.Category.GetCategory(Revit.ActiveDBDocument, BuiltInCategory.OST_GenericModel);
        foreach (var category in categories)
        {
          ListItems.Add(new GH_ValueListItem(category.Name, category.Id.IntegerValue.ToString()));
          if (category.Id.IntegerValue == (int) BuiltInCategory.OST_GenericModel)
            SelectItem(ListItems.Count - 1);
        }
      }
    }
  }
}
