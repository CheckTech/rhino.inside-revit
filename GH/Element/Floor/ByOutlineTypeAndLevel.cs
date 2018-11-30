using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Grasshopper.Kernel;

using Autodesk.Revit.DB;

namespace RhinoInside.Revit.GH.Components
{
  public class FloorByOutlineTypeAndLevel : GH_TransactionalComponent
  {
    public override Guid ComponentGuid => new Guid("DC8DAF4F-CC93-43E2-A871-3A01A920A722");
    public override GH_Exposure Exposure => GH_Exposure.primary;
    protected override System.Drawing.Bitmap Icon => ImageBuilder.BuildIcon("F");

    public FloorByOutlineTypeAndLevel() : base
    (
      "Floor.ByOutlineTypeAndLevel", "ByOutlineTypeAndLevel",
      "Create a Floor element from curves",
      "Revit", "Floor"
    )
    { }

    protected override void RegisterInputParams(GH_InputParamManager manager)
    {
      manager.AddCurveParameter("Boundary", "B", string.Empty, GH_ParamAccess.item);
      manager[manager.AddParameter(new Parameters.Element(), "FloorType", "FT", string.Empty, GH_ParamAccess.item)].Optional = true;
      manager[manager.AddParameter(new Parameters.Element(), "Level", "L", string.Empty, GH_ParamAccess.item)].Optional = true;
      manager.AddBooleanParameter("Structural", "S", string.Empty, GH_ParamAccess.item, true);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager manager)
    {
      manager.AddParameter(new Parameters.Element(), "Floor", "F", "New Floor", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
      Rhino.Geometry.Curve boundary = null;
      DA.GetData("Boundary", ref boundary);

      Autodesk.Revit.DB.FloorType floorType = null;
      if (!DA.GetData("FloorType", ref floorType) && Params.Input[1].Sources.Count == 0)
        floorType = Revit.ActiveDBDocument.GetElement(Revit.ActiveDBDocument.GetDefaultElementTypeId(ElementTypeGroup.FloorType)) as FloorType;

      Autodesk.Revit.DB.Level level = null;
      DA.GetData("Level", ref level);

      bool structural = true;
      DA.GetData("Structural", ref structural);

      DA.DisableGapLogic();
      int Iteration = DA.Iteration;
      Revit.EnqueueAction((doc) => CommitInstance(doc, DA, Iteration, boundary, floorType, level, structural));
    }

    void CommitInstance
    (
      Document doc, IGH_DataAccess DA, int Iteration,
      Rhino.Geometry.Curve boundary,
      Autodesk.Revit.DB.FloorType floorType,
      Autodesk.Revit.DB.Level level,
      bool structural
    )
    {
      Floor floor = null;
      try
      {
        if
        (
          boundary == null ||
          boundary.IsShort(Revit.ShortCurveTolerance) ||
          !boundary.IsClosed ||
          !boundary.TryGetPlane(out var boundaryPlane, Revit.VertexTolerance) ||
          boundaryPlane.ZAxis.IsParallelTo(Rhino.Geometry.Vector3d.ZAxis) == 0
        )
        {
          AddRuntimeMessage(GH_RuntimeMessageLevel.Error, string.Format("Parameter '{0}' must be an horizontal planar closed curve.", Params.Input[0].Name));
        }
        else
        {
          var curveArray = new CurveArray();
          foreach (var curve in boundary.ToHost())
            curveArray.Append(curve);

          if (floorType.IsFoundationSlab)
          {
            floor = doc.Create.NewFoundationSlab(curveArray, floorType, level, structural, XYZ.BasisZ);
          }
          else
          {
            floor = doc.Create.NewFloor(curveArray, floorType, level, structural, XYZ.BasisZ);
          }
        }
      }
      catch (Exception e)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
      }
      finally
      {
        ReplaceElement(doc, DA, Iteration, floor);
      }
    }
  }
}
