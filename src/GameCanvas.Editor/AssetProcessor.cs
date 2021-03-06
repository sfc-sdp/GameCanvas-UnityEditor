﻿/*------------------------------------------------------------*/
/// <summary>GameCanvas for Unity</summary>
/// <author>Seibe TAKAHASHI</author>
/// <remarks>
/// (c) 2015-2017 Smart Device Programming.
/// This software is released under the MIT License.
/// http://opensource.org/licenses/mit-license.php
/// </remarks>
/*------------------------------------------------------------*/

using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameCanvas.Editor
{
    /// <summary>
    /// アセットの後処理
    /// </summary>
    internal class AssetProcessor : AssetPostprocessor
    {
        public static void RebuildAssetDatabase()
        {
            willRebuildAssetDB = false;
            var projectDir = Path.GetDirectoryName(Application.dataPath);

            // 画像データの取得
            var sprites = new Dictionary<int, Sprite>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2d", new string[1] { ResourceDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var index = int.Parse(sprite.name.Substring(3));
                sprites.Add(index, sprite);
            }

            // 音声データの取得
            var clips = new Dictionary<int, AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new string[1] { ResourceDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var index = int.Parse(clip.name.Substring(3));
                clips.Add(index, clip);
            }

            // 図形データの取得
            var rect = AssetDatabase.LoadAssetAtPath<Sprite>(RectPath);
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            var dummy = AssetDatabase.LoadAssetAtPath<Sprite>(DummyPath);

            // 文字データの取得
            var characters = new List<Sprite>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(FontPath))
            {
                if (obj is Sprite)
                {
                    characters.Add(obj as Sprite);
                }
            }

            // マテリアルの取得
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            // データベースの作成
            if (!File.Exists(AssetHolder.Path))
            {
                var newDB = ScriptableObject.CreateInstance<AssetHolder>();
                AssetDatabase.CreateAsset(newDB, AssetHolder.Path);
            }

            // データベースの保存
            var db = AssetDatabase.LoadAssetAtPath<AssetHolder>(AssetHolder.Path);
            db.Images = sprites.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
            db.Sounds = clips.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
            db.Rect = rect;
            db.Circle = circle;
            db.Dummy = dummy;
            db.Characters = characters.ToArray();
            db.Material = mat;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        /*------------------------------------------------------------*/

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (willRebuildAssetDB) return;

            var assets = deletedAssets
                .Concat(movedAssets)
                .Concat(movedFromAssetPaths)
                .Distinct();

            foreach (string assetPath in assets)
            {
                if (cReg.IsMatch(assetPath))
                {
                    EditorApplication.delayCall += RebuildAssetDatabase;
                    willRebuildAssetDB = true;
                    return;
                }
            }
        }

        private void OnPostprocessTexture(Texture2D tex)
        {
            var importer = (TextureImporter)assetImporter;
            if (importer.userData == Version) return;

            if (cReg.IsMatch(base.assetPath))
            {
                // インポートした画像をパッキングタグ付きスプライトにします
                importer.userData = Version;
                importer.textureType = TextureImporterType.Sprite;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SetPlatformTextureSettings(cTextureSetting_Android);
                importer.SetPlatformTextureSettings(cTextureSetting_iOS);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 1f;
                importer.spritePackingTag = PackingTag;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();

                var so = new SerializedObject(importer);
                var sp = so.FindProperty("m_Alignment");
                if (sp.intValue != (int)SpriteAlignment.TopLeft)
                {
                    sp.intValue = (int)SpriteAlignment.TopLeft;
                    so.ApplyModifiedProperties();
                }
                so.Dispose();

                if (!willRebuildAssetDB)
                {
                    EditorApplication.delayCall += RebuildAssetDatabase;
                    willRebuildAssetDB = true;
                }
            }
            else if (base.assetPath.IndexOf(SpriteDir) == 0)
            {
                // インポートした画像をパッキングタグ付きスプライトにします
                importer.userData = Version;
                importer.textureType = TextureImporterType.Sprite;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SetPlatformTextureSettings(cTextureSetting_Android);
                importer.SetPlatformTextureSettings(cTextureSetting_iOS);
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.spriteImportMode = base.assetPath.Contains("Circle") ? SpriteImportMode.Polygon :
                                            base.assetPath.Contains("PixelMplus10") ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                importer.spritePackingTag = base.assetPath.Contains("Dummy") ? string.Empty : PackingTag;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }

        private void OnPostprocessAudio(AudioClip audio)
        {
            if (cReg.IsMatch(assetPath))
            {
                // インポートした音声の圧縮を設定します
                var audioImporter = (AudioImporter)assetImporter;
                var setting = audioImporter.defaultSampleSettings;
                setting.compressionFormat = AudioCompressionFormat.ADPCM;
                setting.loadType = AudioClipLoadType.Streaming;
                audioImporter.defaultSampleSettings = setting;

                if (!willRebuildAssetDB)
                {
                    EditorApplication.update += RebuildAssetDatabase;
                    willRebuildAssetDB = true;
                }
            }
        }

        /*------------------------------------------------------------*/

        const string Version = "GameCanvas AssetImporter 1.3.2.7";

        const string ResourceDir = "Assets/Res";
        const string PackingTag = "GCAtlas";
        const string SpriteDir = "Assets/Plugins/UnityGC/Sprites/";
        const string RectPath = SpriteDir + "Rect.png";
        const string CirclePath = SpriteDir + "Circle.png";
        const string DummyPath = SpriteDir + "Dummy.png";
        const string FontPath = SpriteDir + "PixelMplus10.png";
        const string MaterialDir = "Assets/Plugins/UnityGC/Materials";
        const string MaterialPath = MaterialDir + "/GCSpriteDefault.mat";

        static readonly Regex cReg = new Regex(@"^Assets/Res/(img|snd)\d+\.\w+$");

        static readonly TextureImporterPlatformSettings cTextureSetting_Android = new TextureImporterPlatformSettings()
        {
            name = "Android",
            maxTextureSize = 2048,
            format = TextureImporterFormat.ETC2_RGBA8,
            compressionQuality = 80,
            textureCompression = TextureImporterCompression.CompressedHQ,
            allowsAlphaSplitting = false,
            overridden = true
        };
        static readonly TextureImporterPlatformSettings cTextureSetting_iOS = new TextureImporterPlatformSettings()
        {
            name = "iPhone",
            maxTextureSize = 2048,
            format = TextureImporterFormat.RGBA32,
            textureCompression = TextureImporterCompression.Uncompressed,
            allowsAlphaSplitting = false,
            overridden = true
        };

        static bool willRebuildAssetDB = false;
    }
}
