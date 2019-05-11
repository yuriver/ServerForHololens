using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using System;

public class ImageUtil
{
    public static Texture2D RawToTexture2D(byte[] rawdata)
    {
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(rawdata);

        return tex;
    }

    public static Sprite TextureToSprite(Texture2D tex)
    {
        Rect rect = new Rect(0, 0, tex.width, tex.height);
        Sprite sprite = Sprite.Create(tex, rect, new Vector2(.5f, .5f));

        return sprite;
    }

    public static string SaveImage(byte[] imageRawData)
    {
        string directoryName = MakeSaveDirectory();
        string fileName = GetFileName();
        string path = Path.Combine(directoryName, fileName);

        File.WriteAllBytes(path, imageRawData);

        Debug.LogFormat("image save to: {0}", path);
        return path;
    }

    private static readonly string DEFAULT_SAVE_PATH = @"C:\HololensImages\";
    private static string MakeSaveDirectory()
    {
        if (!Directory.Exists(DEFAULT_SAVE_PATH))
        {
            Directory.CreateDirectory(DEFAULT_SAVE_PATH);
        }

        return DEFAULT_SAVE_PATH;
    }

    private static string GetFileName()
    {
        string date = DateTime.Now.ToString("yyyyMMddHHmmss");
        string extension = "jpg";
        string fileName = string.Format("{0}.{1}", date, extension);

        return fileName;
    }
}
