---
title: Known Issues
order: 6
---

This guide looks at errors that can appear with {{ site.terms.rir }}. This address most of the common errors we have seen. [Please Contact Us](https://www.rhino3d.com/support) whether any of these options worked or did not work. We are working to minimize any of these messages.

## Initialization Error -200

### Problem

When {{ site.terms.rir }} loads, the error below appears.

![]({{ "/static/images/reference/knownissues/error-200.png" | prepend: site.baseurl }})

### Workaround

This normally appears when there is a conflict between Rhino.inside and one or more Revit plugins that have loaded already. 

A common conflict is an older version of the {{ site.terms.pyrevit }} plugin.  While the newer versions to {{ site.terms.pyrevit }} do not cause a problem, an older version might.  Information on the {{ site.terms.pyrevit }} site can be found [{{ site.terms.pyrevit }} issue #628](https://github.com/eirannejad/pyRevit/issues/628). To update the older version of {{ site.terms.pyrevit }} use these steps:

  - Download [Microsoft.WindowsAPICodePack.Shell](https://www.nuget.org/packages/Microsoft.WindowsAPICodePack.Shell/) and place under `bin/` directory in pyRevit installation directory. This fix will be shipped with the next pyRevit version

  - DLL is also uploaded here for convenience if you don't know how to download NuGet packages. It's placed inside a ZIP archive for security. Unpack and place under `bin/` directory in pyRevit installation directory. [Microsoft.WindowsAPICodePack.Shell.dll.zip](https://github.com/eirannejad/pyRevit/files/3503717/Microsoft.WindowsAPICodePack.Shell.dll.zip)

If this does not solve the problem, then using the *Search for Conflicting Plugins* section.

## JSON Error

### Problem

A Long JSON error shows up as shown below

![]({{ "/static/images/reference/knownissues/error-json.png" | prepend: site.baseurl }})

### Workaround

Like the previous -200 error, this is a conflict with another plugin. See the Error - 200 solution for this problem, and the *Search for Conflicting Plugins* section below.

## Logging and Debugging Messages

{{ site.terms.rhino }} can creates a log on the desktop to see all the loading frameworks that are required. By sending us this log we can determine many conflicts.

The {{ site.terms.rhino }} being used must date later then 8-20-2019, or use [this specific build](http://files.mcneel.com/dujour/exe/20190814/rhino_en-us_7.0.19226.11575.exe) if it is before that day.

1. Once installed, create a blank text file on your desktop named exactly  `RhinoAssemblyResolveLog.txt` 
2. Run Revit, Rhino.inside and Grasshopper.  
3. Then close the applications
4. Send [McNeel Technical Support](https://www.rhino3d.com/support) the resulting log file.
5. Rename the log file to something else, so that logging will not continue.


## Search for Conflicting Plugins

Here is a tool that allows you to disable all but one plugin easily and test if it works. Once determining the conflicting plugins, [Please Contact Us](https://www.rhino3d.com/support)

If {{ site.terms.rir }} works when loaded alone, then use this app to enable more plugins and test again until the plugins are not compatible are found. 

**Hint:** there may be more then one plugin that causes trouble.

![]({{ "/static/images/reference/knownissues/addin-conflict-tool.png" | prepend: site.baseurl }})

To download and install the troubleshooter please follow instructions found [here](http://revitaddons.blogspot.com/2016/11/free-and-open-source-add-in-manager.html)

> 1. Download the tool from [Bitbucket](https://bitbucket.org/BoostYourBIM/stantecaddinmanager/downloads).
> 2. Find where you downloaded the zip file and unzip it.
> 3. When unzipped, there will be a BIN folder; browse into the BIN folder then the Debug sub-folder.
> 4. Run the tool by double-clicking on the exe file.
> 5. In the window that opens, pick your version of Revit (or go with all if you like).
> 6. The data grid will update to display all of the machine wide and the logged in user specific add-ins installed. You can pick and choose, invert, select all, then simply click on the “Enable/Disable” button to either enable or disable the selected add-ins.
> 7. Once you’ve made your choices, start Revit in the normal manner. Simple and easy.

Alternatively, you can download the tool from [here](https://bitbucket.org/BoostYourBIM/stantecaddinmanager/raw/03365f38188029436251f88f88dfa26db22bf8aa/AddInManager/bin/Debug/AddInManager.exe) as well

## SDK Debug Messages

There is a way to increase the number and detail of the error messages in {{ site.terms.rir }}. This is a good way to find a specific error that may lead to a solution.

1. Please unzip [RhinoSDK-Messages.zip](https://aws1.discourse-cdn.com/mcneel/uploads/default/original/3X/6/3/6348e99914b9e66417720df74f4cc35ba3e31c6f.zip) and double-click on the file **Enable RhinoSDK Messages.reg**. Windows will ask if you want to modify registry. Say yes
2. Then run again {{ site.terms.rir }}
3. A few message boxes should appear
4. Please capture those messages using a screenshot tool (e.g. [ShareX](https://getsharex.com/) and send it to  [McNeel Technical Support](https://www.rhino3d.com/support)
5. Once you are done, remember to open the **Disable RhinoSDK Messages.reg** to turn those dialogs off again