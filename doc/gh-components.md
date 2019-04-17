# Grasshopper components for Revit
The Rhino Inside® technology allows Rhino and Grasshopper to be embedded within Revit.

It is important to have a basic understanding to the [Revit Data Hierarchy](https://www.modelical.com/en/gdocs/revit-data-hierarchy/) of Category -> Family -> Type -> Instance/Element to create and select elements.

This guide documents the Grasshopper components that support Revit interaction.

## Components

#### Build Components

 | Icon | Name | Description |
 | --- | --- | --- |
 | ![](GH/BeamByCurve.png) | Beam By Curve | Create a Revit Beam Object using a 2d or 3d curve for an axis |
 | ![](GH/FloorByOutline.png) | Floor By Outline | Create a Revit Floor Object using a 2d curve |
 | ![](GH/ColumnByCurve.png) | Column By Curve | Create a Revit Column Object using an axial curve|
 | ![](GH/WallByCurve.png) | Wall By Curve | Create a Revit Wall Object using a plan curve|
 | ![](GH/DirectShapeByGeometry.png) | DirectShape | Create a Directshape Element using a plan curve. This is the most generic way to import Geometry. |
 | ![](GH/DirectShapeCategories.png) | DirectShape Category | Create a Directshape category For using with the DirectShape Component |

#### Category Components

  | Icon | Name | Description |
  | --- | --- | --- |
  | ![](GH/CategoryDecompose.png) | Category Decompose | Break a Revit Category into its component parts.  Name, Parent, Line-Color, Material, Allow Bounds and Material Quantities |
  | ![](GH/Category.png) | Category | Revit Category Param |
  | ![](GH/CategoryTypes.png) | Category Types | A pick list of category types in Revit. |

#### Document Components

   | Icon | Name | Description |
   | --- | --- | --- |
   | ![](GH/DocumentCategories.png) | Document Categories | Get Active Revit Document Category list |
   | ![](GH/DocumentElements.jpg) | Document Elements | Get Active Document Elements list using a Category filter|
   | ![](GH/DocumentElementTypes.png) | Document Element Types | Get Active Document Element Types using the Category, Family and Type filter |
   | ![](GH/DocumentLevels.png) | Document Levels | Get Active Document levels list from Revit |
   | ![](GH/DocumentParameters.png) | Document Parameters | Get Active Document Parameters attached to a specific category from Revit |

#### Elements Components

  | Icon | Name | Description |
  | --- | --- | --- |
  | ![](GH/ElementDecompose.png) | Element Decompose | Decompose an Element into its associated data including Constraints, Dimensions, Identity Data, Category, Family, Type, ID, etc...|
  | ![](GH/ElementGeometry.png) | Element Geometry | Returns one or more Rhino Breps(s) form a Revit Element|
  | ![](GH/ElementIdentity.png) | Element Identity | Returns Element Name, Category, Type and UUID|
  | ![](GH/ElementParameterGet.png) | Element Parameter Get | Get Active Document levels list from Revit|
  | ![](GH/ElementParameters.png) | Element Parameters | Get Active Document Parameters attached to a specific category from Revit |
  | ![](GH/ElementParameterSet.png) | Element Parameters Set | Get Active Document Elements list using a Category filter|
  | ![](GH/ElementPreview.png) | Element Preview | Get Active Document Element Types using the Category, Family and Type filter|
  | ![](GH/Element.png) | Element| Select one or more persistent Element(s) in Revit|
  | ![](GH/ElementType.png) | Element Type | Get Active Document Parameters attached to a specific category from Revit |
