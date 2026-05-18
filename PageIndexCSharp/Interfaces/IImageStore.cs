namespace PageIndexCSharp.Interfaces;

/// <summary>
/// 图片存储
/// </summary>
public interface IImageStore
{
    /// <summary>
    /// 存储图片信息,返回图片的路径
    /// </summary>
    /// <param name="image"></param>
    /// <param name="fileName">文件名</param>
    /// <returns></returns>
    Task<string> AddAsync(byte[]  image,string dirName,string fileName);

    /// <summary>
    /// 根据图片路径获取图片的资源
    /// </summary>
    /// <param name="imagePath"></param>
    /// <returns></returns>
    Task<byte[]?> GetAsync(string imagePath);
}