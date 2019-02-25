using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

using Autodesk;
using Autodesk.Revit;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.ExternalService;

using Rhino;
using Rhino.Runtime.InProcess;
using Rhino.PlugIns;

using Grasshopper;
using Grasshopper.Kernel;

namespace RhinoInside.Revit
{
  [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
  [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
  [Autodesk.Revit.Attributes.Journaling(Autodesk.Revit.Attributes.JournalingMode.NoCommandData)]
  public partial class Revit : IExternalApplication
  {
    #region Revit static constructor
    static Revit()
    {
      ResolveEventHandler OnRhinoCommonResolve = null;
      AppDomain.CurrentDomain.AssemblyResolve += OnRhinoCommonResolve = (sender, args) =>
      {
        const string rhinoCommonAssemblyName = "RhinoCommon";
        var assemblyName = new AssemblyName(args.Name).Name;

        if (assemblyName != rhinoCommonAssemblyName)
          return null;

        AppDomain.CurrentDomain.AssemblyResolve -= OnRhinoCommonResolve;

        var rhinoSystemDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Rhino WIP", "System");
        return Assembly.LoadFrom(Path.Combine(rhinoSystemDir, rhinoCommonAssemblyName + ".dll"));
      };
    }
    #endregion

    #region IExternalApplication Members
    RhinoCore rhinoCore;
    GH.PreviewServer grasshopperPreviewServer;

    public Result OnStartup(UIControlledApplication applicationUI)
    {
      ApplicationUI = applicationUI;

#if REVIT_2019
      MainWindowHandle = ApplicationUI.MainWindowHandle;
#else
      MainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
#endif

      // Load Rhino
      try
      {
        var schemeName = ApplicationUI.ControlledApplication.VersionName.Replace(' ', '-');
        rhinoCore = new RhinoCore(new string[] { $"/scheme={schemeName}", "/nosplash" }, WindowStyle.Hidden, MainWindowHandle);
      }
      catch (Exception e)
      {
        Debug.Fail(e.Source, e.Message);
        return Result.Failed;
      }

      // Reset document units
      UI.RhinoCommand.ResetDocumentUnits(Rhino.RhinoDoc.ActiveDoc);

      // Register UI on Revit
      {
        var ribbonPanel = ApplicationUI.CreateRibbonPanel("Rhinoceros");

        UI.RhinoCommand.CreateUI(ribbonPanel);
        UI.GrasshopperCommand.CreateUI(ribbonPanel);
        ribbonPanel.AddSeparator();
        Sample1.CreateUI(ribbonPanel);
        Sample4.CreateUI(ribbonPanel);
        Sample6.CreateUI(ribbonPanel);
        ribbonPanel.AddSeparator();
        UI.APIDocsCommand.CreateUI(ribbonPanel);
      }

      // Register some events
      ApplicationUI.Idling += OnIdle;
      ApplicationUI.ControlledApplication.DocumentChanged += OnDocumentChanged;

      // Register GrasshopperPreviewServer
      grasshopperPreviewServer = new GH.PreviewServer();
      grasshopperPreviewServer.Register();

      return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication applicationUI)
    {
      // Unregister GrasshopperPreviewServer
      grasshopperPreviewServer?.Unregister();
      grasshopperPreviewServer = null;

      // Unregister some events
      ApplicationUI.ControlledApplication.DocumentChanged -= OnDocumentChanged;
      ApplicationUI.Idling -= OnIdle;

      // Unload Rhino
      try
      {
        rhinoCore.Dispose();
      }
      catch (Exception e)
      {
        Debug.Fail(e.Source, e.Message);
        return Result.Failed;
      }

      ApplicationUI = null;
      return Result.Succeeded;
    }

    static bool pendingRefreshActiveView = false;
    public static void RefreshActiveView() { pendingRefreshActiveView = true; }

    public static bool Committing = false;
    static bool LoadGrasshopperComponents()
    {
      var LoadGHAProc = Instances.ComponentServer.GetType().GetMethod("LoadGHA", BindingFlags.NonPublic | BindingFlags.Instance);
      if (LoadGHAProc == null)
        return false;

      var bCoff = Instances.Settings.GetValue("Assemblies:COFF", true);
      Instances.Settings.SetValue("Assemblies:COFF", false);

      var rc = (bool) LoadGHAProc.Invoke
      (
        Instances.ComponentServer,
        new object[] { new GH_ExternalFile(Assembly.GetExecutingAssembly().Location), false }
      );

      Instances.Settings.SetValue("Assemblies:COFF", bCoff);

      if (rc)
        GH_ComponentServer.UpdateRibbonUI();

      return rc;
    }

    static bool LoadedAsGHA = false;
    void OnIdle(object sender, IdlingEventArgs args)
    {
      // 1. Do Rhino pending OnIdle tasks
      if (rhinoCore.OnIdle())
      {
        args.SetRaiseWithoutDelay();
        return;
      }

      // Load this assembly as a Grasshopper assembly
      if (!LoadedAsGHA && PlugIn.GetPlugInInfo(new Guid(0xB45A29B1, 0x4343, 0x4035, 0x98, 0x9E, 0x04, 0x4E, 0x85, 0x80, 0xD9, 0xCF)).IsLoaded)
        LoadedAsGHA = LoadGrasshopperComponents();

      // Document dependant tasks need a document
      ActiveUIApplication = (sender as UIApplication);
      if (ActiveDBDocument != null)
      {
        // 1. Do all document read actions
        if (ProcessReadActions())
        {
          args.SetRaiseWithoutDelay();
          return;
        }

        // 2. Do all document write actions
        if (!ActiveDBDocument.IsReadOnly)
          ProcessWriteActions();

        // 3. Refresh Active View if necesary
        if (pendingRefreshActiveView || GH.PreviewServer.PreviewChanged())
        {
          pendingRefreshActiveView = false;
          ActiveUIApplication.ActiveUIDocument.RefreshActiveView();
        }
      }
    }

    private void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
    {
      if (Committing)
        return;

      var document = e.GetDocument();
      if (!document.Equals(ActiveDBDocument))
        return;

      ProcessReadActions(true);

      var materialsChanged = e.GetModifiedElementIds().Select((x) => document.GetElement(x)).OfType<Material>().Any();

      foreach (GH_Document definition in Grasshopper.Instances.DocumentServer)
      {
        foreach (var obj in definition.Objects)
        {
          if (obj is GH.Parameters.Element element)
          {
            if (element.SourceCount > 0)
              continue;

            if (element.Phase == GH_SolutionPhase.Blank)
              continue;

            element.ExpireSolution(false);
          }
          else if (obj is GH_Component component)
          {
            if (component is GH.Components.DocumentElements)
            {
              component.ExpireSolution(false);
            }
            else foreach (var param in component.Params.Output)
            {
              if (param is GH.Parameters.Element outElement)
              {
                if (materialsChanged)
                {
                  foreach (var goo in param.VolatileData.AllData(true))
                  {
                    if (goo is IGH_PreviewMeshData previewMeshData)
                      previewMeshData.DestroyPreviewMeshes();
                  }
                }

                foreach (var r in param.Recipients)
                  r.ExpireSolution(false);
              }
            }
          }
        }

        if (definition.Enabled)
          definition.NewSolution(false);
      }
    }
    #endregion

    #region Bake Recipe
    public static void BakeGeometry(IEnumerable<Rhino.Geometry.GeometryBase> geometries, BuiltInCategory categoryToBakeInto = BuiltInCategory.OST_GenericModel)
    {
      if (categoryToBakeInto == BuiltInCategory.INVALID)
        return;

      EnqueueAction
      (
        (doc) =>
        {
          foreach (var geometryToBake in geometries.ToHost())
          {
            if (geometryToBake == null)
              continue;

            BakeGeometry(doc, geometryToBake, categoryToBakeInto);
          }
        }
      );
    }

    static partial void TraceGeometry(IEnumerable<Rhino.Geometry.GeometryBase> geometries);
#if DEBUG
    static partial void TraceGeometry(IEnumerable<Rhino.Geometry.GeometryBase> geometries)
    {
      EnqueueAction
      (
        (doc) =>
        {
          using (var attributes = Convert.GraphicAttributes.Push())
          {
            using (var collector = new FilteredElementCollector(ActiveDBDocument))
            {
              var materials = collector.OfClass(typeof(Material)).Cast<Material>();
              attributes.MaterialId = (materials.Where((x) => x.Name == "Debug").FirstOrDefault()?.Id) ?? ElementId.InvalidElementId;
            }

            foreach (var geometryToBake in geometries.ToHost())
            {
              if (geometryToBake == null)
                continue;

              BakeGeometry(doc, geometryToBake, BuiltInCategory.OST_GenericModel);
            }
          }
        }
      );
    }
#endif

    static void BakeGeometry(Document doc, IEnumerable<GeometryObject> geometryToBake, BuiltInCategory categoryToBakeInto)
    {
      try
      {
        var geometryList = new List<GeometryObject>();

        // DirectShape only accepts those types and no nulls
        foreach (var g in geometryToBake)
        {
          switch (g)
          {
            case Point p: geometryList.Add(p); break;
            case Curve c: geometryList.Add(c); break;
            case Solid s: geometryList.Add(s); break;
            case Mesh m: geometryList.Add(m); break;
          }
        }

        if (geometryList.Count > 0)
        {
          var category = new ElementId(categoryToBakeInto);
          if (!DirectShape.IsValidCategoryId(category, doc))
            category = new ElementId(BuiltInCategory.OST_GenericModel);

          var ds = DirectShape.CreateElement(doc, category);
          ds.SetShape(geometryList);
        }
      }
      catch (Exception e)
      {
        Debug.Fail(e.Source, e.Message);
      }
    }
    #endregion

    #region Document Actions
    private static Queue<Action<Document>> docWriteActions = new Queue<Action<Document>>();
    public static void EnqueueAction(Action<Document> action)
    {
      lock (docWriteActions)
        docWriteActions.Enqueue(action);
    }

    void ProcessWriteActions()
    {
      lock (docWriteActions)
      {
        if (docWriteActions.Count > 0)
        {
          using (var trans = new Transaction(ActiveDBDocument))
          {
            try
            {
              if (trans.Start("RhinoInside") == TransactionStatus.Started)
              {
                while (docWriteActions.Count > 0)
                  docWriteActions.Dequeue().Invoke(ActiveDBDocument);

                Committing = true;
                var options = trans.GetFailureHandlingOptions();
                trans.Commit(options.SetDelayedMiniWarnings(true).SetForcedModalHandling(false).SetFailuresPreprocessor(new FailuresPreprocessor()));
                Committing = false;

                foreach (GH_Document definition in Grasshopper.Instances.DocumentServer)
                {
                  if (definition.Enabled)
                    definition.NewSolution(false);
                }
              }
            }
            catch (Exception e)
            {
              Debug.Fail(e.Source, e.Message);

              if (trans.HasStarted())
                trans.RollBack();
            }
            finally
            {
              docWriteActions.Clear();
            }
          }
        }
      }
    }

    private static Queue<Action<Document, bool>> docReadActions = new Queue<Action<Document, bool>>();
    public static void EnqueueReadAction(Action<Document, bool> action)
    {
      lock (docReadActions)
        docReadActions.Enqueue(action);
    }

    bool ProcessReadActions(bool cancel = false)
    {
      lock (docReadActions)
      {
        if (docReadActions.Count > 0)
        {
          var stopWatch = new Stopwatch();

          while (docReadActions.Count > 0)
          {
            // We will do as much work as possible in 150 ms on each OnIdle event
            if (!cancel && stopWatch.ElapsedMilliseconds > 150)
              return true; // there is pending work to do

            stopWatch.Start();
            try { docReadActions.Dequeue().Invoke(ActiveDBDocument, cancel); }
            catch (Exception e) { Debug.Fail(e.Source, e.Message); }
            stopWatch.Stop();
          }
        }
      }

      // there is no more work to do
      return false;
    }
    #endregion

    #region Public Properties
    public static IntPtr MainWindowHandle { get; private set; }
    public static Autodesk.Revit.UI.UIControlledApplication ApplicationUI { get; private set; }
    public static Autodesk.Revit.UI.UIApplication ActiveUIApplication { get; private set; }
    public static Autodesk.Revit.ApplicationServices.Application Services => ActiveUIApplication?.Application;

    public static Autodesk.Revit.UI.UIDocument ActiveUIDocument => ActiveUIApplication?.ActiveUIDocument;
    public static Autodesk.Revit.DB.Document   ActiveDBDocument => ActiveUIDocument?.Document;

    private const double AbsoluteTolerance = (1.0 / 12.0) / 16.0; // 1/16 inch in feet
    public static double AngleTolerance => Services != null ? Services.AngleTolerance : Math.PI / 180.0; // in rad
    public static double ShortCurveTolerance => Services != null ? Services.ShortCurveTolerance : AbsoluteTolerance / 2.0;
    public static double VertexTolerance => Services != null ? Services.VertexTolerance : AbsoluteTolerance / 10.0;
    public const Rhino.UnitSystem ModelUnitSystem = Rhino.UnitSystem.Feet; // Always feet
    public static double ModelUnits => RhinoDoc.ActiveDoc == null ? double.NaN : RhinoMath.UnitScale(ModelUnitSystem, RhinoDoc.ActiveDoc.ModelUnitSystem); // 1 feet in Rhino units
    #endregion
  }

  public abstract class DirectContext3DServer : IDirectContext3DServer
  {
    #region IExternalServer
    public abstract string GetDescription();
    public abstract string GetName();
    string IExternalServer.GetVendorId() => "RMA";
    ExternalServiceId IExternalServer.GetServiceId() => ExternalServices.BuiltInExternalServices.DirectContext3DService;
    public abstract Guid GetServerId();
    #endregion

    #region IDirectContext3DServer
    string IDirectContext3DServer.GetApplicationId() => string.Empty;
    string IDirectContext3DServer.GetSourceId() => string.Empty;
    bool IDirectContext3DServer.UsesHandles() => false;
    public virtual bool UseInTransparentPass(Autodesk.Revit.DB.View dBView) => false;
    public abstract bool CanExecute(Autodesk.Revit.DB.View dBView);
    public abstract Outline GetBoundingBox(Autodesk.Revit.DB.View dBView);
    public abstract void RenderScene(Autodesk.Revit.DB.View dBView, DisplayStyle displayStyle);
    #endregion

    virtual public void Register()
    {
      using (var service = ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService) as MultiServerService)
      {
        service.AddServer(this);

        var activeServerIds = service.GetActiveServerIds();
        activeServerIds.Add(GetServerId());
        service.SetActiveServers(activeServerIds);
      }
    }

    virtual public void Unregister()
    {
      using (var service = ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService) as MultiServerService)
      {
        var activeServerIds = service.GetActiveServerIds();
        activeServerIds.Remove(GetServerId());
        service.SetActiveServers(activeServerIds);

        service.RemoveServer(GetServerId());
      }
    }

    protected static VertexBuffer ToVertexBuffer(Rhino.Geometry.Mesh mesh, out VertexFormatBits vertexFormatBits, System.Drawing.Color color = default(System.Drawing.Color))
    {
      int verticesCount = mesh.Vertices.Count;
      int normalCount = mesh.Normals.Count;
      int colorsCount = color.IsEmpty ? mesh.VertexColors.Count : verticesCount;
      bool hasVertices = verticesCount > 0;
      bool hasNormals = normalCount == verticesCount;
      bool hasColors = colorsCount == verticesCount;
      int floatCount = verticesCount + (hasNormals ? normalCount : 0) + (hasColors ? colorsCount : 0);

      if (hasVertices)
      {
        var vertices = mesh.Vertices;
        if (hasNormals)
        {
          var normals = mesh.Normals;
          if (hasColors)
          {
            vertexFormatBits = VertexFormatBits.PositionNormalColored;
            var colors = mesh.VertexColors;
            var vb = new VertexBuffer(verticesCount * VertexPositionNormalColored.GetSizeInFloats());
            vb.Map(verticesCount * VertexPositionNormalColored.GetSizeInFloats());
            using (var stream = vb.GetVertexStreamPositionNormalColored())
            {
              for (int v = 0; v < verticesCount; ++v)
              {
                var c = !color.IsEmpty ? color : colors[v];
                stream.AddVertex(new VertexPositionNormalColored(vertices[v].ToHost(), normals[v].ToHost(), new ColorWithTransparency(c.R, c.G, c.B, 255u - c.A)));
              }
            }
            vb.Unmap();
            return vb;
          }
          else
          {
            vertexFormatBits = VertexFormatBits.PositionNormal;
            var vb = new VertexBuffer(verticesCount * VertexPositionNormal.GetSizeInFloats());
            vb.Map(verticesCount * VertexPositionNormal.GetSizeInFloats());
            using (var stream = vb.GetVertexStreamPositionNormal())
            {
              for (int v = 0; v < verticesCount; ++v)
                stream.AddVertex(new VertexPositionNormal(vertices[v].ToHost(), normals[v].ToHost()));
            }
            vb.Unmap();
            return vb;
          }
        }
        else
        {
          if (hasColors)
          {
            vertexFormatBits = VertexFormatBits.PositionColored;
            var colors = mesh.VertexColors;
            var vb = new VertexBuffer(verticesCount * VertexPositionColored.GetSizeInFloats());
            vb.Map(verticesCount * VertexPositionColored.GetSizeInFloats());
            using (var stream = vb.GetVertexStreamPositionColored())
            {
              for (int v = 0; v < verticesCount; ++v)
              {
                var c = !color.IsEmpty ? color : colors[v];
                stream.AddVertex(new VertexPositionColored(vertices[v].ToHost(), new ColorWithTransparency(c.R, c.G, c.B, 255u - c.A)));
              }
            }
            vb.Unmap();
            return vb;
          }
          else
          {
            vertexFormatBits = VertexFormatBits.Position;
            var vb = new VertexBuffer(verticesCount * VertexPosition.GetSizeInFloats());
            vb.Map(verticesCount * VertexPosition.GetSizeInFloats());
            using (var stream = vb.GetVertexStreamPosition())
            {
              for (int v = 0; v < verticesCount; ++v)
                stream.AddVertex(new VertexPosition(vertices[v].ToHost()));
            }
            vb.Unmap();
            return vb;
          }
        }
      }

      vertexFormatBits = 0;
      return null;
    }

    protected static IndexBuffer ToTrianglesBuffer(Rhino.Geometry.Mesh mesh, out int triangleCount)
    {
      triangleCount = mesh.Faces.Count + mesh.Faces.QuadCount;
      if (triangleCount > 0)
      {
        var ib = new IndexBuffer(triangleCount * 3);

        ib.Map(triangleCount * 3);
        using (var istream = ib.GetIndexStreamTriangle())
        {
          foreach (var face in mesh.Faces)
          {
            istream.AddTriangle(new IndexTriangle(face.A, face.B, face.C));
            if (face.IsQuad)
              istream.AddTriangle(new IndexTriangle(face.C, face.D, face.A));
          }
        }

        ib.Unmap();
        return ib;
      }

      return null;
    }

    protected static IndexBuffer ToWireframeBuffer(Rhino.Geometry.Mesh mesh, out int linesCount)
    {
      linesCount = (mesh.Faces.Count * 3) + mesh.Faces.QuadCount;
      if (linesCount > 0)
      {
        var ib = new IndexBuffer(linesCount * 2);

        ib.Map(linesCount * 2);
        using (var istream = ib.GetIndexStreamLine())
        {
          foreach (var face in mesh.Faces)
          {
            istream.AddLine(new IndexLine(face.A, face.B));
            istream.AddLine(new IndexLine(face.B, face.C));
            istream.AddLine(new IndexLine(face.C, face.D));
            if (face.IsQuad)
              istream.AddLine(new IndexLine(face.D, face.A));
          }
        }

        ib.Unmap();
        return ib;
      }

      return null;
    }

    protected static IndexBuffer ToEdgeBuffer(Rhino.Geometry.Mesh mesh, out int linesCount)
    {
      var vertices = mesh.TopologyVertices;
      var edgeIndices = new List<IndexPair>();
      {
        var edges = mesh.TopologyEdges;
        var edgeCount = edges.Count;
        for (int e = 0; e < edgeCount; ++e)
        {
          if (edges.IsEdgeUnwelded(e) || edges.GetConnectedFaces(e).Length < 2)
            edgeIndices.Add(edges.GetTopologyVertices(e));
        }
      }

      linesCount = edgeIndices.Count;
      if (linesCount > 0)
      {
        var ib = new IndexBuffer(linesCount * 2);

        ib.Map(linesCount * 2);
        using (var istream = ib.GetIndexStreamLine())
        {
          foreach (var edge in edgeIndices)
          {
            var vi = vertices.MeshVertexIndices(edge.I);
            var vj = vertices.MeshVertexIndices(edge.J);

            istream.AddLine(new IndexLine(vi[0], vj[0]));
          }
        }
        ib.Unmap();

        return ib;
      }

      return null;
    }

    protected static int ToLinesBuffer(Rhino.Geometry.Polyline[] wires, out VertexBuffer vb, out IndexBuffer ib)
    {
      int linesCount = 0;
      vb = null;
      ib = null;

      if (wires?.Length > 0)
      {
        foreach (var polyline in wires)
          linesCount += polyline.SegmentCount;

        vb = new VertexBuffer(linesCount * 2 * VertexPosition.GetSizeInFloats());
        vb.Map(linesCount * 2 * VertexPosition.GetSizeInFloats());

        ib = new IndexBuffer(linesCount * 2);
        ib.Map(linesCount * 2);

        int vi = 0;
        using (var vstream = vb.GetVertexStreamPosition())
        using (var istream = ib.GetIndexStreamLine())
        {
          foreach (var polyline in wires)
          {
            int segmentCount = polyline.SegmentCount;
            for (int s = 0; s < segmentCount; ++s)
            {
              var line = polyline.SegmentAt(s);
              vstream.AddVertex(new VertexPosition(line.From.ToHost()));
              vstream.AddVertex(new VertexPosition(line.To.ToHost()));
              istream.AddLine(new IndexLine(vi++, vi++));
            }
          }
        }

        vb.Unmap();
        ib.Unmap();
      }

      return linesCount;
    }

    protected static int ToPointsBuffer(Rhino.Geometry.Point[] points, out VertexBuffer vb, out IndexBuffer ib)
    {
      int pointsCount = 0;
      vb = null;
      ib = null;

      if (points?.Length > 0)
      {
        pointsCount = points.Length;

        vb = new VertexBuffer(pointsCount * VertexPosition.GetSizeInFloats());
        vb.Map(pointsCount * VertexPosition.GetSizeInFloats());

        ib = new IndexBuffer(pointsCount);
        ib.Map(pointsCount);

        int vi = 0;
        using (var vstream = vb.GetVertexStreamPosition())
        using (var istream = ib.GetIndexStreamPoint())
        {
          foreach (var point in points)
          {
            vstream.AddVertex(new VertexPosition(point.Location.ToHost()));
            istream.AddPoint(new IndexPoint(vi++));
          }
        }

        vb.Unmap();
        ib.Unmap();
      }

      return pointsCount;
    }

    #region Primitive
    protected class Primitive : IDisposable
    {
      protected VertexFormatBits vertexFormatBits;
      protected int vertexCount;
      protected VertexBuffer vertexBuffer;
      protected VertexFormat vertexFormat;

      protected int triangleCount;
      protected IndexBuffer triangleBuffer;

      protected int linesCount;
      protected IndexBuffer linesBuffer;

      protected EffectInstance effectInstance;
      protected Rhino.Geometry.GeometryBase geometry;
      public Rhino.Geometry.BoundingBox ClippingBox => geometry.GetBoundingBox(false);

      public Primitive(Rhino.Geometry.Point p) { geometry = p; }
      public Primitive(Rhino.Geometry.Curve c) { geometry = c; }
      public Primitive(Rhino.Geometry.Mesh  m) { geometry = m; }

      void IDisposable.Dispose()
      {
        effectInstance?.Dispose(); effectInstance = null;
        linesBuffer?.Dispose();    linesBuffer = null; linesCount = 0;
        triangleBuffer?.Dispose(); triangleBuffer = null; triangleCount = 0;
        vertexFormat?.Dispose();   vertexFormat = null;
        vertexBuffer?.Dispose();   vertexBuffer = null; vertexCount = 0;
        geometry?.Dispose();           geometry = null;
      }

      public virtual EffectInstance EffectInstance(DisplayStyle displayStyle)
      {
        if (effectInstance == null)
          effectInstance = new EffectInstance(vertexFormatBits);

        return effectInstance;
      }

      public virtual void Regen()
      {
        if (geometry is Rhino.Geometry.Mesh mesh)
        {
          vertexBuffer?.Dispose();
          vertexBuffer = ToVertexBuffer(mesh, out vertexFormatBits);
          vertexCount = mesh.Vertices.Count;

          triangleBuffer?.Dispose();
          triangleBuffer = ToTrianglesBuffer(mesh, out triangleCount);

          linesBuffer?.Dispose();
          linesBuffer = ToEdgeBuffer(mesh, out linesCount);
        }
        else if (geometry is Rhino.Geometry.Curve curve)
        {
          var polyline = curve.ToPolyline(Revit.VertexTolerance * 10.0, Revit.AngleTolerance, Revit.ShortCurveTolerance, 0.0);
          linesCount = ToLinesBuffer(new Rhino.Geometry.Polyline[] { polyline.ToPolyline() }, out vertexBuffer, out linesBuffer);
          vertexFormatBits = VertexFormatBits.Position;
          vertexCount = linesCount * 2;
        }
        else if (geometry is Rhino.Geometry.Point point)
        {
          linesCount = -ToPointsBuffer(new Rhino.Geometry.Point[] { point }, out vertexBuffer, out linesBuffer);
          vertexFormatBits = VertexFormatBits.Position;
          vertexCount = -linesCount;
        }

        vertexFormat?.Dispose();
        vertexFormat = new VertexFormat(vertexFormatBits);

        effectInstance?.Dispose();
        geometry?.Dispose(); geometry = null;
      }

      public virtual void Draw(DisplayStyle displayStyle)
      {
        if (geometry != null)
          Regen();

        var ei = EffectInstance(displayStyle);

        bool wires = displayStyle == DisplayStyle.Wireframe ||
                     displayStyle == DisplayStyle.HLR ||
                     displayStyle == DisplayStyle.ShadingWithEdges ||
                     displayStyle == DisplayStyle.FlatColors ||
                     displayStyle == DisplayStyle.RealisticWithEdges;

        if (triangleCount > 0)
        {
          DrawContext.FlushBuffer
          (
            vertexBuffer, vertexCount,
            triangleBuffer, triangleCount * 3,
            vertexFormat, ei,
            PrimitiveType.TriangleList,
            0, triangleCount
          );
        }

        if (wires && linesCount != 0)
        {
          ei.SetTransparency(0.0);

          if (triangleBuffer != null)
          {
            ei.SetDiffuseColor(System.Drawing.Color.Black.ToHost());
            ei.SetEmissiveColor(System.Drawing.Color.Black.ToHost());
          }

          if (linesCount > 0)
          {
            DrawContext.FlushBuffer
            (
              vertexBuffer, vertexCount,
              linesBuffer, linesCount * 2,
              vertexFormat, ei,
              PrimitiveType.LineList,
              0, linesCount
            );
          }
          else
          {
            DrawContext.FlushBuffer
            (
              vertexBuffer, vertexCount,
              linesBuffer, Math.Abs(linesCount),
              vertexFormat, ei,
              PrimitiveType.PointList,
              0, Math.Abs(linesCount)
            );
          }
        }

      }
    }
    #endregion
  }
}
