using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class TiledTextureAtlasEditor : EditorWindow {

	[MenuItem("Window/TiledTextureAtlas")]
	static void Init() {
		var window = EditorWindow.GetWindow<TiledTextureAtlasEditor>();
	}

	GameObject selectedObject;
	bool option1;
	bool option2;
	bool option3;

	static readonly List<string> shaderNames = new List<string>() {
		"Diffuse",
		"Mobile/Diffuse"
	};

	const int ATLAS_SIZE = 2048;
	const string ATLAS_SHADER_NAME = "Mobile/Diffuse Tileable Atlas";

	[Serializable]
	[JsonFx.Json.JsonName("Atlas")]
	public class Atlas {
		public Texture2D texture;
		public Material material;
		public List<Texture2D> texturesOriginal = new List<Texture2D>();
		public List<Texture2D> texturesInAtlas = new List<Texture2D>();

		[SerializeField]
		[JsonFx.Json.JsonMember]
		public Rect[] rects;

		[SerializeField]
		[JsonFx.Json.JsonMember]
		public List<string> texturesOriginalNames {
			get {
				return texturesOriginal.Select(t => t.name).ToList();
			}
		}

		[SerializeField]
		[JsonFx.Json.JsonMember]
		public List<string> texturesInAtlasNames {
			get {
				return texturesInAtlas.Select(t => t.name).ToList();
			}
		}

		public void SortTextures() {
			texturesInAtlas.Sort((a,b) => {
				if(a.width == b.width && a.height == b.height)
					return 0;
				else if(a.width > b.width || a.height > b.height)
					return -1;
				else
					return 1;
			});
			UpdateOriginalTextureOrder();
		}

		public void UpdateOriginalTextureOrder() {
			for(int i=0; i<texturesInAtlas.Count; ++i) {
				int index = texturesOriginal.FindIndex(t => t.name == texturesInAtlas[i].name);
				if(index >= 0) {
					Texture2D temp = texturesOriginal[i];
					texturesOriginal[i] = texturesOriginal[index];
					texturesOriginal[index] = temp;
				}
			}
		}

		public static void SortTextures(List<Texture2D> textures) {
			textures.Sort((a,b) => {
				if(a.width == b.width && a.height == b.height)
					return 0;
				else if(a.width > b.width || a.height > b.height)
					return -1;
				else
					return 1;
			});
		}
	}

	List<Atlas> atlases = new List<Atlas>();
	List<MeshRenderer> meshRenderers = new List<MeshRenderer>();

	void OnGUI() {
		GUILayout.Space(10);
		GUILayout.BeginHorizontal();
		GUILayout.Label("Model: ");
		selectedObject = (GameObject)EditorGUILayout.ObjectField(selectedObject, typeof(GameObject), true);
		GUILayout.EndHorizontal();

		option1 = GUILayout.Toggle(option1, "Enable option 1");
		option2 = GUILayout.Toggle(option2, "Enable option 2");
		option3 = GUILayout.Toggle(option3, "Enable option 3");

		if(GUILayout.Button("Process")) {
			string path = EditorUtility.SaveFolderPanel("Save to path", "", "");
			if(!string.IsNullOrEmpty(path))
				ProcessObject(path);
		}
	}

	void ProcessObject(string path) {
		if(selectedObject == null)
			return;

		CombineTexturesIntoAtlas(path);
		SaveAtlasesToDisk(path);
		UpdateMeshes(path);
		EditorUtility.ClearProgressBar();
	}

	void CombineTexturesIntoAtlas(string path) {
		atlases.Clear();
		meshRenderers.Clear();

		Atlas currentAtlas = CreateNewAtlas();
		List<Texture2D> processedTextures = new List<Texture2D>();
		MeshRenderer[] allRenderers = selectedObject.GetComponentsInChildren<MeshRenderer>();

		for(int i=0; i<allRenderers.Length; ++i) {
			EditorUtility.DisplayProgressBar(
				"Combining object textures",
				string.Format("Object {0} of {1}", i+1, allRenderers.Length),
				(float)(i+1)/(float)allRenderers.Length
			);
			MeshRenderer meshRenderer = allRenderers[i];
			for(int j=0; j<meshRenderer.sharedMaterials.Length; ++j) {
				Material material = meshRenderer.sharedMaterials[j];
				if(!IsIncludedInCompatibleShaders(material)) continue;

				if(!meshRenderers.Contains(meshRenderer))
					meshRenderers.Add(meshRenderer);
				
				Texture2D mainTexture = (Texture2D)material.mainTexture;
				Texture2D mainTextureInAtlas = mainTexture;
				if(mainTexture == null) continue;

				if(processedTextures.Contains(mainTexture))
					continue;

				MakeTextureReadable(mainTexture);
				if(material.mainTextureScale != Vector2.one) {
					mainTextureInAtlas = Create2x2Texture(mainTexture, path);
					MakeTextureReadable(mainTextureInAtlas);
				}

				List<Texture2D> texturesToProcess = new List<Texture2D>(currentAtlas.texturesInAtlas);
				texturesToProcess.Add(mainTextureInAtlas);
				Atlas.SortTextures(texturesToProcess);

				Rect[] rects = UITexturePacker.PackTextures(
					currentAtlas.texture, texturesToProcess.ToArray(), 1, 1, 0, ATLAS_SIZE);
				
				if(rects != null) {
					currentAtlas.texturesInAtlas = texturesToProcess;
					currentAtlas.texturesOriginal.Add(mainTexture);
					currentAtlas.UpdateOriginalTextureOrder();
				}else{
					currentAtlas = CreateNewAtlas();
					currentAtlas.texturesOriginal.Add(mainTexture);
					currentAtlas.texturesInAtlas.Add(mainTextureInAtlas);
					currentAtlas.SortTextures();
					rects = UITexturePacker.PackTextures(
						currentAtlas.texture, currentAtlas.texturesInAtlas.ToArray(), 1, 1, 0, ATLAS_SIZE);
				}
				
				currentAtlas.rects = new Rect[rects.Length];
				Array.Copy(rects, currentAtlas.rects, rects.Length);

				processedTextures.Add(mainTexture);
			}

		}
	}

	void SaveAtlasesToDisk(string path) {
		for(int i=0; i<atlases.Count; ++i) {
			EditorUtility.DisplayProgressBar(
				"Saving all texture atlases",
				string.Format("Atlas {0} of {1}", i+1, atlases.Count),
				(float)(i+1)/(float)atlases.Count
			);
			Atlas atlas = atlases[i];
			string atlasPath = Path.Combine(path, "atlas" + i + ".png");
			File.WriteAllBytes(atlasPath, atlas.texture.EncodeToPNG());
			atlasPath = atlasPath.Substring(atlasPath.IndexOf("Assets/"));
			AssetDatabase.ImportAsset(atlasPath);
			atlas.texture = (Texture2D)AssetDatabase.LoadAssetAtPath(atlasPath, typeof(Texture));
			File.WriteAllText(Path.Combine(path, "atlas" + i + ".txt"), JsonFx.Json.JsonWriter.Serialize(atlas));

			string materialPath = Path.Combine(path, "atlas" + i + ".mat");
			materialPath = materialPath.Substring(materialPath.IndexOf("Assets/"));
			Material newMaterial = new Material(Shader.Find(ATLAS_SHADER_NAME));
			newMaterial.SetTexture("_MainTex", atlas.texture);
			AssetDatabase.CreateAsset(newMaterial, materialPath);
			atlas.material = newMaterial;
		}
	}

	void UpdateMeshes(string path) {
		List<Material> processedMaterials = new List<Material>();
		List<Mesh> processedMeshes = new List<Mesh>();
		for(int i=0; i<meshRenderers.Count; ++i) {
			EditorUtility.DisplayProgressBar(
				"Updating original meshes",
				string.Format("Mesh {0} of {1}", i+1, meshRenderers.Count),
				(float)(i+1)/(float)meshRenderers.Count
			);

			MeshRenderer mr = meshRenderers[i];
			MeshFilter mf = mr.GetComponent<MeshFilter>();

			Color[] colors = mf.sharedMesh.colors;
			if(colors == null || colors.Length == 0) {
				colors = new Color[mf.sharedMesh.vertexCount];
			}

			Vector2[] newUv = mf.sharedMesh.uv;
			Vector2[] newUv2 = mf.sharedMesh.uv;
			if(newUv == null || newUv.Length == 0)
				newUv = new Vector2[mf.sharedMesh.vertexCount];
			if(newUv2 == null || newUv2.Length == 0) 
				newUv2 = new Vector2[mf.sharedMesh.vertexCount];

			for(int j=0; j<mr.sharedMaterials.Length; ++j) {
				Material material = mr.sharedMaterials[j];
				if(!IsIncludedInCompatibleShaders(material)) continue;

				Texture2D mainTexture = (Texture2D)material.GetTexture("_MainTex");
				if(mainTexture == null) continue;

				Atlas atlas = GetAtlasContainingTexture(mainTexture);
				Rect mainRect = atlas.rects[atlas.texturesOriginal.IndexOf(mainTexture)];

				float pixelOffset = 0f;
				float pixelWidth = mainRect.width/mainTexture.width;
				float pixelHeight = mainRect.height/mainTexture.height;
				float x = mainRect.x + pixelOffset*pixelWidth;
				float y = mainRect.y + pixelOffset*pixelHeight;
				float width = mainRect.width - 2f*pixelOffset*pixelWidth;
				float height = mainRect.height - 2f*pixelOffset*pixelHeight;
				Vector2 scale = material.mainTextureScale;
				Vector2 offset = material.mainTextureOffset;

				if(j < mf.sharedMesh.subMeshCount) {
					int[] tris = mf.sharedMesh.GetTriangles(j);
					for(int k=0; k<tris.Length; ++k) {
						int vertex = tris[k];
						if(scale.x != 1 || scale.y != 1)
							colors[vertex] = new Color(scale.x/128f, scale.y/128f, width/2f, 1f);
						else
							colors[vertex] = new Color(scale.x/128f, scale.y/128f, width, 0f);
					}
				}

				for(int k=0; k<mf.sharedMesh.uv.Length; ++k) {
					newUv2[k] = new Vector2(x, y);
				}
				
				// Update the textures and shaders for this material
				List<Material> sharedMaterials = new List<Material>();
				sharedMaterials.AddRange(mr.sharedMaterials);
				sharedMaterials[j] = atlas.material;
				mr.sharedMaterials = sharedMaterials.ToArray();
			}

			// Update UV information for this mesh
			string meshPath = Path.Combine(path, mf.sharedMesh.name + mf.sharedMesh.GetInstanceID().ToString() + ".asset");
			meshPath = meshPath.Substring(meshPath.IndexOf("Assets/"));
			if(!processedMeshes.Contains(mf.sharedMesh)) {
				processedMeshes.Add(mf.sharedMesh);

				Mesh newMesh = new Mesh();
				newMesh.name = mf.sharedMesh.name;
				newMesh.vertices = mf.sharedMesh.vertices;
				newMesh.triangles = mf.sharedMesh.triangles;
				newMesh.normals = mf.sharedMesh.normals;
				newMesh.tangents = mf.sharedMesh.tangents;
				newMesh.uv = newUv;
				newMesh.uv2 = newUv2;
				newMesh.colors = colors;
				newMesh.subMeshCount = mf.sharedMesh.subMeshCount;
				for(int j=0; j<mf.sharedMesh.subMeshCount; ++j) {
					newMesh.SetTriangles(mf.sharedMesh.GetTriangles(j), j);
				}
				newMesh.RecalculateBounds();
				mf.sharedMesh = newMesh;

				AssetDatabase.CreateAsset(newMesh, meshPath);
			} else {
				mf.sharedMesh = (Mesh)AssetDatabase.LoadAssetAtPath(meshPath, typeof(Mesh));
			}
		}
	}

	void LogVector2Array(Vector2[] arr) {
		string msg = "";
		foreach(Vector2 v2 in arr)
			msg += v2.ToString() + ",";
		Debug.Log(msg);
	}

	Vector2[] RecalculateUVs(Vector2[] uv, Rect rect) {
		Vector2[] arr = new Vector2[uv.Length];
		//~TODO: this only works for a plane. make computation work for any kind of mesh
		arr[0] = new Vector2(rect.x, rect.y);
		arr[1] = new Vector2(rect.x + rect.width, rect.y + rect.height);
		arr[2] = new Vector2(rect.x + rect.width, rect.y);
		arr[3] = new Vector2(rect.x, rect.y + rect.height);
		return arr;
	}

	Atlas CreateNewAtlas() {
		Atlas atlas = new Atlas();
		atlas.texture = new Texture2D(ATLAS_SIZE, ATLAS_SIZE);
		atlas.rects = null;
		atlases.Add(atlas);
		return atlas;
	}

	Texture2D Create2x2Texture(Texture2D mainTexture, string path) {
		// Create a 2x2 texture tile
		Texture2D texture = new Texture2D(mainTexture.width*2, mainTexture.height*2, TextureFormat.RGBA32, true);
		texture.name = mainTexture.name;
		for(int x=0; x<2; ++x) {
			for(int y=0; y<2; ++y) {
				texture.SetPixels(
					x*mainTexture.width, 
					y*mainTexture.height, 
					mainTexture.width, 
					mainTexture.height, 
					mainTexture.GetPixels()
					);
			}
		}
		texture.Apply();

		// Save this asset in disk so it won't be loaded in memory all the time
		string texturePath = Path.Combine(path, texture.name + ".png");
		File.WriteAllBytes(texturePath, texture.EncodeToPNG());
		texturePath = texturePath.Substring(texturePath.IndexOf("Assets/"));
		AssetDatabase.ImportAsset(texturePath);
		return (Texture2D)AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture));
	}

	Atlas GetAtlasContainingTexture(Texture2D texture) {
		for(int i=0; i<atlases.Count; ++i) {
			Atlas atlas = atlases[i];
			if(atlas.texturesOriginal.Contains(texture))
				return atlas;
		}
		return null;
	}

	bool IsIncludedInCompatibleShaders(Material material) {
		for(int i=0; i<shaderNames.Count; ++i) {
			if(shaderNames[i] == material.shader.name)
				return true;
		}
		return false;
	}

	void MakeTextureReadable(Texture2D texture) {
		string assetPath = AssetDatabase.GetAssetPath(texture);
		TextureImporter texImporter = (TextureImporter)TextureImporter.GetAtPath(assetPath);
		if(texImporter != null) {
			texImporter.isReadable = true;
			AssetDatabase.ImportAsset(assetPath);
		}
	}
}
