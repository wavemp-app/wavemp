namespace WaveMP.Core.Caching.Images;

/// <summary>
/// A WebP picture to create from a cover.
/// </summary>
/// <param name="MaxSize">Largest width and height in pixels; the aspect ratio stays and pictures are never enlarged.
/// </param>
/// <param name="OutputPath">File to write.</param>
/// <param name="Square">Crops the picture to a square around its center before it is scaled, e.g. for uploaded
/// covers of playlists.</param>
/// <param name="Crop">Crops the picture to this square section before it is scaled instead of the center square,
/// e.g. the section an admin chose; implies a square picture.</param>
public sealed record CoverEncodeTarget(int MaxSize, string OutputPath, bool Square = false, CoverCrop? Crop = null);
