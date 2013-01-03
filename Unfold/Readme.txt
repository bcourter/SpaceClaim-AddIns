WARNING: 
These are experimental add-ins that are not supported by SpaceClaim.  They could cause SpaceClaim to crash, or, more likely, become unresponsive.  Make sure you save any work before using them!  Also, unfolding and tessellating large models can take a long time, so you might be able to wait for a result.  Start with coarse settings and refine as necessary.

INSTALLATION:
Unzip the contents of the attached and place them in C:\ProgramData\SpaceClaim\Addins.  You might need to create the directory.  Next time you start SpaceClaim, you should see some new tabs.  

The Unfold Body command automatically creates a flat pattern at an arbitrary position on the XY plane.  It requires that a face or body be selected to work.  The “Curves on Breaks” option creates dashed lines where the folds are on edges whose bend exceeds a minimum angle.  

The Tessellate Body coverts a selected set of faces to a planar-faced set of faces using the same type of technology used to create STL files.  

The Loft command creates a tessellated loft between two selected curves or edges.  It can be useful when you have a shape that is doubly curved and tessellates into too many triangles to be a useful as a flat pattern.  Instead, you can loft between pairs of curves and have faces that are guaranteed to unfold.  
