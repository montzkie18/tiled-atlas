Tileable Texture Atlas Shader
=============================
Provides a tool and a shader to create a texture atlas for a mesh or a group of meshes, wherein a single mesh can be mapped to a subtile inside the atlas to be repeated many times based on a blog post by [Mark Hogan][1].

One problem with tiling inside an atlas though is how LOD is calculated inside texture regions, which causes grey bars or seems between texture repetitions.

![Texture Seams][3]

To fix this, we used 4 tap sampling method which is perfectly discussed by [Mikola Lysenko][2].

Dependencies
------------
[JsonFX v2.0][5] serialization framework for .Net

[1]: http://www.gamasutra.com/blogs/MarkHogan/20140721/221458/Unity_Optimizing_For_Mobile_Using_SubTile_Meshes.php
[2]: http://0fps.net/2013/07/09/texture-atlases-wrapping-and-mip-mapping/
[3]: http://0fps.files.wordpress.com/2013/07/screen-shot-2013-07-04-at-1-51-11-pm.png
[4]: http://0fps.files.wordpress.com
[5]: https://github.com/jsonfx/jsonfx
