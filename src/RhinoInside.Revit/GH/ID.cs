using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

namespace RhinoInside.Revit.GH.Types
{
  public interface IGH_ElementId : IGH_Goo
  {
    ElementId Id { get; }
    bool IsReferencedElement { get; }
    string UniqueID { get; }
    bool IsElementLoaded { get; }
    bool LoadElement(Document doc);
    void UnloadElement();
  }

  public abstract class ID : GH_Goo<ElementId>, IEquatable<ID>, IGH_ElementId
  {
    public override string TypeName => "Revit Model Object";
    public override string TypeDescription => "Represents a Revit model object";
    public override bool IsValid => !(Value is null || Value == ElementId.InvalidElementId);
    public override sealed IGH_Goo Duplicate() => (IGH_Goo) MemberwiseClone();
    protected virtual Type ScriptVariableType => typeof(Autodesk.Revit.DB.ElementId);
    public static implicit operator ElementId(ID self) { return self.Value; }

    public static ID Make(ElementId Id)
    {
      if (Id == ElementId.InvalidElementId)
        return null;

      if (Id.IsCategoryId(Revit.ActiveDBDocument))
        return Category.Make(Id);

      return Element.Make(Id);
    }

    public static bool TryParseUniqueID(string s, out Guid guid, out int id)
    {
      guid = Guid.Empty;
      id = -1;
      if (s.Length == 0)
        return true;

      var index = s.LastIndexOf('-');
      if (index < 0)
        return false;

      return Guid.TryParseExact(s.Substring(0, index), "D", out guid) &&
             int.TryParse(s.Substring(index + 1), System.Globalization.NumberStyles.HexNumber, null, out id);
    }
    public static string FormatUniqueID(Guid guid, int index) => $"{guid:D}-{index,8:x}";

    public void SetValue(Document doc, ElementId id)
    {
      Value = id;
      UniqueID = doc?.GetElement(id)?.UniqueId ??
                 (
                   id.IntegerValue < ElementId.InvalidElementId.IntegerValue ?
                     FormatUniqueID(Guid.Empty, id.IntegerValue) : 
                     string.Empty
                 );
    }
    public void SetValue(Autodesk.Revit.DB.Element element)
    {
      Value = element?.Id;
      UniqueID = element?.UniqueId;
    }

    #region IGH_ElementId
    public ElementId Id => Value;
    public bool IsReferencedElement => !string.IsNullOrEmpty(UniqueID);
    public string UniqueID { get; private set; }
    public bool IsElementLoaded => !(Value is null);
    public virtual bool LoadElement(Document doc)
    {
      try { Value = doc?.GetElement(UniqueID)?.Id; }
      catch (ArgumentNullException) { Value = null; }

      return IsElementLoaded;
    }
    public void UnloadElement() => Value = null;
    #endregion

    public ID() { Value = ElementId.InvalidElementId; UniqueID = string.Empty; }
    protected ID(ElementId id, string uniqueId) { Value = id; UniqueID = uniqueId; }
    protected ID(ElementId id) => SetValue(Revit.ActiveDBDocument, id);

    public override bool CastFrom(object source)
    {
      if (source is GH_Integer integer)
      {
        Value = new ElementId(integer.Value);
        UniqueID = string.Empty;
        return true;
      }
      if (source is ElementId id)
      {
        Value = id;
        UniqueID = string.Empty;
        return true;
      }

      return false;
    }

    public override bool CastTo<Q>(ref Q target)
    {
      if (typeof(Q).IsAssignableFrom(typeof(Autodesk.Revit.DB.ElementId)))
      {
        target = (Q) (object) Value;
        return true;
      }
      if(typeof(Q).IsAssignableFrom(typeof(GH_Integer)))
      {
        target = (Q) (object) new GH_Integer(Value.IntegerValue);
        return true;
      }
      if(typeof(Q).IsAssignableFrom(typeof(GH_String)))
      {
        target = (Q) (object) new GH_String(UniqueID);
        return true;
      }

      return base.CastTo<Q>(ref target);
    }

    public bool Equals(ID id) => id?.Value.IntegerValue == Value.IntegerValue && id?.UniqueID == UniqueID;
    public override bool Equals(object obj) => (obj is ID id) ? Equals(id) : base.Equals(obj);
    public override int GetHashCode() => Value?.IntegerValue ?? 0;

    public override string ToString()
    {
      if (!IsValid)
        return $"Null {TypeName}";

      return $"{TypeName} : id {Value.IntegerValue}";
    }

    public override sealed bool Read(GH_IReader reader)
    {
      Value = null;
      UniqueID = reader.GetString("UniqueID");
      return true;
    }

    public override sealed bool Write(GH_IWriter writer)
    {
      writer.SetString("UniqueID", UniqueID);
      return true;
    }
  }
}

namespace RhinoInside.Revit.GH.Parameters
{
  public abstract class GH_PersistentParam<T>: Grasshopper.Kernel.GH_PersistentParam<T> where T : class, IGH_Goo
  {
    protected override sealed Bitmap Icon => ((Bitmap) Properties.Resources.ResourceManager.GetObject(GetType().Name)) ??
                                      ImageBuilder.BuildIcon(IconTag);

    protected virtual string IconTag => GetType().Name.Substring(0, 1);

    protected GH_PersistentParam(string name, string nickname, string description, string category, string subcategory) :
      base(name, nickname, description, category, subcategory) { }

    protected override void PrepareForPrompt() { }
    protected override void RecoverFromPrompt() { }

    protected override GH_GetterResult Prompt_Plural(ref List<T> values) => GH_GetterResult.cancel;
    protected override GH_GetterResult Prompt_Singular(ref T value) => GH_GetterResult.cancel;
  }

  public interface IGH_ElementIdParam : IGH_Param
  {
    bool NeedsToBeExpired(Document doc, ICollection<ElementId> added, ICollection<ElementId> deleted, ICollection<ElementId> modified);
  }

  public abstract class ElementIdParam<T> : GH_PersistentParam<T>, IGH_ElementIdParam
    where T : class, Types.IGH_ElementId
  {
    protected ElementIdParam(string name, string nickname, string description, string category, string subcategory) :
      base(name, nickname, description, category, subcategory)
    { }

    public override void ClearData()
    {
      base.ClearData();

      if (PersistentDataCount == 0)
        return;

      foreach (var goo in PersistentData.OfType<T>())
        goo?.UnloadElement();
    }

    protected override void OnVolatileDataCollected()
    {
      if (SourceCount == 0)
      {
        foreach (var branch in m_data.Branches)
        {
          for (int i = 0; i < branch.Count; i++)
          {
            var item = branch[i];
            if (item?.IsReferencedElement ?? false)
            {
              if (!item.LoadElement(Revit.ActiveDBDocument))
              {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"A referenced {item.TypeName} could not be found in the Revit document.");
                branch[i] = null;
              }
            }
          }
        }
      }

      base.OnVolatileDataCollected();
    }

    protected void Menu_Connect(object sender, EventArgs e)
    {
      if (sender is ToolStripMenuItem item && item.Tag is Guid componentGuid)
      {
        var obj = this.ConnectNewObject(componentGuid);
        if (obj is null)
          return;

        obj.ExpireSolution(true);
      }
    }

    #region IGH_ElementIdParam
    bool IGH_ElementIdParam.NeedsToBeExpired(Document doc, ICollection<ElementId> added, ICollection<ElementId> deleted, ICollection<ElementId> modified)
    {
      if (DataType == GH_ParamData.remote)
        return false;

      foreach (var data in VolatileData.AllData(true).OfType<Types.IGH_ElementId>())
      {
        if (!data.IsElementLoaded)
          continue;

        if (modified.Contains(data.Id))
          return true;

        if (deleted.Contains(data.Id))
          return true;
      }

      return false;
    }
    #endregion
  }

  public abstract class ElementIdNonGeometryParam<T> : ElementIdParam<T>
    where T : class, Types.IGH_ElementId
  {
    protected ElementIdNonGeometryParam(string name, string nickname, string description, string category, string subcategory) :
      base(name, nickname, description, category, subcategory)
    { }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
      base.AppendAdditionalMenuItems(menu);
      this.Menu_AppendConnect(menu, Menu_Connect);
    }

    protected override void Menu_AppendPromptOne(ToolStripDropDown menu) { }
    protected override void Menu_AppendPromptMore(ToolStripDropDown menu) { }
  }
}

namespace RhinoInside.Revit.GH.Components
{
  public interface IGH_ElementIdComponent : IGH_Component
  {
    bool NeedsToBeExpired(Autodesk.Revit.DB.Events.DocumentChangedEventArgs args);
  }

  public abstract class GH_Component : Grasshopper.Kernel.GH_Component
  {
    protected GH_Component(string name, string nickname, string description, string category, string subCategory)
    : base(name, nickname, description, category, subCategory) { }

    // Grasshopper default implementation has a bug, it checks inputs instead of outputs
    public override bool IsBakeCapable => Params?.Output.OfType<IGH_BakeAwareObject>().Where(x => x.IsBakeCapable).Any() ?? false;

    protected override Bitmap Icon => ((Bitmap) Properties.Resources.ResourceManager.GetObject(GetType().Name)) ??
                                      ImageBuilder.BuildIcon(IconTag);

    protected virtual string IconTag => GetType().Name.Substring(0, 1);
  }

  public abstract class Component : GH_Component, IGH_ElementIdComponent
  {
    protected Component(string name, string nickname, string description, string category, string subCategory)
    : base(name, nickname, description, category, subCategory) { }

    protected virtual ElementFilter ElementFilter { get; }
    public virtual bool NeedsToBeExpired(Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
    {
      var persistentInputs = Params.Input.
        Where(x => x.DataType == GH_ParamData.local && x.Phase != GH_SolutionPhase.Blank).
        OfType<Parameters.IGH_ElementIdParam>();

      if (persistentInputs.Any())
      {
        var filter = ElementFilter;

        var modified = filter is null ? e.GetModifiedElementIds() : e.GetModifiedElementIds(filter);
        var deleted = e.GetDeletedElementIds();

        if (modified.Count > 0 || deleted.Count > 0)
        {
          var document = e.GetDocument();
          var empty = new ElementId[0];

          foreach (var param in persistentInputs)
          {
            if (param.NeedsToBeExpired(document, empty, deleted, modified))
              return true;
          }
        }
      }

      return false;
    }
  }
}
