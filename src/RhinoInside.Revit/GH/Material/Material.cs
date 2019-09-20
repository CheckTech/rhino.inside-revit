using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace RhinoInside.Revit.GH.Types
{
  public class Material : Element
  {
    public override string TypeName => "Revit material";
    public override string TypeDescription => "Represents a Revit material";
    protected override Type ScriptVariableType => typeof(Autodesk.Revit.DB.Material);
    public static explicit operator Autodesk.Revit.DB.Material(Material self) => Revit.ActiveDBDocument?.GetElement(self) as Autodesk.Revit.DB.Material;

    public Material() { }
    public Material(Autodesk.Revit.DB.Material material) : base(material) { }
  }
}

namespace RhinoInside.Revit.GH.Parameters
{
  public class Material : ElementIdNonGeometryParam<Types.Material>
  {
    public override GH_Exposure Exposure => GH_Exposure.tertiary;
    public override Guid ComponentGuid => new Guid("B18EF2CC-2E67-4A5E-9241-9010FB7D27CE");

    public Material() : base("Material", "Material", "Represents a Revit document material.", "Params", "Revit") { }
  }
}
