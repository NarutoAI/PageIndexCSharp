using PageIndexCSharp.Interfaces;

namespace PageIndexCSharp.Store;

/// <summary>
/// 
/// </summary>
public class FileImageStore : IImageStore
{
    private const string ImageFile = "images";

    /// <summary>
    /// 
    /// </summary>
    /// <param name="image"></param>
    /// <param name="fileName">文件名</param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<string> AddAsync(byte[] image,string dirName, string fileName)
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ImageFile, dirName);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var path = Path.Combine(dir, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        await fs.WriteAsync(image);
        return Path.Combine(ImageFile,dirName, fileName);
    }


    /// <summary>
    /// 获取所有的文件
    /// </summary>
    /// <param name="imagePath"></param>
    /// <returns></returns>
    public async Task<byte[]?> GetAsync(string imagePath)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imagePath);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(imagePath);
    }
}