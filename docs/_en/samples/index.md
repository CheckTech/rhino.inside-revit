---
title: Rhino Inside Revit Samples
description: Official developer resources for Rhino and Grasshopper.  Rhino developer tools are royalty free and include support.
lang: en
authors: ['scott_davidson']
layout: fullwidth-page
---


Wecome to the Revolution!
The Rhino.Inside.Revit Project is a super exciting new devlopement sponsored by Robert McNeel and Associates.



## Sample projects using Rhino.Inside.Revit

<div class="trigger">
  {% assign guides = site.en | sort:"order" %}
  <ul>
  {% for guide in guides %}
    {% if guide.categories contains 'general' %}
      {% if guide.title and guide.order %}
        <li><a class="page-link" href="{{ guide.url | prepend: site.baseurl }}" title="{{ guide.description }}">{{ guide.title }}</a></li>
      {% endif %}
    {% endif %}
  {% endfor %}
  </ul>
</div>


---
