using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class AlphaMapExtracter : ScriptableObject
{
	const string POSTFIX = "[a].png";

	public const string iOS = "iPhone";
	public const string Android = "Android";

	public class ImportSettings : System.IDisposable
	{
		public bool hasChanged {
			get { return _importer != null; }
		}
		
		TextureImporter _importer = null;

		bool _isReadable = true;

		bool _isUnityPacker = false;

		bool _trueColorAtlas = false;

		bool _forceSqureAtlas = false;

		bool _atlasPMA = false;

		bool _atlasTrimming = false;

		TextureImporterCompression _compression = TextureImporterCompression.Uncompressed;

		TextureImporterFormat _platformFormat = TextureImporterFormat.RGBA32;

		TextureImporterNPOTScale _npotScale = TextureImporterNPOTScale.None;

		TextureImporterPlatformSettings _platformSettings = new TextureImporterPlatformSettings();

		//int _quality;

		bool _autoPackAlpha = false;

		public ImportSettings(Material material, bool autoPackAlpha = false)
		{
			var t = material.mainTexture;
			_autoPackAlpha = autoPackAlpha;
			var path = AssetDatabase.GetAssetPath(t);
			StoreSetting(path);
			material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		public ImportSettings(Texture t, bool autoPackAlpha = false)
		{
			_autoPackAlpha = autoPackAlpha;
			var path = AssetDatabase.GetAssetPath(t);
			StoreSetting(path);
		}

		public ImportSettings(string path)
		{
			StoreSetting(path);
		}

		private bool HasTransparency(TextureImporter importer) {
			var platform = GetActivePlatform();
			var setting = importer.GetPlatformTextureSettings(platform);
			TextureImporterFormat fmt = setting.format;
			if (fmt == TextureImporterFormat.ARGB32 || fmt == TextureImporterFormat.ARGB16 ||
				fmt == TextureImporterFormat.RGBA32 || fmt == TextureImporterFormat.RGBA16) {
				return true;
			}
			else return false;
		}

		private bool NeedReimport(TextureImporter importer) {
			if (HasTransparency(importer) == false) return true;
			if (importer.isReadable == false) return true;
			if (importer.npotScale != TextureImporterNPOTScale.None) return true;
			return false;
		}

		private void StoreSetting(string path)
		{
			var importer = AssetImporter.GetAtPath(path) as TextureImporter;
			if (importer == null) return;
			if (NeedReimport(importer))
			{
				_isUnityPacker = NGUISettings.unityPacking;
				_forceSqureAtlas = NGUISettings.forceSquareAtlas;
				_trueColorAtlas = NGUISettings.trueColorAtlas;
				_atlasTrimming = NGUISettings.atlasTrimming;
				_atlasPMA = NGUISettings.atlasPMA;
				_compression = importer.textureCompression;
				_isReadable = importer.isReadable;
				_npotScale = importer.npotScale;
				NGUISettings.unityPacking = false;
				NGUISettings.forceSquareAtlas = true;
				NGUISettings.trueColorAtlas = false;
				NGUISettings.atlasPMA = false;
				NGUISettings.atlasTrimming = true;
				importer.isReadable = true;
				importer.textureType = TextureImporterType.Default;
				importer.npotScale = TextureImporterNPOTScale.None;

				VerifyPlatformSettings(importer);
				importer.SaveAndReimport();
				_importer = importer;
			}
		}

		private void VerifyPlatformSettings(TextureImporter importer)
		{
			string target = GetActivePlatform();
			_platformSettings = importer.GetPlatformTextureSettings(target);
			_platformFormat = _platformSettings.format;
			if (_platformSettings.overridden)
			{
				if (_platformFormat != TextureImporterFormat.RGB16 &&
					_platformFormat != TextureImporterFormat.RGB24 &&
					_platformFormat != TextureImporterFormat.ARGB32 &&
					_platformFormat != TextureImporterFormat.ARGB16 &&
					_platformFormat != TextureImporterFormat.RGBA32)
				{
					importer.SetPlatformTextureFormat(target, TextureImporterFormat.RGBA32);
				}
			}
			else
			{
				_platformFormat = TextureImporterFormat.RGBA32;
			}
		}

		private bool IsCompressed(TextureImporterFormat fmt) {
			return fmt == TextureImporterFormat.PVRTC_RGB4 ||
				fmt == TextureImporterFormat.ETC_RGB4;
		}

		public void Dispose()
		{
			if (_importer != null)
			{
				if (_autoPackAlpha && IsCompressed(_platformFormat)) {
					MakeAlphaMapCompressed4bit(_importer.assetPath);
				}
				NGUISettings.unityPacking = _isUnityPacker;
				NGUISettings.forceSquareAtlas = _forceSqureAtlas;
				NGUISettings.trueColorAtlas = _trueColorAtlas;
				NGUISettings.atlasTrimming = _atlasTrimming;
				NGUISettings.atlasPMA = _atlasPMA;
				_importer.textureCompression = _compression;
				_importer.isReadable = _isReadable;
				_importer.npotScale = _npotScale;
				RestorePlatformSettings();
				_importer.SaveAndReimport();
				//AssetDatabase.ImportAsset(_importer.assetPath, ImportAssetOptions.ForceSynchronousImport);
			}
		}

		private void RestorePlatformSettings()
		{
			if (_platformSettings.overridden)
			{
				string target = GetActivePlatform();

				_importer.SetPlatformTextureFormat(target, _platformFormat);

				_platformFormat = TextureImporterFormat.RGBA32;
			}
		}
	}

	private static string GetActivePlatform() {
		var target = EditorUserBuildSettings.activeBuildTarget;
		var str = GetActivePlatform(target);
		return str;
	}

	private static string GetActivePlatform(BuildTarget target) {
		switch (target) {
		case BuildTarget.Android: return Android;
		case BuildTarget.iOS: return iOS;
		}
		return target.ToString();
	}

	[MenuItem("CONTEXT/UIAtlas/Pack Alpha 'N Compress")]
	[MenuItem("CONTEXT/Material/Pack Alpha 'N Compress")]
	private static void PackAlphaAndCompress(MenuCommand cmd)
	{
		var mat = cmd.context as Material;
		if (mat == null)
		{
			var atlas = cmd.context as UIAtlas;
			if (atlas == null)
				return;
			mat = atlas.spriteMaterial;
		}
		if (mat == null)
			return;
		PackAlphaAndCompress(mat);
		AssetDatabase.SaveAssets();
		for (int i = 0; i < UIPanel.list.Count; ++i)
			UIPanel.list[i].Refresh();
	}

	private static void PackAlphaAndCompress(Material mat)
	{
		if (PackAlphaAndCompress(mat, "Unlit/Transparent Colored", "Unlit/Transparent Masked[a]"))
			return;
		if (PackAlphaAndCompress(mat, "Particles/Additive", "Particles/Additive Masked[a]"))
			return;
		if (PackAlphaAndCompress(mat, "Unlit/Particles/Additive", "Particles/Additive Masked[a]"))
			return;
		if (PackAlphaAndCompress(mat, "Mobile/Particles/Additive", "Particles/Additive Masked[a]"))
			return;
		if (PackAlphaAndCompress(mat, "Particles/Alpha Blended", "Particles/Alpha Blended[a]"))
			return;
	}

	private static bool PackAlphaAndCompress(Material mat, string oldName, string newName) {
		var texture = mat.mainTexture as Texture2D;
		if (texture == null || texture.width != texture.height) {
			Debug.Log(string.Format("texture {0} is not a square.", texture.name));
			return false;
		}
		if (IsAlphaMap(texture)) {
			return false;
		}
		var shader = Shader.Find(newName);
		if (shader == null) return false;
		if (mat.shader.name == oldName) {
			mat.shader = shader;
			EditorUtility.SetDirty(mat);
		}
		if (mat.shader.name != newName) {
			return false;
		}
		var path = MakeAlphaMapCompressed4bit(texture);
		var alphaMap = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		mat.SetTexture("_Mask", alphaMap);
		ImportTexture(texture, true);
		return true;
	}

	private static bool IsAlphaMap(Texture2D texture) {
		if (texture.name.Contains("[a]")) {
			if (texture.format == TextureFormat.PVRTC_RGB4 ||
				texture.format == TextureFormat.ETC_RGB4)
				return true;
		}
		return false;
	}

//	private static bool HasAlphaMap(Texture2D t) {
//		var path = AssetDatabase.GetAssetPath(t);
//		return HasAlphaMap(path);
//	}
//
//	private static bool HasAlphaMap(string path) {
//		//var ext = System.IO.Path.GetExtension(path);
//		var alphaPath = System.IO.Path.GetFileNameWithoutExtension(path) + POSTFIX;
//		var alphaMap = AssetDatabase.LoadAssetAtPath<Texture2D>(alphaPath);
//		if (alphaMap) {
//			if (alphaMap.format == TextureFormat.PVRTC_RGB4 || alphaMap.format == TextureFormat.ETC_RGB4)
//				return true;
//		}
//		return false;
//	}

	[MenuItem("Tools/Pack Alpha 'N Compress (Material)")]
	private static void PackAlphaAndCompress2()
	{
		Object[] materials = Selection.GetFiltered(typeof(Material), SelectionMode.DeepAssets);
		foreach (Material mat in materials)
		{
			if (mat != null)
			{
				PackAlphaAndCompress(mat);
			}
		}
		AssetDatabase.SaveAssets();
	}

	[MenuItem("Tools/Pack Alpha (Texture)")]
	private static void PackAlpha() {
		Object[] textures = Selection.GetFiltered(typeof(Texture2D), SelectionMode.DeepAssets);

		foreach (Texture2D t in textures)
		{
			if (IsAlphaMap(t))
				continue;
			MakeAlphaMapCompressed4bit(t);
		}
	}

	//[MenuItem("Tools/Make AlphaMap/Alpha 8bit")]
	public static void MakeAlphaMap8bit()
	{
		Object[] textures = Selection.GetFiltered(typeof(Texture2D), SelectionMode.DeepAssets);

		foreach (Texture2D t in textures)
		{
			MakeAlphaMap8bit(t);
		}
	}
	
	public static string MakeAlphaMap8bit(Texture2D t)
	{
		string path = AssetDatabase.GetAssetPath(t);

		using (ImportSettings settings = new ImportSettings(path))
		{
			path = ExtractAlphaMap(t, path, importer =>
			{
				importer.textureType = TextureImporterType.Default;
				importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
				importer.textureCompression = TextureImporterCompression.Uncompressed;
				importer.mipmapEnabled = false;
				importer.npotScale = TextureImporterNPOTScale.None;
				importer.wrapMode = TextureWrapMode.Clamp;
				
					var setting = importer.GetPlatformTextureSettings(GetActivePlatform());
					if (setting.overridden && setting.textureCompression != TextureImporterCompression.Uncompressed)
					{
						setting.format = TextureImporterFormat.RGBA32;
						importer.SetPlatformTextureSettings(setting);
					}
			});
		}
		return  path;
	}

	//[MenuItem("Tools/Make AlphaMap/Compressed 4bit")]
	public static void MakeAlphaMapCompressed4bit()
	{
	
	}

	public static string MakeAlphaMapCompressed4bit(string path)
	{
		var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		return MakeAlphaMapCompressed4bit(t);
	}
	
	public static string MakeAlphaMapCompressed4bit(Texture2D t)
	{
		string path = AssetDatabase.GetAssetPath(t);

		using (ImportSettings settings = new ImportSettings(path))
		{
			path = ExtractAlphaMap(t, path, importer =>
			{
				importer.textureType = TextureImporterType.Default;
				importer.alphaSource = TextureImporterAlphaSource.FromGrayScale; // as a notation for "import as compressed 4bit"
				importer.textureCompression = TextureImporterCompression.Uncompressed;
			
				_SetTexturePlatformSetting(importer, Android, TextureImporterFormat.ETC_RGB4);
				_SetTexturePlatformSetting(importer, iOS, TextureImporterFormat.PVRTC_RGB4);
				
				importer.mipmapEnabled = false;
				importer.npotScale = TextureImporterNPOTScale.ToNearest;
				importer.wrapMode = TextureWrapMode.Clamp;
			});
		}
		return  path;
	}
	
	private static void _SetTexturePlatformSetting(TextureImporter importer, string platform, TextureImporterFormat defaultFormat)
	{
		var setting = importer.GetPlatformTextureSettings(platform);
		
		if (!setting.overridden)
		{
			setting.maxTextureSize = importer.maxTextureSize;
			setting.format = defaultFormat;
			setting.overridden = true;
		}

		importer.SetPlatformTextureSettings(setting);
	}

	//[MenuItem("Tools/Make AlphaMap/Import as Alpha 8bit")]
	public static void ImportAlpha4bit()
	{
		ChangeSettings<Texture>((_importer, o) =>
		{
			TextureImporter importer = _importer as TextureImporter;
			
			importer.textureType = TextureImporterType.Default;
			importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			
			var androidConfig = importer.GetPlatformTextureSettings(GetActivePlatform(BuildTarget.Android));
			androidConfig.overridden = true;
			androidConfig.format = TextureImporterFormat.Alpha8;
			importer.SetPlatformTextureSettings(androidConfig);
			var iosConfig = importer.GetPlatformTextureSettings(GetActivePlatform(BuildTarget.iOS));
			iosConfig.overridden = true;
			iosConfig.format = TextureImporterFormat.Alpha8;
			importer.SetPlatformTextureSettings(iosConfig);
			importer.mipmapEnabled = false;
			importer.npotScale = TextureImporterNPOTScale.None;
			importer.wrapMode = TextureWrapMode.Clamp;
		});
	}

	//[MenuItem("Tools/Make AlphaMap/Import as Compressed 4bit")]
	public static void ImportCompressed4bit()
	{
		ChangeSettings<Texture>((_importer, o) =>
		{
			TextureImporter importer = _importer as TextureImporter;
			
			importer.textureType = TextureImporterType.Default;
			//importer.grayscaleToAlpha = false;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SetPlatformTextureFormat(Android, TextureImporterFormat.ETC_RGB4);
			importer.SetPlatformTextureFormat(iOS, TextureImporterFormat.PVRTC_RGB4);
			importer.mipmapEnabled = false;
			importer.isReadable = false;
			importer.npotScale = TextureImporterNPOTScale.ToNearest;
			importer.wrapMode = TextureWrapMode.Clamp;
		});
	}

	//[MenuItem("Tools/Make AlphaMap/Clear Import Settings")]
	public static void ImportClearImprotSettings()
	{
		ChangeSettings<Texture>((_importer, o) =>
		{
			TextureImporter importer = _importer as TextureImporter;

			importer.textureType = TextureImporterType.Default;
			importer.textureCompression = TextureImporterCompression.Uncompressed;

			importer.ClearPlatformTextureSettings(Android);
			importer.ClearPlatformTextureSettings(iOS);

			importer.mipmapEnabled = false;
			importer.wrapMode = TextureWrapMode.Clamp;
		});
	}

	private static string ExtractAlphaMap(Texture2D t, string path, System.Action<TextureImporter> action)
	{
		Debug.Log(path);

		Texture2D grayscale = new Texture2D(t.width, t.height, TextureFormat.RGB24, false);

		Debug.Log(t.format);

		Color[] pixels = t.GetPixels();

		for (int i = 0; i < pixels.Length; ++i)
		{
			pixels[i].r = pixels[i].g = pixels[i].b = pixels[i].a;
		}

		grayscale.SetPixels(pixels);

		byte[] bytes = grayscale.EncodeToPNG();

		int dotIndex = path.LastIndexOf('.');

		path = path.Substring(0, dotIndex) + POSTFIX;

		System.IO.FileStream fs = System.IO.File.Create(path);

		fs.Write(bytes, 0, bytes.Length);

		fs.Close();

		AssetDatabase.ImportAsset(path);

		TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

		if (importer == null)
		{
			Debug.LogWarning("unable to find textureimporter : " + path);
			return "";
		}

		action(importer);
		
		AssetDatabase.ImportAsset(path);

		return path;
	}

	[MenuItem("Tools/Batch/Make Custom Asset")]
	static void MakeCustomAssetInMenu()
	{
		var objs = Selection.objects;

		MakeCustomAsset(objs);
	}

	//[MenuItem("CONTEXT/MonoScript/Make Custom Asset")]
	static void MakeCustomAsset(MenuCommand cmd)
	{
		var script = cmd.context as MonoScript;

		MakeCustomAsset(script);
	}

	static void MakeCustomAsset(params Object[] objs)
	{
		if (objs == null || objs.Length == 0) return;

		var types = new List<System.Type>();

		foreach (var obj in objs)
		{
			var target = obj as UnityEditor.MonoScript;

			if (!target) continue;

			var type = target.GetClass();

			if (!type.IsSubclassOf(typeof(ScriptableObject)))
			{
				Debug.LogError(string.Format("{0} is not a subclass of ScriptableObject.", type.Name));
				continue;
			}

			types.Add(type);
		}

		var counts = new SortedList<string, int>();

		for (int i = 0; i < types.Count; ++i)
		{
			var type = types[i];

			int count = 0;

			if (counts.TryGetValue(type.Name, out count))
				counts[type.Name] = count;

			var path = AssetDatabase.GetAssetPath(objs[i]);

			path = path.Substring(0, path.LastIndexOf('/'));

			path += (types.Count > 1) ? string.Format("/{0}{1:D2}.asset", type.Name, count++) : string.Format("/{0}.asset", type.Name);

			var sobj = CreateInstance(type);

			AssetDatabase.CreateAsset(sobj, path);
		}
	}
	
	public static void MakeAlphaMapFromMaterial(Material mat)
	{
		Texture2D colorTexture = mat.mainTexture as Texture2D;

		if (colorTexture == null) return;

		VerifyColorTexture(colorTexture);

		string alphaPath;

		if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
		{
			alphaPath = MakeAlphaMapCompressed4bit(colorTexture);
		}
		else if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
		{
			alphaPath = MakeAlphaMapCompressed4bit(colorTexture);
		}
		else
		{
			Debug.LogWarning("unsupported platform. - " + EditorUserBuildSettings.activeBuildTarget);
			return;
		}

		Texture2D alphaMap = AssetDatabase.LoadAssetAtPath(alphaPath, typeof(Texture2D)) as Texture2D;

		if (mat.HasProperty("_AlphaMap"))
		{
			mat.SetTexture("_AlphaMap", alphaMap);
		}
		else if (mat.HasProperty("_GreyTex"))
		{
			mat.SetTexture("_GreyTex", alphaMap);
		}
		else if (mat.HasProperty("_Mask"))
		{
			mat.SetTexture("_Mask", alphaMap);
		}
	}

	private static void VerifyColorTexture(Texture2D colorTexture)
	{
		string colorPath = AssetDatabase.GetAssetPath(colorTexture);

		TextureImporter importer = AssetImporter.GetAtPath(colorPath) as TextureImporter;

		if (importer == null) return;

		bool hasChanged = false;
		
		System.Action<string, TextureImporterFormat> refresh = (platform, format) =>
		{
			if (importer.npotScale == TextureImporterNPOTScale.None)
			{
				importer.npotScale = TextureImporterNPOTScale.ToNearest;
				hasChanged = true;
			}

			var setting = importer.GetPlatformTextureSettings(platform);
			if (setting.overridden)
			{
				if (setting.format == format) return;
			}
			else if (setting.format != format)
			{
				setting.overridden = true;
				setting.format = format;
				importer.SetPlatformTextureSettings(setting);
				hasChanged = true;
			}
		};

		refresh(BuildTarget.Android.ToString(), TextureImporterFormat.ETC_RGB4);
		refresh(BuildTarget.iOS.ToString(), TextureImporterFormat.PVRTC_RGB4);

		if (hasChanged)
		{
			AssetDatabase.ImportAsset(colorPath, ImportAssetOptions.ForceSynchronousImport);
		}
	}
	
	//[MenuItem("Tools/Animation Compression")]
	public static void CompressAnimation()
	{
		ChangeSettings<GameObject>((_importer, o) => 
		{
			ModelImporter importer = _importer as ModelImporter;
			
			importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
		});
	}

	//[MenuItem("Tools/Default Texture2D Settings")]
	public static void DefaultTexture2DImportSettings()
	{
		ChangeSettings<Texture>((_importer, o) =>
		{
			TextureImporter importer = _importer as TextureImporter;

			if (importer == null)
			{
				return;
			}

			importer.textureType = TextureImporterType.Default;
			importer.mipmapEnabled = false;
			importer.isReadable = false;
			importer.npotScale = TextureImporterNPOTScale.None;
			if (importer.textureCompression != TextureImporterCompression.Uncompressed)
				importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.wrapMode = TextureWrapMode.Clamp;
		});
	}

//	[MenuItem("Tools/Default Audio2D Settings")]
//	public static void ChangeAudioImportSettings()
//	{
//		ChangeSettings<AudioClip>((_importer, o) =>
//		{
//			AudioImporter importer = _importer as AudioImporter;

//			if (importer == null)
//			{
//				return;
//			}

//			importer.threeD = false;
//			importer.hardware = true;
////			importer.compressionBitrate = 128000;
////			importer.format = AudioImporterFormat.Native;
////			importer.loadType = AudioImporterLoadType.CompressedInMemory;
//			importer.forceToMono = true;
//			//importer.loadType = AudioImporterLoadType.DecompressOnLoad;
//		});
//	}

	private static void ChangeSettings<Ty>(Object[] assets, System.Action<AssetImporter, Object> action)
	{
		Selection.objects = new Object[0];

		foreach (Object c in assets)
		{
			string path = AssetDatabase.GetAssetPath(c);

			AssetImporter importer = AssetImporter.GetAtPath(path);

			if (importer != null)
			{
				action(importer, c);
			}

			AssetDatabase.ImportAsset(path);
		}
	}


	private static void ChangeSettings<Ty>(System.Action<AssetImporter, Object> action)
	{
		Object[] assets = Selection.GetFiltered(typeof(Ty), SelectionMode.DeepAssets);

		ChangeSettings<Ty>(assets, action);
	}

	//[MenuItem("Tools/Batch/Texture/Half Resolution")]
	public static void SetHalfTextureResolution()
	{
		string platform = EditorUserBuildSettings.activeBuildTarget.ToString();

		ChangeSettings<Texture>((_importer, obj) =>
		{
			TextureImporter importer = _importer as TextureImporter;

			SetTextureResolution(platform, importer, 0.5f);
		});

		EditorUtility.UnloadUnusedAssetsImmediate();
	}

	//[MenuItem("Tools/Batch/Texture/Full Resolution")]
	public static void SetFullTextureResolution()
	{
		//string platform = EditorUserBuildSettings.activeBuildTarget.ToString();

		ChangeSettings<Texture>((_importer, obj) =>
		{
			TextureImporter importer = _importer as TextureImporter;
			//Texture texture = obj as Texture;

			SetTextureResolution(EditorUserBuildSettings.activeBuildTarget.ToString(), importer, 1.0f);
		});

		EditorUtility.UnloadUnusedAssetsImmediate();
	}

	private static void SetTextureResolution(string platform, TextureImporter importer, float multiplier)
	{
		var setting = importer.GetPlatformTextureSettings(platform);
		if (setting.overridden)
		{
			var maxSize = (int)(PowOfTwo(importer) * multiplier);
			setting.maxTextureSize = maxSize;
			importer.SetPlatformTextureSettings(setting);
		}
		else
		{
			setting.maxTextureSize = (int)(Mathf.Min(importer.maxTextureSize, PowOfTwo(importer)) * multiplier);
			setting.overridden = true;
			importer.SetPlatformTextureSettings(setting);
		}
	}

	static int PowOfTwo(TextureImporter importer)
	{
		var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
		try
		{
			var bytes = System.IO.File.ReadAllBytes(importer.assetPath);
			t.LoadImage(bytes);
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning(ex.StackTrace);
			Debug.LogError(ex.Message);
			DestroyImmediate(t);
			return -1;
		}

		int x = (int)Mathf.Max(t.width, t.height);
		
		DestroyImmediate(t);
		
		int pot = 0;
		for (int i = 0; pot < x; pot = (int)Mathf.Pow(2.0f, (float)i++))
			;
		return pot;
	}

	//static int PowOfTwo(Texture t)
	//{
	//    int x = (int)Mathf.Max(t.width, t.height);
		
	//    int pot = 0;
	//    for (int i = 0; pot < x; pot = (int)Mathf.Pow(2.0f, (float)i++))
	//        ;
	//    return pot;
	//}
	
	public static void ForEachSelectedObjects(System.Action<GameObject> action, object[] targets)
	{
		HashSet<GameObject> visited = new HashSet<GameObject>();
		
		System.Action<GameObject> visitor = null;
		
		visitor = (go) =>
		{
			if (visited.Contains(go))
				return;
			
			visited.Add(go);
			
			action(go);
			
			foreach (Transform tm in go.transform)
			{
				visitor(tm.gameObject);
			}
		};
		
		foreach (GameObject go in targets)
		{
			visitor(go);
		}
	}

#region QUALITY

	public static void ForEachFiles(System.Action<string> action, string filter, string subPath = "")
	{
		try
		{
			string assetsPrefix = "Assets/";
			if (subPath.StartsWith(assetsPrefix)) {
				subPath = subPath.Substring(assetsPrefix.Length-1);
			}
			var filenames = System.IO.Directory.GetFiles(Application.dataPath + subPath, filter, System.IO.SearchOption.AllDirectories);
			
			foreach (var name in filenames)
			{
				var assetPath = name.Substring(name.IndexOf("Assets")).Replace('\\', '/');
				action(assetPath);
			}
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning(ex.StackTrace);
			Debug.LogError(ex.Message);
		}
	}

	public static void AdjustAlphaMapSampleRate(float multiplier)
	{
		//var platform = EditorUserBuildSettings.activeBuildTarget.ToString();

		ForEachFiles((filename) => 
		{
			if (!filename.EndsWith(POSTFIX))
				return;

			//var str = name.Replace("\\", "/");

			var importer = AssetImporter.GetAtPath(filename) as TextureImporter;

			if (importer == null) return;

			if (importer.alphaSource != TextureImporterAlphaSource.FromGrayScale)
				Debug.LogWarning("!grayscaleToAlpha : " + importer.assetPath);

			SetTextureResolution(UnityEditor.BuildTarget.Android.ToString(), importer, multiplier);
			SetTextureResolution(UnityEditor.BuildTarget.iOS.ToString(), importer, multiplier);
			
			AssetDatabase.ImportAsset(importer.assetPath);
		}, "*.png");

		EditorUtility.UnloadUnusedAssetsImmediate();
	}

	public static void AdjustAlphaMapCompression(bool compressed)
	{
		ForEachFiles((filename) => 
		{
			if (!filename.EndsWith(POSTFIX) && !filename.EndsWith(POSTFIX))
				return;

			var importer = AssetImporter.GetAtPath(filename) as TextureImporter;

			if (importer == null) return;
			
			if (importer.alphaSource != TextureImporterAlphaSource.FromGrayScale)
				Debug.LogWarning("!grayscaleToAlpha : " + importer.assetPath);

			if (compressed)
			{
				ImportCompressedTexture(importer);
			}
			else
			{
				ImportUncompressedTexture(importer);
			}
			AssetDatabase.ImportAsset(importer.assetPath);
		}, "*.png");
	}

	private static void ImportCompressedTexture(TextureImporter importer)
	{
		importer.textureType = TextureImporterType.Default;
		importer.textureCompression = TextureImporterCompression.Uncompressed;
		var androidConfig = new TextureImporterPlatformSettings();
		androidConfig.name = Android;
		androidConfig.format = TextureImporterFormat.ETC_RGB4;
		androidConfig.maxTextureSize = importer.maxTextureSize;
		androidConfig.overridden = true;
		importer.SetPlatformTextureSettings(androidConfig);
		var iOSConfig = new TextureImporterPlatformSettings();
		iOSConfig.name = iOS;
		iOSConfig.format = TextureImporterFormat.PVRTC_RGB4;
		iOSConfig.maxTextureSize = importer.maxTextureSize;
		iOSConfig.overridden = true;
		importer.SetPlatformTextureSettings(iOSConfig);
		importer.mipmapEnabled = false;
		importer.isReadable = false;
		importer.npotScale = TextureImporterNPOTScale.ToNearest;
		importer.wrapMode = TextureWrapMode.Clamp;
	}

	private static void ImportUncompressedTexture(TextureImporter importer)
	{
		importer.textureType = TextureImporterType.Default;
		importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
		importer.textureCompression = TextureImporterCompression.Uncompressed;
		importer.ClearPlatformTextureSettings(Android);
		importer.ClearPlatformTextureSettings(iOS);
		importer.mipmapEnabled = false;
		importer.npotScale = TextureImporterNPOTScale.None;
		importer.wrapMode = TextureWrapMode.Clamp;
	}

	private static void ImportTexture(Texture2D texture, bool compressed)
	{
		var path = AssetDatabase.GetAssetPath(texture);
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (compressed)
			ImportCompressedTexture(importer);
		else
			ImportUncompressedTexture(importer);
		AssetDatabase.ImportAsset(path);
	}

	private static void AdjustFontCompression(bool compressed)
	{
		try
		{
			var filenames = System.IO.Directory.GetFiles(Application.dataPath + "/Resources/Font", "*.png");
			foreach (var name in filenames)
			{
				var path = name.Substring(name.IndexOf("Assets"));
				var importer = AssetImporter.GetAtPath(path) as TextureImporter;
				if (!importer) continue;
				
				if (compressed)
					ImportCompressedTexture(importer);
				else
					ImportUncompressedTexture(importer);
				
				AssetDatabase.ImportAsset(importer.assetPath);
			}
		}
		catch (System.Exception ex)
		{
			Debug.LogWarning(ex.StackTrace);
			Debug.LogError(ex.Message);
		}
	}
#endregion

	//[MenuItem("Tools/Batch/AlphaMap/Assign")]
	public static void AssignAlphamap()
	{
		ForEachFiles((filename) => 
		{
			var mat = AssetDatabase.LoadAssetAtPath(filename, typeof(Material)) as Material;
			var texture = mat.mainTexture;
			var assetPath = AssetDatabase.GetAssetPath(texture);
			var dotIndex = assetPath.LastIndexOf(".");
			if (dotIndex < 0)
				return;

			System.Func<string, Texture> findAlphaMap = (postfix) =>
				{
					var alphamapPath = assetPath.Remove(dotIndex) + postfix;
					var alphaMapTexture = AssetDatabase.LoadAssetAtPath(alphamapPath, typeof(Texture)) as Texture;
					return alphaMapTexture;
				};

			if (mat.HasProperty("_AlphaMap"))
			{
				var alphamap = mat.GetTexture("_AlphaMap");
				if (!alphamap)
				{
					alphamap = findAlphaMap(POSTFIX);
					if (!alphamap)
					{
						alphamap = findAlphaMap(POSTFIX);
						if (!alphamap) Debug.LogError(assetPath);
					}
					mat.SetTexture("_AlphaMap", alphamap);
				}
			}
			else if (mat.HasProperty("_GreyTex"))
			{
				var alphamap = mat.GetTexture("_GreyTex");
				if (!alphamap)
				{
					alphamap = findAlphaMap(POSTFIX);
					if (!alphamap)
					{
						alphamap = findAlphaMap(POSTFIX);
						if (!alphamap) Debug.LogError(assetPath);
					}
					mat.SetTexture("_GreyTex", alphamap);
				}
			}
		}, "*.mat");
	}

	//[MenuItem("Tools/Batch/AlphaMap/Rename")]
	public static void RenameAlphaMap()
	{
		ForEachFiles((filename) => 
		{
			RenameAlphaMap(filename);
		}, "*.png");
	}

	private static void RenameAlphaMap(string filename)
	{
		if (filename.EndsWith(POSTFIX))
		{
			var oldName = filename;

			var newName = oldName.Remove(oldName.IndexOf(POSTFIX));

			newName += POSTFIX;
			newName = newName.Substring(newName.LastIndexOf("\\"));
			newName = newName.Remove(newName.LastIndexOf("."));

			var result = AssetDatabase.RenameAsset(oldName, newName);

			if (!string.IsNullOrEmpty(result))
			{
				Debug.Log("result:" + result + "\n source = " + oldName + "\n dest = " + newName);
			}
		}
	}

	private static void ForEachScenes(System.Action<string> action)
	{
		var scenes = new List<string>();
		for (int i = 0; i < EditorBuildSettings.scenes.Length; ++i)
			if (EditorBuildSettings.scenes[i].enabled)
				scenes.Add(EditorBuildSettings.scenes[i].path);

		foreach (var scene in scenes)
		{
			action(scene);
		}
	}

	public static TextureImporterFormat ToImporterFormat(TextureFormat format) {
		try {
			var fmt = (TextureImporterFormat)System.Enum.Parse(typeof(TextureImporterFormat), format.ToString());
			return fmt;
		} catch (System.Exception e) {
			Debug.LogWarning(e.StackTrace);
			Debug.LogError(e.Message);
			return TextureImporterFormat.RGBA32;
		}
	}

	public static TextureFormat ToImporterFormat(TextureImporterFormat format) {
		try {
			var fmt = (TextureFormat)System.Enum.Parse(typeof(TextureFormat), format.ToString());
			return fmt;
		} catch (System.Exception e) {
			Debug.LogWarning(e.StackTrace);
			Debug.LogError(e.Message);
			return TextureFormat.RGBA32;
		}
	}

	static void ConfigurePlatformSettings(Texture2D tex, TextureImporter imp, System.Text.StringBuilder sb) {
		var arg = imp.assetPath;
		var platform = GetActivePlatform();

		if (tex.format == TextureFormat.PVRTC_RGB4 || tex.format == TextureFormat.PVRTC_RGB2) {
			imp.textureCompression = TextureImporterCompression.Uncompressed;
			imp.SetPlatformTextureFormat(GetActivePlatform(BuildTarget.iOS), ToImporterFormat(tex.format));
			imp.SetPlatformTextureFormat(platform, TextureImporterFormat.ETC_RGB4);
			sb.AppendFormat("{0}, fmt : {1}\n", arg, tex.format);
			imp.SaveAndReimport();
		} else if (tex.format == TextureFormat.PVRTC_RGBA4 || tex.format == TextureFormat.PVRTC_RGBA2) {
			imp.textureCompression = TextureImporterCompression.Uncompressed;
			imp.SetPlatformTextureFormat(GetActivePlatform(BuildTarget.iOS), ToImporterFormat(tex.format));
			imp.SetPlatformTextureFormat(platform, TextureImporterFormat.RGBA16);
			imp.alphaIsTransparency = true;
			sb.AppendFormat("{0}, fmt : {1}\n", arg, tex.format);
			imp.SaveAndReimport();
		}
	}

//	[MenuItem("Tools/Software Decoded Images")]
	private static void PrintSoftwareDecodedImages() {
		
		//var files = Selection.GetFiltered(typeof(Texture2D), SelectionMode.DeepAssets);
		var sb = new System.Text.StringBuilder();
		ForEachFiles((arg) => {
		//foreach (Texture2D tex in files) {
			var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(arg);
			//var arg = AssetDatabase.GetAssetPath(tex);
			var imp = AssetImporter.GetAtPath(arg) as TextureImporter;
			if (imp == null) {
				return;
			}
			var platform = GetActivePlatform();
			int maxSize;
			TextureImporterFormat fmt = TextureImporterFormat.RGBA32;
			if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) {
//				ConfigurePlatformSettings(tex, imp, sb);
				if (tex.format == TextureFormat.PVRTC_RGB2 || tex.format == TextureFormat.PVRTC_RGB4 ||
					tex.format == TextureFormat.PVRTC_RGBA2 || tex.format == TextureFormat.PVRTC_RGBA4) {
					sb.AppendFormat("{0}, fmt : {1}\n", arg, tex.format);
				} else
				if (imp.GetPlatformTextureSettings(platform, out maxSize, out fmt)) {
					if (fmt == TextureImporterFormat.PVRTC_RGB4 || fmt == TextureImporterFormat.PVRTC_RGBA4 ||
						fmt == TextureImporterFormat.PVRTC_RGB2 || fmt == TextureImporterFormat.PVRTC_RGBA2) {
						sb.AppendFormat("{0}, fmt : {1}\n", arg, fmt);
					}
				}
			} else if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS) {
				if (imp.GetPlatformTextureSettings(platform, out maxSize, out fmt)) {
					if (fmt != TextureImporterFormat.PVRTC_RGB4 || fmt != TextureImporterFormat.PVRTC_RGBA4 ||
						fmt != TextureImporterFormat.PVRTC_RGB2 || fmt != TextureImporterFormat.PVRTC_RGBA2) {
						sb.AppendFormat("{0}, fmt : {1}\n", arg, fmt);
					}
				}
			}
		}, "*.png");

		Debug.LogWarning(sb);
	}

	[MenuItem("Tools/Pack Selected Assets")]
	public static void PackSelectedAssets() {
		var selections = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.TopLevel);
		PackAssets(selections);
	}

	public static void PackAssets(params Object[] selections) {
		var buildList = new List<AssetBundleBuild>();
		foreach (var selection in selections) {
			UnityEngine.Object[] assets = null;
			if (selection is UnityEditor.DefaultAsset) {
				assets = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.DeepAssets)
					.Where(arg => arg.GetType() != typeof(UnityEditor.DefaultAsset)).ToArray();
			} else {
				assets = new Object[] { selection };
			}
			var build = new UnityEditor.AssetBundleBuild();
			build.assetBundleName = selection.name;
			build.assetNames = assets.Select(arg =>
				AssetDatabase.GetAssetPath(arg)).ToArray();
			buildList.Add(build);
		}
		BuildAssetBundle(buildList.ToArray());
	}

	public static void PackAssets(params string[] selections) {
		var buildList = new List<AssetBundleBuild>();
		foreach (var selection in selections) {
			string[] assets = null;
			if (AssetDatabase.LoadAssetAtPath<Object>(selection) is UnityEditor.DefaultAsset) {
				List<string> pathList = new List<string>();
				ForEachFiles((arg) => { 
					if (!System.IO.Path.GetFileName(arg).StartsWith(".") && !arg.EndsWith(".meta"))
						pathList.Add(arg);
				}, "*", selection);
				assets = pathList.ToArray();
			} else {
				assets = new string[] { selection };
			}
			var build = new UnityEditor.AssetBundleBuild();
			build.assetBundleName = System.IO.Path.GetFileNameWithoutExtension(selection);
			build.assetNames = assets;
			buildList.Add(build);
		}
		BuildAssetBundle(buildList.ToArray());
	}

	public static void BuildAssetBundle(params AssetBundleBuild[] buildList) {
		if (buildList.Length > 0) {
			var path = EditorUtility.SaveFolderPanel("Save AssetBundle", Application.dataPath, string.Empty);
			if (string.IsNullOrEmpty(path))
				return;
			BuildAssetBundleOptions option = BuildAssetBundleOptions.DeterministicAssetBundle|BuildAssetBundleOptions.ChunkBasedCompression;
			BuildPipeline.BuildAssetBundles(path, buildList.ToArray(), 
				option, EditorUserBuildSettings.activeBuildTarget);
		}
	}
}

public static class TextureExtension {
	public static bool IsTransparent(this Texture2D texture) {
		var format = texture.format.ToString();
		return format.Contains("RGBA") || format.Contains("ARGB");
	}
	public static bool IsCompressed(this Texture2D texture) {
		return texture.format == TextureFormat.PVRTC_RGB4 || texture.format == TextureFormat.ETC_RGB4;
	}
	public static bool IsCompressed(this TextureImporter importer) {
		return importer.textureCompression != TextureImporterCompression.Uncompressed;
	}
	public static void SetPlatformTextureFormat(this TextureImporter importer, string platform, TextureImporterFormat format)
	{
		var setting = importer.GetPlatformTextureSettings(platform);
		setting.format = format;
		setting.overridden = true;
		importer.SetPlatformTextureSettings(setting);
	}
}
