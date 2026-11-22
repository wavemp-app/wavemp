namespace WaveMP.Data.Repositories;

/// <summary>
/// Songs that moved from one album to another (a stored <see cref="Entities.AlbumMove"/>).
/// </summary>
internal sealed record AlbumSuccessionMove(Guid FromAlbumId, Guid ToAlbumId, int TrackCount, DateTimeOffset MovedAt);
