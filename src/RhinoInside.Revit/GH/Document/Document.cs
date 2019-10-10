using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;

namespace RhinoInside.Revit.GH.Parameters
{
  public abstract class DocumentPicker : GH_ValueList, IGH_ElementIdParam
  {
    #region IGH_ElementIdParam
    protected virtual DB.ElementFilter ElementFilter => null;
    public virtual bool PassesFilter(DB.Document document, Autodesk.Revit.DB.ElementId id)
    {
      return ElementFilter?.PassesFilter(document, id) ?? true;
    }

    bool IGH_ElementIdParam.NeedsToBeExpired
    (
      DB.Document doc,
      ICollection<DB.ElementId> added,
      ICollection<DB.ElementId> deleted,
      ICollection<DB.ElementId> modified
    )
    {
      // If anything of that type is added we need to update ListItems
      if (added.Where(id => PassesFilter(doc, id)).Any())
        return true;

      // If selected items are modified we need to expire dependant components
      foreach (var data in VolatileData.AllData(true).OfType<Types.IGH_ElementId>())
      {
        if (!data.IsElementLoaded)
          continue;

        if (modified.Contains(data.Id))
          return true;
      }

      // If an item in ListItems is deleted we need to update ListItems
      foreach (var item in ListItems.Select(x => x.Value).OfType<Grasshopper.Kernel.Types.GH_Integer>())
      {
        var id = new DB.ElementId(item.Value);

        if (deleted.Contains(id))
          return true;
      }

      return false;
    }
    #endregion
  }
}

namespace RhinoInside.Revit.GH.Components
{
  public abstract class DocumentComponent : Component
  {
    protected DocumentComponent(string name, string nickname, string description, string category, string subCategory)
    : base(name, nickname, description, category, subCategory) { }

    public override bool NeedsToBeExpired(Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
    {
      var elementFilter = ElementFilter;
      var filters = Params.Input.Count > 0 ?
                    Params.Input[0].VolatileData.AllData(true).OfType<Types.ElementFilter>().Select(x => new DB.LogicalAndFilter(x.Value, elementFilter)) :
                    Enumerable.Empty<DB.ElementFilter>();

      foreach (var filter in filters.Any() ? filters : Enumerable.Repeat(elementFilter, 1))
      {
        var added = filter is null ? e.GetAddedElementIds() : e.GetAddedElementIds(filter);
        if (added.Count > 0)
          return true;

        var modified = filter is null ? e.GetModifiedElementIds() : e.GetModifiedElementIds(filter);
        if (modified.Count > 0)
          return true;

        var deleted = e.GetDeletedElementIds();
        if (deleted.Count > 0)
        {
          var document = e.GetDocument();
          var empty = new DB.ElementId[0];
          foreach (var param in Params.Output.OfType<Parameters.IGH_ElementIdParam>())
          {
            if (param.NeedsToBeExpired(document, empty, deleted, empty))
              return true;
          }
        }
      }

      return false;
    }
  }
}
