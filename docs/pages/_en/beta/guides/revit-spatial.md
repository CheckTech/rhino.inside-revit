---
title: Spatial Elements
order: 42
---

{% include ltr/en/wip_note.html %}

{% capture api_note %}
In Revit API, Spatial Elements are represented by the {% include api_type.html type='Autodesk.Revit.DB.SpatialElement' title='DB.SpatialElement' %}. This type is then used to create custom spatial types for Rooms ({% include api_type.html type='Autodesk.Revit.DB.Architecture.Room' title='DB.Architecture.Room' %}), Spaces ({% include api_type.html type='Autodesk.Revit.DB.Mechanical.Space' title='DB.Mechanical.Space' %}), and Areas ({% include api_type.html type='Autodesk.Revit.DB.Area' title='DB.Area' %})
{% endcapture %}
{% include ltr/api_note.html note=api_note %}


## Getting Spatial Element Geometry

To grab the most accurate geometry of a spatial element, use the custom *Analyse Spatial Element* and *Boundary Location* components shared here.

![]({{ "/static/images/guides/revit-spatial01.png" | prepend: site.baseurl }})

{% include ltr/download_comp.html archive='/static/ghnodes/Boundary Location.ghuser' name='Boundary Location' %}
{% include ltr/download_comp.html archive='/static/ghnodes/Analyse Spatial Element.ghuser' name='Analyse Spatial Element' %}

{% include ltr/warning_note.html note='Currently Revit API does not return geometry for `CoreBoundary` and `CoreCenter` boundary location options' %}

Make sure that the *Area and Volume* is checked under *Area and Volume Computations* configuration in your Revit model. Otherwise room geometry is not going to be correctly bound at the top and bottom.

![]({{ "/static/images/guides/revit-spatial03.png" | prepend: site.baseurl }})

Here is an example of using this workflow to get geometry of rooms:

![]({{ "/static/images/guides/revit-spatial02.gif" | prepend: site.baseurl }})
